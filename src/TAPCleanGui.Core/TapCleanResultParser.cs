// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

using System.Globalization;
using System.Text.RegularExpressions;

namespace TAPCleanGui.Core;

/// <summary>
/// Pure parser for TAPClean console output. Exit code is not consulted:
/// TAPClean returns 0 even when the tape fails.
/// </summary>
public static partial class TapCleanResultParser
{
    public static TapCleanResult Parse(string? output)
    {
        output ??= "";

        var overall = OverallResult.Unknown;
        var overallMatch = OverallRegex().Match(output);
        if (overallMatch.Success)
        {
            overall = overallMatch.Groups[1].Value.Equals("PASS", StringComparison.OrdinalIgnoreCase)
                ? OverallResult.Pass
                : OverallResult.Fail;
        }

        double? recognized = null;
        var recognizedMatch = RecognizedRegex().Match(output);
        if (recognizedMatch.Success &&
            double.TryParse(recognizedMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            recognized = percent;
        }

        int? readErrors = null;
        var readMatch = ReadRegex().Match(output);
        if (readMatch.Success && int.TryParse(readMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var errors))
            readErrors = errors;

        string? checksum = null;
        var checksumMatch = ChecksumRegex().Match(output);
        if (checksumMatch.Success)
            checksum = (checksumMatch.Groups[1].Value + checksumMatch.Groups[2].Value).Trim();

        string? bootable = null;
        var bootableMatch = BootableRegex().Match(output);
        if (bootableMatch.Success)
            bootable = bootableMatch.Groups[1].Value.Trim();

        string? savedTap = null;
        string? cleanedName = null;
        foreach (Match match in SavedTapRegex().Matches(output))
        {
            var raw = match.Groups[1].Value.Trim().Trim('"');
            if (raw.Length == 0)
                continue;

            string name;
            try
            {
                name = Path.GetFileName(raw);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!name.StartsWith("clean.", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".tap", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            savedTap = raw;
            cleanedName = name;
        }

        return new TapCleanResult
        {
            Overall = overall,
            RecognizedPercent = recognized,
            ReadErrors = readErrors,
            Checksum = checksum,
            Bootable = bootable,
            RefusedDueToReadErrors = RefusedRegex().IsMatch(output),
            CleanedFileName = cleanedName,
            SavedTapPath = savedTap,
            Output = output,
        };
    }

    [GeneratedRegex(@"Overall\s+Result\s*:\s*(PASS|FAIL)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OverallRegex();

    [GeneratedRegex(@"Recognized\s*:\s*(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecognizedRegex();

    [GeneratedRegex(@"Read\s+test\s*:\s*(PASS|FAIL)\s*\[\s*(\d+)\s+Errors?\s*\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReadRegex();

    [GeneratedRegex(@"Checksum\s+test\s*:\s*(PASS|FAIL)([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChecksumRegex();

    [GeneratedRegex(@"Bootable\s*:\s*([^\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BootableRegex();

    // TAPClean prints either "Saved: clean.name.tap" or a full path. tcreport.txt must not match.
    [GeneratedRegex(@"(?m)^\s*Saved:\s*(.+?\.tap)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SavedTapRegex();

    [GeneratedRegex(@"contains read errors so it won['\u2019]t be cleaned", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RefusedRegex();
}
