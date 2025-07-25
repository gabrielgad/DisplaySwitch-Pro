# Configuration Management

## Overview

The Configuration Management system provides immutable, event-sourced configuration handling for display setups. Built on functional programming principles and Entity-Component-System (ECS) architecture, this system treats configurations as immutable data structures with event sourcing for change tracking, enabling reliable, testable, and auditable configuration management.

## Functional Architecture

### Immutable Configuration Types
**Location**: `Core/Configuration.fs`

```fsharp
// Immutable configuration record types
type DisplayInfo = {
    DeviceName: string
    FriendlyName: string
    IsActive: bool
    Position: int * int
    Resolution: uint * uint
    RefreshRate: uint
    TargetId: uint
    SourceId: uint
}

type DisplayConfig = {
    Name: string
    Displays: DisplayInfo list
    Timestamp: DateTimeOffset
    Version: string
}

// Configuration events for event sourcing
type ConfigEvent = 
    | ConfigCreated of DisplayConfig
    | ConfigUpdated of DisplayConfig * DisplayConfig
    | ConfigDeleted of string * DateTimeOffset
    | ConfigRestored of DisplayConfig
```

**Key Benefits**:
- **Immutability**: Configurations cannot be accidentally modified
- **Type Safety**: F# type system prevents invalid configurations
- **Event Sourcing**: All changes are tracked as events
- **Testability**: Pure data structures enable easy testing

### Event Store
**Location**: `Core/ConfigurationStore.fs`

```fsharp
// Event store for configuration events
module ConfigurationStore =
    
    type EventStore = {
        Events: ConfigEvent list
        Snapshots: Map<string, DisplayConfig>
    }
    
    // Pure functions for event handling
    let applyEvent (store: EventStore) (event: ConfigEvent) : EventStore =
        match event with
        | ConfigCreated config ->
            { store with 
                Events = event :: store.Events
                Snapshots = Map.add config.Name config store.Snapshots }
        | ConfigUpdated (oldConfig, newConfig) ->
            { store with 
                Events = event :: store.Events
                Snapshots = Map.add newConfig.Name newConfig store.Snapshots }
        | ConfigDeleted (name, timestamp) ->
            { store with 
                Events = event :: store.Events
                Snapshots = Map.remove name store.Snapshots }
        | ConfigRestored config ->
            { store with 
                Events = event :: store.Events
                Snapshots = Map.add config.Name config store.Snapshots }
```

**Benefits**:
- **Event Sourcing**: Complete audit trail of all configuration changes
- **Immutable State**: Store state never mutates, only transforms
- **Testability**: Pure functions enable comprehensive testing

## Configuration Operations

### Pure Configuration Functions
**Location**: `Core/ConfigurationLogic.fs`

```fsharp
module ConfigurationLogic =
    
    // Pure function to create new configuration
    let createConfig (name: string) (displays: DisplayInfo list) : DisplayConfig =
        {
            Name = name
            Displays = displays
            Timestamp = DateTimeOffset.Now
            Version = "2.0"
        }
    
    // Pure function to validate configuration
    let validateConfig (config: DisplayConfig) : Result<DisplayConfig, string> =
        if String.IsNullOrWhiteSpace(config.Name) then
            Error "Configuration name cannot be empty"
        elif List.isEmpty config.Displays then
            Error "Configuration must contain at least one display"
        elif config.Displays |> List.exists (fun d -> d.Resolution = (0u, 0u)) then
            Error "All displays must have valid resolution"
        else
            Ok config
    
    // Pure function to merge configurations
    let mergeConfigs (primary: DisplayConfig) (secondary: DisplayConfig) : DisplayConfig =
        let mergedDisplays = 
            primary.Displays @ 
            (secondary.Displays |> List.filter (fun d -> 
                not (primary.Displays |> List.exists (fun pd -> pd.DeviceName = d.DeviceName))))
        
        { primary with Displays = mergedDisplays }
```

### Effect Handlers for I/O Operations
**Location**: `Adapters/ConfigurationEffects.fs`

