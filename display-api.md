# Display API - Platform Adapter Pattern with Effect Isolation

## Overview

The Display API in DisplaySwitch-Pro implements a platform adapter pattern that isolates all display-related side effects at system boundaries. This functional approach provides a pure, testable interface for display management while supporting multiple platforms through specialized adapters. The API integrates seamlessly with the ECS architecture, treating display configurations as immutable data.

## Architectural Principles

### Effect Isolation
All display operations are modeled as effects, ensuring pure functional core logic:

```fsharp
type DisplayEffect =
    | QueryDisplays of AsyncReplyChannel<DisplayInfo list>
    | ApplyConfiguration of DisplayConfig * AsyncReplyChannel<Result<unit, string>>
    | SetDisplayMode of DisplayMode * AsyncReplyChannel<Result<unit, string>>
    | GetCurrentConfig of AsyncReplyChannel<DisplayConfig>
    | ValidateConfig of DisplayConfig * AsyncReplyChannel<ValidationResult>
    | RefreshDisplayDatabase of AsyncReplyChannel<unit>

// Effects are handled by platform adapters, never directly by core logic
let handleDisplayEffect (effect: DisplayEffect) : Async<unit> =
    async {
        match effect with
        | QueryDisplays replyChannel ->
            let! displays = PlatformDisplayAdapter.queryAllDisplays()
            replyChannel.Reply(displays)
            
        | ApplyConfiguration (config, replyChannel) ->
            let! result = PlatformDisplayAdapter.applyConfiguration config
            replyChannel.Reply(result)
            
        | SetDisplayMode (mode, replyChannel) ->
            let! config = DisplayModeCalculator.calculateConfig mode
            let! result = PlatformDisplayAdapter.applyConfiguration config
            replyChannel.Reply(result)
    }
```

### Immutable Display State
All display information is represented as immutable data structures:

```fsharp
type DisplayInfo = {
    Id: DisplayId
    Name: string
    FriendlyName: string
    Resolution: Resolution
    Position: Point
    RefreshRate: int
    IsActive: bool
    IsPrimary: bool
    Capabilities: DisplayCapabilities
    ConnectionType: ConnectionType
    Adapter: AdapterInfo
}

type DisplayConfig = {
    Displays: DisplayInfo list
    Topology: DisplayTopology
    Timestamp: DateTime
    ConfigId: Guid
    IsValid: bool
    Metadata: Map<string, obj>
}

type DisplayMode =
    | PCMode of multiDisplaySettings: MultiDisplaySettings
    | TVMode of singleDisplaySettings: SingleDisplaySettings
    | CustomMode of customConfig: DisplayConfig
```

## Platform Adapter Pattern

The adapter pattern provides a unified interface across different platforms:

### Core Adapter Interface
```fsharp
type IPlatformDisplayAdapter =
    abstract member QueryDisplays : unit -> Async<DisplayInfo list>
    abstract member ApplyConfiguration : DisplayConfig -> Async<Result<unit, string>>
    abstract member GetCapabilities : unit -> Async<PlatformCapabilities>
    abstract member ValidateConfiguration : DisplayConfig -> Async<ValidationResult>
    abstract member SubscribeToChanges : (DisplayEvent -> unit) -> IDisposable

type PlatformCapabilities = {
    MaxDisplays: int
    SupportedResolutions: Resolution list
    SupportedRefreshRates: int list
    SupportsHDR: bool
    SupportsVariableRefresh: bool
    MaxBandwidth: Bandwidth option
}
```

