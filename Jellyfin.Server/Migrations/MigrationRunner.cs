using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations;
using Emby.Server.Implementations.Serialization;
using Jellyfin.Database.Implementations;
using Jellyfin.Server.Implementations;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Migrations
{
    /// <summary>
    /// The class that knows which migrations to apply and how to apply them.
    /// </summary>
    public sealed class MigrationRunner
    {
        /// <summary>
        /// The list of known pre-startup migrations, in order of applicability.
        /// </summary>
        private static readonly Type[] _preStartupMigrationTypes =
        {
            typeof(PreStartupRoutines.CreateNetworkConfiguration),
            typeof(PreStartupRoutines.MigrateMusicBrainzTimeout),
            typeof(PreStartupRoutines.MigrateNetworkConfiguration),
            typeof(PreStartupRoutines.MigrateEncodingOptions),
            typeof(PreStartupRoutines.RenameEnableGroupingIntoCollections)
        };

        /// <summary>
        /// The list of known migrations, in order of applicability.
        /// </summary>
        private static readonly Type[] _migrationTypes =
        {
            typeof(Routines.DisableTranscodingThrottling),
            typeof(Routines.CreateUserLoggingConfigFile),
            typeof(Routines.MigrateActivityLogDb),
            typeof(Routines.RemoveDuplicateExtras),
            typeof(Routines.AddDefaultPluginRepository),
            typeof(Routines.MigrateUserDb),
            typeof(Routines.ReaddDefaultPluginRepository),
            typeof(Routines.MigrateDisplayPreferencesDb),
            typeof(Routines.RemoveDownloadImagesInAdvance),
            typeof(Routines.MigrateAuthenticationDb),
            typeof(Routines.FixPlaylistOwner),
            typeof(Routines.AddDefaultCastReceivers),
            typeof(Routines.UpdateDefaultPluginRepository),
            typeof(Routines.FixAudioData),
            typeof(Routines.RemoveDuplicatePlaylistChildren),
            typeof(Routines.MigrateLibraryDb),
            typeof(Routines.MigrateRatingLevels),
            typeof(Routines.MoveTrickplayFiles),
        };

        /// <summary>
        /// Run all needed migrations.
        /// </summary>
        /// <param name="host">CoreAppHost that hosts current version.</param>
        /// <param name="loggerFactory">Factory for making the logger.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Run(CoreAppHost host, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<MigrationRunner>();
            var migrations = _migrationTypes
                .Select(m => ActivatorUtilities.CreateInstance(host.ServiceProvider, m))
                .OfType<IMigrationRoutine>()
                .ToArray();

            var migrationOptions = host.ConfigurationManager.GetConfiguration<MigrationOptions>(MigrationsListStore.StoreKey);
            HandleStartupWizardCondition(migrations, migrationOptions, host.ConfigurationManager.Configuration.IsStartupWizardCompleted, logger);
            await PerformMigrations(migrations, migrationOptions, options => host.ConfigurationManager.SaveConfiguration(MigrationsListStore.StoreKey, options), logger, host.ServiceProvider.GetRequiredService<IJellyfinDatabaseProvider>())
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Run all needed pre-startup migrations.
        /// </summary>
        /// <param name="appPaths">Application paths.</param>
        /// <param name="loggerFactory">Factory for making the logger.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task RunPreStartup(ServerApplicationPaths appPaths, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<MigrationRunner>();
            var migrations = _preStartupMigrationTypes
                .Select(m => Activator.CreateInstance(m, appPaths, loggerFactory))
                .OfType<IMigrationRoutine>()
                .ToArray();

            var xmlSerializer = new MyXmlSerializer();
            var migrationConfigPath = Path.Join(appPaths.ConfigurationDirectoryPath, MigrationsListStore.StoreKey.ToLowerInvariant() + ".xml");
            var migrationOptions = File.Exists(migrationConfigPath)
                 ? (MigrationOptions)xmlSerializer.DeserializeFromFile(typeof(MigrationOptions), migrationConfigPath)!
                 : new MigrationOptions();

            // We have to deserialize it manually since the configuration manager may overwrite it
            var serverConfig = File.Exists(appPaths.SystemConfigurationFilePath)
                ? (ServerConfiguration)xmlSerializer.DeserializeFromFile(typeof(ServerConfiguration), appPaths.SystemConfigurationFilePath)!
                : new ServerConfiguration();

            HandleStartupWizardCondition(migrations, migrationOptions, serverConfig.IsStartupWizardCompleted, logger);
            await PerformMigrations(migrations, migrationOptions, options => xmlSerializer.SerializeToFile(options, migrationConfigPath), logger, null).ConfigureAwait(false);
        }

        private static void HandleStartupWizardCondition(IEnumerable<IMigrationRoutine> migrations, MigrationOptions migrationOptions, bool isStartWizardCompleted, ILogger logger)
        {
            if (isStartWizardCompleted)
            {
                return;
            }

            // If startup wizard is not finished, this is a fresh install.
            var onlyOldInstalls = migrations.Where(m => !m.PerformOnNewInstall).ToArray();
            logger.LogInformation("Marking following migrations as applied because this is a fresh install: {@OnlyOldInstalls}", onlyOldInstalls.Select(m => m.Name));
            migrationOptions.Applied.AddRange(onlyOldInstalls.Select(m => (m.Id, m.Name)));
        }

        private static async Task PerformMigrations(
            IMigrationRoutine[] migrations,
            MigrationOptions migrationOptions,
            Action<MigrationOptions> saveConfiguration,
            ILogger logger,
            IJellyfinDatabaseProvider? jellyfinDatabaseProvider)
        {
            // save already applied migrations, and skip them thereafter
            saveConfiguration(migrationOptions);
            var appliedMigrationIds = migrationOptions.Applied.Select(m => m.Id).ToHashSet();
            var migrationsToBeApplied = migrations.Where(e => !appliedMigrationIds.Contains(e.Id)).ToArray();

            string? migrationKey = null;
            if (jellyfinDatabaseProvider is not null && migrationsToBeApplied.Any(f => f is IDatabaseMigrationRoutine))
            {
                logger.LogInformation("Performing database backup");
                try
                {
                    migrationKey = await jellyfinDatabaseProvider.MigrationBackupFast(CancellationToken.None).ConfigureAwait(false);
                    logger.LogInformation("Database backup with key '{BackupKey}' has been successfully created.", migrationKey);
                }
                catch (NotImplementedException)
                {
                    logger.LogWarning("Could not perform backup of database before migration because provider does not support it");
                }
            }

            try
            {
                foreach (var migrationRoutine in migrationsToBeApplied)
                {
                    logger.LogInformation("Applying migration '{Name}'", migrationRoutine.Name);

                    try
                    {
                        migrationRoutine.Perform();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Could not apply migration '{Name}'", migrationRoutine.Name);
                        throw;
                    }

                    // Mark the migration as completed
                    logger.LogInformation("Migration '{Name}' applied successfully", migrationRoutine.Name);
                    migrationOptions.Applied.Add((migrationRoutine.Id, migrationRoutine.Name));
                    saveConfiguration(migrationOptions);
                    logger.LogDebug("Migration '{Name}' marked as applied in configuration.", migrationRoutine.Name);
                }
            }
            catch (System.Exception) when (migrationKey is not null && jellyfinDatabaseProvider is not null)
            {
                logger.LogInformation("Rollback on database as migration reported failure.");
                await jellyfinDatabaseProvider.RestoreBackupFast(migrationKey, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
    }
}
