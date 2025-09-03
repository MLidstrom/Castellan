using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Castellan.Worker.Services;

public class StartupOrchestratorService : BackgroundService
{
    private readonly ILogger<StartupOrchestratorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<Process> _managedProcesses = new();
    private readonly IHostApplicationLifetime _lifetime;

    public StartupOrchestratorService(
        ILogger<StartupOrchestratorService> logger,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _configuration = configuration;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the main service to initialize
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        if (_configuration.GetValue<bool>("Startup:AutoStart:Enabled", true))
        {
            _logger.LogInformation("Starting all required services automatically...");

            try
            {
                // Start Qdrant if configured
                if (_configuration.GetValue<bool>("Startup:AutoStart:Qdrant", true))
                {
                    await StartQdrantAsync(stoppingToken);
                }

                // Start Ollama if configured
                if (_configuration.GetValue<bool>("Startup:AutoStart:Ollama", true))
                {
                    await StartOllamaAsync(stoppingToken);
                }

                // Start React Admin if configured
                if (_configuration.GetValue<bool>("Startup:AutoStart:ReactAdmin", true))
                {
                    await StartReactAdminAsync(stoppingToken);
                }

                // Start System Tray if configured
                if (_configuration.GetValue<bool>("Startup:AutoStart:SystemTray", true))
                {
                    await StartSystemTrayAsync(stoppingToken);
                }

                _logger.LogInformation("All configured services started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic service startup");
            }
        }

        // Register cleanup on application shutdown
        _lifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Stopping managed processes...");
            StopAllProcesses();
        });

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            
            // Check if processes are still running
            CheckProcessHealth();
        }
    }

    private async Task StartQdrantAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking Qdrant status...");

            // Check if Qdrant is already running
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            
            try
            {
                var response = await httpClient.GetAsync("http://localhost:6333/collections", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Qdrant is already running");
                    return;
                }
            }
            catch
            {
                // Qdrant not running, start it
            }

            _logger.LogInformation("Starting Qdrant container...");

            // Stop and remove existing container if it exists
            await RunCommandAsync("docker", "stop qdrant", cancellationToken, ignoreErrors: true);
            await RunCommandAsync("docker", "rm qdrant", cancellationToken, ignoreErrors: true);

            // Start Qdrant container
            var dockerArgs = "run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant";
            await RunCommandAsync("docker", dockerArgs, cancellationToken);

            // Wait for Qdrant to be ready
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

            _logger.LogInformation("Qdrant started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Qdrant. Make sure Docker is installed and running.");
        }
    }

    private async Task StartOllamaAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking Ollama status...");

            // Check if Ollama is already running
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            
            try
            {
                var response = await httpClient.GetAsync("http://localhost:11434/api/tags", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Ollama is already running");
                    return;
                }
            }
            catch
            {
                // Ollama not running, start it
            }

            _logger.LogInformation("Starting Ollama service...");

            // Start Ollama service in the background
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _managedProcesses.Add(process);
                
                // Wait for Ollama to be ready (up to 30 seconds)
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    
                    try
                    {
                        var response = await httpClient.GetAsync("http://localhost:11434/api/tags", cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Ollama started successfully");
                            return;
                        }
                    }
                    catch
                    {
                        // Still starting up
                    }
                }
                
                _logger.LogWarning("Ollama may still be starting up - check status manually");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Ollama. Make sure Ollama is installed (https://ollama.ai).");
        }
    }

    private async Task StartReactAdminAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting React Admin interface...");

            var adminPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "castellan-admin");
            adminPath = Path.GetFullPath(adminPath);

            if (!Directory.Exists(adminPath))
            {
                _logger.LogWarning($"React Admin directory not found at: {adminPath}");
                return;
            }

            // Check if node_modules exists, if not run npm install
            var nodeModulesPath = Path.Combine(adminPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                _logger.LogInformation("Installing React Admin dependencies...");
                await RunCommandAsync("npm", "install", cancellationToken, workingDirectory: adminPath);
            }

            // Start React Admin
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npm start",
                WorkingDirectory = adminPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Set PORT environment variable
            startInfo.Environment["PORT"] = "8080";

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _managedProcesses.Add(process);
                _logger.LogInformation("React Admin started on port 8080");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start React Admin interface");
        }
    }

    private async Task StartSystemTrayAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Only start system tray on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("System tray is only supported on Windows");
                return;
            }

            _logger.LogInformation("Starting System Tray application...");

            // Try both Debug and Release configurations
            string[] configurations = { "Debug", "Release" };
            string? trayExePath = null;
            
            foreach (var config in configurations)
            {
                var testPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", 
                    "src", "Castellan.Tray", "bin", config, "net8.0-windows", "Castellan.Tray.exe");
                testPath = Path.GetFullPath(testPath);
                
                if (File.Exists(testPath))
                {
                    trayExePath = testPath;
                    _logger.LogInformation($"Found System Tray executable at: {trayExePath}");
                    break;
                }
            }

            if (string.IsNullOrEmpty(trayExePath))
            {
                _logger.LogWarning("System Tray executable not found in Debug or Release configurations");
                _logger.LogInformation("Building System Tray application...");
                
                // Build the tray app if it doesn't exist
                var trayProjectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
                    "src", "Castellan.Tray", "Castellan.Tray.csproj");
                trayProjectPath = Path.GetFullPath(trayProjectPath);
                
                // Build in Release mode by default
                await RunCommandAsync("dotnet", $"build \"{trayProjectPath}\" -c Release", cancellationToken);
                
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                
                // Set the path to the newly built executable
                trayExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", 
                    "src", "Castellan.Tray", "bin", "Release", "net8.0-windows", "Castellan.Tray.exe");
                trayExePath = Path.GetFullPath(trayExePath);
            }

            if (File.Exists(trayExePath))
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = trayExePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(trayExePath)
                });

                if (process != null)
                {
                    _managedProcesses.Add(process);
                    _logger.LogInformation($"System Tray application started: {Path.GetFileName(trayExePath)}");
                }
            }
            else
            {
                _logger.LogError($"System Tray executable still not found after build attempt: {trayExePath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start System Tray application");
        }
    }

    private async Task RunCommandAsync(string command, string arguments, CancellationToken cancellationToken, 
        bool ignoreErrors = false, string? workingDirectory = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
                
                if (!ignoreErrors && process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Command failed: {command} {arguments}. Error: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            if (!ignoreErrors)
            {
                _logger.LogError(ex, $"Failed to run command: {command} {arguments}");
                throw;
            }
        }
    }

    private void CheckProcessHealth()
    {
        var deadProcesses = _managedProcesses.Where(p => p.HasExited).ToList();
        foreach (var process in deadProcesses)
        {
            _managedProcesses.Remove(process);
            process.Dispose();
        }

        if (deadProcesses.Any())
        {
            _logger.LogWarning($"{deadProcesses.Count} managed process(es) have stopped");
        }
    }

    private void StopAllProcesses()
    {
        foreach (var process in _managedProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping process {process.Id}");
            }
        }
        _managedProcesses.Clear();
    }

    public override void Dispose()
    {
        StopAllProcesses();
        base.Dispose();
    }
}