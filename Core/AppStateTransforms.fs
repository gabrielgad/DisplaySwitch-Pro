namespace DisplaySwitchPro

open System
open DomainTypes
open EnhancedResult

/// Pure state transformation functions separated from side effects
/// This module contains only pure functions that transform application state
module AppStateTransforms =

    /// Pure state transformation operations
    module Transforms =

        /// Add a display to the connected displays map
        let addDisplay (display: DisplayInfo) (state: AppState) : AppState =
            { state with
                ConnectedDisplays = Map.add display.Id display state.ConnectedDisplays
                LastUpdate = DateTime.Now }

        /// Remove a display from the connected displays map
        let removeDisplay (displayId: DisplayId) (state: AppState) : AppState =
            { state with
                ConnectedDisplays = Map.remove displayId state.ConnectedDisplays
                LastUpdate = DateTime.Now }

        /// Update all connected displays at once
        let updateDisplays (displays: DisplayInfo list) (state: AppState) : AppState =
            let displayMap = displays |> List.fold (fun acc d -> Map.add d.Id d acc) Map.empty
            { state with
                ConnectedDisplays = displayMap
                LastUpdate = DateTime.Now }

        /// Set the current configuration
        let setCurrentConfiguration (config: DisplayConfiguration) (state: AppState) : AppState =
            { state with
                CurrentConfiguration = Some config
                LastUpdate = DateTime.Now }

        /// Clear the current configuration
        let clearCurrentConfiguration (state: AppState) : AppState =
            { state with
                CurrentConfiguration = None
                LastUpdate = DateTime.Now }

        /// Save a preset with a new name and configuration
        let savePreset (name: string) (config: DisplayConfiguration) (state: AppState) : AppState =
            let namedConfig = { config with Name = name; CreatedAt = DateTime.Now }
            { state with
                SavedPresets = Map.add name namedConfig state.SavedPresets
                LastUpdate = DateTime.Now }

        /// Load a preset by name if it exists
        let loadPreset (name: string) (state: AppState) : AppState option =
            match Map.tryFind name state.SavedPresets with
            | Some config ->
                Some { state with
                         CurrentConfiguration = Some config
                         LastUpdate = DateTime.Now }
            | None -> None

        /// Delete a preset by name
        let deletePreset (name: string) (state: AppState) : AppState =
            { state with
                SavedPresets = Map.remove name state.SavedPresets
                LastUpdate = DateTime.Now }

        /// Update a display within the connected displays map
        let updateDisplay (displayId: DisplayId) (updater: DisplayInfo -> DisplayInfo) (state: AppState) : AppState =
            match Map.tryFind displayId state.ConnectedDisplays with
            | Some display ->
                let updatedDisplay = updater display
                { state with
                    ConnectedDisplays = Map.add displayId updatedDisplay state.ConnectedDisplays
                    LastUpdate = DateTime.Now }
            | None -> state

        /// Update multiple displays using a function
        let updateMultipleDisplays (displayIds: DisplayId list) (updater: DisplayInfo -> DisplayInfo) (state: AppState) : AppState =
            let updatedDisplays =
                displayIds
                |> List.fold (fun acc displayId ->
                    match Map.tryFind displayId acc with
                    | Some display -> Map.add displayId (updater display) acc
                    | None -> acc) state.ConnectedDisplays

            { state with
                ConnectedDisplays = updatedDisplays
                LastUpdate = DateTime.Now }

    /// Validation functions for state operations
    module Validation =

        /// Validate that a display exists in the state
        let validateDisplayExists (displayId: DisplayId) (state: AppState) : Result<DisplayInfo, string> =
            match Map.tryFind displayId state.ConnectedDisplays with
            | Some display -> Ok display
            | None -> Error (sprintf "Display with ID '%s' not found" displayId)

        /// Validate that a preset name is not empty
        let validatePresetName (name: string) : Result<string, string> =
            if String.IsNullOrWhiteSpace(name) then
                Error "Preset name cannot be empty"
            else
                Ok name

        /// Validate that a preset doesn't already exist (for creation)
        let validatePresetNotExists (name: string) (state: AppState) : Result<unit, string> =
            if Map.containsKey name state.SavedPresets then
                Error (sprintf "Preset '%s' already exists" name)
            else
                Ok ()

        /// Validate that a preset exists (for loading/deleting)
        let validatePresetExists (name: string) (state: AppState) : Result<DisplayConfiguration, string> =
            match Map.tryFind name state.SavedPresets with
            | Some config -> Ok config
            | None -> Error (sprintf "Preset '%s' does not exist" name)

        /// Validate that a configuration has at least one display
        let validateConfigurationNotEmpty (config: DisplayConfiguration) : Result<DisplayConfiguration, string> =
            if List.isEmpty config.Displays then
                Error "Configuration must contain at least one display"
            else
                Ok config

        /// Validate that exactly one display is marked as primary
        let validateExactlyOnePrimary (displays: DisplayInfo list) : Result<unit, string> =
            let primaryDisplays = displays |> List.filter (fun d -> d.IsPrimary)
            match primaryDisplays with
            | [_] -> Ok ()
            | [] -> Error "Configuration must have exactly one primary display"
            | _ -> Error "Configuration cannot have multiple primary displays"

        /// Validate that all display IDs are unique
        let validateUniqueDisplayIds (displays: DisplayInfo list) : Result<unit, string> =
            let displayIds = displays |> List.map (fun d -> d.Id)
            let uniqueIds = displayIds |> List.distinct
            if List.length displayIds = List.length uniqueIds then
                Ok ()
            else
                Error "All display IDs must be unique"

        /// Comprehensive configuration validation
        let validateConfiguration (config: DisplayConfiguration) : Result<DisplayConfiguration, string list> =
            enhancedResult {
                let! _ = validateConfigurationNotEmpty config |> Result.mapError (fun e -> [e])
                let! _ = validateExactlyOnePrimary config.Displays |> Result.mapError (fun e -> [e])
                let! _ = validateUniqueDisplayIds config.Displays |> Result.mapError (fun e -> [e])
                return config
            }

    /// Safe state operations that include validation
    module SafeOperations =

        /// Safely add a display with validation
        let safeAddDisplay (display: DisplayInfo) (state: AppState) : Result<AppState, string> =
            DisplayValidation.validateDisplayInfo display
            |> Result.map (fun validDisplay -> Transforms.addDisplay validDisplay state)

        /// Safely save a preset with comprehensive validation
        let safeSavePreset (name: string) (config: DisplayConfiguration) (state: AppState) : Result<AppState, string list> =
            enhancedResult {
                let! validName = Validation.validatePresetName name |> Result.mapError (fun e -> [e])
                let! validConfig = Validation.validateConfiguration config
                // Note: We allow overwriting existing presets in this operation
                return Transforms.savePreset validName validConfig state
            }

        /// Safely load a preset with validation
        let safeLoadPreset (name: string) (state: AppState) : Result<AppState, string> =
            enhancedResult {
                let! validName = Validation.validatePresetName name
                let! config = Validation.validatePresetExists validName state
                let! validConfig = Validation.validateConfiguration config |> Result.mapError (fun errors -> String.concat "; " errors)
                return { state with
                           CurrentConfiguration = Some validConfig
                           LastUpdate = DateTime.Now }
            }

        /// Safely delete a preset with validation
        let safeDeletePreset (name: string) (state: AppState) : Result<AppState, string> =
            enhancedResult {
                let! validName = Validation.validatePresetName name
                let! _ = Validation.validatePresetExists validName state
                return Transforms.deletePreset validName state
            }

        /// Safely set current configuration with validation
        let safeSetCurrentConfiguration (config: DisplayConfiguration) (state: AppState) : Result<AppState, string list> =
            enhancedResult {
                let! validConfig = Validation.validateConfiguration config
                return Transforms.setCurrentConfiguration validConfig state
            }

        /// Safely update a display with validation
        let safeUpdateDisplay (displayId: DisplayId) (updater: DisplayInfo -> DisplayInfo) (state: AppState) : Result<AppState, string> =
            enhancedResult {
                let! existingDisplay = Validation.validateDisplayExists displayId state
                let updatedDisplay = updater existingDisplay
                let! validatedDisplay = DisplayValidation.validateDisplayInfo updatedDisplay
                return Transforms.updateDisplay displayId (fun _ -> validatedDisplay) state
            }

    /// Query operations for reading state without modification
    module Queries =

        /// Get all connected displays as a list
        let getConnectedDisplays (state: AppState) : DisplayInfo list =
            state.ConnectedDisplays |> Map.values |> List.ofSeq

        /// Get a specific display by ID
        let getDisplay (displayId: DisplayId) (state: AppState) : DisplayInfo option =
            Map.tryFind displayId state.ConnectedDisplays

        /// Get all enabled displays
        let getEnabledDisplays (state: AppState) : DisplayInfo list =
            getConnectedDisplays state |> List.filter (fun d -> d.IsEnabled)

        /// Get all disabled displays
        let getDisabledDisplays (state: AppState) : DisplayInfo list =
            getConnectedDisplays state |> List.filter (fun d -> not d.IsEnabled)

        /// Get the primary display if one exists
        let getPrimaryDisplay (state: AppState) : DisplayInfo option =
            getConnectedDisplays state |> List.tryFind (fun d -> d.IsPrimary)

        /// Get all preset names
        let getPresetNames (state: AppState) : string list =
            state.SavedPresets |> Map.keys |> List.ofSeq |> List.sort

        /// Get a specific preset by name
        let getPreset (name: string) (state: AppState) : DisplayConfiguration option =
            Map.tryFind name state.SavedPresets

        /// Check if a preset exists
        let hasPreset (name: string) (state: AppState) : bool =
            Map.containsKey name state.SavedPresets

        /// Get current configuration
        let getCurrentConfiguration (state: AppState) : DisplayConfiguration option =
            state.CurrentConfiguration

        /// Get the number of connected displays
        let getDisplayCount (state: AppState) : int =
            Map.count state.ConnectedDisplays

        /// Get the number of enabled displays
        let getEnabledDisplayCount (state: AppState) : int =
            getEnabledDisplays state |> List.length

        /// Get the number of saved presets
        let getPresetCount (state: AppState) : int =
            Map.count state.SavedPresets

        /// Find displays by predicate
        let findDisplays (predicate: DisplayInfo -> bool) (state: AppState) : DisplayInfo list =
            getConnectedDisplays state |> List.filter predicate

        /// Check if any display matches a predicate
        let existsDisplay (predicate: DisplayInfo -> bool) (state: AppState) : bool =
            getConnectedDisplays state |> List.exists predicate

        /// Get displays with specific resolution
        let getDisplaysWithResolution (width: int) (height: int) (state: AppState) : DisplayInfo list =
            findDisplays (fun d -> d.Resolution.Width = width && d.Resolution.Height = height) state

        /// Get displays with specific refresh rate
        let getDisplaysWithRefreshRate (refreshRate: int) (state: AppState) : DisplayInfo list =
            findDisplays (fun d -> d.Resolution.RefreshRate = refreshRate) state

    /// State comparison and diff operations
    module Comparison =

        /// Compare two AppState instances for equality
        let areStatesEqual (state1: AppState) (state2: AppState) : bool =
            state1.ConnectedDisplays = state2.ConnectedDisplays &&
            state1.CurrentConfiguration = state2.CurrentConfiguration &&
            state1.SavedPresets = state2.SavedPresets

        /// Check if two states have the same connected displays
        let haveSameDisplays (state1: AppState) (state2: AppState) : bool =
            state1.ConnectedDisplays = state2.ConnectedDisplays

        /// Check if two states have the same current configuration
        let haveSameCurrentConfiguration (state1: AppState) (state2: AppState) : bool =
            state1.CurrentConfiguration = state2.CurrentConfiguration

        /// Check if two states have the same saved presets
        let haveSamePresets (state1: AppState) (state2: AppState) : bool =
            state1.SavedPresets = state2.SavedPresets

        /// Get the differences between two states
        let getStateDifferences (oldState: AppState) (newState: AppState) =
            let displayDifferences =
                let oldDisplays = Set.ofSeq (Map.keys oldState.ConnectedDisplays)
                let newDisplays = Set.ofSeq (Map.keys newState.ConnectedDisplays)
                {|
                    Added = Set.difference newDisplays oldDisplays |> Set.toList
                    Removed = Set.difference oldDisplays newDisplays |> Set.toList
                    Modified =
                        Set.intersect oldDisplays newDisplays
                        |> Set.toList
                        |> List.filter (fun id ->
                            Map.find id oldState.ConnectedDisplays <> Map.find id newState.ConnectedDisplays)
                |}

            let presetDifferences =
                let oldPresets = Set.ofSeq (Map.keys oldState.SavedPresets)
                let newPresets = Set.ofSeq (Map.keys newState.SavedPresets)
                {|
                    Added = Set.difference newPresets oldPresets |> Set.toList
                    Removed = Set.difference oldPresets newPresets |> Set.toList
                    Modified =
                        Set.intersect oldPresets newPresets
                        |> Set.toList
                        |> List.filter (fun name ->
                            Map.find name oldState.SavedPresets <> Map.find name newState.SavedPresets)
                |}

            {|
                Displays = displayDifferences
                Presets = presetDifferences
                CurrentConfigurationChanged = oldState.CurrentConfiguration <> newState.CurrentConfiguration
            |}

    /// Builder pattern for constructing AppState
    module Builder =

        /// Start with an empty state
        let empty () = AppState.empty

        /// Add a display to the builder
        let withDisplay (display: DisplayInfo) (state: AppState) : AppState =
            Transforms.addDisplay display state

        /// Add multiple displays to the builder
        let withDisplays (displays: DisplayInfo list) (state: AppState) : AppState =
            displays |> List.fold (fun acc display -> Transforms.addDisplay display acc) state

        /// Set the current configuration in the builder
        let withCurrentConfiguration (config: DisplayConfiguration) (state: AppState) : AppState =
            Transforms.setCurrentConfiguration config state

        /// Add a preset to the builder
        let withPreset (name: string) (config: DisplayConfiguration) (state: AppState) : AppState =
            Transforms.savePreset name config state

        /// Add multiple presets to the builder
        let withPresets (presets: (string * DisplayConfiguration) list) (state: AppState) : AppState =
            presets |> List.fold (fun acc (name, config) -> Transforms.savePreset name config acc) state

        /// Build the final state with validation
        let build (state: AppState) : Result<AppState, string list> =
            // Validate the entire constructed state
            let displays = Queries.getConnectedDisplays state
            if List.isEmpty displays then
                Error ["State must contain at least one display"]
            else
                // Additional validation can be added here
                Ok state