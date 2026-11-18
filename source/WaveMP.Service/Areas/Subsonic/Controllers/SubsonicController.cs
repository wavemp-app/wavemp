using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using WaveMP.Core.Subsonic.Annotations;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Browsing;
using WaveMP.Core.Subsonic.Media;
using WaveMP.Core.Subsonic.Playlists;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Service.Extensions;

namespace WaveMP.Service.Areas.Subsonic.Controllers;

/// <summary>
/// The Subsonic API (version 1.16.1 with OpenSubsonic extensions) under <c>/rest</c> for Subsonic apps. Every endpoint
/// answers GET and POST (form parameters, OpenSubsonic <c>formPost</c>), with or without the suffix <c>.view</c>, in
/// XML (default), JSON or JSONP (parameter <c>f</c>). Apps sign in with the name of a profile and its app password
/// (parameters <c>u</c> and <c>p</c>, or <c>t</c> and <c>s</c>) or with the app password as API key
/// (<c>apiKey</c>); see <see cref="ISubsonicAuthenticator"/>. Errors are Subsonic errors with HTTP 200, as the
/// specification demands. Not part of the OpenAPI document: the Subsonic specification is its contract.
/// </summary>
[AllowAnonymous]
[Area(SubsonicDefaults.Area)]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableCors(SubsonicDefaults.CorsPolicy)]
[Route(SubsonicDefaults.RoutePrefix)]
public sealed class SubsonicController(
    ISubsonicAuthenticator authenticator,
    ISubsonicBrowsingService browsing,
    ISubsonicPlaylistService playlists,
    ISubsonicAnnotationService annotations,
    ISubsonicMediaService media,
    ISubsonicTranscoder transcoder,
    ILogger<SubsonicController> logger) : ControllerBase
{
    private static readonly string _serverVersion = GetServerVersion();

    /// <summary>
    /// Answers an endpoint of the Subsonic API.
    /// </summary>
    /// <param name="endpoint">Name of the endpoint, e.g. <c>ping</c> or <c>ping.view</c>.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    [AcceptVerbs("GET", "POST", Route = "{endpoint}")]
    public async Task<IActionResult> HandleAsync(string endpoint, CancellationToken cancellationToken)
    {
        SubsonicParameters parameters = await ReadParametersAsync(cancellationToken);
        string name = endpoint.EndsWith(SubsonicDefaults.ViewSuffix, StringComparison.OrdinalIgnoreCase)
            ? endpoint[..^SubsonicDefaults.ViewSuffix.Length]
            : endpoint;

        // The only endpoint without sign-in: apps ask for the extensions before they know how to sign in.
        if (string.Equals(name, "getOpenSubsonicExtensions", StringComparison.OrdinalIgnoreCase))
        {
            return Answer(SubsonicSystemResponses.OpenSubsonicExtensions(), parameters);
        }

        if (!IsKnown(name))
        {
            return NotFound();
        }

        SubsonicAuthentication authentication = await authenticator.AuthenticateAsync(
            SubsonicCredentials.From(parameters),
            cancellationToken);
        if (authentication.User is not { } user)
        {
            logger.LogDebug("Subsonic sign-in failed with error {Code}", authentication.Error);
            return Answer(SubsonicResponse.Failed(authentication.Error ?? SubsonicErrorCode.WrongCredentials), parameters);
        }

        switch (name.ToLowerInvariant())
        {
            case "stream":
                return await StreamAsync(
                    await media.PrepareStreamAsync(user, parameters, cancellationToken),
                    parameters,
                    cancellationToken);
            case "download":
                return Download(await media.PrepareDownloadAsync(user, parameters, cancellationToken), parameters);
            case "getcoverart":
                return Cover(await media.GetCoverAsync(user, parameters, cancellationToken), parameters);
        }

        SubsonicResponse response = name.ToLowerInvariant() switch
        {
            "ping" => SubsonicSystemResponses.Ping(),
            "getlicense" => SubsonicSystemResponses.License(),
            "tokeninfo" => SubsonicSystemResponses.TokenInfo(user),
            "getmusicfolders" => browsing.GetMusicFolders(),
            "getindexes" => await browsing.GetIndexesAsync(user, cancellationToken),
            "getartists" => await browsing.GetArtistsAsync(user, cancellationToken),
            "getartist" => await browsing.GetArtistAsync(user, parameters, cancellationToken),
            "getalbum" => await browsing.GetAlbumAsync(user, parameters, cancellationToken),
            "getsong" => await browsing.GetSongAsync(user, parameters, cancellationToken),
            "getmusicdirectory" => await browsing.GetMusicDirectoryAsync(user, parameters, cancellationToken),
            "getalbumlist2" => await browsing.GetAlbumList2Async(user, parameters, cancellationToken),
            "getrandomsongs" => await browsing.GetRandomSongsAsync(user, parameters, cancellationToken),
            "search3" => await browsing.Search3Async(user, parameters, cancellationToken),
            "getstarred2" => await browsing.GetStarred2Async(user, cancellationToken),
            "getplaylists" => await playlists.GetPlaylistsAsync(user, parameters, cancellationToken),
            "getplaylist" => await playlists.GetPlaylistAsync(user, parameters, cancellationToken),
            "createplaylist" => await playlists.CreatePlaylistAsync(user, parameters, cancellationToken),
            "updateplaylist" => await playlists.UpdatePlaylistAsync(user, parameters, cancellationToken),
            "deleteplaylist" => await playlists.DeletePlaylistAsync(user, parameters, cancellationToken),
            "star" => await annotations.StarAsync(user, parameters, cancellationToken),
            "unstar" => await annotations.UnstarAsync(user, parameters, cancellationToken),
            "setrating" => await annotations.SetRatingAsync(user, parameters, cancellationToken),
            "scrobble" => await annotations.ScrobbleAsync(user, parameters, cancellationToken),
            "getnowplaying" => await annotations.GetNowPlayingAsync(user, cancellationToken),
            _ => SubsonicResponse.Failed(SubsonicErrorCode.Generic),
        };
        return Answer(response, parameters);
    }

    /// <summary>
    /// The endpoints of the Subsonic API that WaveMP answers; others are 404, like unknown paths.
    /// </summary>
    private static bool IsKnown(string name)
    {
        return name.ToLowerInvariant() is "ping" or "getlicense" or "tokeninfo" or "getmusicfolders" or "getindexes"
            or "getartists" or "getartist" or "getalbum" or "getsong" or "getmusicdirectory" or "getalbumlist2"
            or "getrandomsongs" or "search3" or "getstarred2" or "getplaylists" or "getplaylist" or "createplaylist"
            or "updateplaylist" or "deleteplaylist" or "star" or "unstar" or "setrating" or "scrobble"
            or "getnowplaying" or "stream" or "download" or "getcoverart";
    }

    private async Task<IActionResult> StreamAsync(
        SubsonicStreamResult result,
        SubsonicParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!result.IsFound)
        {
            return Answer(SubsonicResponse.Failed(result.Error ?? SubsonicErrorCode.NotFound), parameters);
        }

        if (!result.Plan.Transcode)
        {
            return PhysicalFile(
                result.Source.FullPath,
                result.Source.ContentType,
                result.Source.LastModified,
                new EntityTagHeaderValue($"\"{result.Source.ETag}\""),
                enableRangeProcessing: true);
        }

        using ISubsonicTranscode? transcode = transcoder.Start(result.Source.FullPath, result.Plan);
        if (transcode is null)
        {
            return Answer(SubsonicResponse.Failed(SubsonicErrorCode.Generic), parameters);
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = SubsonicTranscodeFormats.ContentTypeOf(result.Plan.Format);
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        try
        {
            await transcode.Output.CopyToAsync(Response.Body, cancellationToken);
            int exitCode = await transcode.Exited.WaitAsync(cancellationToken);
            if (exitCode != 0)
            {
                logger.LogWarning(
                    "ffmpeg ended with {ExitCode} while transcoding {File} for the Subsonic API: {Error}",
                    exitCode,
                    result.Source.FullPath,
                    transcode.ErrorOutput);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app closed the stream (skipped, paused for long, seeked); disposing the transcode stops ffmpeg.
        }

        return new EmptyResult();
    }

    private IActionResult Download(SubsonicStreamResult result, SubsonicParameters parameters)
    {
        if (!result.IsFound)
        {
            return Answer(SubsonicResponse.Failed(result.Error ?? SubsonicErrorCode.NotFound), parameters);
        }

        PhysicalFileResult file = PhysicalFile(
            result.Source.FullPath,
            result.Source.ContentType,
            Path.GetFileName(result.Source.FullPath),
            result.Source.LastModified,
            new EntityTagHeaderValue($"\"{result.Source.ETag}\""),
            enableRangeProcessing: true);
        return file;
    }

    private IActionResult Cover(SubsonicCoverResult result, SubsonicParameters parameters)
    {
        if (result.Delivery is not { } delivery)
        {
            return Answer(SubsonicResponse.Failed(result.Error ?? SubsonicErrorCode.NotFound), parameters);
        }

        Response.Headers.CacheControl = ControllerBaseExtensions.CoverCacheControl;
        return File(
            delivery.Content,
            delivery.ContentType,
            delivery.LastModified,
            new EntityTagHeaderValue($"\"{delivery.ETag}\""));
    }

    private FileContentResult Answer(SubsonicResponse response, SubsonicParameters parameters)
    {
        (byte[] body, string contentType) = SubsonicResponseWriter.Write(
            response,
            SubsonicResponseWriter.ParseFormat(parameters.Get("f")),
            _serverVersion,
            parameters.Get("callback"));
        Response.Headers.CacheControl = "no-store";
        return File(body, contentType);
    }

    /// <summary>
    /// The parameters of the query and, for POST with a form, of the form.
    /// </summary>
    private async Task<SubsonicParameters> ReadParametersAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> values = new(StringComparer.Ordinal);
        foreach ((string key, StringValues value) in Request.Query)
        {
            Add(key, value);
        }

        if (Request.HasFormContentType)
        {
            IFormCollection form = await Request.ReadFormAsync(cancellationToken);
            foreach ((string key, StringValues value) in form)
            {
                Add(key, value);
            }
        }

        return new SubsonicParameters(values.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal));

        void Add(string key, StringValues value)
        {
            if (!values.TryGetValue(key, out List<string>? list))
            {
                list = [];
                values[key] = list;
            }

            foreach (string? item in value)
            {
                if (item is not null)
                {
                    list.Add(item);
                }
            }
        }
    }

    private static string GetServerVersion()
    {
        string? version = typeof(SubsonicController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // Strip the source revision ("+<commit>") that the SDK appends.
        return version?.Split('+')[0] ?? "unknown";
    }
}
