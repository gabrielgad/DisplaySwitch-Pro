# Advanced Features

## Overview

This document covers advanced features and extensions beyond the core DisplaySwitch-Pro functionality. These features provide enhanced automation, integration capabilities, and power-user functionality for sophisticated display management scenarios.

## Global Hotkey Registration

### System-Wide Hotkey Support
While the base application supports application-level shortcuts, global hotkeys provide system-wide access regardless of the active window.

#### Implementation
**Location**: Extension of `DisplayManagerGUI.cs`

```csharp
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll")]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

// Modifier constants
private const uint MOD_ALT = 0x0001;
private const uint MOD_CONTROL = 0x0002;
private const uint MOD_SHIFT = 0x0004;
private const uint MOD_WIN = 0x0008;

// Virtual key codes
private const uint VK_F1 = 0x70;
private const uint VK_F2 = 0x71;
private const uint VK_F3 = 0x72;
private const uint VK_F4 = 0x73;

// Hotkey IDs
private const int HOTKEY_ID_PC = 1;
private const int HOTKEY_ID_TV = 2;
private const int HOTKEY_ID_REFRESH = 3;
private const int HOTKEY_ID_CYCLE = 4;
```

#### Registration Process
```csharp
private void RegisterGlobalHotkeys()
{
    // Ctrl+Alt+F1 - PC Mode
    RegisterHotKey(this.Handle, HOTKEY_ID_PC, MOD_CONTROL | MOD_ALT, VK_F1);
    
    // Ctrl+Alt+F2 - TV Mode
    RegisterHotKey(this.Handle, HOTKEY_ID_TV, MOD_CONTROL | MOD_ALT, VK_F2);
    
    // Ctrl+Alt+F3 - Refresh
    RegisterHotKey(this.Handle, HOTKEY_ID_REFRESH, MOD_CONTROL | MOD_ALT, VK_F3);
    
    // Ctrl+Alt+F4 - Cycle displays
    RegisterHotKey(this.Handle, HOTKEY_ID_CYCLE, MOD_CONTROL | MOD_ALT, VK_F4);
}

protected override void WndProc(ref Message m)
{
    const int WM_HOTKEY = 0x0312;
    
    if (m.Msg == WM_HOTKEY)
    {
        switch (m.WParam.ToInt32())
        {
            case HOTKEY_ID_PC:
                SetPCMode();
                break;
            case HOTKEY_ID_TV:
                SetTVMode();
                break;
            case HOTKEY_ID_REFRESH:
                LoadDisplayInfo();
                break;
            case HOTKEY_ID_CYCLE:
                CycleDisplayModes();
                break;
        }
    }
    
    base.WndProc(ref m);
}
```

## Time-Based Auto-Switching

### Scheduled Display Configuration
Automatically switch display modes based on time of day or day of week.

#### Schedule Configuration
```csharp
public class DisplaySchedule
{
    public string Name { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DayOfWeek[] Days { get; set; }
    public DisplayMode Mode { get; set; }
    public bool Enabled { get; set; }
}

public class ScheduleManager
{
    private List<DisplaySchedule> schedules = new List<DisplaySchedule>();
    private System.Windows.Forms.Timer scheduleTimer;
    
    public void Initialize()
    {
        scheduleTimer = new System.Windows.Forms.Timer();
        scheduleTimer.Interval = 60000; // Check every minute
        scheduleTimer.Tick += CheckSchedules;
        scheduleTimer.Start();
        
        LoadSchedules();
    }
    
    private void CheckSchedules(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        var currentTime = now.TimeOfDay;
        var currentDay = now.DayOfWeek;
        
        foreach (var schedule in schedules.Where(s => s.Enabled))
        {
            if (schedule.Days.Contains(currentDay) &&
                currentTime >= schedule.StartTime &&
                currentTime <= schedule.EndTime)
            {
                DisplayManager.SetDisplayMode(schedule.Mode);
                LogScheduleActivation(schedule);
                break;
            }
        }
    }
}
```

#### Schedule Examples
```csharp
// Work hours: PC mode Monday-Friday 9AM-5PM
var workSchedule = new DisplaySchedule
{
    Name = "Work Hours",
    StartTime = new TimeSpan(9, 0, 0),
    EndTime = new TimeSpan(17, 0, 0),
    Days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
    Mode = DisplayMode.PCMode,
    Enabled = true
};

// Evening: TV mode every day 7PM-11PM
var eveningSchedule = new DisplaySchedule
{
    Name = "Evening Entertainment",
    StartTime = new TimeSpan(19, 0, 0),
    EndTime = new TimeSpan(23, 0, 0),
    Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToArray(),
    Mode = DisplayMode.TVMode,
    Enabled = true
};
```

