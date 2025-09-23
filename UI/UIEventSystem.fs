namespace DisplaySwitchPro

open System
open System.Threading

/// Event-driven architecture foundation for UI orchestration
/// This module replaces mutable references with functional event publishing
module UIEventSystem =


    // Core UI events that drive the application
    type UIEvent =
        | RefreshMainWindow
        | DisplayToggled of DisplayId: string * Enabled: bool
        | PresetApplied of PresetName: string
        | PresetSaved of PresetName: string
        | PresetDeleted of PresetName: string
        | ThemeChanged of Theme.Theme
        | WindowResized of Width: float * Height: float
        | DisplayDetectionRequested
        | DisplayPositionChanged of DisplayId: string * Position: Position
        | DisplayDragCompleted of DisplayId: string * Position: Position
        | DisplaySettingsChanged of DisplayId: string * DisplayInfo: DisplayInfo
        | ErrorOccurred of ErrorMessage: string
        | UIInitialized
        | UIShutdown


    // Messages that flow through the system
    type UIMessage =
        | UIEvent of UIEvent
        | StateUpdate of AppState
        | ConfigurationChanged of DisplayConfiguration
        | SystemMessage of string

    // Enhanced Result type for UI operations
    type UIResult<'T> = Result<'T, string>

    // Event bus implementation with thread safety
    module EventBus =
        type EventBus<'Event> = {
            Subscribe: ('Event -> unit) -> IDisposable
            Publish: 'Event -> unit
            Clear: unit -> unit
            SubscriberCount: unit -> int
        }

        // Create a new event bus instance
        let create<'Event>() =
            let subscribers = ref ([]: ('Event -> unit) list)
            let subscribersLock = obj()

            let subscribe handler =
                lock subscribersLock (fun () ->
                    subscribers := handler :: !subscribers)

                // Return IDisposable for unsubscription
                { new IDisposable with
                    member _.Dispose() =
                        lock subscribersLock (fun () ->
                            subscribers := !subscribers |> List.filter (fun h -> not (obj.ReferenceEquals(h, handler)))) }

            let publish event =
                let currentSubscribers =
                    lock subscribersLock (fun () -> !subscribers)

                for handler in currentSubscribers do
                    try
                        handler event
                    with ex ->
                        Logging.logError (sprintf "Event handler failed: %s" ex.Message)

            let clear() =
                lock subscribersLock (fun () -> subscribers := [])

            let getSubscriberCount() =
                lock subscribersLock (fun () -> (!subscribers).Length)

            {
                Subscribe = subscribe
                Publish = publish
                Clear = clear
                SubscriberCount = getSubscriberCount
            }

    // Global event coordination
    module UICoordinator =
        let private eventBus = EventBus.create<UIMessage>()
        let private isInitialized = ref false

        // Initialize the UI coordinator
        let initialize() =
            if not !isInitialized then
                Logging.logNormal "UICoordinator: Initializing event-driven architecture"
                isInitialized := true
                eventBus.Publish(UIEvent UIInitialized)

        // Subscribe to UI messages
        let subscribeToUIMessages (handler: UIMessage -> unit) : IDisposable =
            eventBus.Subscribe handler

        // Publish UI messages
        let publishUIMessage (message: UIMessage) =
            if !isInitialized then
                eventBus.Publish message
            else
                Logging.logError "UICoordinator: Attempting to publish before initialization"

        // Convenience functions for publishing specific events
        let refreshMainWindow() =
            publishUIMessage (UIEvent RefreshMainWindow)

        let notifyDisplayToggled displayId enabled =
            publishUIMessage (UIEvent (DisplayToggled (displayId, enabled)))

        let notifyPresetApplied presetName =
            publishUIMessage (UIEvent (PresetApplied presetName))

        let notifyPresetSaved presetName =
            publishUIMessage (UIEvent (PresetSaved presetName))

        let notifyPresetDeleted presetName =
            publishUIMessage (UIEvent (PresetDeleted presetName))

        let notifyThemeChanged theme =
            publishUIMessage (UIEvent (ThemeChanged theme))

        let notifyDisplayPositionChanged displayId position =
            publishUIMessage (UIEvent (DisplayPositionChanged (displayId, position)))

        let notifyDisplayDragCompleted displayId position =
            publishUIMessage (UIEvent (DisplayDragCompleted (displayId, position)))

        let notifyDisplaySettingsChanged displayId displayInfo =
            publishUIMessage (UIEvent (DisplaySettingsChanged (displayId, displayInfo)))

        let notifyError errorMessage =
            publishUIMessage (UIEvent (ErrorOccurred errorMessage))

        let updateAppState newAppState =
            publishUIMessage (StateUpdate newAppState)

        let updateConfiguration config =
            publishUIMessage (ConfigurationChanged config)


        // Get diagnostic information
        let getDiagnostics() = {|
            IsInitialized = !isInitialized
            SubscriberCount = eventBus.SubscriberCount()
        |}

        // Shutdown coordination
        let shutdown() =
            if !isInitialized then
                publishUIMessage (UIEvent UIShutdown)
                eventBus.Clear()
                isInitialized := false
                Logging.logNormal "UICoordinator: Event system shutdown completed"

    // Event validation and processing utilities
    module EventProcessing =

        // Validate UI events before processing
        let validateUIEvent (event: UIEvent) : UIResult<UIEvent> =
            match event with
            | DisplayToggled (displayId, _) when String.IsNullOrEmpty displayId ->
                Error "Display ID cannot be empty"
            | PresetApplied presetName when String.IsNullOrEmpty presetName ->
                Error "Preset name cannot be empty"
            | PresetSaved presetName when String.IsNullOrEmpty presetName ->
                Error "Preset name cannot be empty"
            | PresetDeleted presetName when String.IsNullOrEmpty presetName ->
                Error "Preset name cannot be empty"
            | DisplayPositionChanged (displayId, _) when String.IsNullOrEmpty displayId ->
                Error "Display ID cannot be empty"
            | DisplayDragCompleted (displayId, _) when String.IsNullOrEmpty displayId ->
                Error "Display ID cannot be empty"
            | DisplaySettingsChanged (displayId, _) when String.IsNullOrEmpty displayId ->
                Error "Display ID cannot be empty"
            | ErrorOccurred msg when String.IsNullOrEmpty msg ->
                Error "Error message cannot be empty"
            | validEvent -> Ok validEvent

        // Process validated events
        let processValidatedEvent (event: UIEvent) : UIResult<unit> =
            try
                Logging.logVerbose (sprintf "Processing UI event: %A" event)
                Ok ()
            with ex ->
                Error (sprintf "Event processing failed: %s" ex.Message)

        // Safe event processing with validation
        let processUIEventSafely (event: UIEvent) : UIResult<unit> =
            validateUIEvent event
            |> Result.bind processValidatedEvent

    // Functional composition utilities for event handling
    module EventComposition =

        // Railway-oriented programming for event processing
        let (>>=) (result: UIResult<'a>) (next: 'a -> UIResult<'b>) : UIResult<'b> =
            Result.bind next result

        let (>=>) (f: 'a -> UIResult<'b>) (g: 'b -> UIResult<'c>) : 'a -> UIResult<'c> =
            fun x -> f x >>= g

        // Create a safe event handler with logging
        let createSafeHandler (name: string) (handler: 'T -> UIResult<unit>) : 'T -> unit =
            fun input ->
                match handler input with
                | Ok () ->
                    Logging.logVerbose (sprintf "%s completed successfully" name)
                | Error error ->
                    Logging.logError (sprintf "%s failed: %s" name error)

        // Compose multiple handlers safely
        let composeHandlers (handlers: ('T -> UIResult<unit>) list) : 'T -> UIResult<unit> =
            fun input ->
                handlers
                |> List.fold (fun acc handler ->
                    match acc with
                    | Ok () -> handler input
                    | Error _ -> acc) (Ok ())

        // Create a conditional handler
        let conditionalHandler (condition: 'T -> bool) (handler: 'T -> UIResult<unit>) : 'T -> UIResult<unit> =
            fun input ->
                if condition input then
                    handler input
                else
                    Ok ()