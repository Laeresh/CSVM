using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSVM.Utils;

/// <summary>A published release: its tag and its release page.</summary>
public sealed record Release(string Tag, string Url);

/// <summary>
/// Asks GitHub once per launch whether a release newer than this build is published, for the
/// build stamp's notice line. The launcher calls it from an exported build only, so editor runs,
/// worktrees, the suites and agents never send the request. Every failure is one info line and
/// no notice. Nothing is downloaded: the notice only opens the release page.
/// </summary>
public static class ReleaseCheck
{
    /// <summary>The config key that turns the check off.</summary>
    public const string Key = "updates.checkReleases";

    /// <summary>The key's default: on.</summary>
    public const bool DefaultEnabled = true;

    // The newest published release. GitHub skips drafts and prereleases here, so a prerelease
    // never reaches a stable player.
    private const string LatestUrl = "https://api.github.com/repos/Laeresh/CSVM/releases/latest";

    private const double FetchSeconds = 5.0;

    // The only pages the notice may open, so an answer cannot hand the shell any other address.
    private const string ReleasesPage = "https://github.com/Laeresh/CSVM/releases/";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(FetchSeconds) };

    /// <summary>Starts the check on the thread pool and returns at once. The task never faults: it
    /// answers null on every failure, and when nothing newer is published.</summary>
    public static Task<Release?> Start(string current) => Task.Run(() => Fetch(current));

    /// <summary>Whether <paramref name="tag"/>, with or without a leading <c>v</c>, names a version
    /// above <paramref name="current"/>. A missing component counts as 0, so <c>0.4</c> equals
    /// <c>0.4.0</c>. Either side failing to parse is not newer.</summary>
    public static bool IsNewer(string tag, string current) =>
        Version.TryParse(tag.StartsWith('v') ? tag[1..] : tag, out var latest)
        && Version.TryParse(current, out var running)
        && Padded(latest) > Padded(running);

    /// <summary>The tag and release page out of a <c>releases/latest</c> answer, or null when either
    /// is missing or the page is not this repo's.</summary>
    public static Release? ParseAnswer(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String
            && root.TryGetProperty("html_url", out var url) && url.ValueKind == JsonValueKind.String
            && url.GetString()!.StartsWith(ReleasesPage, StringComparison.Ordinal)
                ? new Release(tag.GetString()!, url.GetString()!)
                : null;
    }

    // System.Version orders 0.4 below 0.4.0, since an absent component is -1.
    private static Version Padded(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    // Info, not Warn: an offline machine or GitHub's hourly rate limit is an expected answer, and
    // the player is owed nothing for it.
    private static async Task<Release?> Fetch(string current)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
            // GitHub refuses a request with no User-Agent.
            request.Headers.UserAgent.ParseAdd($"CSVM/{current}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var answer = await Http.SendAsync(request).ConfigureAwait(false);
            if (!answer.IsSuccessStatusCode)
            {
                string status = ((int)answer.StatusCode).ToString(CultureInfo.InvariantCulture);
                Log.Info("core", $"release check: GitHub answered {status}, no notice");
                return null;
            }

            var latest = ParseAnswer(await answer.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (latest == null || !IsNewer(latest.Tag, current))
            {
                Log.Info("core", $"release check: latest {latest?.Tag ?? "unreadable"}, running {current}, no notice");
                return null;
            }

            Log.Info("core", $"release check: {latest.Tag} available, running {current}");
            return latest;
        }
        catch (Exception e)
        {
            // The boundary of a fire-and-forget task: whatever fails here must end as the one line.
            Log.Info("core", $"release check failed, no notice: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }
}
