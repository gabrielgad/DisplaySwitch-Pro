namespace DisplaySwitchPro

open System
open Avalonia.Input
open CanvasState
open CoordinateTransforms
open DomainTypes
open EnhancedResult

/// Functional event processing system for canvas interactions
/// Provides pure event transformation and command generation
module CanvasEventProcessing =

    /// Enhanced canvas event with additional context
    type EnhancedCanvasEvent = {
        /// Core event data
        Event: CanvasEvent
        /// Timestamp when event occurred
        Timestamp: DateTime
        /// Modifier keys pressed during event
        Modifiers: KeyModifiers
        /// Event source information
        Source: EventSource
        /// Sequence number for ordering
        SequenceNumber: int64
    }

    /// Source of the canvas event
    and EventSource =
        | Mouse of button: PointerButton
        | Keyboard of key: Key
        | Touch of touchId: int
        | System

    /// Event processing result
    type EventProcessingResult = {
        /// Commands to execute
        Commands: CanvasCommand list
        /// New events generated (for cascading)
        GeneratedEvents: EnhancedCanvasEvent list
        /// Processing warnings (non-fatal)
        Warnings: string list
        /// Whether processing was successful
        Success: bool
    }

    /// Event validation result
    type EventValidation = {
        /// Whether the event is valid
        IsValid: bool
        /// Validation errors
        Errors: string list
        /// Validation warnings
        Warnings: string list
        /// Suggested modifications to make event valid
        Suggestions: string list
    }

    /// Event processing configuration
    type EventProcessingConfig = {
        /// Whether to validate events before processing
        ValidateEvents: bool
        /// Whether to generate detailed logging events
        EnableDetailedLogging: bool
        /// Maximum number of commands per event
        MaxCommandsPerEvent: int
        /// Whether to enable event debouncing
        EnableDebouncing: bool
        /// Debounce timeout in milliseconds
        DebounceTimeoutMs: int
        /// Whether to enable gesture recognition
        EnableGestures: bool
    }

    /// Gesture recognition state
    type GestureState = {
        /// Current gesture being tracked
        CurrentGesture: Gesture option
        /// Points in the current gesture
        GesturePoints: (CanvasPoint * DateTime) list
        /// When the gesture started
        GestureStartTime: DateTime option
        /// Minimum points required for gesture recognition
        MinGesturePoints: int
        /// Maximum time between points for gesture continuity
        MaxPointIntervalMs: int
    }

    /// Recognized gestures
    and Gesture =
        | Tap of point: CanvasPoint
        | DoubleTap of point: CanvasPoint
        | Drag of startPoint: CanvasPoint * endPoint: CanvasPoint
        | Pinch of centerPoint: CanvasPoint * scaleFactor: float
        | Pan of startPoint: CanvasPoint * endPoint: CanvasPoint * velocity: float

    /// Core event processing functions
    module Core =

        /// Default event processing configuration
        let defaultConfig = {
            ValidateEvents = true
            EnableDetailedLogging = false
            MaxCommandsPerEvent = 10
            EnableDebouncing = true
            DebounceTimeoutMs = 50
            EnableGestures = true
        }

        /// Create enhanced event from basic event
        let createEnhancedEvent (event: CanvasEvent) (modifiers: KeyModifiers) (source: EventSource) (sequenceNumber: int64) = {
            Event = event
            Timestamp = DateTime.Now
            Modifiers = modifiers
            Source = source
            SequenceNumber = sequenceNumber
        }

        /// Validate a canvas event
        let validateEvent (event: EnhancedCanvasEvent) (state: CanvasState) : EventValidation =
            let errors = []
            let warnings = []
            let suggestions = []

            let (newErrors, newWarnings, newSuggestions) =
                match event.Event with
                | PointerDown (displayId, point) ->
                    let errs = if not (Queries.getAllDisplayVisuals state |> Map.containsKey displayId) then
                                   ["Display not found in canvas state"] else []
                    let warns = if point.X < 0.0 || point.Y < 0.0 then
                                    ["Pointer coordinates are negative"] else []
                    let suggs = if not (Core.isPointInRectangle point state.ViewportBounds) then
                                    ["Point is outside viewport bounds"] else []
                    (errs, warns, suggs)

                | PointerMove (displayId, point) ->
                    let errs = if not (Queries.isDisplayBeingDragged displayId state) then
                                   ["Pointer move for display not being dragged"] else []
                    (errs, [], [])

                | PointerUp (displayId, point) ->
                    let errs = if not (Queries.isDisplayBeingDragged displayId state) then
                                   ["Pointer up for display not being dragged"] else []
                    (errs, [], [])

                | KeyDown key ->
                    ([], [], [])

                | Scroll (point, delta) ->
                    let warns = if Math.Abs(delta) > 10.0 then
                                    ["Large scroll delta detected"] else []
                    ([], warns, [])

                | DoubleClick (displayId, point) ->
                    let errs = if not (Queries.getAllDisplayVisuals state |> Map.containsKey displayId) then
                                   ["Display not found for double-click"] else []
                    (errs, [], [])

                | DisplaySelectionChanged displayIds ->
                    let errs = displayIds
                               |> Set.filter (fun id -> not (Queries.getAllDisplayVisuals state |> Map.containsKey id))
                               |> Set.toList
                               |> List.map (fun id -> sprintf "Selected display %A not found" id)
                    (errs, [], [])

            let allErrors = errors @ newErrors
            let allWarnings = warnings @ newWarnings
            let allSuggestions = suggestions @ newSuggestions

            {
                IsValid = List.isEmpty allErrors
                Errors = allErrors
                Warnings = allWarnings
                Suggestions = allSuggestions
            }

        /// Process a canvas event into commands
        let processEvent (config: EventProcessingConfig) (event: EnhancedCanvasEvent) (state: CanvasState) : EventProcessingResult =
            let validation = if config.ValidateEvents then Some (validateEvent event state) else None

            match validation with
            | Some v when not v.IsValid ->
                {
                    Commands = []
                    GeneratedEvents = []
                    Warnings = v.Errors
                    Success = false
                }
            | _ ->
                let warnings = validation |> Option.map (fun v -> v.Warnings) |> Option.defaultValue []

                let commands =
                    match event.Event with
                    | PointerDown (displayId, point) ->
                        if event.Modifiers.HasFlag(KeyModifiers.Control) then
                            // Ctrl+Click for multi-selection
                            if Queries.isDisplaySelected displayId state then
                                let newSelection = Set.remove displayId state.SelectedDisplays
                                [SelectMultiple newSelection]
                            else
                                let newSelection = Set.add displayId state.SelectedDisplays
                                [SelectMultiple newSelection]
                        else
                            // Normal click - start drag if not already selected, or just select
                            if Queries.isDisplaySelected displayId state then
                                [StartDrag (displayId, point)]
                            else
                                [SelectDisplay displayId; StartDrag (displayId, point)]

                    | PointerMove (displayId, point) ->
                        if Queries.isDisplayBeingDragged displayId state then
                            [UpdateDrag (displayId, point)]
                        else
                            [] // Ignore move events for non-dragging displays

                    | PointerUp (displayId, point) ->
                        if Queries.isDisplayBeingDragged displayId state then
                            [EndDrag (displayId, point)]
                        else
                            []

                    | KeyDown key ->
                        match key with
                        | Key.G when event.Modifiers.HasFlag(KeyModifiers.Control) ->
                            [ToggleSnap]
                        | Key.Add | Key.OemPlus when event.Modifiers.HasFlag(KeyModifiers.Control) ->
                            [ZoomIn None]
                        | Key.Subtract | Key.OemMinus when event.Modifiers.HasFlag(KeyModifiers.Control) ->
                            [ZoomOut None]
                        | Key.Escape ->
                            [ClearSelection]
                        | Key.A when event.Modifiers.HasFlag(KeyModifiers.Control) ->
                            let allDisplays = Queries.getAllDisplayVisuals state |> Map.keys |> Set.ofSeq
                            [SelectMultiple allDisplays]
                        | Key.Home when event.Modifiers.HasFlag(KeyModifiers.Control) ->
                            [ResetView]
                        | Key.F when event.Modifiers.HasFlag(KeyModifiers.Control) ->
                            [FitToCanvas]
                        | _ -> []

                    | Scroll (point, delta) ->
                        if event.Modifiers.HasFlag(KeyModifiers.Control) then
                            // Zoom with Ctrl+Scroll
                            if delta > 0.0 then [ZoomIn (Some point)]
                            else [ZoomOut (Some point)]
                        else
                            [] // Regular scroll - could implement pan here

                    | DoubleClick (displayId, point) ->
                        [ShowContextMenu (displayId, point)]

                    | DisplaySelectionChanged displayIds ->
                        [SelectMultiple displayIds]

                let limitedCommands =
                    if List.length commands > config.MaxCommandsPerEvent then
                        commands |> List.take config.MaxCommandsPerEvent
                    else
                        commands

                let commandLimitWarning =
                    if List.length commands > config.MaxCommandsPerEvent then
                        [sprintf "Event generated %d commands, limited to %d" (List.length commands) config.MaxCommandsPerEvent]
                    else
                        []

                {
                    Commands = limitedCommands
                    GeneratedEvents = [] // Could generate logging events here
                    Warnings = warnings @ commandLimitWarning
                    Success = true
                }

        /// Execute a command and return the new state
        let executeCommand (command: CanvasCommand) (state: CanvasState) : Result<CanvasState, string> =
            try
                let newState =
                    match command with
                    | StartDrag (displayId, point) ->
                        StateTransitions.startDrag displayId point state

                    | UpdateDrag (displayId, point) ->
                        StateTransitions.updateDrag displayId point state

                    | EndDrag (displayId, point) ->
                        StateTransitions.endDrag displayId point state

                    | ToggleSnap ->
                        StateTransitions.toggleSnap state

                    | ZoomIn pointOpt ->
                        let zoomFactor = 1.2
                        match Zoom.zoomIn state.TransformParams zoomFactor pointOpt with
                        | Ok newTransform -> StateTransitions.updateTransform newTransform state
                        | Error _ -> state // Keep current state if zoom fails

                    | ZoomOut pointOpt ->
                        let zoomFactor = 1.2
                        match Zoom.zoomOut state.TransformParams zoomFactor pointOpt with
                        | Ok newTransform -> StateTransitions.updateTransform newTransform state
                        | Error _ -> state // Keep current state if zoom fails

                    | SelectDisplay displayId ->
                        StateTransitions.selectDisplay displayId state

                    | SelectMultiple displayIds ->
                        StateTransitions.selectMultiple displayIds state

                    | ClearSelection ->
                        StateTransitions.clearSelection state

                    | ShowContextMenu (displayId, point) ->
                        // Context menu is a UI side effect, no state change
                        state

                    | ResetView ->
                        StateTransitions.resetView state

                    | FitToCanvas ->
                        // This would require display information, simplified for now
                        state

                Ok newState
            with
            | ex -> Error (sprintf "Command execution failed: %s" ex.Message)

    /// Event debouncing for performance optimization
    module Debouncing =

        /// Debounce state for tracking recent events
        type DebounceState = {
            LastEventTime: Map<EventKey, DateTime>
            PendingEvents: Map<EventKey, EnhancedCanvasEvent>
            TimeoutMs: int
        }

        /// Key for identifying similar events for debouncing
        and EventKey =
            | PointerMoveKey of DisplayId
            | ScrollKey
            | KeyRepeatKey of Key
            | OtherKey of string

        /// Create debounce state
        let createDebounceState (timeoutMs: int) = {
            LastEventTime = Map.empty
            PendingEvents = Map.empty
            TimeoutMs = timeoutMs
        }

        /// Generate event key for debouncing
        let getEventKey (event: CanvasEvent) : EventKey =
            match event with
            | PointerMove (displayId, _) -> PointerMoveKey displayId
            | Scroll (_, _) -> ScrollKey
            | KeyDown key -> KeyRepeatKey key
            | _ -> OtherKey (sprintf "%A" event)

        /// Check if event should be debounced
        let shouldDebounce (event: EnhancedCanvasEvent) (debounceState: DebounceState) : bool =
            let eventKey = getEventKey event.Event
            match Map.tryFind eventKey debounceState.LastEventTime with
            | Some lastTime ->
                let timeDiff = event.Timestamp - lastTime
                timeDiff.TotalMilliseconds < float debounceState.TimeoutMs
            | None -> false

        /// Update debounce state with new event
        let updateDebounceState (event: EnhancedCanvasEvent) (debounceState: DebounceState) : DebounceState =
            let eventKey = getEventKey event.Event
            {
                LastEventTime = Map.add eventKey event.Timestamp debounceState.LastEventTime
                PendingEvents = Map.add eventKey event debounceState.PendingEvents
                TimeoutMs = debounceState.TimeoutMs
            }

        /// Get events that are ready to process (timeout expired)
        let getReadyEvents (currentTime: DateTime) (debounceState: DebounceState) : (EnhancedCanvasEvent list * DebounceState) =
            let timeoutThreshold = currentTime.AddMilliseconds(float -debounceState.TimeoutMs)

            let readyEvents =
                debounceState.PendingEvents
                |> Map.filter (fun eventKey event ->
                    match Map.tryFind eventKey debounceState.LastEventTime with
                    | Some lastTime -> lastTime <= timeoutThreshold
                    | None -> true)
                |> Map.values
                |> List.ofSeq

            let updatedPendingEvents =
                debounceState.PendingEvents
                |> Map.filter (fun eventKey _ ->
                    not (readyEvents |> List.exists (fun e -> getEventKey e.Event = eventKey)))

            let updatedLastEventTime =
                debounceState.LastEventTime
                |> Map.filter (fun eventKey _ ->
                    Map.containsKey eventKey updatedPendingEvents)

            (readyEvents, {
                LastEventTime = updatedLastEventTime
                PendingEvents = updatedPendingEvents
                TimeoutMs = debounceState.TimeoutMs
            })

    /// Gesture recognition for touch and mouse interactions
    module Gestures =

        /// Create empty gesture state
        let createEmptyGestureState () = {
            CurrentGesture = None
            GesturePoints = []
            GestureStartTime = None
            MinGesturePoints = 2
            MaxPointIntervalMs = 500
        }

        /// Add point to gesture tracking
        let addGesturePoint (point: CanvasPoint) (timestamp: DateTime) (gestureState: GestureState) : GestureState =
            let newPoints = (point, timestamp) :: gestureState.GesturePoints

            // Clean up old points outside the time window
            let cutoffTime = timestamp.AddMilliseconds(float -gestureState.MaxPointIntervalMs)
            let validPoints =
                newPoints
                |> List.filter (fun (_, t) -> t >= cutoffTime)
                |> List.take 10 // Limit to last 10 points

            {
                gestureState with
                    GesturePoints = validPoints
                    GestureStartTime = if Option.isNone gestureState.GestureStartTime then Some timestamp else gestureState.GestureStartTime
            }

        /// Try to recognize gesture from current points
        let recognizeGesture (gestureState: GestureState) : Gesture option =
            if List.length gestureState.GesturePoints < gestureState.MinGesturePoints then
                None
            else
                let points = gestureState.GesturePoints |> List.map fst |> List.rev
                let timestamps = gestureState.GesturePoints |> List.map snd |> List.rev

                match points with
                | [singlePoint] ->
                    Some (Tap singlePoint)

                | startPoint :: endPoint :: _ when List.length points = 2 ->
                    let distance = Utils.calculateDistance startPoint endPoint
                    if distance < 10.0 then
                        Some (DoubleTap startPoint)
                    else
                        Some (Drag (startPoint, endPoint))

                | startPoint :: _ when List.length points > 2 ->
                    let endPoint = List.last points
                    let totalTime = (List.last timestamps) - (List.head timestamps)
                    let totalDistance = Utils.calculateDistance startPoint endPoint

                    if totalTime.TotalMilliseconds > 0.0 then
                        let velocity = totalDistance / totalTime.TotalMilliseconds
                        Some (Pan (startPoint, endPoint, velocity))
                    else
                        Some (Drag (startPoint, endPoint))

                | _ -> None

        /// Clear gesture state
        let clearGesture (gestureState: GestureState) : GestureState =
            {
                gestureState with
                    CurrentGesture = None
                    GesturePoints = []
                    GestureStartTime = None
            }

        /// Convert gesture to canvas commands
        let gestureToCommands (gesture: Gesture) (state: CanvasState) : CanvasCommand list =
            match gesture with
            | Tap point ->
                match Queries.findDisplayAt point state with
                | Some displayId -> [SelectDisplay displayId]
                | None -> [ClearSelection]

            | DoubleTap point ->
                match Queries.findDisplayAt point state with
                | Some displayId -> [ShowContextMenu (displayId, point)]
                | None -> []

            | Drag (startPoint, endPoint) ->
                match Queries.findDisplayAt startPoint state with
                | Some displayId ->
                    [StartDrag (displayId, startPoint); UpdateDrag (displayId, endPoint); EndDrag (displayId, endPoint)]
                | None -> []

            | Pinch (centerPoint, scaleFactor) ->
                if scaleFactor > 1.0 then
                    [ZoomIn (Some centerPoint)]
                else
                    [ZoomOut (Some centerPoint)]

            | Pan (startPoint, endPoint, velocity) ->
                // High velocity pan could trigger momentum scrolling
                if velocity > 1.0 then
                    [ZoomOut (Some startPoint)] // Or implement pan commands
                else
                    []

    /// Event pipeline for processing sequences of events
    module Pipeline =

        /// Pipeline state for processing event sequences
        type PipelineState = {
            Config: EventProcessingConfig
            DebounceState: Debouncing.DebounceState option
            GestureState: Gestures.GestureState option
            SequenceNumber: int64
            ProcessingHistory: (EnhancedCanvasEvent * EventProcessingResult) list
        }

        /// Create pipeline state
        let createPipelineState (config: EventProcessingConfig) = {
            Config = config
            DebounceState = if config.EnableDebouncing then Some (Debouncing.createDebounceState config.DebounceTimeoutMs) else None
            GestureState = if config.EnableGestures then Some (Gestures.createEmptyGestureState ()) else None
            SequenceNumber = 0L
            ProcessingHistory = []
        }

        /// Process single event through the pipeline
        let processEventThroughPipeline (event: CanvasEvent) (modifiers: KeyModifiers) (source: EventSource)
                                      (canvasState: CanvasState) (pipelineState: PipelineState) : (EventProcessingResult * PipelineState) =

            let enhancedEvent = Core.createEnhancedEvent event modifiers source pipelineState.SequenceNumber

            // Check debouncing if enabled
            let shouldProcess =
                match pipelineState.DebounceState with
                | Some debounceState when Debouncing.shouldDebounce enhancedEvent debounceState -> false
                | _ -> true

            if not shouldProcess then
                // Event was debounced, update debounce state but don't process
                let newDebounceState = pipelineState.DebounceState |> Option.map (Debouncing.updateDebounceState enhancedEvent)
                let newPipelineState = { pipelineState with DebounceState = newDebounceState; SequenceNumber = pipelineState.SequenceNumber + 1L }
                ({Commands = []; GeneratedEvents = []; Warnings = ["Event debounced"]; Success = false}, newPipelineState)
            else
                // Process the event
                let processingResult = Core.processEvent pipelineState.Config enhancedEvent canvasState

                // Update gesture state if enabled
                let newGestureState =
                    match pipelineState.GestureState, event with
                    | Some gestureState, PointerDown (_, point) ->
                        Some (Gestures.addGesturePoint point enhancedEvent.Timestamp gestureState)
                    | Some gestureState, PointerMove (_, point) ->
                        Some (Gestures.addGesturePoint point enhancedEvent.Timestamp gestureState)
                    | Some gestureState, PointerUp (_, point) ->
                        let updatedGestureState = Gestures.addGesturePoint point enhancedEvent.Timestamp gestureState
                        let recognizedGesture = Gestures.recognizeGesture updatedGestureState
                        match recognizedGesture with
                        | Some gesture ->
                            // Convert gesture to commands and clear gesture state
                            Some (Gestures.clearGesture updatedGestureState)
                        | None ->
                            Some (Gestures.clearGesture updatedGestureState)
                    | state, _ -> state

                // Update debounce state if enabled
                let newDebounceState = pipelineState.DebounceState |> Option.map (Debouncing.updateDebounceState enhancedEvent)

                let newPipelineState = {
                    pipelineState with
                        DebounceState = newDebounceState
                        GestureState = newGestureState
                        SequenceNumber = pipelineState.SequenceNumber + 1L
                        ProcessingHistory = (enhancedEvent, processingResult) :: (List.take 99 pipelineState.ProcessingHistory)
                }

                (processingResult, newPipelineState)

        /// Process multiple commands in sequence
        let processCommandSequence (commands: CanvasCommand list) (initialState: CanvasState) : Result<CanvasState, string list> =
            commands
            |> List.fold (fun accResult command ->
                accResult
                |> Result.bind (fun state ->
                    Core.executeCommand command state
                    |> Result.mapError (fun err -> [err])))
                (Ok initialState)
            |> Result.mapError (fun errors -> errors)

        /// Get recent processing history
        let getRecentHistory (count: int) (pipelineState: PipelineState) : (EnhancedCanvasEvent * EventProcessingResult) list =
            pipelineState.ProcessingHistory |> List.take (min count (List.length pipelineState.ProcessingHistory))

        /// Clear processing history
        let clearHistory (pipelineState: PipelineState) : PipelineState =
            { pipelineState with ProcessingHistory = [] }