### Windows Platform Adapter
```fsharp
module WindowsDisplayAdapter =
    open System.Runtime.InteropServices
    
    // Windows Display Configuration API wrapper
    type WindowsDisplayAdapter() =
        interface IPlatformDisplayAdapter with
            member _.QueryDisplays() = async {
                let! pathArray, modeArray = queryDisplayConfigNative()
                let displays = pathArray 
                    |> Array.map (fun path -> convertPathToDisplayInfo path modeArray)
                    |> Array.toList
                return displays
            }
            
            member _.ApplyConfiguration(config) = async {
                try
                    let pathArray, modeArray = convertConfigToNativeArrays config
                    let result = SetDisplayConfig(
                        uint32 pathArray.Length,
                        pathArray,
                        uint32 modeArray.Length,
                        modeArray,
                        SetDisplayConfigFlags.Apply ||| SetDisplayConfigFlags.SaveToDatabase
                    )
                    
                    if result = 0 then
                        return Ok ()
                    else
                        return Error $"SetDisplayConfig failed with error code {result}"
                with
                | ex -> return Error ex.Message
            }
    
    // Low-level Windows API interop
    [<DllImport("user32.dll")>]
    extern int SetDisplayConfig(
        uint32 numPathArrayElements,
        DisplayConfigPathInfo[] pathInfoArray,
        uint32 numModeInfoArrayElements,
        DisplayConfigModeInfo[] modeInfoArray,
        SetDisplayConfigFlags flags)
    
    [<DllImport("user32.dll")>]
    extern int QueryDisplayConfig(
        QueryDisplayConfigFlags flags,
        uint32& numPathArrayElements,
        DisplayConfigPathInfo[] pathInfoArray,
        uint32& numModeInfoArrayElements,
        DisplayConfigModeInfo[] modeInfoArray,
        DisplayConfigTopologyId& currentTopologyId)
```

### macOS Platform Adapter
```fsharp
module MacOSDisplayAdapter =
    open CoreGraphics
    open Foundation
    
    type MacOSDisplayAdapter() =
        interface IPlatformDisplayAdapter with
            member _.QueryDisplays() = async {
                let displayIds = CGDisplay.GetActiveDisplayList()
                let displays = displayIds
                    |> Array.map convertCGDisplayToDisplayInfo
                    |> Array.toList
                return displays
            }
            
            member _.ApplyConfiguration(config) = async {
                try
                    // macOS uses display reconfiguration transactions
                    use transaction = CGDisplayConfiguration.BeginConfiguration()
                    
                    for display in config.Displays do
                        if display.IsActive then
                            CGDisplayConfiguration.SetDisplayMode(
                                display.Id.Handle,
                                createCGDisplayMode display.Resolution display.RefreshRate
                            )
                        else
                            CGDisplayConfiguration.DisableDisplay(display.Id.Handle)
                    
                    let result = CGDisplayConfiguration.CompleteConfiguration(transaction)
                    if result = CGError.Success then
                        return Ok ()
                    else
                        return Error $"Display configuration failed: {result}"
                with
                | ex -> return Error ex.Message
            }
    
    let convertCGDisplayToDisplayInfo (displayId: uint32) : DisplayInfo =
        let bounds = CGDisplay.GetBounds(displayId)
        let mode = CGDisplay.GetDisplayMode(displayId)
        
        {
            Id = DisplayId.Create(displayId.ToString())
            Name = CGDisplay.GetDisplayName(displayId)
            FriendlyName = CGDisplay.GetDisplayName(displayId)
            Resolution = { Width = int bounds.Width; Height = int bounds.Height }
            Position = { X = int bounds.X; Y = int bounds.Y }
            RefreshRate = int (CGDisplayMode.GetRefreshRate(mode))
            IsActive = CGDisplay.IsActive(displayId)
            IsPrimary = CGDisplay.IsMain(displayId)
            Capabilities = queryDisplayCapabilities displayId
            ConnectionType = inferConnectionType displayId
            Adapter = queryAdapterInfo displayId
        }
```

### Linux Platform Adapter
```fsharp
module LinuxDisplayAdapter =
    open X11
    open RandR
    
    type LinuxDisplayAdapter() =
        interface IPlatformDisplayAdapter with
            member _.QueryDisplays() = async {
                use display = XOpenDisplay(null)
                let screen = XDefaultScreen(display)
                let root = XRootWindow(display, screen)
                
                let resources = XRRGetScreenResources(display, root)
                let outputs = XRRGetOutputs(resources)
                
                let displays = outputs
                    |> Array.map (convertXRROutputToDisplayInfo display)
                    |> Array.toList
                    
                return displays
            }
            
            member _.ApplyConfiguration(config) = async {
                try
                    use display = XOpenDisplay(null)
                    let screen = XDefaultScreen(display)
                    let root = XRootWindow(display, screen)
                    
                    // Apply configuration using XRandR
                    for displayInfo in config.Displays do
                        if displayInfo.IsActive then
                            XRRSetCrtcConfig(
                                display,
                                displayInfo.Id.Handle,
                                displayInfo.Position.X,
                                displayInfo.Position.Y,
                                findModeId displayInfo.Resolution displayInfo.RefreshRate,
                                RRRotate_0,
                                [| displayInfo.Id.Handle |]
                            )
                        else
                            XRRSetCrtcConfig(display, displayInfo.Id.Handle, 0, 0, 0, RRRotate_0, [||])
                    
                    return Ok ()
                with
                | ex -> return Error ex.Message
            }
```

