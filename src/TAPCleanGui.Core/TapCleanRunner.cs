// SPDX-License-Identifier: GPL-2.0-or-later
// TAPClean GUI - unofficial front end for TAPClean. See LICENSE and NOTICE.md.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace TAPCleanGui.Core;

public static class TapCleanRunner
{
    public static async Task<TapCleanResult> RunAsync(
        TapCleanRequest request,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        if (cancellationToken.IsCancellationRequested)
            return new TapCleanResult { Cancelled = true };

        var startInfo = BuildStartInfo(request);
        onLine?.Invoke(FormatCommand(startInfo));

        var output = new StringBuilder();
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        // Taken immediately before start so a file left from an earlier run is not treated as this run's output.
        var startedUtc = DateTime.UtcNow;
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("TAPClean could not be started.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("TAPClean could not be started. " + ex.Message, ex);
        }

        var stdoutTask = PumpAsync(process.StandardOutput, output, onLine);
        var stderrTask = PumpAsync(process.StandardError, output, onLine);

        var cancelled = false;
        using (cancellationToken.Register(() => Kill(process)))
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                Kill(process);
            }
        }

        if (!process.HasExited)
        {
            Kill(process);
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Process already left.
            }
        }

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Keep whatever was captured. The pipes did not close in time.
        }

        var exitCode = -1;
        try
        {
            if (process.HasExited)
                exitCode = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            exitCode = -1;
        }

        string text;
        lock (output)
            text = output.ToString();

        var parsed = TapCleanResultParser.Parse(text);
        var cleanedPath = ResolveCleanedPath(request, parsed, startedUtc);
        var reportPath = await CopyReportAsync(request, startedUtc).ConfigureAwait(false);

        if (cancelled)
            onLine?.Invoke("Cancelled.");
        else
            onLine?.Invoke("Exit code: " + exitCode);

        if (!string.IsNullOrEmpty(reportPath))
            onLine?.Invoke("Report copied to: " + reportPath);
        if (!string.IsNullOrEmpty(cleanedPath))
            onLine?.Invoke("Cleaned file: " + cleanedPath);

        return parsed with
        {
            ExitCode = exitCode,
            Cancelled = cancelled || cancellationToken.IsCancellationRequested,
            CleanedFilePath = cleanedPath,
            ReportCopyPath = reportPath,
            Output = text,
        };
    }

    public static ProcessStartInfo BuildStartInfo(TapCleanRequest request)
    {
        var tolerance = Math.Clamp(request.Tolerance, 0, 15);
        var startInfo = new ProcessStartInfo
        {
            FileName = request.TapCleanExePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // ArgumentList quotes paths that contain spaces. Do not build one argument string.
        switch (request.Operation)
        {
            case TapCleanOperation.Test:
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add(request.InputFile!);
                startInfo.ArgumentList.Add("-tol");
                startInfo.ArgumentList.Add(tolerance.ToString());
                break;
            case TapCleanOperation.Clean:
                startInfo.ArgumentList.Add("-o");
                startInfo.ArgumentList.Add(request.InputFile!);
                startInfo.ArgumentList.Add("-tol");
                startInfo.ArgumentList.Add(tolerance.ToString());
                if (request.Reckless)
                    startInfo.ArgumentList.Add("-reckless");
                break;
            case TapCleanOperation.Wav:
                startInfo.ArgumentList.Add("-wav");
                startInfo.ArgumentList.Add(request.InputFile!);
                break;
            case TapCleanOperation.ListScanners:
                startInfo.ArgumentList.Add("-list");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Operation, "Unknown TAPClean operation.");
        }

        return startInfo;
    }

    static void Validate(TapCleanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TapCleanExePath) || !File.Exists(request.TapCleanExePath))
        {
            throw new FileNotFoundException(
                "TAPClean.exe was not found: " + request.TapCleanExePath,
                request.TapCleanExePath);
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory) || !Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException(
                "The output folder does not exist: " + request.WorkingDirectory);
        }

        if (request.Operation == TapCleanOperation.ListScanners)
            return;

        if (string.IsNullOrWhiteSpace(request.InputFile) || !File.Exists(request.InputFile))
        {
            throw new FileNotFoundException(
                "The tape file was not found: " + request.InputFile,
                request.InputFile);
        }
    }

    static readonly TimeSpan FreshFileSlack = TimeSpan.FromSeconds(2);

    static string? ResolveCleanedPath(TapCleanRequest request, TapCleanResult parsed, DateTime startedUtc)
    {
        // A "Saved: clean….tap" line is from this run's stdout. An older file with the same name is not.
        if (!string.IsNullOrWhiteSpace(parsed.SavedTapPath))
        {
            if (Path.IsPathRooted(parsed.SavedTapPath) && TryExistingFile(parsed.SavedTapPath, out var savedFull))
                return savedFull;
            var besideWork = Path.Combine(request.WorkingDirectory, Path.GetFileName(parsed.SavedTapPath));
            if (TryExistingFile(besideWork, out var workFull))
                return workFull;
        }

        if (request.Operation != TapCleanOperation.Clean || string.IsNullOrWhiteSpace(request.InputFile))
            return null;

        var conventional = Path.Combine(request.WorkingDirectory, "clean." + Path.GetFileName(request.InputFile));
        if (!TryExistingFile(conventional, out var full))
            return null;
        return WasWrittenThisRun(full, startedUtc) ? full : null;
    }

    static async Task<string?> CopyReportAsync(TapCleanRequest request, DateTime startedUtc)
    {
        if (request.Operation == TapCleanOperation.ListScanners)
            return null;

        var exeDirectory = Path.GetDirectoryName(request.TapCleanExePath);
        if (string.IsNullOrEmpty(exeDirectory))
            return null;

        var source = Path.Combine(exeDirectory, "tcreport.txt");
        if (!File.Exists(source) || !WasWrittenThisRun(source, startedUtc))
            return null;

        var destinationDirectory = string.IsNullOrWhiteSpace(request.ReportDirectory)
            ? request.WorkingDirectory
            : request.ReportDirectory;
        if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
            return null;

        var name = string.IsNullOrWhiteSpace(request.InputFile)
            ? "tcreport.txt"
            : Path.GetFileNameWithoutExtension(request.InputFile) + ".tcreport.txt";
        var destination = Path.Combine(destinationDirectory, name);

        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return destination;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return destination;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }

    static bool TryExistingFile(string candidate, out string fullPath)
    {
        fullPath = "";
        if (candidate.Length == 0)
            return false;
        try
        {
            if (!File.Exists(candidate))
                return false;
            fullPath = Path.GetFullPath(candidate);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    static bool WasWrittenThisRun(string path, DateTime startedUtc)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path) >= startedUtc - FreshFileSlack;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    static async Task PumpAsync(StreamReader reader, StringBuilder sink, Action<string>? onLine)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    break;
                lock (sink)
                    sink.AppendLine(line);
                try
                {
                    onLine?.Invoke(line);
                }
                catch (Exception)
                {
                    // A listener failure must not drop the rest of the tool output.
                }
            }
        }
        catch (IOException)
        {
            // The pipe closed because the process exited or was killed.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    static string FormatCommand(ProcessStartInfo startInfo)
    {
        var args = string.Join(" ", startInfo.ArgumentList.Select(Quote));
        return "> " + Path.GetFileName(startInfo.FileName) + " " + args;
    }

    static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) ? "\"" + value + "\"" : value;
}