```fsharp
module ConfigurationEffects =
    
    // Effect types for configuration operations
    type ConfigEffect<'T> =
        | Pure of 'T
        | SaveConfig of DisplayConfig * string * (Result<unit, string> -> ConfigEffect<'T>)
        | LoadConfig of string * (Result<DisplayConfig, string> -> ConfigEffect<'T>)
        | GetCurrentConfig of (DisplayConfig -> ConfigEffect<'T>)
        | ShowDialog of string * (bool -> ConfigEffect<'T>)
    
    // Pure configuration save logic
    let saveConfigLogic (config: DisplayConfig) (path: string) : ConfigEffect<Result<unit, string>> =
        config
        |> ConfigurationLogic.validateConfig
        |> function
            | Ok validConfig ->
                SaveConfig(validConfig, path, fun result ->
                    match result with
                    | Ok () -> Pure (Ok ())
                    | Error msg -> Pure (Error msg))
            | Error msg -> Pure (Error msg)
    
    // Effect interpreter for file operations
    let rec interpretConfigEffect<'T> (effect: ConfigEffect<'T>) : 'T =
        match effect with
        | Pure value -> value
        | SaveConfig (config, path, cont) ->
            try
                let json = JsonSerializer.Serialize(config, JsonSerializerOptions(WriteIndented = true))
                File.WriteAllText(path, json)
                cont (Ok ()) |> interpretConfigEffect
            with
            | ex -> cont (Error ex.Message) |> interpretConfigEffect
        | LoadConfig (path, cont) ->
            try
                let json = File.ReadAllText(path)
                let config = JsonSerializer.Deserialize<DisplayConfig>(json)
                cont (Ok config) |> interpretConfigEffect
            with
            | ex -> cont (Error ex.Message) |> interpretConfigEffect
        | GetCurrentConfig cont ->
            let currentConfig = DisplayAPI.getCurrentConfiguration()
            cont currentConfig |> interpretConfigEffect
        | ShowDialog (message, cont) ->
            let result = MessageBox.Show(message, "Confirm", MessageBoxButtons.YesNo) = DialogResult.Yes
            cont result |> interpretConfigEffect
```

**Benefits**:
- **Separation of Concerns**: Pure logic separated from side effects
- **Testability**: Effects can be mocked and tested independently
- **Composability**: Effects can be combined and reused

### Event-Sourced Configuration Workflow
**Location**: `Systems/ConfigurationSystem.fs`

```fsharp
module ConfigurationSystem =
    
    // Pure function composition for configuration operations
    let saveConfigurationWorkflow (name: string) : ConfigEffect<Result<unit, string>> =
        configEffect {
            let! currentConfig = GetCurrentConfig
            let namedConfig = { currentConfig with Name = name }
            let validationResult = ConfigurationLogic.validateConfig namedConfig
            
            match validationResult with
            | Ok validConfig ->
                let! saveResult = SaveConfig(validConfig, $"{name}.json")
                match saveResult with
                | Ok () ->
                    // Create event for event store
                    let event = ConfigCreated validConfig
                    let! _ = applyConfigEvent event
                    return Ok ()
                | Error msg -> return Error msg
            | Error msg -> return Error msg
        }
    
    // Functional pipeline for loading configurations
    let loadConfigurationWorkflow (path: string) : ConfigEffect<Result<DisplayConfig, string>> =
        configEffect {
            let! loadResult = LoadConfig path
            match loadResult with
            | Ok config ->
                let! confirmed = ShowDialog $"Load configuration '{config.Name}'?"
                if confirmed then
                    let event = ConfigRestored config
                    let! _ = applyConfigEvent event
                    return Ok config
                else
                    return Error "Load cancelled by user"
            | Error msg -> return Error msg
        }
```

## Configuration Event Processing

### Event Sourcing Implementation
**Location**: `Core/EventSourcing.fs`