## Pure Functional Display Logic

Core display logic is implemented as pure functions:

### Display Mode Calculation
```fsharp
module DisplayModeCalculator =
    
    let calculatePCModeConfig (displays: DisplayInfo list) : DisplayConfig =
        let activeDisplays = displays |> List.map (fun d -> { d with IsActive = true })
        let arrangedDisplays = arrangeDisplaysOptimally activeDisplays
        
        {
            Displays = arrangedDisplays
            Topology = Extended
            Timestamp = DateTime.UtcNow
            ConfigId = Guid.NewGuid()
            IsValid = true
            Metadata = Map.empty
        }
    
    let calculateTVModeConfig (displays: DisplayInfo list) (preferredDisplay: DisplayId option) : DisplayConfig =
        let externalDisplays = displays |> List.filter (fun d -> not (isBuiltInDisplay d))
        let targetDisplay = 
            match preferredDisplay with
            | Some id -> externalDisplays |> List.tryFind (fun d -> d.Id = id)
            | None -> externalDisplays |> List.tryHead
            
        match targetDisplay with
        | Some display ->
            let activeDisplay = { display with IsActive = true; IsPrimary = true }
            let inactiveDisplays = displays 
                |> List.filter (fun d -> d.Id <> display.Id)
                |> List.map (fun d -> { d with IsActive = false; IsPrimary = false })
                
            {
                Displays = activeDisplay :: inactiveDisplays
                Topology = Single
                Timestamp = DateTime.UtcNow
                ConfigId = Guid.NewGuid()
                IsValid = true
                Metadata = Map.empty
            }
        | None -> 
            failwith "No external display available for TV mode"
    
    let arrangeDisplaysOptimally (displays: DisplayInfo list) : DisplayInfo list =
        displays
        |> List.sortBy (fun d -> d.Position.X, d.Position.Y)
        |> List.mapi (fun i display ->
            { display with Position = { X = i * display.Resolution.Width; Y = 0 } })
```

### Configuration Validation
```fsharp
module DisplayConfigValidator =
    
    type ValidationResult = {
        IsValid: bool
        Errors: ValidationError list
        Warnings: ValidationWarning list
    }
    
    type ValidationError =
        | InvalidResolution of DisplayId * Resolution
        | UnsupportedRefreshRate of DisplayId * int
        | OverlappingDisplays of DisplayId * DisplayId
        | ExceedsHardwareLimits of string
        | MissingPrimaryDisplay
    
    let validateDisplayConfig (config: DisplayConfig) (capabilities: PlatformCapabilities) : ValidationResult =
        let errors = [
            yield! validateResolutions config.Displays capabilities
            yield! validateRefreshRates config.Displays capabilities
            yield! validateDisplayOverlaps config.Displays
            yield! validateHardwareLimits config capabilities
            yield! validatePrimaryDisplay config.Displays
        ]
        
        let warnings = [
            yield! checkSuboptimalArrangements config.Displays
            yield! checkPowerConsumption config.Displays
        ]
        
        {
            IsValid = List.isEmpty errors
            Errors = errors
            Warnings = warnings
        }
    
    let validateResolutions (displays: DisplayInfo list) (capabilities: PlatformCapabilities) : ValidationError list =
        displays
        |> List.filter (fun d -> d.IsActive)
        |> List.choose (fun display ->
            if capabilities.SupportedResolutions |> List.contains display.Resolution then
                None
            else
                Some (InvalidResolution (display.Id, display.Resolution)))
```

## Event-Driven Display Monitoring

Display changes are monitored through events:

