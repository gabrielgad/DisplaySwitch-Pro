# Advanced Features

## Overview

This document covers advanced features enabled by the F# Entity Component System (ECS) architecture in DisplaySwitch-Pro. The functional programming approach enables sophisticated features that would be difficult or impossible with traditional object-oriented architectures, including time-travel debugging, event sourcing, functional composition plugins, and immutable state management.

## Functional Hotkey System

### Cross-Platform Hotkey Management
The ECS architecture enables cross-platform hotkey handling through functional composition and immutable event streams.

#### F# ECS Implementation
**Functional Hotkey Components**:

```fsharp
// Immutable hotkey configuration
type HotkeyConfig = {
    Id: HotkeyId
    Keys: KeyCombination
    Action: HotkeyAction
    Platform: Platform list
    Enabled: bool
    Description: string
}

// Hotkey events in the ECS
type HotkeyEvent =
    | HotkeyPressed of HotkeyId * timestamp: DateTime
    | HotkeyRegistered of HotkeyConfig
    | HotkeyUnregistered of HotkeyId
    | HotkeyConflict of HotkeyId * conflictWith: HotkeyId

// Cross-platform key mapping
type KeyCombination = {
    Modifiers: ModifierKey set
    Key: VirtualKey
}

type ModifierKey = Ctrl | Alt | Shift | Super | Meta
type VirtualKey = F1 | F2 | F3 | F4 | Letter of char | Number of int

// Platform-specific implementations via functional interface
type IHotkeyPlatform = {
    RegisterHotkey: HotkeyConfig -> Result<unit, HotkeyError>
    UnregisterHotkey: HotkeyId -> Result<unit, HotkeyError>
    PollEvents: unit -> HotkeyEvent list
}
```

#### Functional Hotkey System
```fsharp
// Hotkey system as pure functions
module HotkeySystem =
    // Pure hotkey state management
    type HotkeyState = {
        RegisteredHotkeys: Map<HotkeyId, HotkeyConfig>
        PlatformBindings: Map<Platform, IHotkeyPlatform>
        EventQueue: HotkeyEvent list
    }
    
    // Register hotkey with functional composition
    let registerHotkey (config: HotkeyConfig) (state: HotkeyState) : Result<HotkeyState, HotkeyError> =
        result {
            // Check for conflicts using pure functions
            let! conflictCheck = checkHotkeyConflicts config state.RegisteredHotkeys
            
            // Register on all supported platforms
            let! platformResults = 
                config.Platform
                |> List.map (fun platform -> 
                    state.PlatformBindings.[platform].RegisterHotkey config)
                |> Result.sequence
            
            // Update state immutably
            let newState = {
                state with 
                    RegisteredHotkeys = state.RegisteredHotkeys |> Map.add config.Id config
                    EventQueue = (HotkeyRegistered config) :: state.EventQueue
            }
            
            return newState
        }
    
    // Process hotkey events functionally
    let processHotkeyEvent (event: HotkeyEvent) (world: ECSWorld) : ECSWorld =
        match event with
        | HotkeyPressed (hotkeyId, timestamp) ->
            // Find action and execute through ECS event system
            world.RegisteredHotkeys
            |> Map.tryFind hotkeyId
            |> Option.map (fun config -> executeHotkeyAction config.Action world)
            |> Option.defaultValue world
        | HotkeyRegistered config ->
            // Update ECS components
            world |> addComponent (HotkeyComponent config)
        | _ -> world
```

#### Cross-Platform Implementation
```fsharp
// Linux (X11/Wayland) implementation
let linuxHotkeyPlatform : IHotkeyPlatform = {
    RegisterHotkey = fun config ->
        match Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") with
        | "wayland" -> registerWaylandHotkey config
        | "x11" | _ -> registerX11Hotkey config
    
    UnregisterHotkey = fun hotkeyId ->
        // Platform-specific unregistration
        unregisterPlatformHotkey hotkeyId
    
    PollEvents = fun () ->
        // Non-blocking event polling
        pollPlatformHotkeyEvents()
}

// Windows implementation
let windowsHotkeyPlatform : IHotkeyPlatform = {
    RegisterHotkey = fun config ->
        registerWin32Hotkey config
    
    UnregisterHotkey = unregisterWin32Hotkey
    PollEvents = pollWin32HotkeyEvents
}
```

## Event-Driven Scheduling System

