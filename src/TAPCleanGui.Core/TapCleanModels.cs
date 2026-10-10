// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

namespace TAPCleanGui.Core;

public enum TapCleanOperation
{
    Test,
    Clean,
    Wav,
    ListScanners,
}

public enum OverallResult
{
    Unknown = 0,
    Pass,
    Fail,
}

public sealed class TapCleanRequest
{
    public required string TapCleanExePath { get; init; }
    public string? InputFile { get; init; }
    public required TapCleanOperation Operation { get; init; }
    public int Tolerance { get; init; } = 10;
    public bool Reckless { get; init; }

    /// <summary>
    /// TAPClean writes cleaned tapes into its process working directory.
    /// Pass the chosen output folder here, and pass <see cref="InputFile"/> as a full path.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Where the copied tcreport is written. Defaults to <see cref="WorkingDirectory"/>.</summary>
    public string? ReportDirectory { get; init; }
}

public sealed record TapCleanResult
{
    public int ExitCode { get; init; }
    public bool Cancelled { get; init; }
    public OverallResult Overall { get; init; } = OverallResult.Unknown;
    public double? RecognizedPercent { get; init; }
    public int? ReadErrors { get; init; }
    public string? Checksum { get; init; }
    public string? Bootable { get; init; }
    public bool RefusedDueToReadErrors { get; init; }

    /// <summary>File name only, for example <c>clean.Sample Tape.tap</c>.</summary>
    public string? CleanedFileName { get; init; }

    /// <summary>Path text from a <c>Saved:</c> line, relative or absolute. No file-system check.</summary>
    public string? SavedTapPath { get; init; }

    public string? CleanedFilePath { get; init; }
    public string? ReportCopyPath { get; init; }
    public string Output { get; init; } = "";
}
