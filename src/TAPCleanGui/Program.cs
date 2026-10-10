// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

namespace TAPCleanGui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
