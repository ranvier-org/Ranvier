namespace Ranvier.Dom

open System

/// <summary><c>MediaDecodingType</c>.</summary>
[<RequireQualifiedAccess>]
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type MediaDecodingType =
    | File
    | MediaSource
    | WebRtc

#if !FABLE_COMPILER
    /// <summary>Returns the declared union case name.</summary>
    override this.ToString() =
        match this with
        | MediaDecodingType.File -> "File"
        | MediaDecodingType.MediaSource -> "MediaSource"
        | MediaDecodingType.WebRtc -> "WebRtc"
#endif

/// <summary><c>MediaEncodingType</c>.</summary>
[<RequireQualifiedAccess>]
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type MediaEncodingType =
    | Record
    | WebRtc

#if !FABLE_COMPILER
    /// <summary>Returns the declared union case name.</summary>
    override this.ToString() =
        match this with
        | MediaEncodingType.Record -> "Record"
        | MediaEncodingType.WebRtc -> "WebRtc"
#endif

/// <summary><c>HdrMetadataType</c>.</summary>
[<RequireQualifiedAccess>]
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type MediaHdrMetadataType =
    | SmpteSt2086
    | SmpteSt2094_10
    | SmpteSt2094_40

#if !FABLE_COMPILER
    /// <summary>Returns the declared union case name.</summary>
    override this.ToString() =
        match this with
        | MediaHdrMetadataType.SmpteSt2086 -> "SmpteSt2086"
        | MediaHdrMetadataType.SmpteSt2094_10 -> "SmpteSt2094_10"
        | MediaHdrMetadataType.SmpteSt2094_40 -> "SmpteSt2094_40"
#endif

/// <summary><c>ColorGamut</c>.</summary>
[<RequireQualifiedAccess>]
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type MediaColorGamut =
    | Srgb
    | P3
    | Rec2020

#if !FABLE_COMPILER
    /// <summary>Returns the declared union case name.</summary>
    override this.ToString() =
        match this with
        | MediaColorGamut.Srgb -> "Srgb"
        | MediaColorGamut.P3 -> "P3"
        | MediaColorGamut.Rec2020 -> "Rec2020"
#endif

/// <summary><c>TransferFunction</c>.</summary>
[<RequireQualifiedAccess>]
#if !FABLE_COMPILER
[<System.Diagnostics.DebuggerDisplay("{ToString(),nq}")>]
#endif
type MediaTransferFunction =
    | Srgb
    | Pq
    | Hlg

#if !FABLE_COMPILER
    /// <summary>Returns the declared union case name.</summary>
    override this.ToString() =
        match this with
        | MediaTransferFunction.Srgb -> "Srgb"
        | MediaTransferFunction.Pq -> "Pq"
        | MediaTransferFunction.Hlg -> "Hlg"
#endif

/// <summary><c>VideoConfiguration</c>; <c>None</c> leaves an optional member absent.</summary>
type MediaVideoConfiguration =
    {
        ContentType: string
        Width: uint32
        Height: uint32
        Bitrate: uint64
        Framerate: float
        AlphaChannel: bool option
        HdrMetadataType: MediaHdrMetadataType option
        ColorGamut: MediaColorGamut option
        TransferFunction: MediaTransferFunction option
    }

/// <summary><c>AudioConfiguration</c>; <c>None</c> leaves an optional member absent.</summary>
type MediaAudioConfiguration =
    {
        ContentType: string
        Channels: string option
        Bitrate: uint64 option
        Samplerate: uint32 option
        SpatialRendering: bool option
    }

/// <summary><c>MediaCapabilitiesInfo</c>.</summary>
type MediaCapabilitiesInfo =
    {
        Supported: bool
        Smooth: bool
        PowerEfficient: bool
    }

/// <summary>
/// <c>navigator.mediaCapabilities.decodingInfo()</c> and <c>encodingInfo()</c> for one document (Ranvier.WebCore #116).
/// </summary>
/// <remarks>
/// A configuration the engine rejects settles with <c>ResourceFailure.LoadFailed "TypeError"</c>; each other
/// configuration succeeds with the engine's answer. Terminal callbacks run on the service's owner context, after the
/// starting call returns. A query enables no playback, codec or capture: an engine without a media engine
/// configuration provider answers every valid configuration as unsupported, not smooth and not power efficient, and
/// reports the provider through the <c>media-decoding-provider</c> and <c>media-encoding-provider</c> capabilities.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type MediaCapabilitiesService =
    inherit IDisposable

    /// <summary><c>decodingInfo({ type, video, audio })</c>.</summary>
    abstract DecodingInfo:
        kind: MediaDecodingType *
        video: MediaVideoConfiguration option *
        audio: MediaAudioConfiguration option *
        terminal: (ResourceOutcome<MediaCapabilitiesInfo> -> unit) ->
            ResourceOperation<MediaCapabilitiesInfo>

    /// <summary><c>encodingInfo({ type, video, audio })</c>.</summary>
    abstract EncodingInfo:
        kind: MediaEncodingType *
        video: MediaVideoConfiguration option *
        audio: MediaAudioConfiguration option *
        terminal: (ResourceOutcome<MediaCapabilitiesInfo> -> unit) ->
            ResourceOperation<MediaCapabilitiesInfo>

/// <summary>Values shared by media capability providers.</summary>
module MediaCapabilitiesContract =
    /// <summary>The failure of a configuration the engine rejects.</summary>
    let rejected = ResourceFailure.LoadFailed "TypeError"

    /// <summary>Raises ArgumentNullException for a null content type in either configuration.</summary>
    let configuration (video: MediaVideoConfiguration option) (audio: MediaAudioConfiguration option) =
        match video with
        | Some value when isNull value.ContentType -> nullArg "video"
        | _ -> ()

        match audio with
        | Some value when isNull value.ContentType -> nullArg "audio"
        | Some { Channels = Some channels } when isNull channels -> nullArg "audio"
        | _ -> ()