```fsharp
type DisplayEvent =
    | DisplayConnected of DisplayInfo
    | DisplayDisconnected of DisplayId
    | DisplayModeChanged of DisplayId * Resolution * int
    | PrimaryDisplayChanged of DisplayId
    | DisplayArrangementChanged of DisplayInfo list

module DisplayEventMonitor =
    
    let subscribeToDisplayEvents (handler: DisplayEvent -> unit) : IDisposable =
        let disposables = ResizeArray<IDisposable>()
        
        // Subscribe to platform-specific events
        let platformSubscription = PlatformDisplayAdapter.SubscribeToChanges(fun evt ->
            match evt with
            | PlatformDisplayEvent.Connected info -> handler (DisplayConnected info)
            | PlatformDisplayEvent.Disconnected id -> handler (DisplayDisconnected id)
            | PlatformDisplayEvent.ModeChanged (id, res, rate) -> handler (DisplayModeChanged (id, res, rate))
        )
        
        disposables.Add(platformSubscription)
        
        // Composite disposable
        { new IDisposable with
            member _.Dispose() = 
                for disposable in disposables do
                    disposable.Dispose()
        }
```

## Integration with ECS

The display API integrates with the ECS world state:

```fsharp
// ECS Components for display management
type DisplayComponent = {
    Info: DisplayInfo
    LastUpdate: DateTime
}

type ActiveDisplayComponent = {
    Mode: DisplayMode
    AppliedAt: DateTime
}

type DisplayConfigurationComponent = {
    Config: DisplayConfig
    ValidationResult: ValidationResult
    IsApplied: bool
}

// Systems that work with display components
module DisplaySystems =
    
    let updateDisplaysSystem (world: World) : World =
        // Query all display entities
        let displayEntities = world.Query<DisplayComponent>()
        
        // Check for changes and update components
        displayEntities
        |> Seq.fold (fun w (entity, displayComp) ->
            let currentInfo = PlatformDisplayAdapter.GetDisplayInfo(displayComp.Info.Id)
            if currentInfo <> displayComp.Info then
                w.UpdateComponent(entity, { displayComp with Info = currentInfo; LastUpdate = DateTime.UtcNow })
            else w
        ) world
    
    let displayModeApplicationSystem (world: World) : World =
        // Find entities with pending display mode changes
        world.Query<DisplayConfigurationComponent>()
        |> Seq.filter (fun (_, config) -> not config.IsApplied)
        |> Seq.fold (fun w (entity, configComp) ->
            // Apply configuration through effect system
            let effect = ApplyConfiguration (configComp.Config, AsyncReplyChannel<_>())
            world.SendEffect(effect)
            
            // Mark as applied
            w.UpdateComponent(entity, { configComp with IsApplied = true })
        ) world
```

## Testing Strategy

The functional approach enables comprehensive testing:

### Pure Function Testing
```fsharp
[<Test>]
let ``calculatePCModeConfig activates all displays`` () =
    // Arrange
    let displays = [
        createTestDisplay "Display1" false
        createTestDisplay "Display2" false
        createTestDisplay "Display3" false
    ]
    
    // Act
    let config = DisplayModeCalculator.calculatePCModeConfig displays
    
    // Assert
    config.Displays |> List.forall (fun d -> d.IsActive) |> should be True
    config.Topology |> should equal Extended

[<Test>]
let ``validateDisplayConfig detects invalid resolution`` () =
    // Arrange
    let config = createTestConfig [
        { createTestDisplay "Display1" true with Resolution = { Width = 9999; Height = 9999 } }
    ]
    let capabilities = { PlatformCapabilities.Default with SupportedResolutions = [{ Width = 1920; Height = 1080 }] }
    
    // Act
    let result = DisplayConfigValidator.validateDisplayConfig config capabilities
    
    // Assert
    result.IsValid |> should be False
    result.Errors |> should contain (InvalidResolution (DisplayId.Create("Display1"), { Width = 9999; Height = 9999 }))
```

### Platform Adapter Testing
```fsharp
[<Test>]
let ``WindowsDisplayAdapter queries displays without errors`` () =
    async {
        // Arrange
        let adapter = WindowsDisplayAdapter()
        
        // Act & Assert
        let! displays = adapter.QueryDisplays()
        displays |> should not' (be Empty)
        displays |> List.forall (fun d -> String.IsNullOrEmpty(d.Name) |> not) |> should be True
    }

[<Test>]
let ``MacOSDisplayAdapter applies configuration successfully`` () =
    async {
        // Arrange
        let adapter = MacOSDisplayAdapter()
        let config = createValidTestConfig()
        
        // Act
        let! result = adapter.ApplyConfiguration(config)
        
        // Assert
        match result with
        | Ok () -> () // Success
        | Error msg -> failwith $"Configuration failed: {msg}"
    }
```

