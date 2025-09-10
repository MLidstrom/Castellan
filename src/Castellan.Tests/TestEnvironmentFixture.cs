using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Castellan.Tests;

/// <summary>
/// Manages Castellan processes during test execution to prevent file locking issues.
/// This fixture stops all Castellan processes before tests run and optionally restarts them afterward.
/// </summary>
public class TestEnvironmentFixture : IDisposable
{
    private readonly List<ProcessInfo> _stoppedProcesses = new();
    private bool _disposed = false;

    public TestEnvironmentFixture()
    {
        StopCastellanProcesses();
    }

    private void StopCastellanProcesses()
    {
        try
        {
            // Find all Castellan-related processes
            var processes = Process.GetProcesses()
                .Where(p => p.ProcessName.Contains("Castellan", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var process in processes)
            {
                try
                {
                    var processInfo = new ProcessInfo
                    {
                        ProcessName = process.ProcessName,
                        Id = process.Id,
                        MainModuleFileName = GetSafeMainModuleFileName(process)
                    };

                    _stoppedProcesses.Add(processInfo);

                    // Kill the process gracefully first, then forcefully if needed
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(5000)) // Wait up to 5 seconds
                        {
                            process.Kill(true); // Force kill with entire process tree
                        }
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    // Best effort - some processes might already be closed or inaccessible
                    Console.WriteLine($"Warning: Could not stop process {process.ProcessName} (PID: {process.Id}): {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (_stoppedProcesses.Any())
            {
                Console.WriteLine($"Stopped {_stoppedProcesses.Count} Castellan process(es) for testing.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during process cleanup: {ex.Message}");
        }
    }

    private static string? GetSafeMainModuleFileName(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            // Access denied or process has exited
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            RestartCastellanProcesses();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void RestartCastellanProcesses()
    {
        if (!_stoppedProcesses.Any())
        {
            return;
        }

        try
        {
            // Use the start script to restart Castellan
            var startScriptPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, 
                "..", "..", "..", "..", 
                "scripts", "start.ps1"));

            if (File.Exists(startScriptPath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{startScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var startProcess = Process.Start(startInfo);
                Console.WriteLine($"Restarted Castellan using {startScriptPath}");
            }
            else
            {
                Console.WriteLine($"Warning: Start script not found at {startScriptPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not restart Castellan processes: {ex.Message}");
        }
    }

    private class ProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public int Id { get; set; }
        public string? MainModuleFileName { get; set; }
    }
}

/// <summary>
/// Test collection that uses the TestEnvironmentFixture to manage process lifecycle.
/// </summary>
[CollectionDefinition("TestEnvironment")]
public class TestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