### Functional Time-Based Display Management
The ECS architecture enables sophisticated scheduling through event sourcing and functional composition.

#### Immutable Schedule Configuration
```fsharp
// Time-based events in the ECS
type TimeEvent =
    | ScheduleTriggered of ScheduleId * DateTime
    | ScheduleExpired of ScheduleId * DateTime
    | TimeZoneChanged of TimeZoneInfo
    | SystemTimeChanged of DateTime

// Immutable schedule definition
type DisplaySchedule = {
    Id: ScheduleId
    Name: string
    Trigger: ScheduleTrigger
    Action: ScheduleAction
    Conditions: ScheduleCondition list
    Enabled: bool
    CreatedAt: DateTime
    LastTriggered: DateTime option
}

// Functional time triggers
type ScheduleTrigger =
    | TimeOfDay of TimeSpan
    | DaysOfWeek of DayOfWeek set * TimeSpan
    | Interval of Duration
    | CronExpression of string
    | Sunrise | Sunset  // Astronomical events
    | SystemEvent of SystemEventType

type ScheduleAction =
    | SwitchDisplayMode of DisplayMode  
    | ApplyProfile of ProfileId
    | ExecuteCommand of Command
    | TriggerEvent of EventType
    | ConditionalAction of condition: (ECSWorld -> bool) * action: ScheduleAction

type ScheduleCondition =
    | UserPresent | UserAway
    | FullscreenApp of processName: string
    | PowerMode of PowerMode
    | NetworkState of NetworkState
    | CustomPredicate of (ECSWorld -> bool)
```

#### Pure Functional Scheduler
```fsharp
module ScheduleSystem =
    // Pure schedule evaluation
    let evaluateSchedule (schedule: DisplaySchedule) (currentTime: DateTime) (world: ECSWorld) : bool =
        let triggerMatches = 
            match schedule.Trigger with
            | TimeOfDay time -> currentTime.TimeOfDay = time
            | DaysOfWeek (days, time) -> 
                days.Contains(currentTime.DayOfWeek) && currentTime.TimeOfDay = time
            | Interval duration ->
                schedule.LastTriggered
                |> Option.map (fun last -> currentTime - last >= duration.ToTimeSpan())
                |> Option.defaultValue true
            | CronExpression cron -> evaluateCronExpression cron currentTime
            | Sunrise -> isApproximately currentTime (calculateSunrise currentTime world)
            | Sunset -> isApproximately currentTime (calculateSunset currentTime world)
            | SystemEvent eventType -> hasSystemEvent eventType world
        
        let conditionsMatch = 
            schedule.Conditions
            |> List.forall (evaluateCondition world)
        
        schedule.Enabled && triggerMatches && conditionsMatch
    
    // Process schedule events functionally
    let processScheduleEvents (events: TimeEvent list) (world: ECSWorld) : ECSWorld =
        events
        |> List.fold (fun acc event ->
            match event with
            | ScheduleTriggered (scheduleId, time) ->
                acc |> executeScheduleAction scheduleId time
            | SystemTimeChanged newTime ->
                acc |> updateSystemTime newTime
            | _ -> acc
        ) world
    
    // Event sourcing for schedule history
    let recordScheduleEvent (event: TimeEvent) (world: ECSWorld) : ECSWorld =
        let scheduleEvent = {
            Id = EventId.NewGuid()
            Type = "Schedule"
            Data = event
            Timestamp = DateTime.UtcNow
            Source = "ScheduleSystem"
        }
        world |> appendEvent scheduleEvent
```

