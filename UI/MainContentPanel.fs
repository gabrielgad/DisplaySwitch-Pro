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
        | None -> Logging.logErrorf "WARNING: Refresh function not set"
    
    // Create preset save dialog
    let private createPresetSaveDialog (colors: Theme.ThemeColors) currentAppState =
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
        textBox.Background <- SolidColorBrush(colors.TextBoxBackground) :> IBrush
        textBox.Foreground <- SolidColorBrush(colors.TextBoxForeground) :> IBrush
        textBox.BorderBrush <- SolidColorBrush(colors.TextBoxBorder) :> IBrush
        textBox.CaretBrush <- SolidColorBrush(colors.TextBoxForeground) :> IBrush
        textBox.SelectionBrush <- SolidColorBrush(Color.FromArgb(100uy, colors.Primary.R, colors.Primary.G, colors.Primary.B)) :> IBrush
        textBox.SelectionForegroundBrush <- SolidColorBrush(colors.TextBoxBackground) :> IBrush
        
        // Add Enter key support to save preset
        textBox.KeyDown.Add(fun e ->
            if e.Key = Avalonia.Input.Key.Enter then
                let name = textBox.Text.Trim()
                if not (String.IsNullOrEmpty(name)) then
                    // Capture CURRENT Windows display state, not UI state
                    Logging.logVerbosef " Saving preset '%s' - capturing current Windows configuration..." name
                    let currentWindowsConfig = PresetManager.getCurrentConfiguration()
                    let namedConfig = { currentWindowsConfig with Name = name; CreatedAt = DateTime.Now }
                    
                    Logging.logVerbosef " Captured configuration with %d displays, hash: %s" 
                        namedConfig.Displays.Length (namedConfig.ConfigurationHash |> Option.defaultValue "None")
                    
                    let updatedAppState = AppState.savePreset name namedConfig currentAppState
                    UIState.updateAppState updatedAppState
                    
                    // Save to disk
                    match PresetManager.savePresetsToDisk updatedAppState.SavedPresets with
                    | Ok () -> 
                        Logging.logNormalf "Preset saved successfully: %s" name
                        dialog.Close()
                        refreshMainWindowContent ()
                    | Error err -> 
                        Logging.logErrorf "Failed to save preset to disk: %s" err
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
                // Capture CURRENT Windows display state, not UI state
                Logging.logVerbosef " Saving preset '%s' - capturing current Windows configuration..." name
                let currentWindowsConfig = PresetManager.getCurrentConfiguration()
                let namedConfig = { currentWindowsConfig with Name = name; CreatedAt = DateTime.Now }
                
                Logging.logVerbosef " Captured configuration with %d displays, hash: %s" 
                    namedConfig.Displays.Length (namedConfig.ConfigurationHash |> Option.defaultValue "None")
                
                let updatedAppState = AppState.savePreset name namedConfig currentAppState
                UIState.updateAppState updatedAppState
                
                // Save to disk
                match PresetManager.savePresetsToDisk updatedAppState.SavedPresets with
                | Ok () -> 
                    Logging.logNormalf "Preset saved successfully: %s" name
                    dialog.Close()
                    refreshMainWindowContent ()
                | Error err -> 
                    Logging.logErrorf "Failed to save preset to disk: %s" err
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
    let createMainContentPanel (appState: AppState) (adapter: IPlatformAdapter) =
        let colors = Theme.getCurrentColors()
        
        // Update global state
        UIState.updateAppState appState
        UIState.updateAdapter adapter
        
        let currentAppStateRef = ref appState
        let displays = appState.ConnectedDisplays |> Map.values |> List.ofSeq
        let presets = AppState.listPresets appState
        
        Logging.logVerbose "Creating content with displays:"
        displays |> List.iter (fun display ->
            Logging.logVerbosef "  - %s at (%d, %d) enabled: %b" display.Name display.Position.X display.Position.Y display.IsEnabled)
        
        // Update display position during drag (no compacting)
        let onDisplayPositionUpdate displayId (updatedDisplay: DisplayInfo) =
            let currentAppState = !currentAppStateRef
            let updatedAppState = AppState.addDisplay updatedDisplay currentAppState
            
            let allDisplays = updatedAppState.ConnectedDisplays |> Map.values |> List.ofSeq
            let newConfig = DisplayHelpers.createDisplayConfiguration "Current" allDisplays
            let finalAppState = AppState.setCurrentConfiguration newConfig updatedAppState
            
            currentAppStateRef := finalAppState
            UIState.updateAppState finalAppState
        
        // Handle drag completion with compacting
        let onDisplayDragComplete displayId (updatedDisplay: DisplayInfo) =
            let currentAppState = !currentAppStateRef
            let updatedAppState = AppState.addDisplay updatedDisplay currentAppState
            
            let allDisplays = updatedAppState.ConnectedDisplays |> Map.values |> List.ofSeq
            Logging.logVerbosef "Display %s drag completed at (%d, %d)" displayId updatedDisplay.Position.X updatedDisplay.Position.Y
            
            // Apply compacting to show final positions
            let enabledDisplays = allDisplays |> List.filter (fun d -> d.IsEnabled)
            if enabledDisplays.Length > 1 then
                Logging.logVerbosef " Applying compacting after drag completion"
                
                // Only compact if there are multiple displays to arrange
                let displayPositions = enabledDisplays |> List.map (fun d -> (d.Id, d.Position, d))
                let compactedPositions = DisplayControl.compactDisplayPositions displayPositions
                
                Logging.logVerbosef " Compacted positions after drag completion:"
                compactedPositions |> List.iter (fun (id, newPos, _) ->
                    Logging.logVerbosef "   %s: (%d, %d)" id newPos.X newPos.Y)
                
                // Update displays with compacted positions
                let compactedDisplays = 
                    compactedPositions 
                    |> List.map (fun (id, newPos, info) -> 
                        { info with Position = newPos })
                
                // Create updated app state with compacted positions
                let compactedAppState = 
                    (currentAppState, compactedDisplays) ||> List.fold (fun state display -> AppState.addDisplay display state)
                
                let allFinalDisplays = compactedDisplays @ (allDisplays |> List.filter (fun d -> not d.IsEnabled))
                let newConfig = DisplayHelpers.createDisplayConfiguration "Current" allFinalDisplays
                let finalAppState = AppState.setCurrentConfiguration newConfig compactedAppState
                
                currentAppStateRef := finalAppState
                UIState.updateAppState finalAppState
                
                // Refresh the UI with updated display positions (without re-detecting from Windows)
                printfn "[DEBUG GUI] UI refreshed with %d displays" (List.length compactedDisplays)
            else
                // Single display or no enabled displays - no compacting needed
                let newConfig = DisplayHelpers.createDisplayConfiguration "Current" allDisplays
                let finalAppState = AppState.setCurrentConfiguration newConfig updatedAppState
                
                currentAppStateRef := finalAppState
                UIState.updateAppState finalAppState
        
        
        // Preset click handler
        let onPresetClick (presetName: string) =
            if presetName = "SAVE_NEW" then
                let dialog = createPresetSaveDialog colors (!currentAppStateRef)
                dialog.ShowDialog(UIState.getMainWindow() |> Option.defaultValue null) |> ignore
            else
                // Load existing preset
                Logging.logNormalf "Loading preset: %s" presetName
                
                match AppState.getPreset presetName (!currentAppStateRef) with
                | Some preset ->
                    // Validate preset can be applied
                    match PresetManager.validatePreset preset with
                    | Ok () ->
                        Logging.logNormalf "Applying preset: %s" preset.Name
                        match PresetManager.applyPreset preset with
                        | Ok () -> 
                            Logging.logNormalf "Successfully applied preset %s" preset.Name
                            
                            // Get actual current Windows display state after preset application
                            let actualCurrentConfig = PresetManager.getCurrentConfiguration()
                            let updatedAppState = AppState.setCurrentConfiguration actualCurrentConfig (!currentAppStateRef)
                            currentAppStateRef := updatedAppState
                            UIState.updateAppState updatedAppState
                            
                            // Add debug logging to show what's happening
                            printfn "[DEBUG GUI] Refreshing display information..."
                            refreshMainWindowContent ()
                            
                        | Error err -> 
                            Logging.logErrorf "Failed to apply preset %s: %s" preset.Name err
                            
                    | Error err ->
                        Logging.logErrorf "Cannot apply preset %s: %s" preset.Name err
                            
                | None ->
                    Logging.logErrorf "Preset %s not found!" presetName

        // Create the display canvas
        let displayCanvas = DisplayCanvas.createDisplayCanvas displays onDisplayPositionUpdate onDisplayDragComplete
        
        // Display canvas is already a ScrollViewer from DisplayCanvas.createDisplayCanvas
        displayCanvas.Background <- SolidColorBrush(colors.Background) :> IBrush
        
        // Display toggle handler - now functional
        let onDisplayToggle (displayId: DisplayId) (enabled: bool) =
            Logging.logVerbosef "Toggle display %s to %b" displayId enabled
            let currentAppState = !currentAppStateRef
            match Map.tryFind displayId (!currentAppStateRef).ConnectedDisplays with
            | Some display ->
                let updatedDisplay = { display with IsEnabled = enabled }
                let updatedAppState = AppState.addDisplay updatedDisplay (!currentAppStateRef)
                currentAppStateRef := updatedAppState
                UIState.updateAppState updatedAppState
                
                // Apply to physical display
                match WindowsDisplaySystem.setDisplayEnabled displayId enabled with
                | Ok () -> 
                    Logging.logNormalf "Successfully toggled display %s" displayId
                    refreshMainWindowContent ()
                | Error err -> 
                    Logging.logErrorf "Failed to toggle display %s: %s" displayId err
            | None -> 
                Logging.logErrorf "Display %s not found" displayId

        // Display settings handler
        let onSettingsClick (display: DisplayInfo) =
            Logging.logVerbosef "Opening settings for display: %s" display.Name
            let onApply = fun displayId mode orientation isPrimary ->
                Logging.logVerbosef "Applying settings: %dx%d@%dHz, orientation: %A, primary: %b" 
                        mode.Width mode.Height mode.RefreshRate orientation isPrimary
                
                // Apply the settings
                match WindowsDisplaySystem.applyDisplayMode displayId mode orientation with
                | Ok () ->
                    if isPrimary then
                        match WindowsDisplaySystem.setPrimaryDisplay displayId with
                        | Ok () -> Logging.logNormal "Successfully set as primary"
                        | Error err -> Logging.logErrorf "Failed to set as primary: %s" err
                    
                    // Update the display info and refresh UI
                    match Map.tryFind displayId (!currentAppStateRef).ConnectedDisplays with
                    | Some existingDisplay ->
                        let updatedDisplay = { 
                            existingDisplay with 
                                Resolution = { Width = mode.Width; Height = mode.Height; RefreshRate = mode.RefreshRate }
                                Orientation = orientation
                                IsPrimary = isPrimary 
                        }
                        let updatedAppState = AppState.addDisplay updatedDisplay (!currentAppStateRef)
                        currentAppStateRef := updatedAppState
                        UIState.updateAppState updatedAppState
                        refreshMainWindowContent ()
                    | None -> ()
                | Error err ->
                    Logging.logErrorf "Failed to apply display mode: %s" err
            
            let onClose = fun () -> ()
            let dialog = UIComponents.createResolutionPickerDialog display onApply onClose
                
            match UIState.getMainWindow() with
            | Some mainWindow -> dialog.ShowDialog(mainWindow) |> ignore
            | None -> ()

        // Create display list view with handlers
        let displayListView = UIComponents.createDisplayListView displays onDisplayToggle onSettingsClick
        
        // Create preset panel with delete handler
        let onPresetDelete (presetName: string) = 
            Logging.logNormalf "Deleting preset: %s" presetName
            let currentAppState = !currentAppStateRef
            match PresetManager.deletePreset presetName currentAppState.SavedPresets with
            | Ok updatedPresets ->
                let updatedAppState = AppState.deletePreset presetName currentAppState
                let finalAppState = { updatedAppState with SavedPresets = updatedPresets }
                currentAppStateRef := finalAppState
                UIState.updateAppState finalAppState
                Logging.logNormalf "Successfully deleted preset: %s" presetName
                refreshMainWindowContent()
            | Error err ->
                Logging.logErrorf "Failed to delete preset %s: %s" presetName err
        let presetPanel = UIComponents.createPresetPanel presets onPresetClick onPresetDelete
        
        // Split the UI: 3 columns - display info (left), display canvas (center), presets (right)
        let leftColumn = ColumnDefinition()
        leftColumn.Width <- GridLength(320.0, GridUnitType.Pixel)
        
        let centerColumn = ColumnDefinition()
        centerColumn.Width <- GridLength(1.0, GridUnitType.Star)
        
        let rightColumn = ColumnDefinition() 
        rightColumn.Width <- GridLength(300.0, GridUnitType.Pixel)
        
        let mainGrid = Grid()
        mainGrid.ColumnDefinitions.Add(leftColumn)
        mainGrid.ColumnDefinitions.Add(centerColumn)
        mainGrid.ColumnDefinitions.Add(rightColumn)
        mainGrid.Background <- SolidColorBrush(colors.Background) :> IBrush
        
        // LEFT COLUMN: Display Info with header and scrollable content
        let leftContainer = Border()
        leftContainer.Background <- SolidColorBrush(colors.Surface) :> IBrush
        leftContainer.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        leftContainer.BorderThickness <- Thickness(0.0, 0.0, 1.0, 0.0)
        leftContainer.CornerRadius <- CornerRadius(12.0, 0.0, 0.0, 12.0)
        leftContainer.Margin <- Thickness(10.0, 10.0, 0.0, 10.0)
        
        let leftContent = DockPanel()
        leftContent.LastChildFill <- true
        
        // Left header
        let leftHeader = Border()
        leftHeader.Background <- SolidColorBrush(colors.PrimaryDark) :> IBrush
        leftHeader.Height <- 45.0
        leftHeader.Padding <- Thickness(15.0, 12.0)
        leftHeader.CornerRadius <- CornerRadius(12.0, 0.0, 0.0, 0.0)
        DockPanel.SetDock(leftHeader, Dock.Top)
        
        let leftHeaderText = TextBlock()
        leftHeaderText.Text <- "Display Information"
        leftHeaderText.FontSize <- 14.0
        leftHeaderText.FontWeight <- FontWeight.SemiBold
        leftHeaderText.Foreground <- Brushes.White
        leftHeaderText.VerticalAlignment <- VerticalAlignment.Center
        leftHeader.Child <- leftHeaderText
        leftContent.Children.Add(leftHeader)
        
        // Left scrollable content
        let leftScrollViewer = ScrollViewer()
        leftScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        leftScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        leftScrollViewer.Content <- displayListView
        leftContent.Children.Add(leftScrollViewer)
        
        leftContainer.Child <- leftContent
        Grid.SetColumn(leftContainer, 0)
        mainGrid.Children.Add(leftContainer)
        
        // CENTER COLUMN: Display Canvas with header
        let centerContainer = Border()
        centerContainer.Background <- SolidColorBrush(colors.Background) :> IBrush
        centerContainer.Margin <- Thickness(0.0, 10.0, 0.0, 10.0)
        
        let centerContent = DockPanel()
        centerContent.LastChildFill <- true
        
        // Center header
        let centerHeader = Border()
        centerHeader.Background <- SolidColorBrush(colors.Primary) :> IBrush
        centerHeader.Height <- 45.0
        centerHeader.Padding <- Thickness(15.0, 12.0)
        DockPanel.SetDock(centerHeader, Dock.Top)
        
        let centerHeaderText = TextBlock()
        centerHeaderText.Text <- "Display Layout"
        centerHeaderText.FontSize <- 14.0
        centerHeaderText.FontWeight <- FontWeight.SemiBold
        centerHeaderText.Foreground <- Brushes.White
        centerHeaderText.VerticalAlignment <- VerticalAlignment.Center
        centerHeader.Child <- centerHeaderText
        centerContent.Children.Add(centerHeader)
        
        // Apply button for position changes
        let applyPositionButton = Border()
        applyPositionButton.Background <- SolidColorBrush(colors.Primary) :> IBrush
        applyPositionButton.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        applyPositionButton.BorderThickness <- Thickness(1.0)
        applyPositionButton.CornerRadius <- CornerRadius(6.0)
        applyPositionButton.Padding <- Thickness(15.0, 8.0)
        applyPositionButton.Margin <- Thickness(10.0, 5.0, 10.0, 5.0)
        applyPositionButton.Cursor <- new Cursor(StandardCursorType.Hand)
        applyPositionButton.HorizontalAlignment <- HorizontalAlignment.Center
        DockPanel.SetDock(applyPositionButton, Dock.Top)
        
        let applyButtonText = TextBlock()
        applyButtonText.Text <- "Apply Position Changes"
        applyButtonText.FontSize <- 13.0
        applyButtonText.FontWeight <- FontWeight.Medium
        applyButtonText.Foreground <- Brushes.White
        applyButtonText.HorizontalAlignment <- HorizontalAlignment.Center
        applyButtonText.VerticalAlignment <- VerticalAlignment.Center
        applyPositionButton.Child <- applyButtonText
        
        // Apply button click handler
        applyPositionButton.PointerPressed.Add(fun _ ->
            Logging.logNormal "Applying display position changes to Windows..."
            
            // Get current display positions from canvas
            let currentDisplays = (!currentAppStateRef).ConnectedDisplays |> Map.values |> List.ofSeq
            let enabledDisplays = currentDisplays |> List.filter (fun d -> d.IsEnabled)
            let displayPositions = enabledDisplays |> List.map (fun d -> (d.Id, d.Position))
            
            if not displayPositions.IsEmpty then
                match DisplayControl.applyMultipleDisplayPositions displayPositions with
                | Ok () -> 
                    Logging.logNormal "Successfully applied all position changes"
                    applyButtonText.Text <- "✓ Applied"
                    applyPositionButton.Background <- SolidColorBrush(colors.Success) :> IBrush
                    
                    // Reset button text after 2 seconds
                    let timer = new System.Threading.Timer((fun _ ->
                        Dispatcher.UIThread.InvokeAsync(fun () ->
                            applyButtonText.Text <- "Apply Position Changes"
                            applyPositionButton.Background <- SolidColorBrush(colors.Primary) :> IBrush
                        ) |> ignore
                    ), null, 2000, System.Threading.Timeout.Infinite) // UI refresh timer
                    ()
                    
                | Error err -> 
                    Logging.logErrorf "Failed to apply position changes: %s" err
                    applyButtonText.Text <- "✗ Failed"
                    applyPositionButton.Background <- SolidColorBrush(colors.Error) :> IBrush
                    
                    // Reset button text after 3 seconds
                    let timer = new System.Threading.Timer((fun _ ->
                        Dispatcher.UIThread.InvokeAsync(fun () ->
                            applyButtonText.Text <- "Apply Position Changes"
                            applyPositionButton.Background <- SolidColorBrush(colors.Primary) :> IBrush
                        ) |> ignore
                    ), null, 3000, System.Threading.Timeout.Infinite) // Preset apply feedback timer
                    ()
            else
                Logging.logNormal "No enabled displays to apply positions for"
        )
        
        centerContent.Children.Add(applyPositionButton)
        
        // Center canvas content
        centerContent.Children.Add(displayCanvas)
        centerContainer.Child <- centerContent
        Grid.SetColumn(centerContainer, 1)
        mainGrid.Children.Add(centerContainer)
        
        // RIGHT COLUMN: Presets with header and scrollable content
        let rightContainer = Border()
        rightContainer.Background <- SolidColorBrush(colors.Surface) :> IBrush
        rightContainer.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        rightContainer.BorderThickness <- Thickness(1.0, 0.0, 0.0, 0.0)
        rightContainer.CornerRadius <- CornerRadius(0.0, 12.0, 12.0, 0.0)
        rightContainer.Margin <- Thickness(0.0, 10.0, 10.0, 10.0)
        
        let rightContent = DockPanel()
        rightContent.LastChildFill <- true
        
        // Right header
        let rightHeader = Border()
        rightHeader.Background <- SolidColorBrush(colors.Secondary) :> IBrush
        rightHeader.Height <- 45.0
        rightHeader.Padding <- Thickness(15.0, 12.0)
        rightHeader.CornerRadius <- CornerRadius(0.0, 12.0, 0.0, 0.0)
        DockPanel.SetDock(rightHeader, Dock.Top)
        
        let rightHeaderText = TextBlock()
        rightHeaderText.Text <- "Presets"
        rightHeaderText.FontSize <- 14.0
        rightHeaderText.FontWeight <- FontWeight.SemiBold
        rightHeaderText.Foreground <- Brushes.White
        rightHeaderText.VerticalAlignment <- VerticalAlignment.Center
        rightHeader.Child <- rightHeaderText
        rightContent.Children.Add(rightHeader)
        
        // Right scrollable content
        let rightScrollViewer = ScrollViewer()
        rightScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        rightScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        rightScrollViewer.Content <- presetPanel
        rightContent.Children.Add(rightScrollViewer)
        
        rightContainer.Child <- rightContent
        Grid.SetColumn(rightContainer, 2)
        mainGrid.Children.Add(rightContainer)
        
        // Acrylic background effect with modern styling
        let acrylicBorder = Border()
        acrylicBorder.Background <- SolidColorBrush(colors.Background) :> IBrush
        acrylicBorder.CornerRadius <- CornerRadius(12.0)
        acrylicBorder.BorderBrush <- SolidColorBrush(Color.FromArgb(60uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        acrylicBorder.BorderThickness <- Thickness(1.0)
        acrylicBorder.Margin <- Thickness(8.0)
        acrylicBorder.Child <- mainGrid
        
        // Status bar at bottom
        let statusBar = Border()
        statusBar.Height <- 35.0
        statusBar.Background <- SolidColorBrush(Color.FromArgb(90uy, colors.Surface.R, colors.Surface.G, colors.Surface.B)) :> IBrush
        statusBar.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        statusBar.BorderThickness <- Thickness(0.0, 1.0, 0.0, 0.0)
        statusBar.CornerRadius <- CornerRadius(0.0, 0.0, 12.0, 12.0)
        
        let statusPanel = DockPanel()
        statusPanel.LastChildFill <- true
        statusPanel.Margin <- Thickness(10.0, 5.0, 10.0, 5.0)
        
        // Theme toggle button (left side)
        let themeToggleButton = Button()
        themeToggleButton.Content <- if Theme.currentTheme = Theme.Light then "🌙 Dark" else "☀️ Light"
        themeToggleButton.Width <- 80.0
        themeToggleButton.Height <- 25.0
        themeToggleButton.CornerRadius <- CornerRadius(12.0)
        themeToggleButton.FontSize <- 11.0
        themeToggleButton.Background <- SolidColorBrush(colors.Secondary) :> IBrush
        themeToggleButton.Foreground <- Brushes.White
        themeToggleButton.BorderThickness <- Thickness(0.0)
        ToolTip.SetTip(themeToggleButton, "Toggle between light and dark theme")
        themeToggleButton.Click.Add(fun _ ->
            Theme.toggleTheme() |> ignore
            refreshMainWindowContent ()
        )
        DockPanel.SetDock(themeToggleButton, Dock.Left)
        statusPanel.Children.Add(themeToggleButton)
        
        // App name/version (left-center)
        let appInfo = TextBlock()
        appInfo.Text <- "DisplaySwitch-Pro v1.0"
        appInfo.FontSize <- 11.0
        appInfo.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        appInfo.VerticalAlignment <- VerticalAlignment.Center
        appInfo.HorizontalAlignment <- HorizontalAlignment.Left
        appInfo.Opacity <- 0.6
        appInfo.Margin <- Thickness(10.0, 0.0, 0.0, 0.0)
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