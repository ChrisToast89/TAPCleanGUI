// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

using System.Text.Json;

namespace TAPCleanGui.Core;

public static class SettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string DefaultSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TAPCleanGUI",
            "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultSettingsPath;
        try
        {
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Normalize(settings);
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        path ??= DefaultSettingsPath;
        Normalize(settings);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    public static void RememberFile(AppSettings settings, string path)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(path))
            return;

        var full = Path.GetFullPath(path);
        settings.RecentFiles ??= new List<string>();
        settings.RecentFiles.RemoveAll(existing =>
            string.Equals(existing, full, StringComparison.OrdinalIgnoreCase));
        settings.RecentFiles.Insert(0, full);
        if (settings.RecentFiles.Count > AppSettings.MaxRecentFiles)
        {
            settings.RecentFiles.RemoveRange(
                AppSettings.MaxRecentFiles,
                settings.RecentFiles.Count - AppSettings.MaxRecentFiles);
        }
    }

    public static void Normalize(AppSettings settings)
    {
        // Empty stays empty. A saved path is never replaced by auto-discovery.
        settings.TapCleanExePath = (settings.TapCleanExePath ?? "").Trim();
        settings.ViceExePath = (settings.ViceExePath ?? "").Trim();
        settings.Tolerance = Math.Clamp(settings.Tolerance, 0, 15);
        settings.LastOutputFolder ??= "";
        settings.RecentFiles ??= new List<string>();
        if (settings.RecentFiles.Count > AppSettings.MaxRecentFiles)
        {
            settings.RecentFiles = settings.RecentFiles
                .Take(AppSettings.MaxRecentFiles)
                .ToList();
        }
    }
}
