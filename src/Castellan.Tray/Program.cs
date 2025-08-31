using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Castellan.Tray;

public static class Program
{
    private static NotifyIcon? _notifyIcon;
    private static Timer? _statusTimer;
    private static bool _isCastellanRunning = false;

    [STAThread]
    static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeTrayIcon();
            StartStatusMonitoring();

            Application.Run();
        }
        catch (Exception ex)
        {
            // Log error to file for debugging
            File.WriteAllText("tray_error.log", $"Tray application error: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Castellan Tray Error: {ex.Message}", "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateSimpleIcon(),
            Text = "Castellan - Security Log Analysis",
            Visible = true
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        var statusItem = new ToolStripMenuItem("Status: Checking...");
        statusItem.Enabled = false;
        contextMenu.Items.Add(statusItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var startItem = new ToolStripMenuItem("Start Castellan");
        startItem.Click += (s, e) => StartCastellan();
        contextMenu.Items.Add(startItem);

        var stopItem = new ToolStripMenuItem("Stop Castellan");
        stopItem.Click += (s, e) => StopCastellan();
        contextMenu.Items.Add(stopItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var statusCheckItem = new ToolStripMenuItem("Check System Status");
        statusCheckItem.Click += (s, e) => CheckSystemStatus();
        contextMenu.Items.Add(statusCheckItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var dashboardItem = new ToolStripMenuItem("Dashboard");
        dashboardItem.Click += (s, e) => OpenDashboard();
        contextMenu.Items.Add(dashboardItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Double-click to check status
        _notifyIcon.DoubleClick += (s, e) => CheckSystemStatus();
    }

    private static void StartStatusMonitoring()
    {
        _statusTimer = new Timer
        {
            Interval = 5000 // Check every 5 seconds
        };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();
        
        // Initial check
        StatusTimer_Tick(null, EventArgs.Empty);
    }

    private static void StatusTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var wasRunning = _isCastellanRunning;
            _isCastellanRunning = IsCastellanRunning();

            if (_notifyIcon?.ContextMenuStrip?.Items[0] is ToolStripMenuItem statusItem)
            {
                if (_isCastellanRunning)
                {
                    statusItem.Text = "Status: Running";
                    statusItem.ForeColor = Color.Green;
                    _notifyIcon.Icon = CreateSimpleIcon(Color.Green);
                    _notifyIcon.Text = "Castellan - Running";
                }
                else
                {
                    statusItem.Text = "Status: Stopped";
                    statusItem.ForeColor = Color.Red;
                    _notifyIcon.Icon = CreateSimpleIcon(Color.Red);
                    _notifyIcon.Text = "Castellan - Stopped";
                }
            }

            // Show notification on state change
            if (wasRunning != _isCastellanRunning)
            {
                var message = _isCastellanRunning ? "Castellan is now running" : "Castellan has stopped";
                _notifyIcon?.ShowBalloonTip(2000, "Castellan Status", message, ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText("tray_error.log", $"Status check error: {ex.Message}\n");
        }
    }

    private static bool IsCastellanRunning()
    {
        try
        {
            // Check for the actual executable
            var workerProcesses = Process.GetProcessesByName("Castellan.Worker");
            if (workerProcesses.Length > 0)
            {
                return true;
            }

            // Check for dotnet processes running the Worker
            var dotnetProcesses = Process.GetProcessesByName("dotnet");
            foreach (var process in dotnetProcesses)
            {
                try
                {
                    if (process.MainModule?.FileName?.Contains("dotnet") == true)
                    {
                        // Check if this dotnet process is running our Worker
                        var commandLine = GetProcessCommandLine(process.Id);
                        if (!string.IsNullOrEmpty(commandLine) && 
                            (commandLine.Contains("Castellan.Worker") || commandLine.Contains("Castellan.Worker.dll")))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // Skip processes we can't access
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void StartCastellan()
    {
        try
        {
            // Get the project root directory (go up from the tray app directory)
            var trayAppDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var projectRoot = Path.GetFullPath(Path.Combine(trayAppDir!, "..", "..", "..", "..", ".."));
            
            // Try multiple possible paths for the Worker
            var possiblePaths = new[]
            {
                Path.Combine(projectRoot, "src", "Castellan.Worker", "bin", "Release", "net8.0-windows", "Castellan.Worker.exe"),
                Path.Combine(projectRoot, "src", "Castellan.Worker", "bin", "Release", "net8.0", "Castellan.Worker.exe"),
                Path.Combine(projectRoot, "src", "Castellan.Worker", "bin", "Debug", "net8.0-windows", "Castellan.Worker.exe"),
                Path.Combine(projectRoot, "src", "Castellan.Worker", "bin", "Debug", "net8.0", "Castellan.Worker.exe")
            };

            string? workerPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    workerPath = path;
                    break;
                }
            }

            if (workerPath != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = workerPath,
                    WorkingDirectory = Path.GetDirectoryName(workerPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else
            {
                // Try to run with dotnet
                var projectPath = Path.Combine(projectRoot, "src", "Castellan.Worker");
                if (Directory.Exists(projectPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "run",
                        WorkingDirectory = projectPath,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else
                {
                    MessageBox.Show("Castellan.Worker not found. Please ensure Castellan is built.", 
                        "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start Castellan: {ex.Message}", 
                "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void StopCastellan()
    {
        try
        {
            var processes = Process.GetProcessesByName("Castellan.Worker");
            foreach (var process in processes)
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop Castellan: {ex.Message}", 
                "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void CheckSystemStatus()
    {
        try
        {
            // Look for status script in the Castellan root directory (two levels up from tray)
            var statusScript = Path.Combine(Environment.CurrentDirectory, "..", "..", "scripts", "status.ps1");
            if (File.Exists(statusScript))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{statusScript}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                // Try alternative paths if the relative path doesn't work
                var alternativePaths = new[]
                {
                    Path.Combine(Environment.CurrentDirectory, "..", "..", "scripts", "status.ps1"),
                    Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "scripts", "status.ps1"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "scripts", "status.ps1"),
                    "scripts\\status.ps1"
                };

                string? foundScript = null;
                foreach (var path in alternativePaths)
                {
                    if (File.Exists(path))
                    {
                        foundScript = path;
                        break;
                    }
                }

                if (foundScript != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-ExecutionPolicy Bypass -File \"{foundScript}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Status script not found. Please ensure the scripts directory exists in the Castellan root folder.", 
                        "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to check status: {ex.Message}", 
                "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenDashboard()
    {
        try
        {
            // Get the project root directory (go up from the tray app directory)
            var trayAppDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var projectRoot = Path.GetFullPath(Path.Combine(trayAppDir!, "..", "..", "..", "..", ".."));
            
            // Try multiple possible paths for the Dashboard
            var possiblePaths = new[]
            {
                Path.Combine(projectRoot, "src", "Castellan.Dashboard", "bin", "Release", "net8.0-windows", "Castellan.Dashboard.exe"),
                Path.Combine(projectRoot, "src", "Castellan.Dashboard", "bin", "Release", "net8.0", "Castellan.Dashboard.exe"),
                Path.Combine(projectRoot, "src", "Castellan.Dashboard", "bin", "Debug", "net8.0-windows", "Castellan.Dashboard.exe"),
                Path.Combine(projectRoot, "src", "Castellan.Dashboard", "bin", "Debug", "net8.0", "Castellan.Dashboard.exe")
            };

            string? dashboardPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    dashboardPath = path;
                    break;
                }
            }

            if (dashboardPath != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dashboardPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Castellan Dashboard not found. Please ensure Castellan Dashboard is built.", 
                    "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open dashboard: {ex.Message}", 
                "Castellan Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExitApplication()
    {
        _statusTimer?.Stop();
        _notifyIcon?.Dispose();
        Application.Exit();
    }

    public static Icon CreateSimpleIcon(Color? statusColor = null)
    {
        try
        {
            // Create a 32x32 icon
            var bitmap = new Bitmap(32, 32);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Background
            graphics.Clear(Color.Transparent);
            
            // Simple shield shape
            var shieldColor = statusColor ?? Color.Gray;
            var brush = new SolidBrush(shieldColor);
            
            // Simple triangle shape (scaled for 32x32)
            var points = new Point[]
            {
                new Point(16, 4),   // Top
                new Point(28, 12),  // Right
                new Point(24, 28),  // Right bottom
                new Point(16, 24),  // Bottom
                new Point(8, 28),   // Left bottom
                new Point(4, 12)    // Left
            };
            
            graphics.FillPolygon(brush, points);
            
            // Add "LS" text
            var textBrush = new SolidBrush(Color.White);
            var font = new Font("Arial", 12, FontStyle.Bold);
            graphics.DrawString("LS", font, textBrush, 8, 6);
            
            // Create icon
            var iconHandle = bitmap.GetHicon();
            var icon = Icon.FromHandle(iconHandle);
            
            // Clone to avoid disposal issues
            icon = icon.Clone() as Icon ?? icon;
            DestroyIcon(iconHandle);
            
            return icon;
        }
        catch
        {
            // Fallback to system icon if custom icon fails
            return SystemIcons.Application;
        }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static string GetProcessCommandLine(int processId)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "wmic";
            process.StartInfo.Arguments = $"process where processid={processId} get commandline /value";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse WMIC output
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("CommandLine="))
                {
                    return line.Substring("CommandLine=".Length).Trim();
                }
            }
        }
        catch
        {
            // Fallback method failed
        }
        return string.Empty;
    }
}

