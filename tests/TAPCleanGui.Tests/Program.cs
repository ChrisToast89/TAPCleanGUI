// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

using TAPCleanGui.Core;

internal static class Program
{
    const string GoodSample =
        "Recognized  : 100%\n" +
        "Data Files  : 14\n" +
        "Bootable    : YES (2 parts, 1st name: SAMPLE TAPE)\n" +
        "Overall Result    : PASS\n" +
        "Header test       : PASS [Sig: OK] [Ver: OK] [Siz: OK]\n" +
        "Recognition test  : PASS [559097 of 559097 bytes accounted for] [100%]\n" +
        "Checksum test     : PASS [14 of 14 checksummed files OK]\n" +
        "Read test         : PASS [0 Errors]\n" +
        "Optimization test : PASS [14 of 14 files OK]\n";

    const string BadSample =
        "Recognized  : 42%\n" +
        "Overall Result    : FAIL\n" +
        "Read test         : FAIL [76 Errors]\n";

    const string SkipHelp =
        "Set TAPCLEAN_GUI_GOOD_TAP (a tape that passes and cleans), " +
        "TAPCLEAN_GUI_BAD_TAP (a tape with read errors that TAPClean refuses to clean), " +
        "and optionally TAPCLEAN_GUI_TAPCLEAN_EXE. " +
        "Command-line overrides: --good <path> --bad <path> --tapclean <path>. " +
        "When TAPCLEAN_GUI_TAPCLEAN_EXE and --tapclean are omitted, TAPClean.exe is taken from " +
        "next to this program or from PATH. Integration tests were not run.";