#### Functional Schedule Examples
```fsharp
// Work hours with complex conditions
let workSchedule = {
    Id = ScheduleId "work-hours"
    Name = "Work Hours PC Mode"
    Trigger = DaysOfWeek (set [Monday; Tuesday; Wednesday; Thursday; Friday], TimeSpan(9, 0, 0))
    Action = SwitchDisplayMode PCMode
    Conditions = [
        UserPresent
        CustomPredicate (fun world -> not (hasFullscreenApp world))
        PowerMode ACPower
    ]
    Enabled = true
    CreatedAt = DateTime.UtcNow
    LastTriggered = None
}

// Evening entertainment with smart conditions
let eveningSchedule = {
    Id = ScheduleId "evening-tv"
    Name = "Evening TV Mode"
    Trigger = Sunset  // Automatically adjust for seasons/location
    Action = ConditionalAction (
        (fun world -> not (hasActiveVideoCall world)),  // Don't switch during calls
        SwitchDisplayMode TVMode
    )
    Conditions = [
        UserPresent
        CustomPredicate (fun world -> 
            // Only on weekends or after work hours
            let now = DateTime.Now
            now.DayOfWeek = Saturday || now.DayOfWeek = Sunday || now.Hour >= 18
        )
    ]
    Enabled = true
    CreatedAt = DateTime.UtcNow
    LastTriggered = None
}

// Gaming session detection
let gamingSchedule = {
    Id = ScheduleId "auto-gaming"
    Name = "Gaming Mode Detection"
    Trigger = SystemEvent ProcessStarted
    Action = ConditionalAction (
        (fun world -> hasGamingProcess world),
        ApplyProfile (ProfileId "gaming-ultrawide")
    )
    Conditions = [
        UserPresent
        FullscreenApp "steam"
    ]
    Enabled = true
    CreatedAt = DateTime.UtcNow
    LastTriggered = None
}
```

## Immutable Profile System

### Functional Display Profile Management
Profiles are immutable data structures with event sourcing for changes, enabling time-travel and perfect rollback capabilities.

#### Immutable Profile Structure
```fsharp
// Core immutable profile
type DisplayProfile = {
    Id: ProfileId
    Name: string
    Description: string
    DisplayConfiguration: DisplayConfiguration
    SystemSettings: SystemSettings
    ApplicationLaunchers: ApplicationLauncher list
    Metadata: ProfileMetadata
    Version: int  // For profile evolution
}

// Platform-agnostic display configuration
type DisplayConfiguration = {
    Displays: Display list
    Layout: LayoutConfiguration
    ColorProfile: ColorProfile option
    HDRSettings: HDRConfiguration option
}

type Display = {
    Id: DisplayId
    Name: string
    Enabled: bool
    Position: Position
    Resolution: Resolution
    RefreshRate: RefreshRate
    Rotation: Rotation
    IsPrimary: bool
    ColorDepth: ColorDepth
    Scaling: ScalingFactor
}

// Cross-platform system settings
type SystemSettings = {
    Audio: AudioSettings option
    Wallpaper: WallpaperSettings option
    PowerManagement: PowerSettings option
    Accessibility: AccessibilitySettings option
}

type AudioSettings = {
    OutputDevice: AudioDevice option
    Volume: float  // 0.0 to 1.0
    Muted: bool
}

// Profile metadata for analytics
type ProfileMetadata = {
    CreatedAt: DateTime
    LastUsed: DateTime option
    UsageCount: int
    AverageUsageDuration: TimeSpan option
    Tags: string set
    CreatedBy: string  // User or system
    AutoGenerated: bool
}
```

#### Functional Profile Management
```fsharp
module ProfileSystem =
    // Profile events for event sourcing
    type ProfileEvent =
        | ProfileCreated of DisplayProfile
        | ProfileUpdated of ProfileId * changes: ProfileChange list
        | ProfileApplied of ProfileId * timestamp: DateTime
        | ProfileDeleted of ProfileId
        | ProfileCloned of source: ProfileId * target: ProfileId
        | ProfileExported of ProfileId * format: string
        | ProfileImported of DisplayProfile * source: string
    
    type ProfileChange =
        | NameChanged of string
        | DescriptionChanged of string
        | DisplayAdded of Display
        | DisplayRemoved of DisplayId
        | DisplayModified of DisplayId * Display
        | SettingChanged of string * obj
    
    // Pure profile operations
    let createProfile (name: string) (description: string) (currentState: ECSWorld) : DisplayProfile =
        {
            Id = ProfileId.NewGuid()
            Name = name
            Description = description
            DisplayConfiguration = captureCurrentDisplayConfiguration currentState
            SystemSettings = captureCurrentSystemSettings currentState
            ApplicationLaunchers = []
            Metadata = {
                CreatedAt = DateTime.UtcNow
                LastUsed = None
                UsageCount = 0
                AverageUsageDuration = None
                Tags = Set.empty
                CreatedBy = getCurrentUser()
                AutoGenerated = false
            }
            Version = 1
        }
    
    // Apply profile with functional composition
    let applyProfile (profileId: ProfileId) (world: ECSWorld) : Result<ECSWorld, ProfileError> =
        result {
            let! profile = findProfile profileId world
            let! validatedProfile = validateProfile profile world
            
            // Create checkpoint for rollback
            let checkpoint = createCheckpoint world
            
            try
                // Apply changes functionally
                let! newWorld = 
                    world
                    |> applyDisplayConfiguration validatedProfile.DisplayConfiguration
                    |> Result.bind (applySystemSettings validatedProfile.SystemSettings)
                    |> Result.bind (launchApplications validatedProfile.ApplicationLaunchers)
                
                // Record usage event
                let usageEvent = ProfileApplied (profileId, DateTime.UtcNow)
                let worldWithEvent = newWorld |> recordProfileEvent usageEvent
                
                return worldWithEvent
            with
            | ex -> 
                // Rollback on error
                let! rolledBackWorld = rollbackToCheckpoint checkpoint world
                return! Error (ProfileApplicationFailed (profileId, ex.Message))
        }
    
    // Smart profile suggestions using functional composition
    let suggestProfiles (world: ECSWorld) : DisplayProfile list =
        let currentContext = analyzeCurrentContext world
        let historicalPatterns = analyzeUsagePatterns world
        let environmentFactors = analyzeEnvironmentFactors world
        
        [currentContext; historicalPatterns; environmentFactors]
        |> List.collect (fun analyzer -> analyzer.SuggestedProfiles)
        |> List.distinctBy (fun p -> p.Id)
        |> List.sortByDescending (fun p -> p.Metadata.UsageCount)
```

