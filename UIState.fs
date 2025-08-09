namespace DisplaySwitchPro

open System
open Avalonia.Controls

// Global UI state management
module UIState =
    
    // Global state variables
    let mutable globalWorld = { Components = Components.empty; LastUpdate = DateTime.Now }
    let mutable globalAdapter: IPlatformAdapter option = None
    let mutable mainWindow: Window option = None
    let mutable globalDisplaySettingsDialog: Window option = None
    let mutable globalCurrentDialogDisplay: DisplayInfo option = None
    
    // Update the global world state
    let updateWorld newWorld =
        globalWorld <- newWorld
    
    // Update the global adapter
    let updateAdapter adapter =
        globalAdapter <- Some adapter
    
    // Set the main window reference
    let setMainWindow window =
        mainWindow <- Some window
        
    // Set the display settings dialog reference
    let setDisplaySettingsDialog dialog =
        globalDisplaySettingsDialog <- dialog
        
    // Set the current dialog display
    let setCurrentDialogDisplay display =
        globalCurrentDialogDisplay <- display
        
    // Get the current world state
    let getCurrentWorld() = globalWorld
    
    // Get the current adapter
    let getCurrentAdapter() = globalAdapter
    
    // Get the main window
    let getMainWindow() = mainWindow
    
    // Get the display settings dialog
    let getDisplaySettingsDialog() = globalDisplaySettingsDialog
    
    // Get the current dialog display
    let getCurrentDialogDisplay() = globalCurrentDialogDisplay