## Profile Management System

### Advanced Display Profiles
Extend beyond simple PC/TV modes with custom profiles for different scenarios.

#### Profile Structure
```csharp
public class DisplayProfile
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<DisplaySetting> DisplaySettings { get; set; }
    public List<string> LaunchApplications { get; set; }
    public WindowsSettingsProfile WindowsSettings { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
}

public class DisplaySetting
{
    public string DeviceName { get; set; }
    public bool IsEnabled { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public DisplayOrientation Orientation { get; set; }
    public bool IsPrimary { get; set; }
}

public class WindowsSettingsProfile
{
    public int TaskbarPosition { get; set; }
    public bool AutoHideTaskbar { get; set; }
    public string WallpaperPath { get; set; }
    public int SoundVolume { get; set; }
    public string AudioDevice { get; set; }
}
```

#### Profile Manager
```csharp
public class ProfileManager
{
    private List<DisplayProfile> profiles = new List<DisplayProfile>();
    private string profilesPath;
    
    public void CreateProfile(string name, string description)
    {
        var profile = new DisplayProfile
        {
            Name = name,
            Description = description,
            DisplaySettings = CaptureCurrentDisplaySettings(),
            LaunchApplications = new List<string>(),
            WindowsSettings = CaptureWindowsSettings(),
            CreatedAt = DateTime.Now,
            LastUsed = DateTime.Now
        };
        
        profiles.Add(profile);
        SaveProfiles();
    }
    
    public void ApplyProfile(string profileName)
    {
        var profile = profiles.FirstOrDefault(p => p.Name == profileName);
        if (profile == null) return;
        
        ApplyDisplaySettings(profile.DisplaySettings);
        LaunchApplications(profile.LaunchApplications);
        ApplyWindowsSettings(profile.WindowsSettings);
        
        profile.LastUsed = DateTime.Now;
        SaveProfiles();
    }
    
    private void ApplyDisplaySettings(List<DisplaySetting> settings)
    {
        foreach (var setting in settings)
        {
            // Apply individual display configuration
            SetDisplayConfiguration(setting);
        }
    }
    
    private void LaunchApplications(List<string> applications)
    {
        foreach (var app in applications)
        {
            try
            {
                Process.Start(app);
            }
            catch (Exception ex)
            {
                LogError($"Failed to launch {app}: {ex.Message}");
            }
        }
    }
}
```

## Network Remote Control

### REST API Server
Enable remote control of display configurations through HTTP API.

#### HTTP Server Implementation
```csharp
public class DisplayManagerWebServer
{
    private HttpListener listener;
    private bool isRunning;
    private int port;
    
    public DisplayManagerWebServer(int port = 8080)
    {
        this.port = port;
    }
    
    public void Start()
    {
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        isRunning = true;
        
        Task.Run(() => HandleRequests());
    }
    
    private async void HandleRequests()
    {
        while (isRunning)
        {
            try
            {
                var context = await listener.GetContextAsync();
                await ProcessRequest(context);
            }
            catch (Exception ex)
            {
                LogError($"Web server error: {ex.Message}");
            }
        }
    }
    
    private async Task ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        
        string responseText = "";
        
        switch (request.Url.LocalPath.ToLower())
        {
            case "/api/mode/pc":
                DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
                responseText = JsonSerializer.Serialize(new { status = "success", mode = "pc" });
                break;
                
            case "/api/mode/tv":
                DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.TVMode);
                responseText = JsonSerializer.Serialize(new { status = "success", mode = "tv" });
                break;
                
            case "/api/status":
                var config = DisplayManager.GetCurrentConfiguration();
                responseText = JsonSerializer.Serialize(config);
                break;
                
            case "/api/profiles":
                if (request.HttpMethod == "GET")
                {
                    var profiles = ProfileManager.GetProfiles();
                    responseText = JsonSerializer.Serialize(profiles);
                }
                break;
                
            default:
                response.StatusCode = 404;
                responseText = JsonSerializer.Serialize(new { error = "Not found" });
                break;
        }
        
        response.ContentType = "application/json";
        byte[] buffer = Encoding.UTF8.GetBytes(responseText);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }
}
```

#### API Endpoints
```bash
# Switch to PC mode
curl -X POST http://localhost:8080/api/mode/pc

# Switch to TV mode
curl -X POST http://localhost:8080/api/mode/tv

# Get current status
curl -X GET http://localhost:8080/api/status

# Get available profiles
curl -X GET http://localhost:8080/api/profiles

# Apply profile
curl -X POST http://localhost:8080/api/profiles/gaming/apply
```

## Gaming Mode Integration

