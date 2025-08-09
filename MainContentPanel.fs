namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

// Main UI content panel creation and management
module MainContentPanel =
    
    // Import the refreshMainWindowContent function reference
    // This will need to be set by the main GUI module
    let mutable refreshMainWindowContentRef: (unit -> unit) option = None
    
    // Set the refresh function reference
    let setRefreshFunction refreshFunc =
        refreshMainWindowContentRef <- Some refreshFunc
    
    // Helper function to call refresh
    let private refreshMainWindowContent() =
        match refreshMainWindowContentRef with
        | Some refreshFunc -> refreshFunc()
        | None -> printfn "[WARNING] Refresh function not set"
    
    // Create preset save dialog
    let private createPresetSaveDialog (colors: Theme.ThemeColors) currentWorld =
        let dialog = Window()
        dialog.Title <- "Save Preset"
        dialog.Width <- 400.0
        dialog.Height <- 200.0
        dialog.WindowStartupLocation <- WindowStartupLocation.CenterOwner
        dialog.Background <- SolidColorBrush(colors.Background) :> IBrush
        
        let panel = StackPanel()
        panel.Margin <- Thickness(20.0)
        panel.Orientation <- Orientation.Vertical
        
        let label = TextBlock()
        label.Text <- "Enter a name for this display preset:"
        label.FontSize <- 14.0
        label.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        label.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        panel.Children.Add(label)
        
        let textBox = TextBox()
        textBox.Text <- sprintf "Layout_%s" (DateTime.Now.ToString("yyyy-MM-dd_HH-mm"))
        textBox.FontSize <- 13.0
        textBox.Margin <- Thickness(0.0, 0.0, 0.0, 20.0)
        textBox.Background <- SolidColorBrush(colors.Surface) :> IBrush
        textBox.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        textBox.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        textBox.CaretBrush <- SolidColorBrush(colors.Text) :> IBrush
        textBox.SelectionBrush <- SolidColorBrush(Color.FromArgb(100uy, colors.Primary.R, colors.Primary.G, colors.Primary.B)) :> IBrush
        textBox.SelectionForegroundBrush <- SolidColorBrush(colors.Surface) :> IBrush
        
        // Add Enter key support to save preset
        textBox.KeyDown.Add(fun e ->
            if e.Key = Avalonia.Input.Key.Enter then
                let name = textBox.Text.Trim()
                if not (String.IsNullOrEmpty(name)) then
                    let updatedWorld = PresetSystem.saveCurrentAsPreset name currentWorld
                    UIState.updateWorld updatedWorld
                    printfn "Saving preset: %s" name
                    dialog.Close()
                    printfn "Preset saved successfully"
                    refreshMainWindowContent ()
        )
        
        panel.Children.Add(textBox)
        
        let buttonPanel = StackPanel()
        buttonPanel.Orientation <- Orientation.Horizontal
        buttonPanel.HorizontalAlignment <- HorizontalAlignment.Right
        
        let saveButton = Button()
        saveButton.Content <- "Save"
        saveButton.Width <- 80.0
        saveButton.Height <- 30.0
        saveButton.Margin <- Thickness(0.0, 0.0, 10.0, 0.0)
        saveButton.Background <- SolidColorBrush(colors.Secondary) :> IBrush
        saveButton.Foreground <- Brushes.White
        saveButton.Click.Add(fun _ ->
            let name = textBox.Text.Trim()
            if not (String.IsNullOrEmpty(name)) then
                let updatedWorld = PresetSystem.saveCurrentAsPreset name currentWorld
                UIState.updateWorld updatedWorld
                printfn "Saving preset: %s" name
                dialog.Close()
                
                printfn "Preset saved successfully"
                refreshMainWindowContent ()
        )
        buttonPanel.Children.Add(saveButton)
        
        let cancelButton = Button()
        cancelButton.Content <- "Cancel"
        cancelButton.Width <- 80.0
        cancelButton.Height <- 30.0
        cancelButton.Background <- SolidColorBrush(colors.Surface) :> IBrush
        cancelButton.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        cancelButton.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        cancelButton.Click.Add(fun _ -> dialog.Close())
        buttonPanel.Children.Add(cancelButton)
        
        panel.Children.Add(buttonPanel)
        dialog.Content <- panel
        
        dialog

    // Create main content panel
    let createMainContentPanel (world: World) (adapter: IPlatformAdapter) =
        let colors = Theme.getCurrentColors()
        
        // Update global state
        UIState.updateWorld world
        UIState.updateAdapter adapter
        
        let mutable currentWorld = world
        let displays = currentWorld.Components.ConnectedDisplays |> Map.values |> List.ofSeq
        let presets = PresetSystem.listPresets currentWorld
        
        printfn "DEBUG: Creating content with displays:"
        for display in displays do
            printfn "  - %s at (%d, %d) enabled: %b" display.Name display.Position.X display.Position.Y display.IsEnabled
        
        // Display change handler
        let onDisplayChanged displayId (updatedDisplay: DisplayInfo) =
            let updatedComponents = Components.addDisplay updatedDisplay currentWorld.Components
            
            let allDisplays = updatedComponents.ConnectedDisplays |> Map.values |> List.ofSeq
            let newConfig = {
                Displays = allDisplays
                Name = "Current"
                CreatedAt = DateTime.Now
            }
            let componentsWithConfig = Components.setCurrentConfiguration newConfig updatedComponents
            
            currentWorld <- { currentWorld with Components = componentsWithConfig }
            UIState.updateWorld currentWorld
            printfn "Display %s moved to (%d, %d)" displayId updatedDisplay.Position.X updatedDisplay.Position.Y
        
        // Preset click handler
        let onPresetClick (presetName: string) =
            if presetName = "SAVE_NEW" then
                let dialog = createPresetSaveDialog colors currentWorld
                dialog.ShowDialog(UIState.getMainWindow() |> Option.defaultValue null) |> ignore
            else
                // Load existing preset
                printfn "Loading preset: %s" presetName
                
                match Map.tryFind presetName currentWorld.Components.SavedPresets with
                | Some preset ->
                    printfn "Debug: Loaded preset %s with %d displays" preset.Name preset.Displays.Length
                    
                    let mutable updatedComponents = currentWorld.Components
                    
                    for display in preset.Displays do
                        printfn "Debug: Processing preset display %s - Resolution: %dx%d @ %dHz, Orientation: %A" 
                                display.Id display.Resolution.Width display.Resolution.Height display.Resolution.RefreshRate display.Orientation
                        
                        // Update component state
                        updatedComponents <- Components.addDisplay display updatedComponents
                        
                        // Apply settings to physical display if it's enabled
                        if display.IsEnabled then
                            let mode = { 
                                Width = display.Resolution.Width
                                Height = display.Resolution.Height
                                RefreshRate = display.Resolution.RefreshRate
                                BitsPerPixel = 32 
                            }
                            
                            printfn "Debug: Applying physical display mode to %s" display.Id
                            match WindowsDisplaySystem.applyDisplayMode display.Id mode display.Orientation with
                            | Ok () ->
                                printfn "Debug: Successfully applied display mode to %s" display.Id
                                
                                // Set as primary if specified
                                if display.IsPrimary then
                                    printfn "Debug: Setting %s as primary display" display.Id
                                    match WindowsDisplaySystem.setPrimaryDisplay display.Id with
                                    | Ok () -> printfn "Debug: Successfully set %s as primary" display.Id
                                    | Error err -> printfn "Debug: Failed to set %s as primary: %s" display.Id err
                                    
                            | Error err ->
                                printfn "Debug: Failed to apply display mode to %s: %s" display.Id err
                        else
                            printfn "Debug: Skipping disabled display %s" display.Id
                    
                    UIState.updateWorld { UIState.getCurrentWorld() with Components = updatedComponents }
                    
                    printfn "Debug: Preset application completed, refreshing UI"
                    refreshMainWindowContent ()
                    
                    printfn "Debug: Loading preset %s completed successfully" presetName
                    
                | None ->
                    printfn "Debug: Preset %s not found!" presetName

        // Create the display canvas
        let displayCanvas = DisplayCanvas.createDisplayCanvas displays onDisplayChanged
        
        // Display canvas is already a ScrollViewer from DisplayCanvas.createDisplayCanvas
        displayCanvas.Background <- SolidColorBrush(colors.Background) :> IBrush
        
        // Create preset panel with delete handler
        let onPresetDelete (presetName: string) = printfn "Delete preset: %s" presetName
        let presetPanel = UIComponents.createPresetPanel presets onPresetClick onPresetDelete
        
        // Split the UI: Display canvas on left, controls on right
        let leftColumn = ColumnDefinition()
        leftColumn.Width <- GridLength(1.0, GridUnitType.Star)
        
        let rightColumn = ColumnDefinition() 
        rightColumn.Width <- GridLength(300.0, GridUnitType.Pixel)
        
        let mainGrid = Grid()
        mainGrid.ColumnDefinitions.Add(leftColumn)
        mainGrid.ColumnDefinitions.Add(rightColumn)
        mainGrid.Background <- SolidColorBrush(colors.Background) :> IBrush
        
        // Add display canvas to left column
        Grid.SetColumn(displayCanvas, 0)
        mainGrid.Children.Add(displayCanvas)
        
        // Create right panel with presets (no separate controls panel from DisplayCanvas)
        let rightPanel = StackPanel()
        rightPanel.Orientation <- Orientation.Vertical
        rightPanel.Margin <- Thickness(10.0, 10.0, 10.0, 0.0)
        rightPanel.Children.Add(presetPanel)
        
        // Add right panel to right column  
        Grid.SetColumn(rightPanel, 1)
        mainGrid.Children.Add(rightPanel)
        
        // Acrylic background effect
        let acrylicBorder = Border()
        acrylicBorder.Background <- SolidColorBrush(colors.Background) :> IBrush
        acrylicBorder.Child <- mainGrid
        
        // Status bar at bottom
        let statusBar = Border()
        statusBar.Height <- 24.0
        statusBar.Background <- SolidColorBrush(Color.FromArgb(40uy, colors.Surface.R, colors.Surface.G, colors.Surface.B)) :> IBrush
        statusBar.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        statusBar.BorderThickness <- Thickness(0.0, 1.0, 0.0, 0.0)
        
        let statusPanel = DockPanel()
        statusPanel.Margin <- Thickness(12.0, 4.0, 12.0, 4.0)
        
        // App name/version (left side)
        let appInfo = TextBlock()
        appInfo.Text <- "DisplaySwitch-Pro v1.0"
        appInfo.FontSize <- 11.0
        appInfo.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        appInfo.VerticalAlignment <- VerticalAlignment.Center
        appInfo.HorizontalAlignment <- HorizontalAlignment.Left
        appInfo.Opacity <- 0.6
        statusPanel.Children.Add(appInfo)
        
        // Status text (center/right side)
        let statusText = TextBlock()
        statusText.Text <- sprintf "Ready • %d displays detected • %s theme" displays.Length (if Theme.currentTheme = Theme.Light then "Light" else "Dark")
        statusText.FontSize <- 11.0
        statusText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        statusText.VerticalAlignment <- VerticalAlignment.Center
        statusText.HorizontalAlignment <- HorizontalAlignment.Right
        statusText.Opacity <- 0.8
        statusPanel.Children.Add(statusText)
        
        statusBar.Child <- statusPanel
        
        // Create root panel that contains everything
        let rootPanel = DockPanel()
        rootPanel.LastChildFill <- true
        
        // Status bar at the very bottom of window
        DockPanel.SetDock(statusBar, Dock.Bottom)
        rootPanel.Children.Add(statusBar)
        
        // Main content fills the rest
        rootPanel.Children.Add(acrylicBorder)
        
        rootPanel