namespace DisplaySwitchPro

open System

/// Functional event composition with railway-oriented programming
/// This module provides composable event handlers that replace imperative callbacks
module UIEventComposition =

    // Import types from event system
    open UIEventSystem

    // Enhanced command pattern for UI operations
    type UICommand<'T> = {
        Execute: 'T -> UIResult<unit>
        CanExecute: 'T -> bool
        Name: string
        Description: string
    }

    // Command creation helpers
    module Commands =

        let createCommand name description canExecute execute = {
            Execute = execute
            CanExecute = canExecute
            Name = name
            Description = description
        }

        let alwaysExecutable = fun _ -> true
        let neverExecutable = fun _ -> false

        // Combine multiple commands into one
        let combineCommands (commands: UICommand<'T> list) : UICommand<'T> =
            {
                Execute = fun param ->
                    commands
                    |> List.filter (fun cmd -> cmd.CanExecute param)
                    |> List.fold (fun acc cmd ->
                        match acc with
                        | Ok () -> cmd.Execute param
                        | Error _ -> acc) (Ok ())

                CanExecute = fun param ->
                    commands |> List.exists (fun cmd -> cmd.CanExecute param)

                Name = commands |> List.map (fun cmd -> cmd.Name) |> String.concat " + "
                Description = commands |> List.map (fun cmd -> cmd.Description) |> String.concat "; "
            }

        // Create a conditional command
        let conditionalCommand (condition: 'T -> bool) (command: UICommand<'T>) : UICommand<'T> =
            { command with CanExecute = fun param -> condition param && command.CanExecute param }

    // Railway-oriented programming for event handling
    module Railway =

        // Composition operators
        let (>>=) (result: UIResult<'a>) (next: 'a -> UIResult<'b>) : UIResult<'b> =
            Result.bind next result

        let (>=>) (f: 'a -> UIResult<'b>) (g: 'b -> UIResult<'c>) : 'a -> UIResult<'c> =
            fun x -> f x >>= g

        // Helper for handling side effects in the pipeline
        let tee (sideEffect: 'a -> unit) : 'a -> UIResult<'a> =
            fun input ->
                try
                    sideEffect input
                    Ok input
                with ex ->
                    Error (sprintf "Side effect failed: %s" ex.Message)

        // Convert a throwing function to a Result-returning function
        let tryCatch (operation: 'a -> 'b) : 'a -> UIResult<'b> =
            fun input ->
                try
                    Ok (operation input)
                with ex ->
                    Error ex.Message

        // Create a safe handler with logging
        let loggedHandler (name: string) (handler: 'T -> UIResult<'U>) : 'T -> UIResult<'U> =
            fun input ->
                Logging.logVerbose (sprintf "Executing %s" name)
                match handler input with
                | Ok result ->
                    Logging.logVerbose (sprintf "%s completed successfully" name)
                    Ok result
                | Error error ->
                    Logging.logError (sprintf "%s failed: %s" name error)
                    Error error

        // Retry a function with exponential backoff
        let retry (maxAttempts: int) (operation: 'a -> UIResult<'b>) : 'a -> UIResult<'b> =
            let rec retryLoop attempt input =
                match operation input with
                | Ok result -> Ok result
                | Error error when attempt < maxAttempts ->
                    let delay = pown 2 attempt * 100 // Exponential backoff
                    Threading.Thread.Sleep(delay)
                    Logging.logError (sprintf "Retrying operation (attempt %d/%d): %s" (attempt + 1) maxAttempts error)
                    retryLoop (attempt + 1) input
                | Error error ->
                    Error (sprintf "Operation failed after %d attempts: %s" maxAttempts error)

            retryLoop 0

    // Specific UI event handlers using functional composition
    module EventHandlers =

        // Display toggle validation and execution
        let validateDisplayToggle (displayId: string, enabled: bool) : UIResult<string * bool> =
            if String.IsNullOrEmpty displayId then
                Error "Display ID cannot be empty"
            else
                Ok (displayId, enabled)

        let updateDisplayState (displayId: string, enabled: bool) : UIResult<string * bool> =
            try
                UICoordinator.notifyDisplayToggled displayId enabled
                Ok (displayId, enabled)
            with ex ->
                Error (sprintf "Failed to update display state: %s" ex.Message)

        let applyDisplaySettings (displayId: string, enabled: bool) : UIResult<string * bool> =
            match WindowsDisplaySystem.setDisplayEnabled displayId enabled with
            | Ok () ->
                Logging.logNormal (sprintf "Successfully applied display %s enable state: %b" displayId enabled)
                Ok (displayId, enabled)
            | Error err ->
                Error (sprintf "Failed to apply display settings: %s" err)

        let refreshUserInterface (displayId: string, enabled: bool) : UIResult<unit> =
            try
                UICoordinator.refreshMainWindow()
                Ok ()
            with ex ->
                Error (sprintf "Failed to refresh UI: %s" ex.Message)

        // Composed display toggle handler
        let handleDisplayToggle (displayId: string, enabled: bool) =
            match validateDisplayToggle (displayId, enabled) with
            | Ok (id, en) ->
                match updateDisplayState (id, en) with
                | Ok (id2, en2) ->
                    match applyDisplaySettings (id2, en2) with
                    | Ok (id3, en3) -> refreshUserInterface (id3, en3)
                    | Error e -> Error e
                | Error e -> Error e
            | Error e -> Error e

        // Preset application handlers
        let validatePresetApplication (presetName: string) : UIResult<string> =
            if String.IsNullOrEmpty presetName then
                Error "Preset name cannot be empty"
            else
                let currentAppState = UIStateManager.StateManager.getCurrentAppState()
                match AppState.getPreset presetName currentAppState with
                | Some _ -> Ok presetName
                | None -> Error (sprintf "Preset not found: %s" presetName)

        let loadPresetConfiguration (presetName: string) : UIResult<DisplayConfiguration> =
            let currentAppState = UIStateManager.StateManager.getCurrentAppState()
            match AppState.getPreset presetName currentAppState with
            | Some config -> Ok config
            | None -> Error (sprintf "Failed to load preset configuration: %s" presetName)

        let validatePresetCompatibility (config: DisplayConfiguration) : UIResult<DisplayConfiguration> =
            match PresetManager.validatePreset config with
            | Ok () -> Ok config
            | Error err -> Error (sprintf "Preset validation failed: %s" err)

        let applyPresetToSystem (config: DisplayConfiguration) : UIResult<DisplayConfiguration> =
            match PresetManager.applyPreset config with
            | Ok () -> Ok config
            | Error err -> Error (sprintf "Failed to apply preset to system: %s" err)

        let updateStateAfterPreset (config: DisplayConfiguration) : UIResult<unit> =
            try
                UICoordinator.notifyPresetApplied config.Name
                let actualConfig = PresetManager.getCurrentConfiguration()
                UICoordinator.updateConfiguration actualConfig
                Ok ()
            with ex ->
                Error (sprintf "Failed to update state after preset application: %s" ex.Message)

        // Composed preset application handler with retry
        let handlePresetApplication (presetName: string) =
            match validatePresetApplication presetName with
            | Ok name ->
                match loadPresetConfiguration name with
                | Ok config ->
                    match validatePresetCompatibility config with
                    | Ok validConfig ->
                        match applyPresetToSystem validConfig with
                        | Ok appliedConfig -> updateStateAfterPreset appliedConfig
                        | Error e -> Error e
                    | Error e -> Error e
                | Error e -> Error e
            | Error e -> Error e

        // Theme change handlers
        let validateThemeChange (theme: Theme.Theme) : UIResult<Theme.Theme> =
            Ok theme // Theme values are always valid

        let applyThemeChange (theme: Theme.Theme) : UIResult<Theme.Theme> =
            try
                Theme.currentTheme <- theme
                Ok theme
            with ex ->
                Error (sprintf "Failed to apply theme change: %s" ex.Message)

        let notifyThemeUpdated (theme: Theme.Theme) : UIResult<unit> =
            try
                UICoordinator.notifyThemeChanged theme
                Ok ()
            with ex ->
                Error (sprintf "Failed to notify theme change: %s" ex.Message)

        // Composed theme change handler
        let handleThemeChange (theme: Theme.Theme) =
            match validateThemeChange theme with
            | Ok validTheme ->
                match applyThemeChange validTheme with
                | Ok appliedTheme -> notifyThemeUpdated appliedTheme
                | Error e -> Error e
            | Error e -> Error e

        // Position update handlers
        let validatePositionUpdate (displayId: string, position: Position) : UIResult<string * Position> =
            if String.IsNullOrEmpty displayId then
                Error "Display ID cannot be empty"
            else
                Ok (displayId, position)

        let updateDisplayPosition (displayId: string, position: Position) : UIResult<string * Position> =
            try
                UICoordinator.notifyDisplayPositionChanged displayId position
                Ok (displayId, position)
            with ex ->
                Error (sprintf "Failed to update display position: %s" ex.Message)

        // Composed position update handler
        let handleDisplayPositionUpdate (displayId: string, position: Position) =
            match validatePositionUpdate (displayId, position) with
            | Ok (id, pos) -> updateDisplayPosition (id, pos)
            | Error e -> Error e

        // Drag completion handlers
        let handleDisplayDragCompletion (displayId: string, position: Position) : UIResult<unit> =
            match validatePositionUpdate (displayId, position) with
            | Ok (id, pos) ->
                try
                    UICoordinator.notifyDisplayDragCompleted id pos
                    Ok ()
                with ex ->
                    Error (sprintf "Failed to notify drag completion: %s" ex.Message)
            | Error e -> Error e

    // Command factory for creating UI commands
    module CommandFactory =

        let createDisplayToggleCommand (displayId: string) : UICommand<bool> =
            Commands.createCommand
                "ToggleDisplay"
                (sprintf "Toggle display %s" displayId)
                Commands.alwaysExecutable
                (fun enabled ->
                    EventHandlers.handleDisplayToggle (displayId, enabled))

        let createPresetApplicationCommand (presetName: string) : UICommand<unit> =
            Commands.createCommand
                "ApplyPreset"
                (sprintf "Apply preset %s" presetName)
                Commands.alwaysExecutable
                (fun _ ->
                    EventHandlers.handlePresetApplication presetName
                    |> Result.map (fun _ -> ()))

        let createThemeChangeCommand (theme: Theme.Theme) : UICommand<unit> =
            Commands.createCommand
                "ChangeTheme"
                (sprintf "Change to %A theme" theme)
                Commands.alwaysExecutable
                (fun _ ->
                    EventHandlers.handleThemeChange theme
                    |> Result.map (fun _ -> ()))

        let createPositionUpdateCommand (displayId: string) : UICommand<Position> =
            Commands.createCommand
                "UpdatePosition"
                (sprintf "Update position for display %s" displayId)
                Commands.alwaysExecutable
                (fun position ->
                    EventHandlers.handleDisplayPositionUpdate (displayId, position)
                    |> Result.map (fun _ -> ()))

    // Integration utilities for connecting with existing UI components
    module Integration =

        // Convert a functional handler to a callback for existing UI code
        let toCallback (handler: 'T -> UIResult<unit>) : 'T -> unit =
            fun input ->
                match handler input with
                | Ok () -> ()
                | Error error -> Logging.logError error

        // Create an event handler that can be used with Avalonia controls
        let createAvaloniaEventHandler (handler: unit -> UIResult<unit>) : EventHandler =
            EventHandler(fun _ _ ->
                handler () |> function
                | Ok () -> ()
                | Error error -> Logging.logError error)

        // Convert UI events to functional handlers
        let handleUIEvent (event: UIEvent) : UIResult<unit> =
            UIEventSystem.EventProcessing.processUIEventSafely event

        // Bridge between old mutable reference pattern and new event system
        let bridgeRefreshFunction (originalRefreshFunc: unit -> unit option) : unit -> UIResult<unit> =
            fun () ->
                try
                    match originalRefreshFunc() with
                    | Some () -> Ok ()
                    | None -> Error "Refresh function not available"
                with ex ->
                    Error (sprintf "Refresh function failed: %s" ex.Message)