### Effect System Testing
```fsharp
[<Test>]
let ``handleDisplayEffect processes QueryDisplays correctly`` () =
    async {
        // Arrange
        let replyChannel = AsyncReplyChannel<DisplayInfo list>()
        let effect = QueryDisplays replyChannel
        
        // Act
        do! handleDisplayEffect effect
        let! displays = replyChannel.Reply
        
        // Assert
        displays |> should not' (be Empty)
    }
```

## Error Handling and Resilience

Comprehensive error handling throughout the system:

```fsharp
type DisplayError =
    | PlatformNotSupported of string
    | InvalidConfiguration of ValidationError list
    | HardwareFailure of string
    | DriverError of string
    | PermissionDenied of string
    | TimeoutError of string

let executeDisplayOperationSafely (operation: unit -> Async<'T>) : Async<Result<'T, DisplayError>> =
    async {
        try
            let! result = operation()
            return Ok result
        with
        | :? PlatformNotSupportedException as ex ->
            return Error (PlatformNotSupported ex.Message)
        | :? UnauthorizedAccessException as ex ->
            return Error (PermissionDenied ex.Message)
        | :? TimeoutException as ex ->
            return Error (TimeoutError ex.Message)
        | ex ->
            return Error (HardwareFailure ex.Message)
    }

// Retry logic for transient failures
let executeWithRetry (operation: unit -> Async<Result<'T, DisplayError>>) (maxRetries: int) : Async<Result<'T, DisplayError>> =
    let rec attempt retriesLeft =
        async {
            let! result = operation()
            match result, retriesLeft with
            | Ok value, _ -> return Ok value
            | Error (HardwareFailure _ | TimeoutError _), n when n > 0 ->
                do! Async.Sleep 2000
                return! attempt (n - 1)
            | Error error, _ -> return Error error
        }
    attempt maxRetries
```

## Performance Optimizations

### Configuration Caching
```fsharp
module DisplayConfigCache =
    let private cache = ConcurrentDictionary<Guid, DisplayConfig * DateTime>()
    let private cacheTimeout = TimeSpan.FromMinutes(5.0)
    
    let getCachedConfig (configId: Guid) : DisplayConfig option =
        match cache.TryGetValue(configId) with
        | true, (config, timestamp) when DateTime.UtcNow - timestamp < cacheTimeout ->
            Some config
        | _ -> None
    
    let cacheConfig (config: DisplayConfig) : unit =
        cache.TryAdd(config.ConfigId, (config, DateTime.UtcNow)) |> ignore
```

### Lazy Display Detection
```fsharp
module LazyDisplayDetection =
    let mutable lastDisplayCount = 0
    let mutable lastQueryTime = DateTime.MinValue
    let queryInterval = TimeSpan.FromSeconds(10.0)
    
    let shouldRefreshDisplays () : bool =
        let timeSinceLastQuery = DateTime.UtcNow - lastQueryTime
        timeSinceLastQuery > queryInterval
    
    let getDisplaysWithCaching () : Async<DisplayInfo list> =
        async {
            if shouldRefreshDisplays() then
                let! displays = PlatformDisplayAdapter.QueryDisplays()
                lastDisplayCount <- displays.Length
                lastQueryTime <- DateTime.UtcNow
                return displays
            else
                return! getCachedDisplays()
        }
```

## Benefits of Platform Adapter Pattern

### Testability
- Platform-specific code is isolated and can be mocked
- Core logic is pure and easily unit tested
- Effect system allows testing without actual hardware
- Different platforms can be tested independently

### Maintainability
- Clear separation between pure logic and side effects
- Platform-specific implementations are encapsulated
- Easy to add support for new platforms
- Changes to one platform don't affect others

### Cross-Platform Consistency
- Unified interface across all platforms
- Consistent error handling and validation
- Same functional API regardless of underlying platform
- Predictable behavior across different environments

### Performance
- Platform-specific optimizations can be implemented
- Caching and lazy loading reduce unnecessary API calls
- Batch operations where supported by platform
- Efficient resource management through adapters

The platform adapter pattern with effect isolation provides a robust, testable, and maintainable foundation for display management that works consistently across all supported platforms while maintaining the pure functional principles of the ECS architecture.