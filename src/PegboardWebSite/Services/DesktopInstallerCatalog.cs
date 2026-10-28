using System.Text.RegularExpressions;

namespace PegboardWebSite.Services;

/// <summary>
/// The Windows desktop installers the site can actually serve (ado:2173).
///
/// The source of truth is the set of files in <c>wwwroot/downloads</c> named
/// <c>ePegboardSetup_{version}.msi</c>. There is deliberately no second list (no manifest,
/// no config value, no hardcoded "latest"): the advertised version is derived from the
/// files present, so the site cannot advertise a version it cannot serve. Before this
/// class, DownloadLink hardcoded "1.6.0" while the server held no installer at all.
///
/// The installers are NOT in source control and are not part of the site publish. They are
/// placed by the desktop repo's publish step (Pegboard: Setup/publish-installer.ps1), and the
/// site deploy (deploy/deploy-vps.ps1) overlays rather than wipes, so a deploy leaves them in
/// place. The download check (.agents/skills/_shared/desktop-download-check.mjs in the venture
/// repo) catches the case where they go missing anyway.
/// </summary>
public sealed class DesktopInstallerCatalog
{
    public const string FilePrefix = "ePegboardSetup_";
    public const string FileExtension = ".msi";
    public const string MsiContentType = "application/x-msi";

    // Digits and dots only, 2-4 parts. This is also the path-traversal guard: a requested
    // version is only ever combined into a file name after it has matched this pattern.
    private static readonly Regex VersionPattern = new(@"^\d{1,6}(\.\d{1,6}){1,3}$", RegexOptions.CultureInvariant);

    private readonly string _directory;

    public DesktopInstallerCatalog(string downloadsDirectory)
    {
        _directory = downloadsDirectory;
    }

    public static string FileNameFor(string version) => $"{FilePrefix}{version}{FileExtension}";

    public static bool IsWellFormedVersion(string? version) =>
        !string.IsNullOrWhiteSpace(version) && VersionPattern.IsMatch(version);

    /// <summary>Every installer present, newest first.</summary>
    public IReadOnlyList<DesktopInstaller> Available()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<DesktopInstaller>();

        var found = new List<(Version Parsed, DesktopInstaller Installer)>();
        foreach (var path in Directory.EnumerateFiles(_directory, $"{FilePrefix}*{FileExtension}"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var versionPart = name.Substring(FilePrefix.Length);
            if (!IsWellFormedVersion(versionPart) || !Version.TryParse(versionPart, out var parsed))
                continue;

            found.Add((parsed, new DesktopInstaller(versionPart, path, Path.GetFileName(path))));
        }

        return found.OrderByDescending(f => f.Parsed).Select(f => f.Installer).ToList();
    }

    public DesktopInstaller? Latest() => Available().FirstOrDefault();

    /// <summary>
    /// Resolve a request. No version means the latest; a supplied version is honoured
    /// exactly and never falls through to the latest.
    /// </summary>
    public InstallerLookup Find(string? requestedVersion)
    {
        var available = Available();
        var latest = available.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return latest == null
                ? new InstallerLookup(InstallerLookupStatus.NoneAvailable, null, null, null)
                : new InstallerLookup(InstallerLookupStatus.Found, latest, null, latest);
        }

        var requested = requestedVersion.Trim();
        if (!IsWellFormedVersion(requested) || !Version.TryParse(requested, out var parsedRequest))
            return new InstallerLookup(InstallerLookupStatus.InvalidVersion, null, null, latest);

        var match = available.FirstOrDefault(i => Version.Parse(i.Version) == parsedRequest);
        return match == null
            ? new InstallerLookup(InstallerLookupStatus.VersionNotAvailable, null, requested, latest)
            : new InstallerLookup(InstallerLookupStatus.Found, match, requested, latest);
    }
}

public sealed record DesktopInstaller(string Version, string FullPath, string FileName);

public enum InstallerLookupStatus
{
    Found,
    NoneAvailable,
    InvalidVersion,
    VersionNotAvailable,
}

/// <param name="Requested">The requested version, only when it was well formed (safe to echo).</param>
public sealed record InstallerLookup(
    InstallerLookupStatus Status,
    DesktopInstaller? Installer,
    string? Requested,
    DesktopInstaller? Latest)
{
    /// <summary>A plain-English explanation for anything other than <see cref="InstallerLookupStatus.Found"/>.</summary>
    public string Explanation => Status switch
    {
        InstallerLookupStatus.Found => string.Empty,
        InstallerLookupStatus.NoneAvailable =>
            "The Windows installer is not available at the moment. Please get in touch through https://www.epegboard.com/contact and we will send it to you.",
        InstallerLookupStatus.InvalidVersion =>
            "That is not a valid version number." + LatestSuffix,
        InstallerLookupStatus.VersionNotAvailable =>
            $"Version {Requested} of the ePegboard desktop app is not available." + LatestSuffix,
        _ => string.Empty,
    };

    private string LatestSuffix => Latest == null
        ? " No installer is available at the moment."
        : $" The current version is {Latest.Version}.";
}