    static int _passed;
    static int _failed;
    static int _skipped;

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("TAPClean GUI tests");
        Console.WriteLine();
        RunParserTests();
        RunArgumentTests();
        RunSettingsTests();
        RunLocatorTests();
        await RunIntegrationAsync(args);
        Console.WriteLine();
        Console.WriteLine($"Summary: {_passed} passed, {_failed} failed, {_skipped} skipped");
        return _failed == 0 ? 0 : 1;
    }

    static void RunParserTests()
    {
        var good = TapCleanResultParser.Parse(GoodSample);
        Check("parser good overall", good.Overall == OverallResult.Pass, "overall " + good.Overall);
        Check("parser good recognized", Nearly(good.RecognizedPercent, 100), "recognized " + good.RecognizedPercent);
        Check("parser good read errors", good.ReadErrors == 0, "read errors " + good.ReadErrors);
        Check("parser good checksum", good.Checksum == "PASS [14 of 14 checksummed files OK]", "checksum " + good.Checksum);
        Check("parser good bootable", good.Bootable != null && good.Bootable.Contains("YES", StringComparison.Ordinal) && good.Bootable.Contains("SAMPLE TAPE", StringComparison.Ordinal), "bootable " + good.Bootable);
        Check("parser good not refused", !good.RefusedDueToReadErrors, "refused");
        Check("parser good no clean name", good.CleanedFileName == null, good.CleanedFileName ?? "");

        var bad = TapCleanResultParser.Parse(BadSample);
        Check("parser bad overall", bad.Overall == OverallResult.Fail, "overall " + bad.Overall);
        Check("parser bad recognized", Nearly(bad.RecognizedPercent, 42), "recognized " + bad.RecognizedPercent);
        Check("parser bad read errors", bad.ReadErrors == 76, "read errors " + bad.ReadErrors);
        Check("parser bad not refused", !bad.RefusedDueToReadErrors, "refused");

        var refused = TapCleanResultParser.Parse(
            "sample.tap contains read errors so it won't be cleaned.\n" + BadSample);
        Check("parser refusal flag", refused.RefusedDueToReadErrors, "refused flag missing");
        Check("parser refusal still fail", refused.Overall == OverallResult.Fail, "overall " + refused.Overall);
        Check("parser refusal read errors", refused.ReadErrors == 76, "read errors " + refused.ReadErrors);

        var shortSave = TapCleanResultParser.Parse("Saved: clean.Sample Tape.tap\r\nSaved: tcreport.txt\r\n");
        Check("parser short saved name", shortSave.CleanedFileName == "clean.Sample Tape.tap", shortSave.CleanedFileName ?? "<null>");

        const string fullSaved = @"C:\Tools\TAPClean\clean.Sample Tape.tap";
        var fullSave = TapCleanResultParser.Parse("Saved: " + fullSaved + "\r\n");
        Check("parser full saved name", fullSave.CleanedFileName == "clean.Sample Tape.tap", fullSave.CleanedFileName ?? "<null>");
        Check("parser full saved path", fullSave.SavedTapPath == fullSaved, fullSave.SavedTapPath ?? "<null>");

        var reportOnly = TapCleanResultParser.Parse("Saved: tcreport.txt\r\n");
        Check("parser ignores tcreport", reportOnly.CleanedFileName == null && reportOnly.SavedTapPath == null, reportOnly.SavedTapPath ?? "");

        var empty = TapCleanResultParser.Parse("");
        Check("parser empty unknown", empty.Overall == OverallResult.Unknown && empty.ReadErrors == null && !empty.RefusedDueToReadErrors, "empty parse");
    }

    static void RunArgumentTests()
    {
        var clean = TapCleanRunner.BuildStartInfo(new TapCleanRequest
        {
            TapCleanExePath = @"C:\Tools\TAPClean\TAPClean.exe",
            InputFile = @"C:\Tapes\Sample Tape.tap",
            Operation = TapCleanOperation.Clean,
            Tolerance = 12,
            Reckless = true,
            WorkingDirectory = @"C:\Out",
        });
        Check(
            "clean arguments",
            clean.WorkingDirectory == @"C:\Out" &&
            clean.ArgumentList.Count == 5 &&
            clean.ArgumentList[0] == "-o" &&
            clean.ArgumentList[1] == @"C:\Tapes\Sample Tape.tap" &&
            clean.ArgumentList[2] == "-tol" &&
            clean.ArgumentList[3] == "12" &&
            clean.ArgumentList[4] == "-reckless",
            string.Join(" | ", clean.ArgumentList));

        var clamped = TapCleanRunner.BuildStartInfo(new TapCleanRequest
        {
            TapCleanExePath = @"C:\Tools\TAPClean\TAPClean.exe",
            InputFile = @"C:\Tapes\a.tap",
            Operation = TapCleanOperation.Test,
            Tolerance = 99,
            WorkingDirectory = @"C:\Out",
        });
        Check(
            "tolerance clamped",
            clamped.ArgumentList.Count == 4 && clamped.ArgumentList[0] == "-t" && clamped.ArgumentList[3] == "15",
            string.Join(" | ", clamped.ArgumentList));

        var list = TapCleanRunner.BuildStartInfo(new TapCleanRequest
        {
            TapCleanExePath = @"C:\Tools\TAPClean\TAPClean.exe",
            Operation = TapCleanOperation.ListScanners,
            WorkingDirectory = @"C:\Out",
        });
        Check("list arguments", list.ArgumentList.Count == 1 && list.ArgumentList[0] == "-list", string.Join(" | ", list.ArgumentList));

        var wav = TapCleanRunner.BuildStartInfo(new TapCleanRequest
        {
            TapCleanExePath = @"C:\Tools\TAPClean\TAPClean.exe",
            InputFile = @"C:\Tapes\a.tap",
            Operation = TapCleanOperation.Wav,
            WorkingDirectory = @"C:\Out",
        });
        Check(
            "wav arguments",
            wav.ArgumentList.Count == 2 && wav.ArgumentList[0] == "-wav" && wav.ArgumentList[1] == @"C:\Tapes\a.tap",
            string.Join(" | ", wav.ArgumentList));
    }

    static void RunSettingsTests()
    {
        var settings = new AppSettings { Tolerance = 99, RecentFiles = new List<string>() };
        SettingsStore.RememberFile(settings, @"C:\Tapes\b.tap");
        SettingsStore.RememberFile(settings, @"C:\Tapes\a.tap");
        SettingsStore.RememberFile(settings, @"c:\Tapes\b.tap");
        Check("recent moves duplicate to front", settings.RecentFiles.Count == 2 && settings.RecentFiles[0].Equals(@"C:\Tapes\b.tap", StringComparison.OrdinalIgnoreCase), string.Join(", ", settings.RecentFiles));

        for (var i = 0; i < 20; i++)
            SettingsStore.RememberFile(settings, @"C:\Tapes\file" + i + ".tap");
        Check("recent capped at 10", settings.RecentFiles.Count == AppSettings.MaxRecentFiles, "count " + settings.RecentFiles.Count);
        Check("recent newest first", settings.RecentFiles[0].EndsWith("file19.tap", StringComparison.OrdinalIgnoreCase), settings.RecentFiles[0]);

        var directory = Directory.CreateTempSubdirectory("TAPCleanGUI-settings").FullName;
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var missing = SettingsStore.Load(Path.Combine(directory, "missing.json"));
            Check("missing settings are defaults", missing.Tolerance == 10 && missing.TapCleanExePath == "" && missing.ViceExePath == "", missing.TapCleanExePath);
            Check("missing settings file was not created", !File.Exists(Path.Combine(directory, "missing.json")), "file appeared");

            settings.Tolerance = 40;
            settings.Reckless = true;
            settings.TapCleanExePath = @"C:\Tools\TAPClean\TAPClean.exe";
            settings.ViceExePath = @"C:\Tools\VICE\x64sc.exe";
            settings.LastOutputFolder = @"C:\Out";
            settings.OutputSameAsInput = false;
            SettingsStore.Save(settings, path);
            var loaded = SettingsStore.Load(path);
            Check("settings roundtrip tolerance", loaded.Tolerance == 15, "tolerance " + loaded.Tolerance);
            Check("settings roundtrip reckless", loaded.Reckless, "reckless");
            Check("settings roundtrip paths", loaded.TapCleanExePath == @"C:\Tools\TAPClean\TAPClean.exe" && loaded.ViceExePath == @"C:\Tools\VICE\x64sc.exe" && loaded.LastOutputFolder == @"C:\Out" && !loaded.OutputSameAsInput, "paths");
            Check("settings roundtrip recent", loaded.RecentFiles.Count == 10, "recent " + loaded.RecentFiles.Count);

            File.WriteAllText(path, "{ this is not json");
            var recovered = SettingsStore.Load(path);
            Check("corrupt settings fall back", recovered.Tolerance == 10 && recovered.TapCleanExePath == "" && recovered.ViceExePath == "", "tolerance " + recovered.Tolerance);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    static void RunLocatorTests()
    {
        var root = Directory.CreateTempSubdirectory("TAPCleanGUI-locator").FullName;
        try
        {
            var beside = Path.Combine(root, "beside");
            var pathDir = Path.Combine(root, "on-path");
            var emptyDir = Path.Combine(root, "empty");
            var savedDir = Path.Combine(root, "saved");
            Directory.CreateDirectory(beside);
            Directory.CreateDirectory(pathDir);
            Directory.CreateDirectory(emptyDir);
            Directory.CreateDirectory(savedDir);

            var besideTap = Path.Combine(beside, "tapclean.exe");
            var pathTap = Path.Combine(pathDir, "TAPClean.exe");
            var savedTap = Path.Combine(savedDir, "custom-tapclean.exe");
            var besideVice = Path.Combine(beside, "X64SC.EXE");
            var pathVice = Path.Combine(pathDir, "x64sc.exe");
            var savedVice = Path.Combine(savedDir, "my-x64sc.exe");
            File.WriteAllText(besideTap, "beside-tap");
            File.WriteAllText(pathTap, "path-tap");
            File.WriteAllText(savedTap, "saved-tap");
            File.WriteAllText(besideVice, "beside-vice");
            File.WriteAllText(pathVice, "path-vice");
            File.WriteAllText(savedVice, "saved-vice");

            var noPath = "";
            var pathEnv = Path.Combine(root, "missing-dir") + Path.PathSeparator + pathDir;

            Check("locator tapclean beside exe", SamePath(ToolLocator.ResolveTapClean(null, beside, noPath), besideTap), "beside");
            Check("locator tapclean on PATH", SamePath(ToolLocator.ResolveTapClean("", emptyDir, pathEnv), pathTap), "path");
            Check("locator tapclean beside wins over PATH", SamePath(ToolLocator.ResolveTapClean(null, beside, pathEnv), besideTap), "beside over path");
            Check("locator tapclean missing is null", ToolLocator.ResolveTapClean(null, emptyDir, noPath) == null, "expected null");
            Check(
                "locator saved tapclean wins",
                SamePath(ToolLocator.ResolveTapClean(savedTap, beside, pathEnv), savedTap),
                ToolLocator.ResolveTapClean(savedTap, beside, pathEnv) ?? "<null>");
            Check(
                "locator missing saved tapclean falls back",
                SamePath(ToolLocator.ResolveTapClean(Path.Combine(savedDir, "gone.exe"), beside, noPath), besideTap),
                "fallback");

            Check("locator vice beside exe", SamePath(ToolLocator.ResolveVice(null, beside, noPath), besideVice), "beside");
            Check("locator vice on PATH", SamePath(ToolLocator.ResolveVice(null, emptyDir, pathEnv), pathVice), "path");
            Check("locator vice missing is null", ToolLocator.ResolveVice("  ", emptyDir, noPath) == null, "expected null");
            Check(
                "locator saved vice wins",
                SamePath(ToolLocator.ResolveVice(savedVice, beside, pathEnv), savedVice),
                ToolLocator.ResolveVice(savedVice, beside, pathEnv) ?? "<null>");

            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new AppSettings
            {
                TapCleanExePath = savedTap,
                ViceExePath = savedVice,
                Tolerance = 11,
            };
            SettingsStore.Save(settings, settingsPath);
            var loaded = SettingsStore.Load(settingsPath);
            Check("saved path wins over discovery", SamePath(loaded.TapCleanExePath, savedTap) && SamePath(loaded.ViceExePath, savedVice), loaded.TapCleanExePath);
            Check(
                "saved path still resolves to itself",
                SamePath(ToolLocator.ResolveTapClean(loaded.TapCleanExePath, beside, pathEnv), savedTap) &&
                SamePath(ToolLocator.ResolveVice(loaded.ViceExePath, beside, pathEnv), savedVice),
                "resolved away from the saved file");

            loaded.Tolerance = 11;
            SettingsStore.Save(loaded, settingsPath);
            var again = SettingsStore.Load(settingsPath);
            Check("discovery does not overwrite saved paths", SamePath(again.TapCleanExePath, savedTap) && SamePath(again.ViceExePath, savedVice), again.TapCleanExePath + " / " + again.ViceExePath);

            var emptySettings = new AppSettings();
            SettingsStore.Normalize(emptySettings);
            Check("empty tool paths stay empty", emptySettings.TapCleanExePath == "" && emptySettings.ViceExePath == "", "normalized defaults");
            var emptyPath = Path.Combine(root, "empty-settings.json");
            SettingsStore.Save(emptySettings, emptyPath);
            var loadedEmpty = SettingsStore.Load(emptyPath);
            Check("saved empty paths stay empty", loadedEmpty.TapCleanExePath == "" && loadedEmpty.ViceExePath == "", loadedEmpty.TapCleanExePath);
        }
        finally
        {
            TryDelete(root);
        }
    }

    static async Task RunIntegrationAsync(string[] args)
    {
        var inputs = ReadInputs(args);
        if (inputs.GoodTape == null || inputs.BadTape == null)
        {
            Skip("integration", SkipHelp);
            return;
        }

        if (!File.Exists(inputs.GoodTape))
        {
            Fail("integration good tape", "good tape was not found");
            return;
        }

        if (!File.Exists(inputs.BadTape))
        {
            Fail("integration bad tape", "bad tape was not found");
            return;
        }

        string tapCleanExe;
        if (inputs.TapCleanExe != null)
        {
            if (!File.Exists(inputs.TapCleanExe))
            {
                Fail("integration TAPClean.exe", "TAPCLEAN_GUI_TAPCLEAN_EXE or --tapclean does not point at a file");
                return;
            }

            tapCleanExe = Path.GetFullPath(inputs.TapCleanExe);
        }
        else
        {
            var found = ToolLocator.ResolveTapClean(null);
            if (string.IsNullOrEmpty(found))
            {
                Skip(
                    "integration",
                    "TAPClean.exe was not found. Set TAPCLEAN_GUI_TAPCLEAN_EXE or pass --tapclean <path>, " +
                    "or place TAPClean.exe next to this program or on PATH. " + SkipHelp);
                return;
            }

            tapCleanExe = found;
        }

        var goodName = Path.GetFileName(inputs.GoodTape);
        var badName = Path.GetFileName(inputs.BadTape);
        if (string.IsNullOrEmpty(goodName) || string.IsNullOrEmpty(badName))
        {
            Fail("integration file names", "could not read the tape file names");
            return;
        }

        if (string.Equals(goodName, badName, StringComparison.OrdinalIgnoreCase))
        {
            Fail("integration file names", "good tape and bad tape must have different file names");
            return;
        }

        var goodStamp = File.GetLastWriteTimeUtc(inputs.GoodTape);
        var badStamp = File.GetLastWriteTimeUtc(inputs.BadTape);
        var goodLength = new FileInfo(inputs.GoodTape).Length;
        var badLength = new FileInfo(inputs.BadTape).Length;

        var temp = Directory.CreateTempSubdirectory("TAPCleanGUI-tests").FullName;
        try
        {
            var goodCopy = Path.Combine(temp, goodName);
            var badCopy = Path.Combine(temp, badName);
            File.Copy(inputs.GoodTape, goodCopy);
            File.Copy(inputs.BadTape, badCopy);
            var goodCopyLength = new FileInfo(goodCopy).Length;
            var badCopyLength = new FileInfo(badCopy).Length;
            var staleClean = Path.Combine(temp, "clean." + goodName);
            var staleWritten = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.WriteAllText(staleClean, "stale");
            File.SetLastWriteTimeUtc(staleClean, staleWritten);

            Console.WriteLine("Running test on good tape...");
            var testGood = await RunTapeAsync(tapCleanExe, goodCopy, temp, TapCleanOperation.Test, reckless: false);
            Check("good tape test PASS", testGood.Overall == OverallResult.Pass && !testGood.Cancelled && testGood.ExitCode == 0, Describe(testGood));
            Check("good tape test report", File.Exists(testGood.ReportCopyPath) && new FileInfo(testGood.ReportCopyPath!).Length > 0, testGood.ReportCopyPath ?? "<null>");
            Check("test ignores stale clean file", testGood.CleanedFilePath == null, testGood.CleanedFilePath ?? "<null>");
            Check(
                "stale clean file was not rewritten by test",
                File.Exists(staleClean) && File.GetLastWriteTimeUtc(staleClean) == staleWritten,
                File.Exists(staleClean) ? File.GetLastWriteTimeUtc(staleClean).ToString("o") : "missing");

            Console.WriteLine("Running clean on good tape...");
            var cleanGood = await RunTapeAsync(tapCleanExe, goodCopy, temp, TapCleanOperation.Clean, reckless: false);
            var expectedClean = Path.Combine(temp, "clean." + goodName);
            Check("good tape clean PASS", cleanGood.Overall == OverallResult.Pass && !cleanGood.Cancelled && !cleanGood.RefusedDueToReadErrors, Describe(cleanGood));
            Check("good tape clean file exists", File.Exists(expectedClean), "clean." + goodName);
            Check(
                "good tape cleaned path",
                cleanGood.CleanedFilePath != null &&
                string.Equals(Path.GetFullPath(cleanGood.CleanedFilePath), Path.GetFullPath(expectedClean), StringComparison.OrdinalIgnoreCase),
                cleanGood.CleanedFilePath ?? "<null>");
            Check("good tape clean report", File.Exists(cleanGood.ReportCopyPath), cleanGood.ReportCopyPath ?? "<null>");
            Check("good tape copy unchanged", new FileInfo(goodCopy).Length == goodCopyLength, "length changed");

            Console.WriteLine("Running test on bad tape...");
            var testBad = await RunTapeAsync(tapCleanExe, badCopy, temp, TapCleanOperation.Test, reckless: false);
            Check("bad tape test FAIL", testBad.Overall == OverallResult.Fail && !testBad.Cancelled, Describe(testBad));
            Check("bad tape read errors", testBad.ReadErrors is > 0, "read errors " + testBad.ReadErrors);

            var staleBad = Path.Combine(temp, "clean." + badName);
            File.WriteAllText(staleBad, "stale");
            File.SetLastWriteTimeUtc(staleBad, staleWritten);

            Console.WriteLine("Running clean on bad tape (expect refusal)...");
            var cleanBad = await RunTapeAsync(tapCleanExe, badCopy, temp, TapCleanOperation.Clean, reckless: false);
            Check("bad tape clean refused", cleanBad.RefusedDueToReadErrors && !cleanBad.Cancelled, Describe(cleanBad));
            Check("refused clean ignores stale file", cleanBad.CleanedFilePath == null && cleanBad.RefusedDueToReadErrors, cleanBad.CleanedFilePath ?? "<null>");
            Check(
                "stale clean file was not rewritten by refused clean",
                File.Exists(staleBad) && File.GetLastWriteTimeUtc(staleBad) == staleWritten,
                File.Exists(staleBad) ? File.GetLastWriteTimeUtc(staleBad).ToString("o") : "missing");
            Check("bad tape copy unchanged", new FileInfo(badCopy).Length == badCopyLength, "length changed");

            Console.WriteLine("Running list scanners...");
            var listed = await TapCleanRunner.RunAsync(
                new TapCleanRequest
                {
                    TapCleanExePath = tapCleanExe,
                    Operation = TapCleanOperation.ListScanners,
                    WorkingDirectory = temp,
                    ReportDirectory = temp,
                },
                _ => { },
                CancellationToken.None);
            Check("list scanners copies no report", listed.ReportCopyPath == null && listed.CleanedFilePath == null, "report=" + listed.ReportCopyPath + " cleaned=" + listed.CleanedFilePath);

            Console.WriteLine("Running cancelled test...");
            try
            {
                var cancelDir = Path.Combine(temp, "cancel");
                Directory.CreateDirectory(cancelDir);
                var cancelTape = Path.Combine(cancelDir, goodName);
                File.Copy(goodCopy, cancelTape);
                var cancelled = await RunCancelledAsync(tapCleanExe, cancelTape, cancelDir);
                Check("cancelled run", cancelled.Cancelled, Describe(cancelled));
                Check("cancelled run has no cleaned path", cancelled.CleanedFilePath == null, cancelled.CleanedFilePath ?? "<null>");
            }
            catch (Exception ex)
            {
                Fail("cancelled run does not throw", ex.GetType().Name + ": " + ex.Message);
            }

            Check(
                "original good tape untouched",
                File.GetLastWriteTimeUtc(inputs.GoodTape) == goodStamp && new FileInfo(inputs.GoodTape).Length == goodLength,
                "source stamp or length changed");
            Check(
                "original bad tape untouched",
                File.GetLastWriteTimeUtc(inputs.BadTape) == badStamp && new FileInfo(inputs.BadTape).Length == badLength,
                "source stamp or length changed");
        }
        catch (Exception ex)
        {
            Fail("integration exception", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            if (!TryDelete(temp))
                Fail("delete temp folder", "temp folder could not be deleted");
        }
    }

    sealed record TestInputs(string? GoodTape, string? BadTape, string? TapCleanExe);

    static TestInputs ReadInputs(string[] args)
    {
        var good = Environment.GetEnvironmentVariable("TAPCLEAN_GUI_GOOD_TAP");
        var bad = Environment.GetEnvironmentVariable("TAPCLEAN_GUI_BAD_TAP");
        var exe = Environment.GetEnvironmentVariable("TAPCLEAN_GUI_TAPCLEAN_EXE");
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--good" when i + 1 < args.Length:
                    good = args[++i];
                    break;
                case "--bad" when i + 1 < args.Length:
                    bad = args[++i];
                    break;
                case "--tapclean" when i + 1 < args.Length:
                    exe = args[++i];
                    break;
            }
        }

        return new TestInputs(BlankToNull(good), BlankToNull(bad), BlankToNull(exe));
    }

    static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    static async Task<TapCleanResult> RunCancelledAsync(string tapCleanExe, string tape, string directory)
    {
        using var cancellation = new CancellationTokenSource();
        var run = TapCleanRunner.RunAsync(
            new TapCleanRequest
            {
                TapCleanExePath = tapCleanExe,
                InputFile = tape,
                Operation = TapCleanOperation.Test,
                Tolerance = 10,
                WorkingDirectory = directory,
                ReportDirectory = directory,
            },
            _ => { },
            cancellation.Token);
        await Task.Delay(100);
        cancellation.Cancel();
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));
        if (finished != run)
        {
            cancellation.Cancel();
            throw new TimeoutException("Cancelled run did not return within 30 seconds.");
        }

        return await run;
    }

    static async Task<TapCleanResult> RunTapeAsync(string tapCleanExe, string tape, string directory, TapCleanOperation operation, bool reckless)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        return await TapCleanRunner.RunAsync(
            new TapCleanRequest
            {
                TapCleanExePath = tapCleanExe,
                InputFile = tape,
                Operation = operation,
                Tolerance = 10,
                Reckless = reckless,
                WorkingDirectory = directory,
                ReportDirectory = directory,
            },
            _ => { },
            cancellation.Token);
    }

    static string Describe(TapCleanResult result)
    {
        return "overall=" + result.Overall +
               " recognized=" + result.RecognizedPercent +
               " readErrors=" + result.ReadErrors +
               " refused=" + result.RefusedDueToReadErrors +
               " cancelled=" + result.Cancelled +
               " exit=" + result.ExitCode +
               " cleaned=" + (string.IsNullOrEmpty(result.CleanedFilePath) ? "<null>" : Path.GetFileName(result.CleanedFilePath));
    }

    static bool Nearly(double? actual, double expected) =>
        actual is { } value && Math.Abs(value - expected) < 0.001;

    static bool TryDelete(string directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
                return true;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(200);
            }
        }

        return !Directory.Exists(directory);
    }

    static void Check(string name, bool condition, string detail)
    {
        if (condition)
            Pass(name);
        else
            Fail(name, detail);
    }

    static void Pass(string name)
    {
        _passed++;
        Console.WriteLine("PASS  " + name);
    }

    static void Fail(string name, string detail)
    {
        _failed++;
        Console.WriteLine("FAIL  " + name);
        foreach (var line in detail.Split('\n'))
            Console.WriteLine("      " + line.TrimEnd('\r'));
    }

    static void Skip(string name, string detail)
    {
        _skipped++;
        Console.WriteLine("SKIP  " + name);
        foreach (var line in detail.Split('\n'))
            Console.WriteLine("      " + line.TrimEnd('\r'));
    }
}