```fsharp
module EventSourcing =
    
    // Pure function to replay events and build current state
    let replayEvents (events: ConfigEvent list) : Map<string, DisplayConfig> =
        events
        |> List.rev  // Process events in chronological order
        |> List.fold (fun state event ->
            match event with
            | ConfigCreated config -> Map.add config.Name config state
            | ConfigUpdated (_, newConfig) -> Map.add newConfig.Name newConfig state
            | ConfigDeleted (name, _) -> Map.remove name state
            | ConfigRestored config -> Map.add config.Name config state
        ) Map.empty
    
    // Pure function to get configuration at specific timestamp
    let getConfigurationAtTime (events: ConfigEvent list) (timestamp: DateTimeOffset) : Map<string, DisplayConfig> =
        events
        |> List.filter (fun event ->
            match event with
            | ConfigCreated config -> config.Timestamp <= timestamp
            | ConfigUpdated (_, config) -> config.Timestamp <= timestamp
            | ConfigDeleted (_, ts) -> ts <= timestamp
            | ConfigRestored config -> config.Timestamp <= timestamp)
        |> replayEvents
    
    // Functional approach to event validation
    let validateEvent (event: ConfigEvent) : Result<ConfigEvent, string> =
        match event with
        | ConfigCreated config ->
            ConfigurationLogic.validateConfig config
            |> Result.map (fun _ -> event)
        | ConfigUpdated (oldConfig, newConfig) ->
            match ConfigurationLogic.validateConfig newConfig with
            | Ok _ when oldConfig.Name = newConfig.Name -> Ok event
            | Ok _ -> Error "Configuration name cannot be changed during update"
            | Error msg -> Error msg
        | ConfigDeleted (name, _) when not (String.IsNullOrWhiteSpace(name)) -> Ok event
        | ConfigDeleted _ -> Error "Configuration name cannot be empty for deletion"
        | ConfigRestored config ->
            ConfigurationLogic.validateConfig config
            |> Result.map (fun _ -> event)
```

### Testing Pure Functions
**Location**: `Tests/ConfigurationTests.fs`

```fsharp
module ConfigurationTests =
    
    [<Test>]
    let ``createConfig creates valid configuration with timestamp`` () =
        // Arrange
        let name = "TestConfig"
        let displays = [
            { DeviceName = "Display1"
              FriendlyName = "Monitor 1"
              IsActive = true
              Position = (0, 0)
              Resolution = (1920u, 1080u)
              RefreshRate = 60u
              TargetId = 1u
              SourceId = 1u }
        ]
        
        // Act
        let result = ConfigurationLogic.createConfig name displays
        
        // Assert
        Assert.AreEqual(name, result.Name)
        Assert.AreEqual(displays, result.Displays)
        Assert.AreEqual("2.0", result.Version)
        Assert.True(result.Timestamp > DateTimeOffset.MinValue)
    
    [<Test>]
    let ``validateConfig returns error for empty name`` () =
        // Arrange
        let config = { 
            Name = ""
            Displays = []
            Timestamp = DateTimeOffset.Now
            Version = "2.0"
        }
        
        // Act
        let result = ConfigurationLogic.validateConfig config
        
        // Assert
        match result with
        | Error msg -> Assert.AreEqual("Configuration name cannot be empty", msg)
        | Ok _ -> Assert.Fail("Expected validation error")
    
    [<Test>]
    let ``replayEvents correctly builds configuration state`` () =
        // Arrange
        let config1 = ConfigurationLogic.createConfig "Config1" []
        let config2 = ConfigurationLogic.createConfig "Config2" []
        let events = [
            ConfigCreated config1
            ConfigCreated config2
            ConfigDeleted ("Config1", DateTimeOffset.Now)
        ]
        
        // Act
        let result = EventSourcing.replayEvents events
        
        // Assert
        Assert.AreEqual(1, Map.count result)
        Assert.True(Map.containsKey "Config2" result)
        Assert.False(Map.containsKey "Config1" result)
```

**Testing Benefits**:
- **Pure Functions**: Easy to test with no side effects
- **Deterministic**: Same inputs always produce same outputs
- **Isolated**: Each function can be tested independently
- **Property-Based**: Can use property-based testing frameworks

## ECS Architecture Integration

### Configuration Components
**Location**: `Core/Components.fs`