## Functional Network API

### Event-Driven Remote Control
The ECS architecture enables a reactive API where all operations are events, providing real-time updates and full audit trails.

#### Functional HTTP Server
```fsharp
// API events in the ECS
type APIEvent =
    | APIRequest of requestId: RequestId * endpoint: string * method: HttpMethod * timestamp: DateTime
    | APIResponse of requestId: RequestId * statusCode: int * responseTime: TimeSpan
    | APIError of requestId: RequestId * error: string
    | ClientConnected of clientId: ClientId * ipAddress: string
    | ClientDisconnected of clientId: ClientId

// Functional API handlers
type APIHandler = HttpRequest -> ECSWorld -> Result<APIResponse * ECSWorld, APIError>

module WebAPI =
    // Pure API route handling
    let routes : Map<string * HttpMethod, APIHandler> = 
        Map.ofList [
            ("/api/displays", GET), handleGetDisplays
            ("/api/profiles", GET), handleGetProfiles
            ("/api/profiles/{id}", GET), handleGetProfile
            ("/api/profiles/{id}", PUT), handleUpdateProfile
            ("/api/profiles/{id}/apply", POST), handleApplyProfile
            ("/api/events", GET), handleGetEvents
            ("/api/events/stream", GET), handleEventStream
            ("/api/state", GET), handleGetState
            ("/api/state/time-travel", POST), handleTimeTravelQuery
        ]
    
    // Event streaming for real-time updates
    let handleEventStream (request: HttpRequest) (world: ECSWorld) : Result<APIResponse * ECSWorld, APIError> =
        result {
            let eventFilter = parseEventFilter request.Query
            let eventStream = createEventStream eventFilter world
            
            let response = {
                StatusCode = 200
                Headers = Map.ofList [("Content-Type", "text/event-stream"); ("Cache-Control", "no-cache")]
                Body = StreamingBody eventStream
            }
            
            return (response, world)
        }
    
    // Time-travel API for debugging
    let handleTimeTravelQuery (request: HttpRequest) (world: ECSWorld) : Result<APIResponse * ECSWorld, APIError> =
        result {
            let! query = parseTimeTravelQuery request.Body
            let! historicalState = replayEventsToTime query.TargetTime world
            
            let response = {
                StatusCode = 200
                Headers = Map.ofList [("Content-Type", "application/json")]
                Body = JsonBody (serializeState historicalState)
            }
            
            return (response, world)
        }
    
    // Functional request processing
    let processRequest (request: HttpRequest) (world: ECSWorld) : Result<APIResponse * ECSWorld, APIError> =
        let route = (request.Path, request.Method)
        
        match Map.tryFind route routes with
        | Some handler -> 
            try
                handler request world
            with
            | ex -> Error (APIError.HandlerException (route, ex.Message))
        | None -> 
            Error (APIError.RouteNotFound route)
```

