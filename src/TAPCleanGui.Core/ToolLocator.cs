// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

namespace TAPCleanGui.Core;

/// <summary>
/// Finds TAPClean.exe and VICE x64sc.exe.
/// A configured path that points at an existing file always wins.
/// Discovery is not written back to settings.
/// </summary>
public static class ToolLocator
{
    static readonly string[] TapCleanFileNames = ["TAPClean.exe", "tapclean.exe"];
    static readonly string[] ViceFileNames = ["x64sc.exe"];

    public static string? ResolveTapClean(string? configuredPath, string? baseDirectory = null, string? pathVariable = null) =>
        Resolve(configuredPath, TapCleanFileNames, baseDirectory, pathVariable);

    public static string? ResolveVice(string? configuredPath, string? baseDirectory = null, string? pathVariable = null) =>
        Resolve(configuredPath, ViceFileNames, baseDirectory, pathVariable);

    /// <summary>Auto-detection only. Ignores any saved path.</summary>
    public static string? FindTapClean(string? baseDirectory = null, string? pathVariable = null) =>
        Discover(TapCleanFileNames, baseDirectory, pathVariable);

    /// <summary>Auto-detection only. Ignores any saved path.</summary>
    public static string? FindVice(string? baseDirectory = null, string? pathVariable = null) =>
        Discover(ViceFileNames, baseDirectory, pathVariable);

    static string? Resolve(string? configuredPath, string[] fileNames, string? baseDirectory, string? pathVariable)
    {
        var saved = ExistingFile(configuredPath);
        if (saved != null)
            return saved;
        return Discover(fileNames, baseDirectory, pathVariable);
    }

    static string? Discover(string[] fileNames, string? baseDirectory, string? pathVariable)
    {
        var beside = SearchDirectory(baseDirectory ?? AppContext.BaseDirectory, fileNames);
        if (beside != null)
            return beside;
        return SearchPath(pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? "", fileNames);
    }

    static string? ExistingFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var full = Path.GetFullPath(path.Trim());
            return File.Exists(full) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static string? SearchPath(string pathVariable, string[] fileNames)
    {
        var parts = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var dir = part.Trim().Trim('"');
            if (dir.Length == 0)
                continue;
            var found = SearchDirectory(dir, fileNames);
            if (found != null)
                return found;
        }

        return null;
    }

    static string? SearchDirectory(string? directory, string[] fileNames)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;
        try
        {
            if (!Directory.Exists(directory))
                return null;

            List<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            foreach (var name in fileNames)
            {
                foreach (var file in files)
                {
                    if (string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
                        return Path.GetFullPath(file);
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }
}