### Game Detection and Auto-Switch
Automatically switch display modes when games are launched.

#### Game Detection
```csharp
public class GameDetector
{
    private List<string> gameProcesses = new List<string>();
    private System.Windows.Forms.Timer processTimer;
    private bool gameRunning = false;
    
    public void Initialize()
    {
        LoadGameList();
        
        processTimer = new System.Windows.Forms.Timer();
        processTimer.Interval = 5000; // Check every 5 seconds
        processTimer.Tick += CheckForGames;
        processTimer.Start();
    }
    
    private void CheckForGames(object sender, EventArgs e)
    {
        var runningProcesses = Process.GetProcesses().Select(p => p.ProcessName.ToLower()).ToList();
        bool gameDetected = gameProcesses.Any(game => runningProcesses.Contains(game.ToLower()));
        
        if (gameDetected && !gameRunning)
        {
            // Game started
            OnGameStarted();
            gameRunning = true;
        }
        else if (!gameDetected && gameRunning)
        {
            // Game stopped
            OnGameStopped();
            gameRunning = false;
        }
    }
    
    private void OnGameStarted()
    {
        // Switch to gaming display configuration
        DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.GamingMode);
        
        // Optional: Launch gaming peripherals software
        LaunchGamingApps();
        
        // Optional: Adjust Windows settings for gaming
        OptimizeForGaming();
    }
    
    private void OnGameStopped()
    {
        // Return to previous display configuration
        DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
        
        // Restore normal Windows settings
        RestoreNormalSettings();
    }
    
    private void LoadGameList()
    {
        gameProcesses.AddRange(new[]
        {
            "steam", "epicgameslauncher", "origin", "uplay",
            "wow", "overwatch", "csgo", "valorant", "dota2",
            "league of legends", "minecraft", "fortnite"
        });
    }
}
```

## Power Management Integration

### Display Power Control
Advanced power management for displays based on usage patterns.

#### Power Management
```csharp
public class DisplayPowerManager
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    private const int HWND_BROADCAST = 0xFFFF;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;
    private const int MONITOR_ON = -1;
    private const int MONITOR_OFF = 2;
    private const int MONITOR_STANDBY = 1;
    
    public void TurnOffDisplays()
    {
        SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MONITOR_OFF);
    }
    
    public void TurnOnDisplays()
    {
        SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MONITOR_ON);
    }
    
    public void SetDisplayStandby()
    {
        SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MONITOR_STANDBY);
    }
}

public class PowerProfile
{
    public string Name { get; set; }
    public int TurnOffAfterMinutes { get; set; }
    public int StandbyAfterMinutes { get; set; }
    public bool EnableDimming { get; set; }
    public int DimmingLevel { get; set; }
}
```

## Multi-User Support

### User-Specific Configurations
Support different configurations for different Windows users.

#### User Configuration Manager
```csharp
public class UserConfigManager
{
    private Dictionary<string, UserConfig> userConfigs = new Dictionary<string, UserConfig>();
    private string currentUser;
    
    public void Initialize()
    {
        currentUser = Environment.UserName;
        LoadUserConfigs();
        ApplyUserConfig(currentUser);
    }
    
    public void OnUserLogon(string userName)
    {
        currentUser = userName;
        ApplyUserConfig(userName);
    }
    
    private void ApplyUserConfig(string userName)
    {
        if (userConfigs.ContainsKey(userName))
        {
            var config = userConfigs[userName];
            ApplyDisplayMode(config.DefaultDisplayMode);
            ApplySchedules(config.Schedules);
            ApplyProfiles(config.Profiles);
        }
    }
}

public class UserConfig
{
    public string UserName { get; set; }
    public DisplayMode DefaultDisplayMode { get; set; }
    public List<DisplaySchedule> Schedules { get; set; }
    public List<DisplayProfile> Profiles { get; set; }
    public Dictionary<string, string> Preferences { get; set; }
}
```

## Advanced Logging and Monitoring

### Comprehensive Activity Logging
Track all display changes, user interactions, and system events.

#### Activity Logger
```csharp
public class ActivityLogger
{
    private string logPath;
    private StreamWriter logWriter;
    
    public void Initialize()
    {
        logPath = Path.Combine(GetConfigPath(), "activity.log");
        logWriter = new StreamWriter(logPath, true);
        logWriter.AutoFlush = true;
        
        LogEvent("Application Started", "System");
    }
    
    public void LogDisplayChange(string fromMode, string toMode, string trigger)
    {
        var logEntry = new
        {
            Timestamp = DateTime.Now,
            Event = "DisplayModeChanged",
            FromMode = fromMode,
            ToMode = toMode,
            Trigger = trigger,
            User = Environment.UserName
        };
        
        WriteLogEntry(logEntry);
    }
    
    public void LogUserAction(string action, string details)
    {
        var logEntry = new
        {
            Timestamp = DateTime.Now,
            Event = "UserAction",
            Action = action,
            Details = details,
            User = Environment.UserName
        };
        
        WriteLogEntry(logEntry);
    }
    
    private void WriteLogEntry(object logEntry)
    {
        var json = JsonSerializer.Serialize(logEntry);
        logWriter.WriteLine(json);
    }
}
```