#### Advanced ECS API Endpoints
```bash
# Real-time event streaming
curl -N -H "Accept: text/event-stream" \
  "http://localhost:8080/api/events/stream?filter=display,profile"

# Time-travel debugging - get state at specific time
curl -X POST http://localhost:8080/api/state/time-travel \
  -H "Content-Type: application/json" \
  -d '{"target_time": "2025-01-25T14:30:00Z", "include_components": ["Display", "Profile"]}'

# Apply profile with rollback capability
curl -X POST http://localhost:8080/api/profiles/gaming/apply \
  -H "Content-Type: application/json" \
  -d '{"create_checkpoint": true, "timeout_seconds": 30}'

# Get component relationships in ECS
curl "http://localhost:8080/api/components/relationships?entity=display-1"

# Execute pure function without side effects (dry run)
curl -X POST http://localhost:8080/api/functions/test \
  -H "Content-Type: application/json" \
  -d '{"function": "applyProfile", "args": {"profileId": "gaming"}, "dry_run": true}'

# Event causality analysis
curl "http://localhost:8080/api/events/causality?event_id=12345&depth=5"

# System health with functional metrics
curl "http://localhost:8080/api/health" \
  | jq '.ecs_systems[] | select(.cpu_usage > 0.05)'

# Export complete system state
curl "http://localhost:8080/api/export?format=toml&include_events=true" \
  > system-backup.toml
```

#### WebSocket Real-Time Updates
```fsharp
// WebSocket handler for real-time ECS updates
let handleWebSocketConnection (webSocket: WebSocket) (world: ECSWorld) : Async<unit> =
    async {
        // Subscribe to ECS events
        let eventSubscription = subscribeToEvents ["Display"; "Profile"; "System"] world
        
        // Stream events to client
        let! cancellationToken = Async.CancellationToken
        
        while not cancellationToken.IsCancellationRequested do
            let! events = waitForEvents eventSubscription 1000  // 1 second timeout
            
            for event in events do
                let message = {
                    Type = "ecs_event"
                    Data = serializeEvent event
                    Timestamp = DateTime.UtcNow
                }
                
                let json = JsonSerializer.Serialize(message)
                let buffer = Encoding.UTF8.GetBytes(json)
                
                do! webSocket.SendAsync(
                    ArraySegment(buffer), 
                    WebSocketMessageType.Text, 
                    true, 
                    cancellationToken
                )
    }
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

## Functional Plugin Architecture

### Compositional Plugin System
The F# ECS architecture enables plugins through functional composition, allowing pure function plugins without side effects and perfect isolation.

#### Functional Plugin Interface
```fsharp
// Plugin as a collection of pure functions
type Plugin = {
    Metadata: PluginMetadata
    Initialize: PluginConfig -> ECSWorld -> Result<PluginState, PluginError>
    Update: PluginState -> ECSWorld -> float -> Result<PluginState * ECSWorld, PluginError>
    HandleEvent: PluginState -> ECSEvent -> ECSWorld -> Result<PluginState * ECSWorld, PluginError>
    GetComponents: unit -> ComponentType list
    GetSystems: unit -> ECSSystem list
    Shutdown: PluginState -> ECSWorld -> Result<ECSWorld, PluginError>
}

type PluginMetadata = {
    Name: string
    Version: Version
    Description: string
    Author: string
    Dependencies: PluginDependency list
    Permissions: Permission list
    CompatibleVersions: Version list
}

// Plugin events
type PluginEvent =
    | PluginLoaded of PluginId * PluginMetadata
    | PluginInitialized of PluginId * PluginState
    | PluginError of PluginId * PluginError
    | PluginUnloaded of PluginId
    | PluginDependencyResolved of PluginId * dependency: string
    | PluginPermissionRequested of PluginId * Permission

