using System.Text.RegularExpressions;

namespace Domain.ValueObjects;

/// <summary>
/// Represents a semantic version following the SemVer 2.0.0 specification.
/// Immutable value object with validation and comparison capabilities.
/// </summary>
public sealed partial record SemanticVersion : IComparable<SemanticVersion>
{
    // SemVer regex pattern: https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
    private static readonly Regex SemVerPattern = SemVerRegex();

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }
    public string? BuildMetadata { get; }

    private SemanticVersion(int major, int minor, int patch, string? preRelease, string? buildMetadata)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major), "Major version cannot be negative.");
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor), "Minor version cannot be negative.");
        if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch), "Patch version cannot be negative.");

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease;
        BuildMetadata = string.IsNullOrWhiteSpace(buildMetadata) ? null : buildMetadata;
    }

    /// <summary>
    /// Creates a new SemanticVersion from components.
    /// </summary>
    public static SemanticVersion Create(int major, int minor, int patch, string? preRelease = null, string? buildMetadata = null)
        => new(major, minor, patch, preRelease, buildMetadata);

    /// <summary>
    /// Parses a semantic version string.
    /// </summary>
    /// <param name="version">Version string (e.g., "1.2.3", "1.2.3-beta", "1.2.3-beta.1+build.123")</param>
    /// <returns>Parsed SemanticVersion</returns>
    /// <exception cref="FormatException">Thrown when the version string is not a valid semantic version.</exception>
    public static SemanticVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new FormatException("Version string cannot be null or empty.");

        var match = SemVerPattern.Match(version.Trim());
        if (!match.Success)
            throw new FormatException($"'{version}' is not a valid semantic version.");

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);
        var patch = int.Parse(match.Groups["patch"].Value);
        var preRelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null;
        var buildMetadata = match.Groups["buildmetadata"].Success ? match.Groups["buildmetadata"].Value : null;

        return new SemanticVersion(major, minor, patch, preRelease, buildMetadata);
    }

    /// <summary>
    /// Tries to parse a semantic version string.
    /// </summary>
    public static bool TryParse(string? version, out SemanticVersion? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(version))
            return false;

        try
        {
            result = Parse(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Compares two semantic versions according to SemVer precedence rules.
    /// Build metadata is ignored in comparisons per SemVer spec.
    /// </summary>
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;

        // Compare major.minor.patch
        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0) return majorCompare;

        var minorCompare = Minor.CompareTo(other.Minor);
        if (minorCompare != 0) return minorCompare;

        var patchCompare = Patch.CompareTo(other.Patch);
        if (patchCompare != 0) return patchCompare;

        // Pre-release versions have lower precedence than normal versions
        // e.g., 1.0.0-alpha < 1.0.0
        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1; // This is stable, other is pre-release
        if (other.PreRelease is null) return -1; // This is pre-release, other is stable

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    private static int ComparePreRelease(string a, string b)
    {
        var partsA = a.Split('.');
        var partsB = b.Split('.');
        var maxParts = Math.Max(partsA.Length, partsB.Length);

        for (var i = 0; i < maxParts; i++)
        {
            if (i >= partsA.Length) return -1; // Fewer parts = lower precedence
            if (i >= partsB.Length) return 1;

            var partA = partsA[i];
            var partB = partsB[i];

            var aIsNumeric = int.TryParse(partA, out var numA);
            var bIsNumeric = int.TryParse(partB, out var numB);

            if (aIsNumeric && bIsNumeric)
            {
                var numCompare = numA.CompareTo(numB);
                if (numCompare != 0) return numCompare;
            }
            else if (aIsNumeric)
            {
                return -1; // Numeric < alphanumeric
            }
            else if (bIsNumeric)
            {
                return 1;
            }
            else
            {
                var strCompare = string.Compare(partA, partB, StringComparison.Ordinal);
                if (strCompare != 0) return strCompare;
            }
        }

        return 0;
    }

    /// <summary>
    /// Returns true if this version is newer than the other version.
    /// </summary>
    public bool IsNewerThan(SemanticVersion other) => CompareTo(other) > 0;

    /// <summary>
    /// Returns true if this version is older than the other version.
    /// </summary>
    public bool IsOlderThan(SemanticVersion other) => CompareTo(other) < 0;

    /// <summary>
    /// Returns true if the two versions are compatible (same major version).
    /// Per SemVer, versions with the same major version should be backwards compatible.
    /// </summary>
    public bool IsCompatibleWith(SemanticVersion other) => Major == other.Major;

    /// <summary>
    /// Returns true if this version represents a breaking change from the previous version
    /// (i.e., major version increased).
    /// </summary>
    public bool IsBreakingChangeFrom(SemanticVersion previous) => Major > previous.Major;

    /// <summary>
    /// Returns true if this is a pre-release version.
    /// </summary>
    public bool IsPreRelease => PreRelease is not null;

    /// <summary>
    /// Returns true if this is a stable (non-pre-release) version.
    /// </summary>
    public bool IsStable => PreRelease is null;

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (PreRelease is not null) version += $"-{PreRelease}";
        if (BuildMetadata is not null) version += $"+{BuildMetadata}";
        return version;
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$", RegexOptions.Compiled)]
    private static partial Regex SemVerRegex();
}
