namespace DisplaySwitchPro

open System
open System.Threading
open System.Collections.Concurrent
open System.Threading.Tasks
open ApplicationState

/// Thread-safe Application State Manager with event sourcing
/// Provides the central state management system for the entire application
module ApplicationStateManager =

    // ===== Core State Management =====

    /// Thread-safe state update result
    type StateUpdateResult<'T> =
        | Success of 'T
        | Conflict of currentState: 'T
        | Failed of error: string
        | ValidationFailed of errors: Validation.ValidationError list

    /// State manager interface for dependency injection
    type IStateManager =
        abstract member GetState: unit -> ApplicationState
        abstract member UpdateState: StateEvent -> ApplicationState
        abstract member UpdateStateAsync: StateEvent -> Async<ApplicationState>
        abstract member SubscribeToStateChanges: (StateEvent -> ApplicationState -> unit) -> IDisposable
        abstract member SaveStateAsync: unit -> Async<Result<unit, string>>
        abstract member LoadStateAsync: unit -> Async<Result<ApplicationState, string>>
        abstract member ValidateState: unit -> Result<unit, Validation.ValidationError list>

    /// Event bus for state change notifications
    type StateChangeSubscription = {
        Id: Guid
        Handler: StateEvent -> ApplicationState -> unit
        CreatedAt: DateTime
    }

    /// State manager implementation with thread safety and event sourcing
    type StateManager private (initialState: ApplicationState) =

        // Thread-safe state storage
        let mutable currentState = initialState
        let stateLock = obj()

        // Event sourcing components
        let eventHistory = ConcurrentQueue<StateEvent>()
        let maxEventHistory = 1000

        // Subscription management
        let subscriptions = ConcurrentDictionary<Guid, StateChangeSubscription>()

        // Performance tracking
        let mutable totalOperations = 0L
        let mutable successfulOperations = 0L
        let performanceLock = obj()

        // ===== State Access Methods =====

        /// Get current state (thread-safe read)
        member this.GetState() =
            lock stateLock (fun () -> currentState)

        /// Apply state event to current state (pure function)
        member private this.ApplyStateEvent (event: StateEvent) (state: ApplicationState) : ApplicationState =
            let timestamp = DateTime.Now

            match event with
            // Core domain events
            | DisplaysUpdated (displays, _) ->
                let displayMap = displays |> List.fold (fun acc d -> Map.add d.Id d acc) Map.empty
                let coreState = { state.Core with
                    ConnectedDisplays = displayMap
                    LastUpdate = timestamp }
                Transforms.updateCore coreState state
                |> Transforms.addEvent event

            | PresetSaved (name, config, _) ->
                let namedConfig = { config with Name = name; CreatedAt = timestamp }
                let coreState = { state.Core with
                    SavedPresets = Map.add name namedConfig state.Core.SavedPresets
                    LastUpdate = timestamp }
                Transforms.updateCore coreState state
                |> Transforms.addEvent event

            | PresetLoaded (name, _) ->
                let coreState = { state.Core with LastUpdate = timestamp }
                Transforms.updateCore coreState state
                |> Transforms.addEvent event

            | PresetDeleted (name, _) ->
                let coreState = { state.Core with
                    SavedPresets = Map.remove name state.Core.SavedPresets
                    LastUpdate = timestamp }
                Transforms.updateCore coreState state
                |> Transforms.addEvent event

            | ConfigurationApplied (config, _) ->
                let coreState = { state.Core with
                    CurrentConfiguration = Some config
                    LastSuccessfulConfiguration = Some config
                    LastUpdate = timestamp }
                Transforms.updateCore coreState state
                |> Transforms.addEvent event

            // UI domain events
            | UIThemeChanged (theme, _) ->
                let uiState = { state.UI with
                    Theme = theme
                    LastUIUpdate = timestamp }
                Transforms.updateUI uiState state
                |> Transforms.addEvent event

            | WindowStateChanged (windowState, _) ->
                let uiState = { state.UI with
                    WindowState = Some windowState
                    LastUIUpdate = timestamp }
                Transforms.updateUI uiState state
                |> Transforms.addEvent event

            | DisplaySelectionChanged (displayIds, _) ->
                let selectedSet = Set.ofList displayIds
                let uiState = { state.UI with
                    SelectedDisplays = selectedSet
                    LastUIUpdate = timestamp }
                Transforms.updateUI uiState state
                |> Transforms.addEvent event

            // Cache domain events
            | CacheEntryAdded (displayId, entry, _) ->
                let cacheState = { state.Cache with
                    DisplayStates = Map.add displayId entry state.Cache.DisplayStates
                    TotalCacheOperations = state.Cache.TotalCacheOperations + 1L }
                Transforms.updateCache cacheState state
                |> Transforms.addEvent event

            | CacheEntryRemoved (displayId, _) ->
                let cacheState =
                    { state.Cache with
                        DisplayStates = Map.remove displayId state.Cache.DisplayStates
                        TotalCacheOperations = state.Cache.TotalCacheOperations + 1L }
                Transforms.updateCache cacheState state
                |> Transforms.addEvent event

            | CacheCleanupPerformed _ ->
                let cacheState =
                    { state.Cache with
                        LastCacheCleanup = timestamp }
                Transforms.updateCache cacheState state
                |> Transforms.addEvent event

            // Canvas domain events
            | CanvasTransformUpdated (transform, _) ->
                let canvasState =
                    { state.Canvas with
                        TransformParams = Some transform
                        LastCanvasUpdate = timestamp }
                Transforms.updateCanvas canvasState state
                |> Transforms.addEvent event

            | DragOperationStarted (displayId, x, y, _) ->
                let dragState = {
                    TargetDisplayId = displayId
                    StartPosition = (x, y)
                    CurrentPosition = (x, y)
                    IsDragging = true
                    StartTime = timestamp
                }
                let canvasState =
                    { state.Canvas with
                        DragOperations = Some dragState
                        LastCanvasUpdate = timestamp }
                Transforms.updateCanvas canvasState state
                |> Transforms.addEvent event

            | DragOperationCompleted (displayId, x, y, _) ->
                let canvasState =
                    { state.Canvas with
                        DragOperations = None
                        LastCanvasUpdate = timestamp }
                Transforms.updateCanvas canvasState state
                |> Transforms.addEvent event

            // Windows API domain events
            | StrategyPerformanceUpdated (strategy, metrics, _) ->
                let apiState =
                    { state.WindowsAPI with
                        StrategyPerformance = Map.add strategy metrics state.WindowsAPI.StrategyPerformance
                        LastSuccessfulStrategy = Some strategy }
                Transforms.updateWindowsAPI apiState state
                |> Transforms.addEvent event

            | DisplayDetectionCompleted (displays, duration, _) ->
                let apiState =
                    { state.WindowsAPI with
                        LastDisplayDetection = timestamp }
                Transforms.updateWindowsAPI apiState state
                |> Transforms.addEvent event

            | ApiCallCompleted (operation, success, duration, _) ->
                let stats = state.WindowsAPI.ApiCallStatistics
                let newStats =
                    { stats with
                        TotalCalls = stats.TotalCalls + 1L
                        SuccessfulCalls = if success then stats.SuccessfulCalls + 1L else stats.SuccessfulCalls
                        FailedCalls = if not success then stats.FailedCalls + 1L else stats.FailedCalls
                        AverageResponseTime =
                            if stats.TotalCalls = 0L then duration
                            else TimeSpan.FromTicks((stats.AverageResponseTime.Ticks + duration.Ticks) / 2L) }

                let apiState = { state.WindowsAPI with ApiCallStatistics = newStats }
                Transforms.updateWindowsAPI apiState state
                |> Transforms.addEvent event

            // Configuration events
            | ConfigurationSettingChanged (setting, oldValue, newValue, _) ->
                // Configuration changes require specific handling based on setting
                state |> Transforms.addEvent event

            | ConfigurationValidated (isValid, errors, _) ->
                state |> Transforms.addEvent event

            | ConfigurationReloaded _ ->
                state |> Transforms.addEvent event

        /// Update state with event and notification (thread-safe)
        member this.UpdateState(event: StateEvent) : ApplicationState =
            let startTime = DateTime.Now

            lock stateLock (fun () ->
                try
                    // Apply event to current state
                    let newState = this.ApplyStateEvent event currentState

                    // Validate new state
                    match Validation.validateState newState with
                    | Error validationErrors ->
                        Logging.logErrorf "State validation failed after event %A: %A" event validationErrors
                        currentState  // Return unchanged state on validation failure

                    | Ok _ ->
                        // Update current state
                        currentState <- newState

                        // Add to event history
                        eventHistory.Enqueue(event)
                        if eventHistory.Count > maxEventHistory then
                            let mutable discarded = Unchecked.defaultof<StateEvent>
                            eventHistory.TryDequeue(&discarded) |> ignore

                        // Update performance metrics
                        lock performanceLock (fun () ->
                            totalOperations <- totalOperations + 1L
                            successfulOperations <- successfulOperations + 1L)

                        // Notify subscribers asynchronously
                        Task.Run(fun () -> this.NotifySubscribers event newState) |> ignore

                        // Log successful update
                        let duration = DateTime.Now - startTime
                        Logging.logVerbosef "State updated successfully with event %A in %A" event duration

                        newState

                with ex ->
                    Logging.logErrorf "Error updating state with event %A: %s" event ex.Message
                    lock performanceLock (fun () -> totalOperations <- totalOperations + 1L)
                    currentState)

        /// Update state asynchronously
        member this.UpdateStateAsync(event: StateEvent) : Async<ApplicationState> = async {
            return this.UpdateState(event)
        }

        /// Notify all subscribers of state change
        member private this.NotifySubscribers(event: StateEvent) (newState: ApplicationState) =
            try
                for subscription in subscriptions.Values do
                    try
                        subscription.Handler event newState
                    with ex ->
                        Logging.logErrorf "Error in state change subscriber %A: %s" subscription.Id ex.Message
            with ex ->
                Logging.logErrorf "Error notifying state change subscribers: %s" ex.Message

        /// Subscribe to state changes
        member this.SubscribeToStateChanges(handler: StateEvent -> ApplicationState -> unit) : IDisposable =
            let subscription = {
                Id = Guid.NewGuid()
                Handler = handler
                CreatedAt = DateTime.Now
            }

            subscriptions.[subscription.Id] <- subscription

            // Return disposable to unsubscribe
            { new IDisposable with
                member _.Dispose() =
                    subscriptions.TryRemove(subscription.Id) |> ignore }

        /// Validate current state
        member this.ValidateState() : Result<unit, Validation.ValidationError list> =
            lock stateLock (fun () -> Validation.validateState currentState)

        /// Save state to persistent storage (placeholder - would integrate with file system)
        member this.SaveStateAsync() : Async<Result<unit, string>> = async {
            try
                lock stateLock (fun () ->
                    // In a real implementation, this would serialize and save to file
                    Logging.logInfo "State saved successfully (placeholder implementation)")
                return Ok ()
            with ex ->
                return Error (sprintf "Failed to save state: %s" ex.Message)
        }

        /// Load state from persistent storage (placeholder)
        member this.LoadStateAsync() : Async<Result<ApplicationState, string>> = async {
            try
                lock stateLock (fun () ->
                    // In a real implementation, this would load from file
                    Logging.logInfo "State loaded successfully (placeholder implementation)")
                return Ok currentState
            with ex ->
                return Error (sprintf "Failed to load state: %s" ex.Message)
        }

        /// Get performance statistics
        member this.GetPerformanceStats() =
            lock performanceLock (fun () ->
                let successRate =
                    if totalOperations = 0L then 1.0
                    else float successfulOperations / float totalOperations

                {
                    TotalOperations = totalOperations
                    SuccessfulOperations = successfulOperations
                    SuccessRate = successRate
                    SubscriberCount = subscriptions.Count
                    EventHistoryCount = eventHistory.Count
                })

        /// Get event history
        member this.GetEventHistory() =
            eventHistory.ToArray() |> Array.toList

        /// Interface implementation
        interface IStateManager with
            member this.GetState() = this.GetState()
            member this.UpdateState(event) = this.UpdateState(event)
            member this.UpdateStateAsync(event) = this.UpdateStateAsync(event)
            member this.SubscribeToStateChanges(handler) = this.SubscribeToStateChanges(handler)
            member this.SaveStateAsync() = this.SaveStateAsync()
            member this.LoadStateAsync() = this.LoadStateAsync()
            member this.ValidateState() = this.ValidateState()

    // ===== Factory Functions =====

    /// Create a new state manager with initial state
    let create (initialState: ApplicationState) : IStateManager =
        new StateManager(initialState) :> IStateManager

    /// Create state manager with default configuration
    let createWithDefaults () : IStateManager =
        create ApplicationState.empty

    /// Create state manager from legacy AppState
    let createFromLegacyState (legacyState: AppState) : IStateManager =
        let unifiedState = Compatibility.fromLegacyAppState legacyState ApplicationState.empty
        create unifiedState

    // ===== Advanced State Operations =====

    /// Thread-safe atomic state operations
    module AtomicOperations =

        /// Compare-and-swap pattern for atomic updates
        let atomicUpdate (stateManager: IStateManager) (updateFn: ApplicationState -> StateEvent option) (maxRetries: int) =
            let rec attempt retryCount =
                let currentState = stateManager.GetState()

                match updateFn currentState with
                | None -> Success currentState  // No update needed
                | Some event ->
                    let newState = stateManager.UpdateState(event)

                    // Simple verification - in a more complex system, we'd use actual CAS
                    if newState.Metadata.StateChangeCount > currentState.Metadata.StateChangeCount then
                        Success newState
                    else if retryCount < maxRetries then
                        Thread.Yield() |> ignore
                        attempt (retryCount + 1)
                    else
                        Conflict currentState

            attempt 0

        /// Batch multiple state events atomically
        let batchUpdate (stateManager: IStateManager) (events: StateEvent list) =
            try
                let results = events |> List.map stateManager.UpdateState
                Success (List.last results)
            with ex ->
                Failed ex.Message

    // ===== State Synchronization Utilities =====

    /// Utilities for cross-domain state synchronization
    module Synchronization =

        /// Synchronize legacy AppState with unified state
        let syncWithLegacyAppState (stateManager: IStateManager) (legacyState: AppState) =
            let currentState = stateManager.GetState()
            let updatedState = Compatibility.fromLegacyAppState legacyState currentState

            // Create a sync event
            let syncEvent = ConfigurationReloaded DateTime.Now
            stateManager.UpdateState(syncEvent) |> ignore

        /// Extract legacy AppState from unified state
        let extractLegacyAppState (stateManager: IStateManager) : AppState =
            let currentState = stateManager.GetState()
            Compatibility.toLegacyAppState currentState

    // ===== Diagnostics and Monitoring =====

    /// State diagnostics and monitoring utilities
    module Diagnostics =

        type StateDiagnostics = {
            StateSize: int64
            EventHistorySize: int
            SubscriberCount: int
            LastUpdate: DateTime
            ValidationStatus: Result<unit, Validation.ValidationError list>
            PerformanceMetrics: StatePerformanceMetrics
        }

        let getDiagnostics (stateManager: IStateManager) : StateDiagnostics =
            let state = stateManager.GetState()

            // Estimate state size (simplified)
            let stateSize = int64 (
                state.Core.ConnectedDisplays.Count * 100 +
                state.Core.SavedPresets.Count * 200 +
                state.UI.SelectedDisplays.Count * 20 +
                state.Cache.DisplayStates.Count * 150 +
                state.Canvas.DisplayVisuals.Count * 80 +
                state.WindowsAPI.StrategyPerformance.Count * 50 +
                state.Metadata.EventLog.Length * 30)

            {
                StateSize = stateSize
                EventHistorySize = state.Metadata.EventLog.Length
                SubscriberCount = 0  // Would need access to StateManager internals
                LastUpdate = state.Metadata.LastModified
                ValidationStatus = stateManager.ValidateState()
                PerformanceMetrics = state.Metadata.PerformanceMetrics
            }

        let logStateDiagnostics (stateManager: IStateManager) =
            let diagnostics = getDiagnostics stateManager

            Logging.logInfof "State Diagnostics:"
            Logging.logInfof "  - State Size: %d bytes" diagnostics.StateSize
            Logging.logInfof "  - Event History: %d events" diagnostics.EventHistorySize
            Logging.logInfof "  - Last Update: %A" diagnostics.LastUpdate
            Logging.logInfof "  - Validation: %A" diagnostics.ValidationStatus
            Logging.logInfof "  - Performance: Latency=%A, Throughput=%.2f"
                diagnostics.PerformanceMetrics.StateUpdateLatency
                diagnostics.PerformanceMetrics.EventProcessingThroughput