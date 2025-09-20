namespace DisplaySwitchPro

open System
open System.Threading
open System.Collections.Generic

/// Unified application state containing all domain-specific states
/// This module implements the final phase of functional programming transformation:
/// Phase 5 - Application State: Lifecycle & Configuration Management
module ApplicationState =

    // ===== Core Types =====

    /// Unified application state container integrating all domains
    type ApplicationState = {
        Core: CoreApplicationState
        UI: UIApplicationState
        Cache: CacheApplicationState
        Canvas: CanvasApplicationState
        WindowsAPI: WindowsAPIApplicationState
        Configuration: ApplicationConfiguration
        Metadata: StateMetadata
    }

    /// Core domain state from AppState.fs
    and CoreApplicationState = {
        ConnectedDisplays: Map<DisplayId, DisplayInfo>
        SavedPresets: Map<string, DisplayConfiguration>
        CurrentConfiguration: DisplayConfiguration option
        LastSuccessfulConfiguration: DisplayConfiguration option
        LastUpdate: DateTime
    }

    /// UI domain state integration
    and UIApplicationState = {
        Theme: Theme.Theme
        WindowState: WindowState option
        SelectedDisplays: Set<DisplayId>
        MainWindow: obj option  // Avalonia Window
        DisplaySettingsDialog: obj option  // Avalonia Window
        CurrentDialogDisplay: DisplayInfo option
        LastUIUpdate: DateTime
    }

    /// Cache domain state integration
    and CacheApplicationState = {
        DisplayStates: Map<DisplayId, DisplayStateEntry>
        PresetCache: Map<string, CachedConfiguration>
        LastCacheCleanup: DateTime
        CacheHitRate: float
        TotalCacheOperations: int64
    }

    /// Canvas domain state integration
    and CanvasApplicationState = {
        TransformParams: CanvasTransformParams option
        DragOperations: DragState option
        SnapSettings: SnapSettings
        ViewportBounds: Rectangle option
        DisplayVisuals: Map<DisplayId, DisplayVisual>
        InteractionMode: InteractionMode
        LastCanvasUpdate: DateTime
    }

    /// Windows API domain state integration
    and WindowsAPIApplicationState = {
        StrategyPerformance: Map<string, StrategyMetrics>
        LastDisplayDetection: DateTime
        ApiCallStatistics: ApiCallStats
        HardwareCompatibility: Map<string, HardwareStatus>
        LastSuccessfulStrategy: string option
    }

    /// Comprehensive application configuration
    and ApplicationConfiguration = {
        // Core settings
        AutoSavePresets: bool
        AutoDetectDisplays: bool
        StartupPreset: string option

        // UI preferences
        Theme: Theme.Theme
        MinimizeToTray: bool
        RememberWindowState: bool
        WindowSize: float * float
        WindowPosition: (float * float) option

        // System behavior
        LogLevel: LogLevel
        RefreshInterval: TimeSpan option
        HotkeyBindings: Map<string, KeyBinding>

        // Performance settings
        CacheSettings: CacheSettings
        DisplayDetectionTimeout: TimeSpan
        MaxRetryAttempts: int

        // Advanced settings
        EnableDebugMode: bool
        EnableHardwareLogging: bool
        ConfigurationVersion: string
    }

    /// State metadata for tracking and debugging
    and StateMetadata = {
        Version: string
        CreatedAt: DateTime
        LastModified: DateTime
        StateChangeCount: int64
        SessionId: Guid
        EventLog: StateEvent list  // Last 100 events
        PerformanceMetrics: StatePerformanceMetrics
    }

    // ===== Supporting Types =====

    and DisplayStateEntry = {
        DisplayId: DisplayId
        LastUpdated: DateTime
        State: DisplayState
        AccessCount: int
        IsCached: bool
    }

    and CachedConfiguration = {
        Configuration: DisplayConfiguration
        CachedAt: DateTime
        AccessCount: int
        IsValid: bool
    }

    and WindowState = {
        Width: float
        Height: float
        X: float
        Y: float
        IsMaximized: bool
        IsMinimized: bool
    }

    and CanvasTransformParams = {
        ZoomLevel: float
        PanOffset: float * float
        Rotation: float
        LastTransformUpdate: DateTime
    }

    and DragState = {
        TargetDisplayId: DisplayId
        StartPosition: float * float
        CurrentPosition: float * float
        IsDragging: bool
        StartTime: DateTime
    }

    and SnapSettings = {
        EnableSnapping: bool
        SnapThreshold: float
        SnapToGrid: bool
        GridSize: float
    }

    and Rectangle = {
        X: float
        Y: float
        Width: float
        Height: float
    }

    and DisplayVisual = {
        DisplayId: DisplayId
        Position: float * float
        Size: float * float
        IsSelected: bool
        IsHighlighted: bool
        LastUpdate: DateTime
    }

    and InteractionMode =
        | Selection
        | Dragging
        | Resizing
        | Viewing

    and StrategyMetrics = {
        SuccessCount: int
        FailureCount: int
        AverageExecutionTime: TimeSpan
        LastUsed: DateTime
        ReliabilityScore: float
    }

    and ApiCallStats = {
        TotalCalls: int64
        SuccessfulCalls: int64
        FailedCalls: int64
        AverageResponseTime: TimeSpan
        LastReset: DateTime
    }

    and HardwareStatus = {
        DeviceId: string
        IsCompatible: bool
        LastTested: DateTime
        CompatibilityNotes: string list
    }

    and KeyBinding = {
        KeyCombination: string
        Action: string
        Description: string
        IsEnabled: bool
    }

    and CacheSettings = {
        MaxCacheSize: int
        CacheExpirationTime: TimeSpan
        EnableWriteThrough: bool
        EnableReadAhead: bool
    }

    and StatePerformanceMetrics = {
        StateUpdateLatency: TimeSpan
        EventProcessingThroughput: float
        MemoryUsageBytes: int64
        ConcurrentOperationSuccess: float
        LastMeasurement: DateTime
    }

    // ===== State Events for Event Sourcing =====

    /// Events representing state changes for event sourcing
    type StateEvent =
        // Core domain events
        | DisplaysUpdated of DisplayInfo list * timestamp: DateTime
        | PresetSaved of name: string * config: DisplayConfiguration * timestamp: DateTime
        | PresetLoaded of name: string * timestamp: DateTime
        | PresetDeleted of name: string * timestamp: DateTime
        | ConfigurationApplied of config: DisplayConfiguration * timestamp: DateTime

        // UI domain events
        | UIThemeChanged of theme: Theme.Theme * timestamp: DateTime
        | WindowStateChanged of state: WindowState * timestamp: DateTime
        | DisplaySelectionChanged of displayIds: DisplayId list * timestamp: DateTime

        // Cache domain events
        | CacheEntryAdded of DisplayId * entry: DisplayStateEntry * timestamp: DateTime
        | CacheEntryRemoved of DisplayId * timestamp: DateTime
        | CacheCleanupPerformed of timestamp: DateTime

        // Canvas domain events
        | CanvasTransformUpdated of transform: CanvasTransformParams * timestamp: DateTime
        | DragOperationStarted of displayId: DisplayId * position: float * float * timestamp: DateTime
        | DragOperationCompleted of displayId: DisplayId * finalPosition: float * float * timestamp: DateTime

        // Windows API domain events
        | StrategyPerformanceUpdated of strategy: string * metrics: StrategyMetrics * timestamp: DateTime
        | DisplayDetectionCompleted of displays: DisplayInfo list * duration: TimeSpan * timestamp: DateTime
        | ApiCallCompleted of operation: string * success: bool * duration: TimeSpan * timestamp: DateTime

        // Configuration events
        | ConfigurationSettingChanged of setting: string * oldValue: obj * newValue: obj * timestamp: DateTime
        | ConfigurationValidated of isValid: bool * errors: string list * timestamp: DateTime
        | ConfigurationReloaded of timestamp: DateTime

    // ===== Default Values =====

    /// Default application configuration with sensible defaults
    let defaultConfiguration = {
        AutoSavePresets = true
        AutoDetectDisplays = true
        StartupPreset = None
        Theme = Theme.Theme.System
        MinimizeToTray = false
        RememberWindowState = true
        WindowSize = (1200.0, 800.0)
        WindowPosition = None
        LogLevel = LogLevel.Normal
        RefreshInterval = Some (TimeSpan.FromSeconds(30.0))
        HotkeyBindings = Map.empty
        CacheSettings = {
            MaxCacheSize = 100
            CacheExpirationTime = TimeSpan.FromMinutes(30.0)
            EnableWriteThrough = true
            EnableReadAhead = false
        }
        DisplayDetectionTimeout = TimeSpan.FromSeconds(10.0)
        MaxRetryAttempts = 3
        EnableDebugMode = false
        EnableHardwareLogging = false
        ConfigurationVersion = "1.0.0"
    }

    /// Empty application state for initialization
    let empty = {
        Core = {
            ConnectedDisplays = Map.empty
            SavedPresets = Map.empty
            CurrentConfiguration = None
            LastSuccessfulConfiguration = None
            LastUpdate = DateTime.Now
        }
        UI = {
            Theme = Theme.Theme.System
            WindowState = None
            SelectedDisplays = Set.empty
            MainWindow = None
            DisplaySettingsDialog = None
            CurrentDialogDisplay = None
            LastUIUpdate = DateTime.Now
        }
        Cache = {
            DisplayStates = Map.empty
            PresetCache = Map.empty
            LastCacheCleanup = DateTime.Now
            CacheHitRate = 0.0
            TotalCacheOperations = 0L
        }
        Canvas = {
            TransformParams = None
            DragOperations = None
            SnapSettings = {
                EnableSnapping = true
                SnapThreshold = 10.0
                SnapToGrid = false
                GridSize = 20.0
            }
            ViewportBounds = None
            DisplayVisuals = Map.empty
            InteractionMode = Viewing
            LastCanvasUpdate = DateTime.Now
        }
        WindowsAPI = {
            StrategyPerformance = Map.empty
            LastDisplayDetection = DateTime.Now
            ApiCallStatistics = {
                TotalCalls = 0L
                SuccessfulCalls = 0L
                FailedCalls = 0L
                AverageResponseTime = TimeSpan.Zero
                LastReset = DateTime.Now
            }
            HardwareCompatibility = Map.empty
            LastSuccessfulStrategy = None
        }
        Configuration = defaultConfiguration
        Metadata = {
            Version = "1.0.0"
            CreatedAt = DateTime.Now
            LastModified = DateTime.Now
            StateChangeCount = 0L
            SessionId = Guid.NewGuid()
            EventLog = []
            PerformanceMetrics = {
                StateUpdateLatency = TimeSpan.Zero
                EventProcessingThroughput = 0.0
                MemoryUsageBytes = 0L
                ConcurrentOperationSuccess = 1.0
                LastMeasurement = DateTime.Now
            }
        }
    }

    // ===== State Query Functions =====

    /// Pure query functions for accessing state
    module Queries =

        let getConnectedDisplays (state: ApplicationState) =
            state.Core.ConnectedDisplays |> Map.values |> List.ofSeq

        let getDisplayById (displayId: DisplayId) (state: ApplicationState) =
            Map.tryFind displayId state.Core.ConnectedDisplays

        let getSavedPresets (state: ApplicationState) =
            state.Core.SavedPresets |> Map.keys |> List.ofSeq

        let getPresetByName (name: string) (state: ApplicationState) =
            Map.tryFind name state.Core.SavedPresets

        let getCurrentConfiguration (state: ApplicationState) =
            state.Core.CurrentConfiguration

        let getSelectedDisplays (state: ApplicationState) =
            state.UI.SelectedDisplays |> Set.toList

        let getCurrentTheme (state: ApplicationState) =
            state.UI.Theme

        let getCanvasTransform (state: ApplicationState) =
            state.Canvas.TransformParams

        let getCacheStatistics (state: ApplicationState) =
            (state.Cache.CacheHitRate, state.Cache.TotalCacheOperations)

        let getStateMetadata (state: ApplicationState) =
            state.Metadata

        let getEventHistory (state: ApplicationState) =
            state.Metadata.EventLog

        let getPerformanceMetrics (state: ApplicationState) =
            state.Metadata.PerformanceMetrics

    // ===== State Transformation Functions =====

    /// Pure state transformation functions
    module Transforms =

        /// Update core application state
        let updateCore (coreState: CoreApplicationState) (state: ApplicationState) =
            { state with
                Core = coreState
                Metadata =
                    { state.Metadata with
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Update UI state
        let updateUI (uiState: UIApplicationState) (state: ApplicationState) =
            { state with
                UI = uiState
                Metadata =
                    { state.Metadata with
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Update cache state
        let updateCache (cacheState: CacheApplicationState) (state: ApplicationState) =
            { state with
                Cache = cacheState
                Metadata =
                    { state.Metadata with
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Update canvas state
        let updateCanvas (canvasState: CanvasApplicationState) (state: ApplicationState) =
            { state with
                Canvas = canvasState
                Metadata =
                    { state.Metadata with
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Update Windows API state
        let updateWindowsAPI (apiState: WindowsAPIApplicationState) (state: ApplicationState) =
            { state with
                WindowsAPI = apiState
                Metadata =
                    { state.Metadata with
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Update configuration
        let updateConfiguration (config: ApplicationConfiguration) (state: ApplicationState) =
            { state with
                Configuration = config
                Metadata =
                    { state.Metadata with
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Add event to history (keeping last 100 events)
        let addEvent (event: StateEvent) (state: ApplicationState) =
            let newEventLog = event :: (List.take 99 state.Metadata.EventLog)
            { state with
                Metadata =
                    { state.Metadata with
                        EventLog = newEventLog
                        LastModified = DateTime.Now
                        StateChangeCount = state.Metadata.StateChangeCount + 1L } }

    // ===== State Validation Functions =====

    /// State validation and consistency checking
    module Validation =

        type ValidationError =
            | InvalidDisplayConfiguration of string
            | InconsistentCacheState of string
            | InvalidUIState of string
            | ConfigurationError of string list
            | MetadataError of string

        let validateDisplays (state: ApplicationState) : Result<unit, ValidationError> =
            let displays = state.Core.ConnectedDisplays |> Map.values |> List.ofSeq
            if List.isEmpty displays then
                Error (InvalidDisplayConfiguration "No displays connected")
            else
                let invalidDisplays = displays |> List.filter (fun d -> String.IsNullOrEmpty d.Id)
                if not (List.isEmpty invalidDisplays) then
                    Error (InvalidDisplayConfiguration "Some displays have invalid IDs")
                else
                    Ok ()

        let validateConfiguration (config: ApplicationConfiguration) : Result<unit, ValidationError> =
            let errors = [
                if config.RefreshInterval.IsSome && config.RefreshInterval.Value < TimeSpan.FromSeconds(1.0) then
                    yield "Refresh interval must be at least 1 second"

                if config.MaxRetryAttempts < 1 || config.MaxRetryAttempts > 10 then
                    yield "Max retry attempts must be between 1 and 10"

                if config.DisplayDetectionTimeout < TimeSpan.FromSeconds(1.0) then
                    yield "Display detection timeout must be at least 1 second"

                if config.CacheSettings.MaxCacheSize < 10 || config.CacheSettings.MaxCacheSize > 1000 then
                    yield "Cache size must be between 10 and 1000"
            ]

            if List.isEmpty errors then Ok ()
            else Error (ConfigurationError errors)

        let validateState (state: ApplicationState) : Result<unit, ValidationError list> =
            let results = [
                validateDisplays state
                validateConfiguration state.Configuration
            ]

            let errors = results |> List.choose (function | Error e -> Some e | Ok _ -> None)
            if List.isEmpty errors then Ok ()
            else Error errors

    // ===== Backward Compatibility Layer =====

    /// Compatibility functions for existing AppState.fs usage
    module Compatibility =

        /// Convert unified state to legacy AppState format
        let toLegacyAppState (state: ApplicationState) : AppState =
            {
                ConnectedDisplays = state.Core.ConnectedDisplays
                CurrentConfiguration = state.Core.CurrentConfiguration
                SavedPresets = state.Core.SavedPresets
                LastUpdate = state.Core.LastUpdate
            }

        /// Update unified state from legacy AppState
        let fromLegacyAppState (legacyState: AppState) (state: ApplicationState) : ApplicationState =
            { state with
                Core = {
                    ConnectedDisplays = legacyState.ConnectedDisplays
                    SavedPresets = legacyState.SavedPresets
                    CurrentConfiguration = legacyState.CurrentConfiguration
                    LastSuccessfulConfiguration = state.Core.LastSuccessfulConfiguration
                    LastUpdate = legacyState.LastUpdate
                }
                Metadata = { state.Metadata with
                    LastModified = DateTime.Now
                    StateChangeCount = state.Metadata.StateChangeCount + 1L } }

        /// Legacy function adapters for seamless migration
        let updateDisplays displays state =
            let coreState = { state.Core with
                ConnectedDisplays = displays |> List.fold (fun acc d -> Map.add d.Id d acc) Map.empty
                LastUpdate = DateTime.Now }
            Transforms.updateCore coreState state

        let savePreset name config state =
            let namedConfig = { config with Name = name; CreatedAt = DateTime.Now }
            let coreState = { state.Core with
                SavedPresets = Map.add name namedConfig state.Core.SavedPresets
                LastUpdate = DateTime.Now }
            Transforms.updateCore coreState state

        let setCurrentConfiguration config state =
            let coreState = { state.Core with
                CurrentConfiguration = Some config
                LastSuccessfulConfiguration = Some config
                LastUpdate = DateTime.Now }
            Transforms.updateCore coreState state