```fsharp
module Components =
    
    // Configuration component for entities
    type ConfigComponent = {
        Config: DisplayConfig
        LastApplied: DateTimeOffset option
        IsDirty: bool
    }
    
    // Display entity with configuration
    type DisplayEntity = {
        Id: EntityId
        Name: string
        Config: ConfigComponent option
        IsActive: bool
    }

// Configuration system in ECS architecture
module ConfigurationECSSystem =
    
    // Pure function to apply configuration to entities
    let applyConfigToEntities (config: DisplayConfig) (entities: DisplayEntity list) : DisplayEntity list =
        entities
        |> List.map (fun entity ->
            let matchingDisplay = 
                config.Displays 
                |> List.tryFind (fun d -> d.DeviceName = entity.Name)
            
            match matchingDisplay with
            | Some display ->
                let configComponent = {
                    Config = config
                    LastApplied = Some DateTimeOffset.Now
                    IsDirty = false
                }
                { entity with 
                    Config = Some configComponent
                    IsActive = display.IsActive }
            | None -> entity)
    
    // Pure function for TV detection using pattern matching
    let detectDisplayType (display: DisplayInfo) : DisplayType =
        let friendlyName = display.FriendlyName.ToLower()
        match friendlyName with
        | name when name.Contains("tv") -> TVDisplay
        | name when name.Contains("hdmi") -> TVDisplay
        | name when name.Contains("samsung") -> TVDisplay
        | name when name.Contains("lg") -> TVDisplay
        | _ -> MonitorDisplay
    
    // Pure function to determine display mode
    let determineDisplayMode (displays: DisplayInfo list) : DisplayMode =
        let activeDisplays = displays |> List.filter (fun d -> d.IsActive)
        let displayTypes = activeDisplays |> List.map detectDisplayType
        
        match displayTypes with
        | [TVDisplay] -> TVMode
        | types when List.contains TVDisplay types && List.length types > 1 -> ExtendedMode
        | _ -> PCMode
```

**ECS Benefits**:
- **Entity Separation**: Configuration data separated from display entities
- **Pure Transformations**: Functions transform entity collections without side effects
- **Composable Systems**: Configuration system can be combined with other ECS systems

## Functional Configuration Format

### Immutable Configuration Schema
```json
{
  "Name": "Work_Setup",
  "Displays": [
    {
      "DeviceName": "\\\\.\\DISPLAY1",
      "FriendlyName": "DELL U2415",
      "IsActive": true,
      "Position": [0, 0],
      "Resolution": [1920, 1200],
      "RefreshRate": 60,
      "TargetId": 1,
      "SourceId": 1
    },
    {
      "DeviceName": "\\\\.\\DISPLAY2",
      "FriendlyName": "Samsung TV",
      "IsActive": false,
      "Position": [1920, 0],
      "Resolution": [1920, 1080],
      "RefreshRate": 60,
      "TargetId": 2,
      "SourceId": 2
    }
  ],
  "Timestamp": "2024-01-15T10:30:00.000Z",
  "Version": "2.0"
}
```

### Event Store Format
```json
{
  "events": [
    {
      "type": "ConfigCreated",
      "timestamp": "2024-01-15T10:30:00.000Z",
      "data": { /* DisplayConfig object */ }
    },
    {
      "type": "ConfigUpdated", 
      "timestamp": "2024-01-15T11:00:00.000Z",
      "data": {
        "oldConfig": { /* Previous DisplayConfig */ },
        "newConfig": { /* Updated DisplayConfig */ }
      }
    }
  ],
  "snapshots": {
    "Work_Setup": { /* Latest DisplayConfig snapshot */ }
  }
}
```

## Summary: Functional Programming Benefits

### Reliability Through Immutability
- **No Accidental Mutations**: Configuration data cannot be accidentally modified
- **Thread Safety**: Immutable data structures are inherently thread-safe
- **Predictable State**: System state changes only through well-defined events

### Testability Through Pure Functions
- **Deterministic Testing**: Pure functions always produce the same output for the same input
- **Easy Mocking**: Effect handlers can be easily mocked for testing
- **Property-Based Testing**: Can verify properties across large input spaces

### Maintainability Through Composition
- **Function Composition**: Complex operations built from simple, composable functions
- **Separation of Concerns**: Pure logic separated from side effects
- **Type Safety**: F# type system catches errors at compile time

### Auditability Through Event Sourcing
- **Complete History**: Every configuration change is tracked as an event
- **Time Travel**: Can reconstruct system state at any point in time
- **Compliance**: Full audit trail for regulatory requirements

### Integration with ECS Architecture
- **Component-Based**: Configuration data stored as components on entities
- **System Isolation**: Configuration system operates independently
- **Scalable Design**: Can handle complex multi-display scenarios efficiently

The functional approach to configuration management makes DisplaySwitch-Pro more reliable, testable, and maintainable while providing powerful features like event sourcing and time-travel debugging.