#### Performance Monitoring
```csharp
public class PerformanceMonitor
{
    private PerformanceCounter cpuCounter;
    private PerformanceCounter ramCounter;
    private Dictionary<string, long> operationTimes = new Dictionary<string, long>();
    
    public void Initialize()
    {
        cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        ramCounter = new PerformanceCounter("Memory", "Available MBytes");
    }
    
    public void StartOperation(string operationName)
    {
        operationTimes[operationName] = Stopwatch.GetTimestamp();
    }
    
    public void EndOperation(string operationName)
    {
        if (operationTimes.ContainsKey(operationName))
        {
            var elapsed = Stopwatch.GetTimestamp() - operationTimes[operationName];
            var milliseconds = elapsed * 1000 / Stopwatch.Frequency;
            
            LogPerformance(operationName, milliseconds);
            operationTimes.Remove(operationName);
        }
    }
    
    private void LogPerformance(string operation, long milliseconds)
    {
        var perfData = new
        {
            Timestamp = DateTime.Now,
            Operation = operation,
            Duration = milliseconds,
            CPUUsage = cpuCounter.NextValue(),
            AvailableRAM = ramCounter.NextValue()
        };
        
        // Log to performance database or file
        LogPerformanceData(perfData);
    }
}
```

## Plugin Architecture

### Extensible Plugin System
Allow third-party developers to extend functionality.

#### Plugin Interface
```csharp
public interface IDisplayManagerPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    
    void Initialize(IDisplayManagerAPI api);
    void Shutdown();
    
    void OnDisplayModeChanged(DisplayMode oldMode, DisplayMode newMode);
    void OnConfigurationChanged(DisplayConfig config);
    
    List<PluginMenuItem> GetMenuItems();
    UserControl GetSettingsPanel();
}

public interface IDisplayManagerAPI
{
    void SetDisplayMode(DisplayMode mode);
    DisplayConfig GetCurrentConfiguration();
    void ShowNotification(string title, string message);
    void RegisterHotkey(string hotkey, Action callback);
    void AddMenuItem(string text, Action callback);
}
```

#### Plugin Manager
```csharp
public class PluginManager
{
    private List<IDisplayManagerPlugin> plugins = new List<IDisplayManagerPlugin>();
    private IDisplayManagerAPI api;
    
    public void Initialize(IDisplayManagerAPI apiInstance)
    {
        api = apiInstance;
        LoadPlugins();
    }
    
    private void LoadPlugins()
    {
        string pluginPath = Path.Combine(Application.StartupPath, "Plugins");
        if (!Directory.Exists(pluginPath)) return;
        
        foreach (var dll in Directory.GetFiles(pluginPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IDisplayManagerPlugin).IsAssignableFrom(t) && !t.IsInterface);
                
                foreach (var type in pluginTypes)
                {
                    var plugin = (IDisplayManagerPlugin)Activator.CreateInstance(type);
                    plugin.Initialize(api);
                    plugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load plugin {dll}: {ex.Message}");
            }
        }
    }
    
    public void NotifyDisplayModeChanged(DisplayMode oldMode, DisplayMode newMode)
    {
        foreach (var plugin in plugins)
        {
            try
            {
                plugin.OnDisplayModeChanged(oldMode, newMode);
            }
            catch (Exception ex)
            {
                LogError($"Plugin {plugin.Name} error: {ex.Message}");
            }
        }
    }
}
```

## Integration Points

### Related Components
- **[Core Features](core-features.md)**: Advanced features extend core functionality
- **[Configuration Management](config-management.md)**: Advanced profiles and settings
- **[System Tray](system-tray.md)**: Additional tray menu options
- **[Keyboard Shortcuts](keyboard-shortcuts.md)**: Global hotkey registration
- **[CLI Interface](cli-interface.md)**: Extended command-line options

### Future Development
- **AI-Powered Automation**: Machine learning for usage pattern recognition
- **Cloud Integration**: Synchronization across multiple devices
- **IoT Integration**: Smart home integration for environmental triggers
- **Accessibility Enhancements**: Advanced accessibility features
- **Performance Optimization**: Hardware-specific optimizations