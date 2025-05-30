using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace MediaBrowser.Providers.Plugins.MusicBrainz;

/// <summary>
/// MusicBrainz release group external id.
/// </summary>
public class MusicBrainzReleaseGroupExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "MusicBrainz";

    /// <inheritdoc />
    public string Key => MetadataProvider.MusicBrainzReleaseGroup.ToString();

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.ReleaseGroup;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Audio or MusicAlbum;
}
