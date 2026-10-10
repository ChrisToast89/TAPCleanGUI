// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

namespace TAPCleanGui.Core;

public sealed class AppSettings
{
    public const int MaxRecentFiles = 10;

    public string TapCleanExePath { get; set; } = "";
    public string ViceExePath { get; set; } = "";
    public int Tolerance { get; set; } = 10;
    public bool Reckless { get; set; }
    public string LastOutputFolder { get; set; } = "";
    public bool OutputSameAsInput { get; set; } = true;
    public List<string> RecentFiles { get; set; } = new();
}