// Safe plugin API through functional composition
type PluginAPI = {
    // Read-only access to ECS world
    QueryComponents: ComponentQuery -> ComponentResult list
    GetCurrentState: unit -> ECSWorldSnapshot
    GetEventHistory: EventFilter -> EventHistoryRange -> ECSEvent list
    
    // Event emission (only way to affect world)
    EmitEvent: ECSEvent -> Result<unit, PermissionError>
    
    // Pure function execution
    TestFunction: ('a -> 'b) -> 'a -> 'b  // For testing pure functions
    
    // UI integration
    RegisterUIComponent: UIComponent -> Result<UIComponentId, UIError>
    RegisterMenuItem: MenuItem -> Result<MenuItemId, UIError>
}
```

#### Functional Plugin Manager
```fsharp
module PluginSystem =
    // Plugin state management
    type PluginSystemState = {
        LoadedPlugins: Map<PluginId, Plugin * PluginState>
        PluginDependencies: Map<PluginId, PluginId list>
        PluginPermissions: Map<PluginId, Permission set>
        PluginEvents: ECSEvent list
    }
    
    // Pure plugin loading
    let loadPlugin (pluginPath: string) : Result<Plugin, PluginError> =
        result {
            let! assembly = loadAssemblySafely pluginPath
            let! pluginType = findPluginType assembly
            let! plugin = instantiatePlugin pluginType
            let! validatedPlugin = validatePlugin plugin
            return validatedPlugin
        }
    
    // Dependency resolution using topological sort
    let resolveDependencies (plugins: Plugin list) : Result<Plugin list, DependencyError> =
        let dependencyGraph = 
            plugins
            |> List.map (fun p -> (p.Metadata.Name, p.Metadata.Dependencies |> List.map (fun d -> d.Name)))
            |> Map.ofList
        
        match topologicalSort dependencyGraph with
        | Ok sortedNames -> 
            sortedNames 
            |> List.choose (fun name -> plugins |> List.tryFind (fun p -> p.Metadata.Name = name))
            |> Ok
        | Error cycle -> Error (CircularDependency cycle)
    
    // Safe plugin execution with isolation
    let executePluginSafely (plugin: Plugin) (pluginState: PluginState) (operation: PluginState -> ECSWorld -> Result<PluginState * ECSWorld, PluginError>) (world: ECSWorld) : Result<PluginState * ECSWorld, PluginError> =
        try
            // Create isolated execution context
            let isolatedWorld = createIsolatedContext world plugin.Metadata.Permissions
            
            // Execute with timeout
            let result = executeWithTimeout 5000 (fun () -> operation pluginState isolatedWorld)
            
            match result with
            | Ok (newState, modifiedWorld) ->
                // Validate changes are within permissions
                let! validatedWorld = validatePluginChanges plugin.Metadata.Permissions world modifiedWorld
                Ok (newState, validatedWorld)
            | Error error -> Error error
        with
        | ex -> Error (PluginExecutionException (plugin.Metadata.Name, ex.Message))
    
    // Event-driven plugin communication
    let handlePluginEvent (event: ECSEvent) (pluginState: PluginSystemState) (world: ECSWorld) : Result<PluginSystemState * ECSWorld, PluginError> =
        result {
            let mutable newWorld = world
            let mutable newPluginStates = pluginState.LoadedPlugins
            
            // Process event through each plugin
            for KeyValue(pluginId, (plugin, state)) in pluginState.LoadedPlugins do
                let! (newState, updatedWorld) = executePluginSafely plugin state (fun s w -> plugin.HandleEvent s event w) newWorld
                newPluginStates <- newPluginStates |> Map.add pluginId (plugin, newState)
                newWorld <- updatedWorld
            
            let newPluginState = { pluginState with LoadedPlugins = newPluginStates }
            return (newPluginState, newWorld)
        }
