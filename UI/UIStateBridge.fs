namespace DisplaySwitchPro

open System
open Avalonia.Controls

/// Backward-compatible bridge for existing mutable reference patterns
/// This module provides the same interface as the old UIState.fs but uses the new event system internally
module UIStateBridge =

    // Initialize the new system when the bridge is first used
    do UIEventSystem.UICoordinator.initialize()

    // Backward compatibility functions that mirror the old UIState.fs interface
    // These functions maintain the same signatures but use the new unified state management internally

    /// Update the application state (backward compatible)
    let updateAppState (newAppState: AppState) : unit =
        UIStateManager.BackwardCompatibility.updateAppState newAppState

    /// Update the platform adapter (backward compatible)
    let updateAdapter (adapter: IPlatformAdapter) : unit =
        UIStateManager.BackwardCompatibility.updateAdapter adapter

    /// Set the main window reference (backward compatible)
    let setMainWindow (window: Window) : unit =
        UIStateManager.BackwardCompatibility.setMainWindow window

    /// Set the display settings dialog (backward compatible)
    let setDisplaySettingsDialog (dialog: Window option) : unit =
        UIStateManager.BackwardCompatibility.setDisplaySettingsDialog dialog

    /// Set the current dialog display (backward compatible)
    let setCurrentDialogDisplay (display: DisplayInfo option) : unit =
        UIStateManager.BackwardCompatibility.setCurrentDialogDisplay display

    /// Get the current application state (backward compatible)
    let getCurrentAppState() : AppState =
        UIStateManager.BackwardCompatibility.getCurrentAppState()

    /// Get the current adapter (backward compatible)
    let getCurrentAdapter() : IPlatformAdapter option =
        UIStateManager.BackwardCompatibility.getCurrentAdapter()

    /// Get the main window (backward compatible)
    let getMainWindow() : Window option =
        UIStateManager.BackwardCompatibility.getMainWindow()

    /// Get the display settings dialog (backward compatible)
    let getDisplaySettingsDialog() : Window option =
        UIStateManager.BackwardCompatibility.getDisplaySettingsDialog()

    /// Get the current dialog display (backward compatible)
    let getCurrentDialogDisplay() : DisplayInfo option =
        UIStateManager.BackwardCompatibility.getCurrentDialogDisplay()

    // Enhanced functionality available to new code (opt-in)
    module Enhanced =

        /// Subscribe to all UI model updates
        let subscribeToModelUpdates (handler: UIStateManager.UIModel -> unit) : IDisposable =
            UIStateManager.StateManager.subscribeToModelUpdates handler

        /// Get the full UI model (new functionality)
        let getUIModel() : UIStateManager.UIModel =
            UIStateManager.StateManager.getModel()

        /// Update the model with a UI message (new functionality)
        let updateModel (message: UIEventSystem.UIMessage) : UIStateManager.UIModel =
            UIStateManager.StateManager.updateModel message

        /// Publish a UI event directly (new functionality)
        let publishUIEvent (event: UIEventSystem.UIEvent) : unit =
            UIEventSystem.UICoordinator.publishUIMessage (UIEventSystem.UIEvent event)

        /// Get enhanced state information (new functionality)
        let getStateInfo() = UIStateManager.Diagnostics.getStateInfo()

        /// Get recent events for debugging (new functionality)
        let getRecentEvents count = UIStateManager.Diagnostics.getRecentEvents count

        /// Log current state for debugging (new functionality)
        let logCurrentState() = UIStateManager.Diagnostics.logCurrentState()

    // Convenience functions for common operations using the new event system
    module EventDriven =

        /// Refresh the main window using events
        let refreshMainWindow() =
            UIEventSystem.UICoordinator.refreshMainWindow()

        /// Notify of display toggle using events
        let notifyDisplayToggled (displayId: string) (enabled: bool) =
            UIEventSystem.UICoordinator.notifyDisplayToggled displayId enabled

        /// Notify of preset application using events
        let notifyPresetApplied (presetName: string) =
            UIEventSystem.UICoordinator.notifyPresetApplied presetName

        /// Notify of theme change using events
        let notifyThemeChanged (theme: Theme.Theme) =
            UIEventSystem.UICoordinator.notifyThemeChanged theme

        /// Notify of error using events
        let notifyError (errorMessage: string) =
            UIEventSystem.UICoordinator.notifyError errorMessage

        /// Use functional event handlers (new functionality)
        module Functional =

            /// Handle display toggle with full error handling
            let handleDisplayToggle (displayId: string) (enabled: bool) =
                UIEventComposition.EventHandlers.handleDisplayToggle (displayId, enabled)

            /// Handle preset application with full error handling
            let handlePresetApplication (presetName: string) =
                UIEventComposition.EventHandlers.handlePresetApplication presetName

            /// Handle theme change with full error handling
            let handleThemeChange (theme: Theme.Theme) =
                UIEventComposition.EventHandlers.handleThemeChange theme

            /// Handle display position update with full error handling
            let handleDisplayPositionUpdate (displayId: string) (position: Position) =
                UIEventComposition.EventHandlers.handleDisplayPositionUpdate (displayId, position)

            /// Handle display drag completion with full error handling
            let handleDisplayDragCompletion (displayId: string) (position: Position) =
                UIEventComposition.EventHandlers.handleDisplayDragCompletion (displayId, position)

    // Migration utilities for gradually adopting the new system
    module Migration =

        /// Check if the new event system is initialized
        let isEventSystemInitialized() =
            let diagnostics = UIEventSystem.UICoordinator.getDiagnostics()
            diagnostics.IsInitialized

        /// Get event system diagnostics
        let getEventSystemDiagnostics() =
            UIEventSystem.UICoordinator.getDiagnostics()

        /// Convert old-style mutable reference callbacks to event handlers
        let createEventHandler (callback: unit -> unit) : UIEventSystem.UIEvent -> unit =
            fun _ -> callback()

        /// Create a callback that can be used with old UI code but publishes events
        let createCallbackBridge (event: UIEventSystem.UIEvent) : unit -> unit =
            fun () -> UIEventSystem.UICoordinator.publishUIMessage (UIEventSystem.UIEvent event)

        /// Migrate a component to use events gradually
        let migrateComponentToEvents
            (oldRefreshCallback: (unit -> unit) option ref)
            (events: UIEventSystem.UIEvent list) : unit =

            let eventHandler = fun () ->
                events |> List.iter (fun event ->
                    UIEventSystem.UICoordinator.publishUIMessage (UIEventSystem.UIEvent event))

            oldRefreshCallback := Some eventHandler

        /// Setup monitoring to compare old vs new state patterns (for testing)
        let setupStateComparisonMonitoring() =
            let subscription =
                UIStateManager.StateManager.subscribeToModelUpdates (fun model ->
                    Logging.logVerbose (sprintf "New state updated - displays: %d, presets: %d"
                        model.AppState.ConnectedDisplays.Count
                        model.AppState.SavedPresets.Count))
            subscription