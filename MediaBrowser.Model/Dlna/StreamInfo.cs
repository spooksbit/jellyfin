﻿using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MediaBrowser.Model.Dlna
{
    /// <summary>
    /// Class StreamInfo.
    /// </summary>
    public class StreamInfo
    {
        public StreamInfo()
        {
            AudioCodecs = new string[] { };
            VideoCodecs = new string[] { };
            SubtitleCodecs = new string[] { };
            TranscodeReasons = new List<TranscodeReason>();
            StreamOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void SetOption(string qualifier, string name, string value)
        {
            SetOption(qualifier + "-" + name, value);
        }

        public void SetOption(string name, string value)
        {
            StreamOptions[name] = value;
        }

        public string GetOption(string qualifier, string name)
        {
            return GetOption(qualifier + "-" + name);
        }

        public string GetOption(string name)
        {
            string value;
            if (StreamOptions.TryGetValue(name, out value))
            {
                return value;
            }

            return null;
        }

        public string ItemId { get; set; }

        public PlayMethod PlayMethod { get; set; }
        public EncodingContext Context { get; set; }

        public DlnaProfileType MediaType { get; set; }

        public string Container { get; set; }

        public string SubProtocol { get; set; }

        public long StartPositionTicks { get; set; }

        public int? SegmentLength { get; set; }
        public int? MinSegments { get; set; }
        public bool BreakOnNonKeyFrames { get; set; }

        public bool RequireAvc { get; set; }
        public bool RequireNonAnamorphic { get; set; }
        public bool CopyTimestamps { get; set; }
        public bool EnableSubtitlesInManifest { get; set; }
        public string[] AudioCodecs { get; set; }
        public string[] VideoCodecs { get; set; }

        public int? AudioStreamIndex { get; set; }

        public int? SubtitleStreamIndex { get; set; }

        public int? TranscodingMaxAudioChannels { get; set; }
        public int? MaxAudioChannels { get; set; }

        public int? AudioBitrate { get; set; }

        public int? VideoBitrate { get; set; }

        public int? MaxWidth { get; set; }
        public int? MaxHeight { get; set; }

        public int? MaxVideoBitDepth { get; set; }

        public float? MaxFramerate { get; set; }

        public DeviceProfile DeviceProfile { get; set; }
        public string DeviceProfileId { get; set; }
        public string DeviceId { get; set; }

        public long? RunTimeTicks { get; set; }

        public TranscodeSeekInfo TranscodeSeekInfo { get; set; }

        public bool EstimateContentLength { get; set; }

        public MediaSourceInfo MediaSource { get; set; }

        public string[] SubtitleCodecs { get; set; }
        public SubtitleDeliveryMethod SubtitleDeliveryMethod { get; set; }
        public string SubtitleFormat { get; set; }

        public string PlaySessionId { get; set; }
        public List<MediaSourceInfo> AllMediaSources { get; set; }
        public List<TranscodeReason> TranscodeReasons { get; set; }

        public Dictionary<string, string> StreamOptions { get; private set; }

        public string MediaSourceId
        {
            get
            {
                return MediaSource == null ? null : MediaSource.Id;
            }
        }

        public bool IsDirectStream
        {
            get
            {
                return PlayMethod == PlayMethod.DirectStream ||
                    PlayMethod == PlayMethod.DirectPlay;
            }
        }

        public string ToUrl(string baseUrl, string accessToken)
        {
            if (PlayMethod == PlayMethod.DirectPlay)
            {
                return MediaSource.Path;
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentNullException(baseUrl);
            }

            List<string> list = new List<string>();
            foreach (NameValuePair pair in BuildParams(this, accessToken, false))
            {
                if (string.IsNullOrEmpty(pair.Value))
                {
                    continue;
                }

                // Try to keep the url clean by omitting defaults
                if (StringHelper.EqualsIgnoreCase(pair.Name, "StartTimeTicks") &&
                    StringHelper.EqualsIgnoreCase(pair.Value, "0"))
                {
                    continue;
                }
                if (StringHelper.EqualsIgnoreCase(pair.Name, "SubtitleStreamIndex") &&
                    StringHelper.EqualsIgnoreCase(pair.Value, "-1"))
                {
                    continue;
                }
                if (StringHelper.EqualsIgnoreCase(pair.Name, "Static") &&
                    StringHelper.EqualsIgnoreCase(pair.Value, "false"))
                {
                    continue;
                }

                var encodedValue = pair.Value.Replace(" ", "%20");

                list.Add(string.Format("{0}={1}", pair.Name, encodedValue));
            }

            string queryString = string.Join("&", list.ToArray(list.Count));

            return GetUrl(baseUrl, queryString);
        }

        public string ToDlnaUrl(string baseUrl, string accessToken)
        {
            if (PlayMethod == PlayMethod.DirectPlay)
            {
                return MediaSource.Path;
            }

            string dlnaCommand = BuildDlnaParam(this, accessToken);
            return GetUrl(baseUrl, dlnaCommand);
        }

        private string GetUrl(string baseUrl, string queryString)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentNullException(baseUrl);
            }

            string extension = string.IsNullOrEmpty(Container) ? string.Empty : "." + Container;

            baseUrl = baseUrl.TrimEnd('/');

            if (MediaType == DlnaProfileType.Audio)
            {
                if (StringHelper.EqualsIgnoreCase(SubProtocol, "hls"))
                {
                    return string.Format("{0}/audio/{1}/master.m3u8?{2}", baseUrl, ItemId, queryString);
                }

                return string.Format("{0}/audio/{1}/stream{2}?{3}", baseUrl, ItemId, extension, queryString);
            }

            if (StringHelper.EqualsIgnoreCase(SubProtocol, "hls"))
            {
                return string.Format("{0}/videos/{1}/master.m3u8?{2}", baseUrl, ItemId, queryString);
            }

            return string.Format("{0}/videos/{1}/stream{2}?{3}", baseUrl, ItemId, extension, queryString);
        }

        private static string BuildDlnaParam(StreamInfo item, string accessToken)
        {
            List<string> list = new List<string>();

            foreach (NameValuePair pair in BuildParams(item, accessToken, true))
            {
                list.Add(pair.Value);
            }

            return string.Format("Params={0}", string.Join(";", list.ToArray(list.Count)));
        }

        private static List<NameValuePair> BuildParams(StreamInfo item, string accessToken, bool isDlna)
        {
            List<NameValuePair> list = new List<NameValuePair>();

            string audioCodecs = item.AudioCodecs.Length == 0 ?
                string.Empty :
                string.Join(",", item.AudioCodecs);

            string videoCodecs = item.VideoCodecs.Length == 0 ?
                string.Empty :
                string.Join(",", item.VideoCodecs);

            list.Add(new NameValuePair("DeviceProfileId", item.DeviceProfileId ?? string.Empty));
            list.Add(new NameValuePair("DeviceId", item.DeviceId ?? string.Empty));
            list.Add(new NameValuePair("MediaSourceId", item.MediaSourceId ?? string.Empty));
            list.Add(new NameValuePair("Static", item.IsDirectStream.ToString().ToLower()));
            list.Add(new NameValuePair("VideoCodec", videoCodecs));
            list.Add(new NameValuePair("AudioCodec", audioCodecs));
            list.Add(new NameValuePair("AudioStreamIndex", item.AudioStreamIndex.HasValue ? StringHelper.ToStringCultureInvariant(item.AudioStreamIndex.Value) : string.Empty));
            list.Add(new NameValuePair("SubtitleStreamIndex", item.SubtitleStreamIndex.HasValue && item.SubtitleDeliveryMethod != SubtitleDeliveryMethod.External ? StringHelper.ToStringCultureInvariant(item.SubtitleStreamIndex.Value) : string.Empty));
            list.Add(new NameValuePair("VideoBitrate", item.VideoBitrate.HasValue ? StringHelper.ToStringCultureInvariant(item.VideoBitrate.Value) : string.Empty));
            list.Add(new NameValuePair("AudioBitrate", item.AudioBitrate.HasValue ? StringHelper.ToStringCultureInvariant(item.AudioBitrate.Value) : string.Empty));
            list.Add(new NameValuePair("MaxAudioChannels", item.MaxAudioChannels.HasValue ? StringHelper.ToStringCultureInvariant(item.MaxAudioChannels.Value) : string.Empty));
            list.Add(new NameValuePair("MaxFramerate", item.MaxFramerate.HasValue ? StringHelper.ToStringCultureInvariant(item.MaxFramerate.Value) : string.Empty));
            list.Add(new NameValuePair("MaxWidth", item.MaxWidth.HasValue ? StringHelper.ToStringCultureInvariant(item.MaxWidth.Value) : string.Empty));
            list.Add(new NameValuePair("MaxHeight", item.MaxHeight.HasValue ? StringHelper.ToStringCultureInvariant(item.MaxHeight.Value) : string.Empty));

            long startPositionTicks = item.StartPositionTicks;

            var isHls = StringHelper.EqualsIgnoreCase(item.SubProtocol, "hls");

            if (isHls)
            {
                list.Add(new NameValuePair("StartTimeTicks", string.Empty));
            }
            else
            {
                list.Add(new NameValuePair("StartTimeTicks", StringHelper.ToStringCultureInvariant(startPositionTicks)));
            }

            if (isDlna)
            {
                // hack alert
                // dlna needs to be update to support the qualified params
                var level = item.GetTargetVideoLevel("h264");

                list.Add(new NameValuePair("Level", level.HasValue ? StringHelper.ToStringCultureInvariant(level.Value) : string.Empty));
            }

            if (isDlna)
            {
                // hack alert
                // dlna needs to be update to support the qualified params
                var refframes = item.GetTargetRefFrames("h264");

                list.Add(new NameValuePair("MaxRefFrames", refframes.HasValue ? StringHelper.ToStringCultureInvariant(refframes.Value) : string.Empty));
            }

            list.Add(new NameValuePair("MaxVideoBitDepth", item.MaxVideoBitDepth.HasValue ? StringHelper.ToStringCultureInvariant(item.MaxVideoBitDepth.Value) : string.Empty));

            if (isDlna)
            {
                // hack alert
                // dlna needs to be update to support the qualified params
                var profile = item.GetOption("h264", "profile");

                list.Add(new NameValuePair("Profile", profile ?? string.Empty));
            }

            // no longer used
            list.Add(new NameValuePair("Cabac", string.Empty));

            list.Add(new NameValuePair("PlaySessionId", item.PlaySessionId ?? string.Empty));
            list.Add(new NameValuePair("api_key", accessToken ?? string.Empty));

            string liveStreamId = item.MediaSource == null ? null : item.MediaSource.LiveStreamId;
            list.Add(new NameValuePair("LiveStreamId", liveStreamId ?? string.Empty));

            if (isDlna)
            {
                list.Add(new NameValuePair("ItemId", item.ItemId));
            }

            list.Add(new NameValuePair("CopyTimestamps", item.CopyTimestamps.ToString().ToLower()));
            list.Add(new NameValuePair("SubtitleMethod", item.SubtitleStreamIndex.HasValue && item.SubtitleDeliveryMethod != SubtitleDeliveryMethod.External ? item.SubtitleDeliveryMethod.ToString() : string.Empty));

            list.Add(new NameValuePair("TranscodingMaxAudioChannels", item.TranscodingMaxAudioChannels.HasValue ? StringHelper.ToStringCultureInvariant(item.TranscodingMaxAudioChannels.Value) : string.Empty));
            list.Add(new NameValuePair("EnableSubtitlesInManifest", item.EnableSubtitlesInManifest.ToString().ToLower()));

            list.Add(new NameValuePair("Tag", item.MediaSource.ETag ?? string.Empty));
            list.Add(new NameValuePair("RequireAvc", item.RequireAvc.ToString().ToLower()));

            string subtitleCodecs = item.SubtitleCodecs.Length == 0 ?
               string.Empty :
               string.Join(",", item.SubtitleCodecs);

            list.Add(new NameValuePair("SubtitleCodec", item.SubtitleStreamIndex.HasValue && item.SubtitleDeliveryMethod == SubtitleDeliveryMethod.Embed ? subtitleCodecs : string.Empty));

            list.Add(new NameValuePair("RequireNonAnamorphic", item.RequireNonAnamorphic.ToString().ToLower()));

            if (isDlna)
            {
                // hack alert
                // dlna needs to be update to support the qualified params
                var deinterlace = string.Equals(item.GetOption("h264", "deinterlace"), "true", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(item.GetOption("mpeg2video", "deinterlace"), "true", StringComparison.OrdinalIgnoreCase);

                list.Add(new NameValuePair("DeInterlace", deinterlace.ToString().ToLower()));
            }

            if (!isDlna && isHls)
            {
                list.Add(new NameValuePair("SegmentContainer", item.Container ?? string.Empty));

                if (item.SegmentLength.HasValue)
                {
                    list.Add(new NameValuePair("SegmentLength", item.SegmentLength.Value.ToString(CultureInfo.InvariantCulture)));
                }

                if (item.MinSegments.HasValue)
                {
                    list.Add(new NameValuePair("MinSegments", item.MinSegments.Value.ToString(CultureInfo.InvariantCulture)));
                }

                list.Add(new NameValuePair("BreakOnNonKeyFrames", item.BreakOnNonKeyFrames.ToString()));
            }

            if (isDlna || !item.IsDirectStream)
            {
                list.Add(new NameValuePair("TranscodeReasons", string.Join(",", item.TranscodeReasons.Distinct().Select(i => i.ToString()).ToArray())));
            }

            if (!isDlna)
            {
                foreach (var pair in item.StreamOptions)
                {
                    if (string.IsNullOrWhiteSpace(pair.Value))
                    {
                        continue;
                    }

                    list.Add(new NameValuePair(pair.Key, pair.Value));
                }
            }

            return list;
        }

        public List<SubtitleStreamInfo> GetExternalSubtitles(ITranscoderSupport transcoderSupport, bool includeSelectedTrackOnly, string baseUrl, string accessToken)
        {
            return GetExternalSubtitles(transcoderSupport, includeSelectedTrackOnly, false, baseUrl, accessToken);
        }

        public List<SubtitleStreamInfo> GetExternalSubtitles(ITranscoderSupport transcoderSupport, bool includeSelectedTrackOnly, bool enableAllProfiles, string baseUrl, string accessToken)
        {
            List<SubtitleStreamInfo> list = GetSubtitleProfiles(transcoderSupport, includeSelectedTrackOnly, enableAllProfiles, baseUrl, accessToken);
            List<SubtitleStreamInfo> newList = new List<SubtitleStreamInfo>();

            // First add the selected track
            foreach (SubtitleStreamInfo stream in list)
            {
                if (stream.DeliveryMethod == SubtitleDeliveryMethod.External)
                {
                    newList.Add(stream);
                }
            }

            return newList;
        }

        public List<SubtitleStreamInfo> GetSubtitleProfiles(ITranscoderSupport transcoderSupport, bool includeSelectedTrackOnly, string baseUrl, string accessToken)
        {
            return GetSubtitleProfiles(transcoderSupport, includeSelectedTrackOnly, false, baseUrl, accessToken);
        }

        public List<SubtitleStreamInfo> GetSubtitleProfiles(ITranscoderSupport transcoderSupport, bool includeSelectedTrackOnly, bool enableAllProfiles, string baseUrl, string accessToken)
        {
            List<SubtitleStreamInfo> list = new List<SubtitleStreamInfo>();

            // HLS will preserve timestamps so we can just grab the full subtitle stream
            long startPositionTicks = StringHelper.EqualsIgnoreCase(SubProtocol, "hls")
                ? 0
                : (PlayMethod == PlayMethod.Transcode && !CopyTimestamps ? StartPositionTicks : 0);

            // First add the selected track
            if (SubtitleStreamIndex.HasValue)
            {
                foreach (MediaStream stream in MediaSource.MediaStreams)
                {
                    if (stream.Type == MediaStreamType.Subtitle && stream.Index == SubtitleStreamIndex.Value)
                    {
                        AddSubtitleProfiles(list, stream, transcoderSupport, enableAllProfiles, baseUrl, accessToken, startPositionTicks);
                    }
                }
            }

            if (!includeSelectedTrackOnly)
            {
                foreach (MediaStream stream in MediaSource.MediaStreams)
                {
                    if (stream.Type == MediaStreamType.Subtitle && (!SubtitleStreamIndex.HasValue || stream.Index != SubtitleStreamIndex.Value))
                    {
                        AddSubtitleProfiles(list, stream, transcoderSupport, enableAllProfiles, baseUrl, accessToken, startPositionTicks);
                    }
                }
            }

            return list;
        }

        private void AddSubtitleProfiles(List<SubtitleStreamInfo> list, MediaStream stream, ITranscoderSupport transcoderSupport, bool enableAllProfiles, string baseUrl, string accessToken, long startPositionTicks)
        {
            if (enableAllProfiles)
            {
                foreach (SubtitleProfile profile in DeviceProfile.SubtitleProfiles)
                {
                    SubtitleStreamInfo info = GetSubtitleStreamInfo(stream, baseUrl, accessToken, startPositionTicks, new[] { profile }, transcoderSupport);

                    list.Add(info);
                }
            }
            else
            {
                SubtitleStreamInfo info = GetSubtitleStreamInfo(stream, baseUrl, accessToken, startPositionTicks, DeviceProfile.SubtitleProfiles, transcoderSupport);

                list.Add(info);
            }
        }

        private SubtitleStreamInfo GetSubtitleStreamInfo(MediaStream stream, string baseUrl, string accessToken, long startPositionTicks, SubtitleProfile[] subtitleProfiles, ITranscoderSupport transcoderSupport)
        {
            SubtitleProfile subtitleProfile = StreamBuilder.GetSubtitleProfile(stream, subtitleProfiles, PlayMethod, transcoderSupport, SubProtocol, Container);
            SubtitleStreamInfo info = new SubtitleStreamInfo
            {
                IsForced = stream.IsForced,
                Language = stream.Language,
                Name = stream.Language ?? "Unknown",
                Format = subtitleProfile.Format,
                Index = stream.Index,
                DeliveryMethod = subtitleProfile.Method,
                DisplayTitle = stream.DisplayTitle
            };

            if (info.DeliveryMethod == SubtitleDeliveryMethod.External)
            {
                if (MediaSource.Protocol == MediaProtocol.File || !StringHelper.EqualsIgnoreCase(stream.Codec, subtitleProfile.Format))
                {
                    info.Url = string.Format("{0}/Videos/{1}/{2}/Subtitles/{3}/{4}/Stream.{5}",
                        baseUrl,
                        ItemId,
                        MediaSourceId,
                        StringHelper.ToStringCultureInvariant(stream.Index),
                        StringHelper.ToStringCultureInvariant(startPositionTicks),
                        subtitleProfile.Format);

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        info.Url += "?api_key=" + accessToken;
                    }

                    info.IsExternalUrl = false;
                }
                else
                {
                    info.Url = stream.Path;
                    info.IsExternalUrl = true;
                }
            }

            return info;
        }

        /// <summary>
        /// Returns the audio stream that will be used
        /// </summary>
        public MediaStream TargetAudioStream
        {
            get
            {
                if (MediaSource != null)
                {
                    return MediaSource.GetDefaultAudioStream(AudioStreamIndex);
                }

                return null;
            }
        }

        /// <summary>
        /// Returns the video stream that will be used
        /// </summary>
        public MediaStream TargetVideoStream
        {
            get
            {
                if (MediaSource != null)
                {
                    return MediaSource.VideoStream;
                }

                return null;
            }
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public int? TargetAudioSampleRate
        {
            get
            {
                MediaStream stream = TargetAudioStream;
                return stream == null ? null : stream.SampleRate;
            }
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public int? TargetAudioBitDepth
        {
            get
            {
                MediaStream stream = TargetAudioStream;
                return stream == null ? null : stream.BitDepth;
            }
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public int? TargetVideoBitDepth
        {
            get
            {
                MediaStream stream = TargetVideoStream;
                return stream == null || !IsDirectStream ? null : stream.BitDepth;
            }
        }

        /// <summary>
        /// Gets the target reference frames.
        /// </summary>
        /// <value>The target reference frames.</value>
        public int? TargetRefFrames
        {
            get
            {
                if (IsDirectStream)
                {
                    return TargetVideoStream == null ? (int?)null : TargetVideoStream.RefFrames;
                }

                var targetVideoCodecs = TargetVideoCodec;
                var videoCodec = targetVideoCodecs.Length == 0 ? null : targetVideoCodecs[0];
                if (!string.IsNullOrWhiteSpace(videoCodec))
                {
                    return GetTargetRefFrames(videoCodec);
                }

                return TargetVideoStream == null ? (int?)null : TargetVideoStream.RefFrames;
            }
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public float? TargetFramerate
        {
            get
            {
                MediaStream stream = TargetVideoStream;
                return MaxFramerate.HasValue && !IsDirectStream
                    ? MaxFramerate
                    : stream == null ? null : stream.AverageFrameRate ?? stream.RealFrameRate;
            }
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public double? TargetVideoLevel
        {
            get
            {
                if (IsDirectStream)
                {
                    return TargetVideoStream == null ? (double?)null : TargetVideoStream.Level;
                }

                var targetVideoCodecs = TargetVideoCodec;
                var videoCodec = targetVideoCodecs.Length == 0 ? null : targetVideoCodecs[0];
                if (!string.IsNullOrWhiteSpace(videoCodec))
                {
                    return GetTargetVideoLevel(videoCodec);
                }

                return TargetVideoStream == null ? (double?)null : TargetVideoStream.Level;
            }
        }

        public double? GetTargetVideoLevel(string codec)
        {
            var value = GetOption(codec, "level");
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            double result;
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return null;
        }

        public int? GetTargetRefFrames(string codec)
        {
            var value = GetOption(codec, "maxrefframes");
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            int result;
            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public int? TargetPacketLength
        {
            get
            {
                MediaStream stream = TargetVideoStream;
                return !IsDirectStream
                    ? null
                    : stream == null ? null : stream.PacketLength;
            }
        }

        /// <summary>
        /// Predicts the audio sample rate that will be in the output stream
        /// </summary>
        public string TargetVideoProfile
        {
            get
            {
                if (IsDirectStream)
                {
                    return TargetVideoStream == null ? null : TargetVideoStream.Profile;
                }

                var targetVideoCodecs = TargetVideoCodec;
                var videoCodec = targetVideoCodecs.Length == 0 ? null : targetVideoCodecs[0];
                if (!string.IsNullOrWhiteSpace(videoCodec))
                {
                    return GetOption(videoCodec, "profile");
                }

                return TargetVideoStream == null ? null : TargetVideoStream.Profile;
            }
        }

        /// <summary>
        /// Gets the target video codec tag.
        /// </summary>
        /// <value>The target video codec tag.</value>
        public string TargetVideoCodecTag
        {
            get
            {
                MediaStream stream = TargetVideoStream;
                return !IsDirectStream
                    ? null
                    : stream == null ? null : stream.CodecTag;
            }
        }

        /// <summary>
        /// Predicts the audio bitrate that will be in the output stream
        /// </summary>
        public int? TargetAudioBitrate
        {
            get
            {
                MediaStream stream = TargetAudioStream;
                return AudioBitrate.HasValue && !IsDirectStream
                    ? AudioBitrate
                    : stream == null ? null : stream.BitRate;
            }
        }

        /// <summary>
        /// Predicts the audio channels that will be in the output stream
        /// </summary>
        public int? TargetAudioChannels
        {
            get
            {
                MediaStream stream = TargetAudioStream;
                int? streamChannels = stream == null ? null : stream.Channels;

                if (MaxAudioChannels.HasValue && !IsDirectStream)
                {
                    if (streamChannels.HasValue)
                    {
                        return Math.Min(MaxAudioChannels.Value, streamChannels.Value);
                    }

                    return MaxAudioChannels.Value;
                }

                return streamChannels;
            }
        }

        /// <summary>
        /// Predicts the audio codec that will be in the output stream
        /// </summary>
        public string[] TargetAudioCodec
        {
            get
            {
                MediaStream stream = TargetAudioStream;

                string inputCodec = stream == null ? null : stream.Codec;

                if (IsDirectStream)
                {
                    return string.IsNullOrWhiteSpace(inputCodec) ? new string[] { } : new[] { inputCodec };
                }

                foreach (string codec in AudioCodecs)
                {
                    if (StringHelper.EqualsIgnoreCase(codec, inputCodec))
                    {
                        return string.IsNullOrWhiteSpace(codec) ? new string[] { } : new[] { codec };
                    }
                }

                return AudioCodecs;
            }
        }

        public string[] TargetVideoCodec
        {
            get
            {
                MediaStream stream = TargetVideoStream;

                string inputCodec = stream == null ? null : stream.Codec;

                if (IsDirectStream)
                {
                    return string.IsNullOrWhiteSpace(inputCodec) ? new string[] { } : new[] { inputCodec };
                }

                foreach (string codec in VideoCodecs)
                {
                    if (StringHelper.EqualsIgnoreCase(codec, inputCodec))
                    {
                        return string.IsNullOrWhiteSpace(codec) ? new string[] { } : new[] { codec };
                    }
                }

                return VideoCodecs;
            }
        }

        /// <summary>
        /// Predicts the audio channels that will be in the output stream
        /// </summary>
        public long? TargetSize
        {
            get
            {
                if (IsDirectStream)
                {
                    return MediaSource.Size;
                }

                if (RunTimeTicks.HasValue)
                {
                    int? totalBitrate = TargetTotalBitrate;

                    double totalSeconds = RunTimeTicks.Value;
                    // Convert to ms
                    totalSeconds /= 10000;
                    // Convert to seconds
                    totalSeconds /= 1000;

                    return totalBitrate.HasValue ?
                        Convert.ToInt64(totalBitrate.Value * totalSeconds) :
                        (long?)null;
                }

                return null;
            }
        }

        public int? TargetVideoBitrate
        {
            get
            {
                MediaStream stream = TargetVideoStream;

                return VideoBitrate.HasValue && !IsDirectStream
                    ? VideoBitrate
                    : stream == null ? null : stream.BitRate;
            }
        }

        public TransportStreamTimestamp TargetTimestamp
        {
            get
            {
                TransportStreamTimestamp defaultValue = StringHelper.EqualsIgnoreCase(Container, "m2ts")
                    ? TransportStreamTimestamp.Valid
                    : TransportStreamTimestamp.None;

                return !IsDirectStream
                    ? defaultValue
                    : MediaSource == null ? defaultValue : MediaSource.Timestamp ?? TransportStreamTimestamp.None;
            }
        }

        public int? TargetTotalBitrate
        {
            get
            {
                return (TargetAudioBitrate ?? 0) + (TargetVideoBitrate ?? 0);
            }
        }

        public bool? IsTargetAnamorphic
        {
            get
            {
                if (IsDirectStream)
                {
                    return TargetVideoStream == null ? null : TargetVideoStream.IsAnamorphic;
                }

                return false;
            }
        }

        public bool? IsTargetInterlaced
        {
            get
            {
                if (IsDirectStream)
                {
                    return TargetVideoStream == null ? (bool?)null : TargetVideoStream.IsInterlaced;
                }

                var targetVideoCodecs = TargetVideoCodec;
                var videoCodec = targetVideoCodecs.Length == 0 ? null : targetVideoCodecs[0];
                if (!string.IsNullOrWhiteSpace(videoCodec))
                {
                    if (string.Equals(GetOption(videoCodec, "deinterlace"), "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return TargetVideoStream == null ? (bool?)null : TargetVideoStream.IsInterlaced;
            }
        }

        public bool? IsTargetAVC
        {
            get
            {
                if (IsDirectStream)
                {
                    return TargetVideoStream == null ? null : TargetVideoStream.IsAVC;
                }

                return true;
            }
        }

        public int? TargetWidth
        {
            get
            {
                MediaStream videoStream = TargetVideoStream;

                if (videoStream != null && videoStream.Width.HasValue && videoStream.Height.HasValue)
                {
                    ImageSize size = new ImageSize
                    {
                        Width = videoStream.Width.Value,
                        Height = videoStream.Height.Value
                    };

                    double? maxWidth = MaxWidth.HasValue ? (double)MaxWidth.Value : (double?)null;
                    double? maxHeight = MaxHeight.HasValue ? (double)MaxHeight.Value : (double?)null;

                    ImageSize newSize = DrawingUtils.Resize(size,
                        null,
                        null,
                        maxWidth,
                        maxHeight);

                    return Convert.ToInt32(newSize.Width);
                }

                return MaxWidth;
            }
        }

        public int? TargetHeight
        {
            get
            {
                MediaStream videoStream = TargetVideoStream;

                if (videoStream != null && videoStream.Width.HasValue && videoStream.Height.HasValue)
                {
                    ImageSize size = new ImageSize
                    {
                        Width = videoStream.Width.Value,
                        Height = videoStream.Height.Value
                    };

                    double? maxWidth = MaxWidth.HasValue ? (double)MaxWidth.Value : (double?)null;
                    double? maxHeight = MaxHeight.HasValue ? (double)MaxHeight.Value : (double?)null;

                    ImageSize newSize = DrawingUtils.Resize(size,
                        null,
                        null,
                        maxWidth,
                        maxHeight);

                    return Convert.ToInt32(newSize.Height);
                }

                return MaxHeight;
            }
        }

        public int? TargetVideoStreamCount
        {
            get
            {
                if (IsDirectStream)
                {
                    return GetMediaStreamCount(MediaStreamType.Video, int.MaxValue);
                }
                return GetMediaStreamCount(MediaStreamType.Video, 1);
            }
        }

        public int? TargetAudioStreamCount
        {
            get
            {
                if (IsDirectStream)
                {
                    return GetMediaStreamCount(MediaStreamType.Audio, int.MaxValue);
                }
                return GetMediaStreamCount(MediaStreamType.Audio, 1);
            }
        }

        private int? GetMediaStreamCount(MediaStreamType type, int limit)
        {
            var count = MediaSource.GetStreamCount(type);

            if (count.HasValue)
            {
                count = Math.Min(count.Value, limit);
            }

            return count;
        }

        public List<MediaStream> GetSelectableAudioStreams()
        {
            return GetSelectableStreams(MediaStreamType.Audio);
        }

        public List<MediaStream> GetSelectableSubtitleStreams()
        {
            return GetSelectableStreams(MediaStreamType.Subtitle);
        }

        public List<MediaStream> GetSelectableStreams(MediaStreamType type)
        {
            List<MediaStream> list = new List<MediaStream>();

            foreach (MediaStream stream in MediaSource.MediaStreams)
            {
                if (type == stream.Type)
                {
                    list.Add(stream);
                }
            }

            return list;
        }
    }
}
