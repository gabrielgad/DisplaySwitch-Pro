namespace DisplaySwitchPro

open System
open System.Threading
open System.Collections.Concurrent
open ApplicationState
open ApplicationStateManager
open ApplicationLifecycle

/// Cross-Domain Event Coordination System
/// Provides unified event-driven communication between all application domains
module DomainCoordination =

    // ===== Domain Event Types =====

    /// Unified domain events for cross-domain communication
    type DomainEvent =
        | DisplayDomainEvent of DisplayEvent
        | UIEvent of UIEvent
        | PresetEvent of PresetEvent
        | ConfigurationEvent of ConfigurationEvent
        | CanvasEvent of CanvasEvent
        | WindowsAPIEvent of WindowsAPIEvent
        | CacheEvent of CacheEvent
        | LifecycleEvent of LifecycleEvent

    /// Display domain events
    and DisplayEvent =
        | DisplaysDetected of DisplayInfo list * detectionTime: TimeSpan
        | DisplayToggled of DisplayId * enabled: bool * strategy: string
        | DisplayConfigurationApplied of DisplayConfiguration * strategy: string
        | DisplayConfigurationFailed of DisplayConfiguration * error: string
        | DisplayCapabilitiesChanged of DisplayId * capabilities: DisplayCapabilities
        | DisplayConnectionStatusChanged of DisplayId * isConnected: bool

    /// UI domain events
    and UIEvent =
        | DisplayListUpdated of DisplayInfo list
        | DisplaySelectionChanged of DisplayId list
        | ThemeChanged of Theme.Theme
        | WindowResized of width: float * height: float
        | WindowMoved of x: float * y: float
        | UIStateRefreshRequested
        | DialogOpened of dialogType: string
        | DialogClosed of dialogType: string

    /// Preset management events
    and PresetEvent =
        | PresetCreated of name: string * config: DisplayConfiguration
        | PresetUpdated of name: string * oldConfig: DisplayConfiguration * newConfig: DisplayConfiguration
        | PresetDeleted of name: string
        | PresetApplied of name: string * config: DisplayConfiguration
        | PresetApplicationFailed of name: string * error: string
        | PresetListChanged of presetNames: string list

    /// Configuration events
    and ConfigurationEvent =
        | UserPreferencesChanged of UserPreferences
        | ApplicationConfigurationChanged of ApplicationConfiguration
        | ConfigurationValidationFailed of errors: string list
        | ConfigurationReloaded of source: ConfigurationSource
        | HotkeyBindingChanged of action: string * oldBinding: string option * newBinding: string

    /// Canvas domain events
    and CanvasEvent =
        | DisplayDragStarted of DisplayId * startPosition: float * float
        | DisplayDragged of DisplayId * currentPosition: float * float
        | DisplayDragCompleted of DisplayId * finalPosition: float * float
        | DisplayArrangementChanged of DisplayId list * newPositions: (DisplayId * float * float) list
        | CanvasZoomChanged of zoomLevel: float
        | CanvasPanChanged of offset: float * float
        | SnapModeToggled of enabled: bool

    /// Windows API domain events
    and WindowsAPIEvent =
        | StrategyExecuted of strategy: string * displayId: DisplayId * success: bool * duration: TimeSpan
        | StrategyRecommendationChanged of displayId: DisplayId * recommendedStrategy: string
        | APICallCompleted of operation: string * success: bool * duration: TimeSpan
        | HardwareCompatibilityUpdated of deviceId: string * isCompatible: bool
        | DisplayDetectionMethodChanged of method: string

    /// Cache domain events
    and CacheEvent =
        | CacheEntryAdded of key: string * entryType: CacheEntryType
        | CacheEntryEvicted of key: string * reason: EvictionReason
        | CacheHitRateChanged of newRate: float
        | CacheCleanupCompleted of entriesRemoved: int
        | CacheStatisticsUpdated of statistics: CacheStatistics

    /// Application lifecycle events
    and LifecycleEvent =
        | ApplicationStarted of version: string * startTime: DateTime
        | ApplicationStopping of reason: string
        | ServiceInitialized of serviceName: string
        | ServiceFailed of serviceName: string * error: string
        | HealthCheckCompleted of overallHealth: HealthStatus
        | PerformanceAlert of metric: string * value: float * threshold: float

    // ===== Supporting Types =====

    and DisplayCapabilities = {
        SupportedResolutions: (int * int * int) list  // width, height, refresh rate
        SupportedColorDepths: int list
        SupportsHDR: bool
        MaxBrightness: int option
    }

    and ConfigurationSource =
        | UserFile
        | SystemDefaults
        | CommandLine
        | HotReload

    and CacheEntryType =
        | DisplayState
        | PresetConfiguration
        | UIState
        | PerformanceMetrics

    and EvictionReason =
        | Expired
        | MemoryPressure
        | ManualCleanup
        | Replaced

    /// Event metadata for tracking and debugging
    type EventMetadata = {
        EventId: Guid
        Timestamp: DateTime
        SourceDomain: string
        CorrelationId: Guid option
        Priority: EventPriority
        Tags: Set<string>
    }

    and EventPriority =
        | Critical
        | High
        | Normal
        | Low

    /// Enriched domain event with metadata
    type EnrichedDomainEvent = {
        Event: DomainEvent
        Metadata: EventMetadata
    }

    // ===== Event Bus Implementation =====

    /// Advanced event bus with filtering, prioritization, and retry
    type DomainEventBus private () =
        let subscribers = ConcurrentDictionary<Guid, EventSubscription>()
        let eventHistory = ConcurrentQueue<EnrichedDomainEvent>()
        let maxHistorySize = 1000
        let lockObj = obj()

        /// Event subscription with filtering
        member this.Subscribe(filter: DomainEvent -> bool) (handler: EnrichedDomainEvent -> unit) : IDisposable =
            let subscription = {
                Id = Guid.NewGuid()
                Filter = filter
                Handler = handler
                CreatedAt = DateTime.Now
                Priority = Normal
                IsActive = true
            }

            subscribers.[subscription.Id] <- subscription

            { new IDisposable with
                member _.Dispose() =
                    subscribers.TryRemove(subscription.Id) |> ignore }

        /// Subscribe to specific domain events
        member this.SubscribeToDisplayEvents(handler: DisplayEvent -> unit) =
            this.Subscribe
                (function | DisplayDomainEvent _ -> true | _ -> false)
                (fun enrichedEvent -> match enrichedEvent.Event with
                                    | DisplayDomainEvent displayEvent -> handler displayEvent
                                    | _ -> ())

        member this.SubscribeToUIEvents(handler: UIEvent -> unit) =
            this.Subscribe
                (function | UIEvent _ -> true | _ -> false)
                (fun enrichedEvent -> match enrichedEvent.Event with
                                    | UIEvent uiEvent -> handler uiEvent
                                    | _ -> ())

        member this.SubscribeToPresetEvents(handler: PresetEvent -> unit) =
            this.Subscribe
                (function | PresetEvent _ -> true | _ -> false)
                (fun enrichedEvent -> match enrichedEvent.Event with
                                    | PresetEvent presetEvent -> handler presetEvent
                                    | _ -> ())

        member this.SubscribeToConfigurationEvents(handler: ConfigurationEvent -> unit) =
            this.Subscribe
                (function | ConfigurationEvent _ -> true | _ -> false)
                (fun enrichedEvent -> match enrichedEvent.Event with
                                    | ConfigurationEvent configEvent -> handler configEvent
                                    | _ -> ())

        /// Publish domain event with automatic metadata enrichment
        member this.Publish(event: DomainEvent, ?priority: EventPriority, ?correlationId: Guid, ?tags: Set<string>) =
            let enrichedEvent = {
                Event = event
                Metadata = {
                    EventId = Guid.NewGuid()
                    Timestamp = DateTime.Now
                    SourceDomain = this.GetSourceDomain(event)
                    CorrelationId = correlationId
                    Priority = defaultArg priority Normal
                    Tags = defaultArg tags Set.empty
                }
            }

            // Add to history
            eventHistory.Enqueue(enrichedEvent)
            if eventHistory.Count > maxHistorySize then
                let mutable discarded = Unchecked.defaultof<EnrichedDomainEvent>
                eventHistory.TryDequeue(&discarded) |> ignore

            // Notify subscribers
            this.NotifySubscribers(enrichedEvent)

        /// Get source domain from event type
        member private this.GetSourceDomain(event: DomainEvent) =
            match event with
            | DisplayDomainEvent _ -> "Display"
            | UIEvent _ -> "UI"
            | PresetEvent _ -> "Preset"
            | ConfigurationEvent _ -> "Configuration"
            | CanvasEvent _ -> "Canvas"
            | WindowsAPIEvent _ -> "WindowsAPI"
            | CacheEvent _ -> "Cache"
            | LifecycleEvent _ -> "Lifecycle"

        /// Notify all relevant subscribers
        member private this.NotifySubscribers(enrichedEvent: EnrichedDomainEvent) =
            let relevantSubscribers =
                subscribers.Values
                |> Seq.filter (fun sub -> sub.IsActive && sub.Filter enrichedEvent.Event)
                |> Seq.sortBy (fun sub -> sub.Priority)  // Process by priority
                |> Seq.toArray

            for subscriber in relevantSubscribers do
                try
                    subscriber.Handler enrichedEvent
                with ex ->
                    Logging.logErrorf "Error in domain event subscriber %A: %s" subscriber.Id ex.Message

        /// Get event history for debugging
        member this.GetEventHistory() = eventHistory.ToArray() |> Array.toList

        /// Get subscriber statistics
        member this.GetStatistics() =
            let activeSubscribers = subscribers.Values |> Seq.filter (_.IsActive) |> Seq.length
            let totalEvents = eventHistory.Count
            {|
                ActiveSubscribers = activeSubscribers
                TotalSubscribers = subscribers.Count
                EventHistorySize = totalEvents
                LastEventTime = if totalEvents > 0 then Some (Seq.last eventHistory).Metadata.Timestamp else None
            |}

        /// Clear all subscribers and history
        member this.Clear() =
            subscribers.Clear()
            eventHistory.Clear()

    and EventSubscription = {
        Id: Guid
        Filter: DomainEvent -> bool
        Handler: EnrichedDomainEvent -> unit
        CreatedAt: DateTime
        Priority: EventPriority
        IsActive: bool
    }

    // ===== Global Event Coordination =====

    /// Global domain event coordinator
    module GlobalCoordinator =

        let private eventBus = DomainEventBus()
        let mutable stateManager: IStateManager option = None

        /// Initialize the global coordinator with state manager
        let initialize (stateManagerInstance: IStateManager) =
            stateManager <- Some stateManagerInstance
            Logging.logInfo "Global domain coordinator initialized"

        /// Publish domain event
        let publishEvent (event: DomainEvent) =
            eventBus.Publish(event)

        /// Publish event with metadata
        let publishEventWithMetadata (event: DomainEvent) (priority: EventPriority) (correlationId: Guid option) (tags: Set<string>) =
            eventBus.Publish(event, priority, ?correlationId = correlationId, tags = tags)

        /// Subscribe to domain events
        let subscribe (filter: DomainEvent -> bool) (handler: EnrichedDomainEvent -> unit) : IDisposable =
            eventBus.Subscribe filter handler

        /// Get event statistics
        let getStatistics() = eventBus.GetStatistics()

        /// Get event history
        let getEventHistory() = eventBus.GetEventHistory()

    // ===== Cross-Domain Event Handlers =====

    /// Predefined cross-domain event handlers
    module EventHandlers =

        /// Handle display domain events and update state
        let handleDisplayEvents (stateManager: IStateManager) (event: DisplayEvent) = async {
            match event with
            | DisplaysDetected (displays, detectionTime) ->
                let stateEvent = ApplicationState.DisplaysUpdated (displays, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

                // Notify UI to update display list
                GlobalCoordinator.publishEvent (UIEvent (DisplayListUpdated displays))

                Logging.logInfof "Displays detected: %d displays in %A" displays.Length detectionTime

            | DisplayToggled (displayId, enabled, strategy) ->
                Logging.logInfof "Display %s %s using strategy %s" displayId (if enabled then "enabled" else "disabled") strategy

                // Could trigger cache updates, UI refreshes, etc.
                GlobalCoordinator.publishEvent (CacheEvent (CacheEntryAdded (displayId, DisplayState)))

            | DisplayConfigurationApplied (config, strategy) ->
                let stateEvent = ApplicationState.ConfigurationApplied (config, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

                GlobalCoordinator.publishEvent (UIEvent UIStateRefreshRequested)
                Logging.logInfof "Configuration applied successfully using strategy %s" strategy

            | DisplayConfigurationFailed (config, error) ->
                Logging.logErrorf "Configuration application failed: %s" error
                // Could trigger retry logic or fallback strategies

            | DisplayConnectionStatusChanged (displayId, isConnected) ->
                if not isConnected then
                    // Remove from cache when disconnected
                    GlobalCoordinator.publishEvent (CacheEvent (CacheEntryEvicted (displayId, ManualCleanup)))

            | _ -> ()
        }

        /// Handle UI events and coordinate with other domains
        let handleUIEvents (stateManager: IStateManager) (event: UIEvent) = async {
            match event with
            | DisplaySelectionChanged displayIds ->
                let stateEvent = ApplicationState.DisplaySelectionChanged (displayIds, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

            | ThemeChanged theme ->
                let stateEvent = ApplicationState.UIThemeChanged (theme, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

                // Notify configuration system of theme change
                GlobalCoordinator.publishEvent (ConfigurationEvent (UserPreferencesChanged {
                    ApplicationConfiguration.defaultUserPreferences with
                        UIPreferences = { ApplicationConfiguration.defaultUserPreferences.UIPreferences with Theme = theme }
                }))

            | WindowResized (width, height) ->
                let windowState = { Width = width; Height = height; X = 0.0; Y = 0.0; IsMaximized = false; IsMinimized = false }
                let stateEvent = ApplicationState.WindowStateChanged (windowState, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

            | DialogOpened dialogType ->
                Logging.logVerbosef "Dialog opened: %s" dialogType

            | DialogClosed dialogType ->
                Logging.logVerbosef "Dialog closed: %s" dialogType

            | _ -> ()
        }

        /// Handle preset events and coordinate with cache and UI
        let handlePresetEvents (stateManager: IStateManager) (event: PresetEvent) = async {
            match event with
            | PresetCreated (name, config) ->
                let stateEvent = ApplicationState.PresetSaved (name, config, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

                // Add to cache for quick access
                GlobalCoordinator.publishEvent (CacheEvent (CacheEntryAdded (name, PresetConfiguration)))

                Logging.logInfof "Preset '%s' created successfully" name

            | PresetApplied (name, config) ->
                GlobalCoordinator.publishEvent (DisplayDomainEvent (DisplayConfigurationApplied (config, "preset-application")))
                Logging.logInfof "Preset '%s' applied" name

            | PresetDeleted name ->
                let stateEvent = ApplicationState.PresetDeleted (name, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

                // Remove from cache
                GlobalCoordinator.publishEvent (CacheEvent (CacheEntryEvicted (name, ManualCleanup)))

            | _ -> ()
        }

        /// Handle configuration events and update system settings
        let handleConfigurationEvents (stateManager: IStateManager) (event: ConfigurationEvent) = async {
            match event with
            | UserPreferencesChanged preferences ->
                // Update logging level
                Logging.setLogLevel preferences.AdvancedSettings.LogLevel

                // Notify UI of theme changes
                if stateManager.GetState().UI.Theme <> preferences.UIPreferences.Theme then
                    GlobalCoordinator.publishEvent (UIEvent (ThemeChanged preferences.UIPreferences.Theme))

                Logging.logInfo "User preferences updated and applied"

            | ApplicationConfigurationChanged config ->
                let stateEvent = ApplicationState.ConfigurationReloaded DateTime.Now
                stateManager.UpdateState(stateEvent) |> ignore

            | ConfigurationValidationFailed errors ->
                Logging.logErrorf "Configuration validation failed: %A" errors

            | ConfigurationReloaded source ->
                Logging.logInfof "Configuration reloaded from %A" source

            | _ -> ()
        }

        /// Handle canvas events and update display arrangements
        let handleCanvasEvents (stateManager: IStateManager) (event: CanvasEvent) = async {
            match event with
            | DisplayDragStarted (displayId, x, y) ->
                let stateEvent = ApplicationState.DragOperationStarted (displayId, x, y, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

            | DisplayDragCompleted (displayId, x, y) ->
                let stateEvent = ApplicationState.DragOperationCompleted (displayId, x, y, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

                // Notify UI to update display positions
                GlobalCoordinator.publishEvent (UIEvent UIStateRefreshRequested)

            | CanvasZoomChanged zoomLevel ->
                let transformParams = { ZoomLevel = zoomLevel; PanOffset = (0.0, 0.0); Rotation = 0.0; LastTransformUpdate = DateTime.Now }
                let stateEvent = ApplicationState.CanvasTransformUpdated (transformParams, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

            | _ -> ()
        }

        /// Handle Windows API events and update performance metrics
        let handleWindowsAPIEvents (stateManager: IStateManager) (event: WindowsAPIEvent) = async {
            match event with
            | StrategyExecuted (strategy, displayId, success, duration) ->
                let metrics = { SuccessCount = if success then 1 else 0; FailureCount = if not success then 1 else 0; AverageExecutionTime = duration; LastUsed = DateTime.Now; ReliabilityScore = if success then 1.0 else 0.0 }
                let stateEvent = ApplicationState.StrategyPerformanceUpdated (strategy, metrics, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

            | APICallCompleted (operation, success, duration) ->
                let stateEvent = ApplicationState.ApiCallCompleted (operation, success, duration, DateTime.Now)
                stateManager.UpdateState(stateEvent) |> ignore

            | HardwareCompatibilityUpdated (deviceId, isCompatible) ->
                Logging.logInfof "Hardware compatibility updated for %s: %b" deviceId isCompatible

            | _ -> ()
        }

        /// Handle cache events and maintain cache health
        let handleCacheEvents (stateManager: IStateManager) (event: CacheEvent) = async {
            match event with
            | CacheEntryAdded (key, entryType) ->
                Logging.logVerbosef "Cache entry added: %s (%A)" key entryType

            | CacheEntryEvicted (key, reason) ->
                Logging.logVerbosef "Cache entry evicted: %s (%A)" key reason

            | CacheCleanupCompleted entriesRemoved ->
                let stateEvent = ApplicationState.CacheCleanupPerformed DateTime.Now
                stateManager.UpdateState(stateEvent) |> ignore
                Logging.logInfof "Cache cleanup completed, %d entries removed" entriesRemoved

            | _ -> ()
        }

        /// Handle lifecycle events and coordinate system responses
        let handleLifecycleEvents (stateManager: IStateManager) (event: LifecycleEvent) = async {
            match event with
            | ApplicationStarted (version, startTime) ->
                Logging.logInfof "Application started: version %s at %A" version startTime

            | ServiceInitialized serviceName ->
                Logging.logInfof "Service initialized: %s" serviceName

            | ServiceFailed (serviceName, error) ->
                Logging.logErrorf "Service failed: %s - %s" serviceName error

            | HealthCheckCompleted overallHealth ->
                Logging.logInfof "Health check completed: %A" overallHealth

            | PerformanceAlert (metric, value, threshold) ->
                Logging.logWarningf "Performance alert: %s = %.2f (threshold: %.2f)" metric value threshold

            | _ -> ()
        }

    // ===== Event Coordination Setup =====

    /// Initialize complete cross-domain event coordination
    module Setup =

        /// Initialize all cross-domain event handlers
        let initializeEventCoordination (stateManager: IStateManager) : IDisposable list =
            GlobalCoordinator.initialize stateManager

            let disposables = [
                // Display domain event handling
                GlobalCoordinator.eventBus.SubscribeToDisplayEvents(fun event ->
                    EventHandlers.handleDisplayEvents stateManager event |> Async.Start)

                // UI domain event handling
                GlobalCoordinator.eventBus.SubscribeToUIEvents(fun event ->
                    EventHandlers.handleUIEvents stateManager event |> Async.Start)

                // Preset domain event handling
                GlobalCoordinator.eventBus.SubscribeToPresetEvents(fun event ->
                    EventHandlers.handlePresetEvents stateManager event |> Async.Start)

                // Configuration domain event handling
                GlobalCoordinator.eventBus.SubscribeToConfigurationEvents(fun event ->
                    EventHandlers.handleConfigurationEvents stateManager event |> Async.Start)

                // Canvas domain event handling
                GlobalCoordinator.subscribe
                    (function | CanvasEvent _ -> true | _ -> false)
                    (fun enrichedEvent ->
                        match enrichedEvent.Event with
                        | CanvasEvent canvasEvent -> EventHandlers.handleCanvasEvents stateManager canvasEvent |> Async.Start
                        | _ -> ())

                // Windows API domain event handling
                GlobalCoordinator.subscribe
                    (function | WindowsAPIEvent _ -> true | _ -> false)
                    (fun enrichedEvent ->
                        match enrichedEvent.Event with
                        | WindowsAPIEvent apiEvent -> EventHandlers.handleWindowsAPIEvents stateManager apiEvent |> Async.Start
                        | _ -> ())

                // Cache domain event handling
                GlobalCoordinator.subscribe
                    (function | CacheEvent _ -> true | _ -> false)
                    (fun enrichedEvent ->
                        match enrichedEvent.Event with
                        | CacheEvent cacheEvent -> EventHandlers.handleCacheEvents stateManager cacheEvent |> Async.Start
                        | _ -> ())

                // Lifecycle domain event handling
                GlobalCoordinator.subscribe
                    (function | LifecycleEvent _ -> true | _ -> false)
                    (fun enrichedEvent ->
                        match enrichedEvent.Event with
                        | LifecycleEvent lifecycleEvent -> EventHandlers.handleLifecycleEvents stateManager lifecycleEvent |> Async.Start
                        | _ -> ())
            ]

            Logging.logInfo "✅ Cross-domain event coordination initialized with all handlers"
            disposables

        /// Shutdown event coordination
        let shutdownEventCoordination (disposables: IDisposable list) =
            disposables |> List.iter (fun d -> d.Dispose())
            GlobalCoordinator.eventBus.Clear()
            Logging.logInfo "Cross-domain event coordination shutdown completed"

    // ===== Public API =====

    /// Public API for domain event publishing
    let publishDisplayEvent (event: DisplayEvent) = GlobalCoordinator.publishEvent (DisplayDomainEvent event)
    let publishUIEvent (event: UIEvent) = GlobalCoordinator.publishEvent (UIEvent event)
    let publishPresetEvent (event: PresetEvent) = GlobalCoordinator.publishEvent (PresetEvent event)
    let publishConfigurationEvent (event: ConfigurationEvent) = GlobalCoordinator.publishEvent (ConfigurationEvent event)
    let publishCanvasEvent (event: CanvasEvent) = GlobalCoordinator.publishEvent (CanvasEvent event)
    let publishWindowsAPIEvent (event: WindowsAPIEvent) = GlobalCoordinator.publishEvent (WindowsAPIEvent event)
    let publishCacheEvent (event: CacheEvent) = GlobalCoordinator.publishEvent (CacheEvent event)
    let publishLifecycleEvent (event: LifecycleEvent) = GlobalCoordinator.publishEvent (LifecycleEvent event)

    /// Get coordination statistics
    let getCoordinationStatistics() = GlobalCoordinator.getStatistics()

    /// Get event history for debugging
    let getEventHistory() = GlobalCoordinator.getEventHistory()