```

#### Example Functional Plugin
```fsharp
// Notification plugin implemented functionally
let notificationPlugin : Plugin = {
    Metadata = {
        Name = "Advanced Notifications"
        Version = Version(1, 0, 0)
        Description = "Smart notifications with functional composition"
        Author = "DisplaySwitch Pro Team"
        Dependencies = []
        Permissions = [EmitEvent; ReadComponents ["Display"; "Profile"]]
        CompatibleVersions = [Version(1, 0, 0)]
    }
    
    Initialize = fun config world ->
        let initialState = {
            NotificationHistory = []
            UserPreferences = config.UserPreferences
            LastDisplayChange = None
        }
        Ok initialState
    
    Update = fun state world deltaTime ->
        // Pure update logic
        let newState = updateNotificationTimers state deltaTime
        Ok (newState, world)
    
    HandleEvent = fun state event world ->
        match event with
        | DisplayModeChanged (oldMode, newMode) ->
            let notification = createSmartNotification oldMode newMode world
            let newState = { state with 
                NotificationHistory = notification :: state.NotificationHistory
                LastDisplayChange = Some DateTime.UtcNow
            }
            
            // Emit notification event
            let notificationEvent = NotificationRequested notification
            let worldWithEvent = world |> appendEvent notificationEvent
            
            Ok (newState, worldWithEvent)
        | _ -> Ok (state, world)
    
    GetComponents = fun () -> [NotificationComponent]
    GetSystems = fun () -> [NotificationSystem]
    Shutdown = fun state world -> Ok world
}
```

## Integration Points

### Related Components with F# ECS Benefits
- **[Core Features](core-features.md)**: ECS enables composable feature development
- **[Configuration Management](config-management.md)**: Immutable configs with event sourcing
- **[System Tray](system-tray.md)**: Reactive tray updates via event system
- **[Troubleshooting](troubleshooting.md)**: Time-travel debugging and event replay
- **[Installation](installation.md)**: Single binary with zero configuration

### F# ECS Architecture Advantages Over OOP

#### What's Difficult in Traditional OOP
1. **State Management**: Mutable objects lead to race conditions and inconsistent state
2. **Debugging**: Object hierarchies make it hard to trace state changes
3. **Testing**: Side effects and mutable state make unit testing complex
4. **Extensibility**: Inheritance chains create tight coupling
5. **Concurrency**: Shared mutable state requires complex locking
6. **Hot Reload**: Changing object definitions requires application restart

#### How F# ECS Solves These Problems
```fsharp
// 1. Immutable State Management
type ECSWorld = {
    Entities: Map<EntityId, Entity>
    Components: Map<ComponentType, Map<EntityId, Component>>
    Systems: ECSSystem list
    Events: ECSEvent list
} // Immutable by default - no race conditions!

// 2. Perfect Debugging with Event Sourcing
let debugDisplayIssue (issueTime: DateTime) (world: ECSWorld) : DebugInfo =
    world.Events
    |> List.filter (fun e -> e.Timestamp <= issueTime)
    |> List.fold applyEvent emptyWorld  // Reconstruct exact state
    |> analyzeState  // Time-travel debugging!

// 3. Pure Function Testing
let testDisplaySwitch (fromMode: DisplayMode) (toMode: DisplayMode) : bool =
    let initialWorld = createTestWorld fromMode
    let result = switchDisplayMode toMode initialWorld
    match result with
    | Ok newWorld -> validateDisplayMode toMode newWorld
    | Error _ -> false
    // No mocking needed - pure functions!

// 4. Composition Over Inheritance
let createCustomSystem (features: SystemFeature list) : ECSSystem =
    features
    |> List.fold composeSystemFeature baseSystem
    // Add features without touching existing code!

// 5. Fearless Concurrency
let processEventsParallel (events: ECSEvent list) (world: ECSWorld) : ECSWorld =
    events
    |> List.groupBy (fun e -> e.EntityId)
    |> List.map (processEntityEvents world)  // Pure functions
    |> Array.Parallel.map id  // Safe parallelization!
    |> combineResults

// 6. Hot Reload Everything
let hotReloadPlugin (newPlugin: Plugin) (world: ECSWorld) : ECSWorld =
    world
    |> removePlugin oldPluginId  // Remove old version
    |> addPlugin newPlugin       // Add new version
    // No restart needed - just function replacement!
```

### Future Development with Functional Advantages
- **ML-Powered Pattern Recognition**: Pure functions make feature engineering simple
- **Distributed Computing**: Immutable state enables easy clustering
- **Live Programming**: Hot-reload entire system logic without restart
- **Formal Verification**: Prove correctness of display logic mathematically
- **Advanced Time-Travel**: Debug issues that happened weeks ago
- **Automatic Optimization**: AI can safely optimize pure functions

### Performance Benefits
```fsharp
// Memory efficiency through structural sharing
let updateDisplay (displayId: DisplayId) (newConfig: DisplayConfig) (world: ECSWorld) : ECSWorld =
    { world with 
        Components = 
            world.Components 
            |> Map.update "Display" (Map.add displayId (DisplayComponent newConfig))
    } // Only changed parts use new memory!

// Predictable performance - no GC surprises
let processFrame (deltaTime: float) (world: ECSWorld) : ECSWorld =
    world.Systems
    |> List.fold (fun w system -> system.Update w deltaTime) world
    // Stack-allocated, no heap allocations!
```