namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Themes.Fluent

module GUI =
    
    let mutable globalWorld = { Components = Components.empty; LastUpdate = DateTime.Now }
    let mutable globalAdapter: IPlatformAdapter option = None
    let mutable mainWindow: Window option = None
    
    let rec refreshMainWindowContent () =
        match mainWindow with
        | Some window ->
            match globalAdapter with
            | Some adapter ->
                let content = createMainContentPanel globalWorld adapter
                window.Content <- content
                printfn "UI refreshed in-place"
            | None -> ()
        | None -> ()
    
    and createMainContentPanel (world: World) (adapter: IPlatformAdapter) =
        let colors = Theme.getCurrentColors()
        
        globalWorld <- world
        globalAdapter <- Some adapter
        
        let mutable currentWorld = world
        let displays = currentWorld.Components.ConnectedDisplays |> Map.values |> List.ofSeq
        let presets = PresetSystem.listPresets currentWorld
        
        // Shared dialog window for all displays
        let mutable displaySettingsDialog: Window option = None
        
        printfn "DEBUG: Creating content with displays:"
        for display in displays do
            printfn "  - %s at (%d, %d) enabled: %b" display.Name display.Position.X display.Position.Y display.IsEnabled
        
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
            globalWorld <- currentWorld
            printfn "Display %s moved to (%d, %d)" displayId updatedDisplay.Position.X updatedDisplay.Position.Y
        
        let onPresetClick (presetName: string) =
            if presetName = "SAVE_NEW" then
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
                            currentWorld <- PresetSystem.saveCurrentAsPreset name currentWorld
                            globalWorld <- currentWorld
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
                        currentWorld <- PresetSystem.saveCurrentAsPreset name currentWorld
                        globalWorld <- currentWorld
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
                
                // Auto-focus and select text when dialog opens
                dialog.Opened.Add(fun _ ->
                    textBox.Focus() |> ignore
                    textBox.SelectAll()
                )
                
                match mainWindow with
                | Some parentWindow -> dialog.ShowDialog(parentWindow) |> ignore
                | None -> dialog.Show()
            else
                printfn "Debug: Loading preset %s" presetName
                printfn "Debug: Available presets: %A" (PresetSystem.listPresets currentWorld |> List.toArray)
                
                match Map.tryFind presetName currentWorld.Components.SavedPresets with
                | Some config ->
                    printfn "Debug: Found preset config with %d displays" config.Displays.Length
                    
                    currentWorld <- PresetSystem.loadPreset presetName currentWorld
                    
                    let mutable updatedComponents = currentWorld.Components
                    for display in config.Displays do
                        printfn "Debug: Setting display %s to position (%d, %d) and enabled: %b" display.Id display.Position.X display.Position.Y display.IsEnabled
                        updatedComponents <- Components.addDisplay display updatedComponents
                    
                    currentWorld <- { currentWorld with Components = updatedComponents }
                    globalWorld <- currentWorld
                    
                    printfn "Preset loaded - display data updated, refreshing UI"
                    refreshMainWindowContent ()
                    
                    printfn "Loading preset: %s completed" presetName
                | None ->
                    printfn "Debug: Preset %s not found!" presetName
        
        let mainPanel = DockPanel()
        mainPanel.LastChildFill <- true
        mainPanel.Margin <- Thickness(10.0)
        
        let acrylicBorder = Border()
        acrylicBorder.CornerRadius <- CornerRadius(12.0)
        acrylicBorder.Opacity <- 0.95
        
        let mainGradient = LinearGradientBrush()
        mainGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        mainGradient.EndPoint <- RelativePoint(1.0, 1.0, RelativeUnit.Relative)
        
        if Theme.currentTheme = Theme.Light then
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(240uy, 248uy, 250uy, 252uy), 0.0))
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(230uy, 241uy, 245uy, 249uy), 0.3))
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(220uy, 236uy, 240uy, 244uy), 0.7))
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(210uy, 229uy, 234uy, 240uy), 1.0))
        else
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(240uy, 17uy, 24uy, 39uy), 0.0))
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(230uy, 24uy, 32uy, 47uy), 0.3))
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(220uy, 31uy, 41uy, 55uy), 0.7))
            mainGradient.GradientStops.Add(GradientStop(Color.FromArgb(210uy, 15uy, 20uy, 35uy), 1.0))
        
        acrylicBorder.Background <- mainGradient :> IBrush
        acrylicBorder.BorderBrush <- SolidColorBrush(Color.FromArgb(100uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        acrylicBorder.BorderThickness <- Thickness(1.0)
        
        acrylicBorder.Child <- mainPanel
        
        let infoPanel = StackPanel()
        infoPanel.Orientation <- Orientation.Vertical
        infoPanel.Margin <- Thickness(15.0)
        infoPanel.Background <- SolidColorBrush(colors.Surface) :> IBrush
        
        // Wrap infoPanel in ScrollViewer for resizable content
        let infoScrollViewer = ScrollViewer()
        infoScrollViewer.Content <- infoPanel
        infoScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        infoScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        infoScrollViewer.Width <- 220.0
        
        let infoPanelBorder = Border()
        infoPanelBorder.Child <- infoScrollViewer
        infoPanelBorder.CornerRadius <- CornerRadius(12.0)
        infoPanelBorder.Opacity <- 0.9
        infoPanelBorder.BorderThickness <- Thickness(1.0)
        
        let infoPanelGradient = LinearGradientBrush()
        infoPanelGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        infoPanelGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        
        if Theme.currentTheme = Theme.Light then
            infoPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(200uy, 255uy, 255uy, 255uy), 0.0))
            infoPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 248uy, 250uy, 252uy), 1.0))
        else
            infoPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(200uy, 45uy, 55uy, 70uy), 0.0))
            infoPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 31uy, 41uy, 55uy), 1.0))
        
        infoPanelBorder.Background <- infoPanelGradient :> IBrush
        infoPanelBorder.BorderBrush <- SolidColorBrush(Color.FromArgb(120uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        
        let infoTitle = TextBlock()
        infoTitle.Text <- "📺 Display Information"
        infoTitle.FontWeight <- FontWeight.Bold
        infoTitle.FontSize <- 16.0
        infoTitle.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        infoTitle.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        infoPanel.Children.Add(infoTitle)
        
        let onDisplayToggle displayId isEnabled =
            let display = currentWorld.Components.ConnectedDisplays.[displayId]
            let updatedDisplay = { display with IsEnabled = isEnabled }
            let updatedComponents = Components.addDisplay updatedDisplay currentWorld.Components
            currentWorld <- { currentWorld with Components = updatedComponents }
            globalWorld <- currentWorld
            
            printfn "Display %s %s - updated in data model" displayId (if isEnabled then "enabled" else "disabled")
            refreshMainWindowContent ()
        
        // Function to update dialog content for a specific display
        let updateDialogForDisplay (dialog: Window) (display: DisplayInfo) =
            printfn "DEBUG: Updating dialog content for display: %s" display.Name
            dialog.Title <- sprintf "Display Settings - %s" display.Name
            
            // TODO: Update dialog content here when we implement the panels
            // For now, we'll recreate the content
            let colors = Theme.getCurrentColors()
            
            // Create modal handlers
            let onApplyMode (displayId: DisplayId) (mode: DisplayMode) =
                printfn "DEBUG: Would apply mode %dx%d @ %dHz to display %s" mode.Width mode.Height mode.RefreshRate displayId
                // TODO: Implement actual mode switching in Phase 3
            
            let onCloseDialog () =
                printfn "DEBUG: Dialog closed"
                displaySettingsDialog <- None
            
            // Recreate dialog content for now (will optimize later)
            let newDialog = UIComponents.createResolutionPickerDialog display onApplyMode onCloseDialog
            dialog.Content <- newDialog.Content
        
        // Handler for opening display settings dialog
        let rec onDisplaySettingsClick (display: DisplayInfo) =
            printfn "DEBUG: Opening settings for display: %s" display.Name
            
            match displaySettingsDialog with
            | Some existingDialog when not existingDialog.IsVisible ->
                // Dialog exists but was closed, create new one
                displaySettingsDialog <- None
                onDisplaySettingsClick display
            | Some existingDialog ->
                // Dialog exists and is visible, update its content
                updateDialogForDisplay existingDialog display
                existingDialog.Activate() // Bring to front
                printfn "DEBUG: Updated existing dialog"
            | None ->
                // No dialog exists, create new one
                let onApplyMode (displayId: DisplayId) (mode: DisplayMode) =
                    printfn "DEBUG: Would apply mode %dx%d @ %dHz to display %s" mode.Width mode.Height mode.RefreshRate displayId
                    // TODO: Implement actual mode switching in Phase 3
                
                let onCloseDialog () =
                    printfn "DEBUG: Dialog closed"
                    displaySettingsDialog <- None
                
                let dialogWindow = UIComponents.createResolutionPickerDialog display onApplyMode onCloseDialog
                displaySettingsDialog <- Some dialogWindow
                dialogWindow.Show()
                printfn "DEBUG: Created new dialog window"
        
        let displayList = UIComponents.createDisplayListView displays onDisplayToggle onDisplaySettingsClick
        infoPanel.Children.Add(displayList)
        
        DockPanel.SetDock(infoPanelBorder, Dock.Left)
        mainPanel.Children.Add(infoPanelBorder)
        
        let onPresetDelete presetName =
            let updatedPresets = Map.remove presetName currentWorld.Components.SavedPresets
            let updatedComponents = { currentWorld.Components with SavedPresets = updatedPresets }
            currentWorld <- { currentWorld with Components = updatedComponents }
            globalWorld <- currentWorld
            printfn "Deleted preset: %s" presetName
            refreshMainWindowContent ()
        
        let presetPanel = UIComponents.createPresetPanel presets onPresetClick onPresetDelete
        presetPanel.Width <- 240.0
        let presetPanelBorder = Border()
        presetPanelBorder.Child <- presetPanel
        presetPanelBorder.CornerRadius <- CornerRadius(12.0)
        presetPanelBorder.Opacity <- 0.9
        presetPanelBorder.BorderThickness <- Thickness(1.0)
        
        let presetPanelGradient = LinearGradientBrush()
        presetPanelGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        presetPanelGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        
        if Theme.currentTheme = Theme.Light then
            presetPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(200uy, 255uy, 255uy, 255uy), 0.0))
            presetPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 248uy, 250uy, 252uy), 1.0))
        else
            presetPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(200uy, 45uy, 55uy, 70uy), 0.0))
            presetPanelGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 31uy, 41uy, 55uy), 1.0))
        
        presetPanelBorder.Background <- presetPanelGradient :> IBrush
        presetPanelBorder.BorderBrush <- SolidColorBrush(Color.FromArgb(120uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        DockPanel.SetDock(presetPanelBorder, Dock.Right)
        mainPanel.Children.Add(presetPanelBorder)
        
        let canvasContainer = Border()
        canvasContainer.CornerRadius <- CornerRadius(12.0)
        canvasContainer.BorderThickness <- Thickness(1.0)
        canvasContainer.Opacity <- 0.95
        
        let canvasGradient = RadialGradientBrush()
        canvasGradient.Center <- RelativePoint(0.5, 0.5, RelativeUnit.Relative)
        canvasGradient.Radius <- 1.2
        
        if Theme.currentTheme = Theme.Light then
            canvasGradient.GradientStops.Add(GradientStop(Color.FromArgb(220uy, 200uy, 200uy, 200uy), 0.0))
            canvasGradient.GradientStops.Add(GradientStop(Color.FromArgb(180uy, 190uy, 190uy, 190uy), 0.6))
            canvasGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 180uy, 180uy, 180uy), 1.0))
        else
            canvasGradient.GradientStops.Add(GradientStop(Color.FromArgb(220uy, 30uy, 38uy, 52uy), 0.0))
            canvasGradient.GradientStops.Add(GradientStop(Color.FromArgb(180uy, 24uy, 32uy, 47uy), 0.6))
            canvasGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 17uy, 24uy, 39uy), 1.0))
        
        canvasContainer.Background <- canvasGradient :> IBrush
        canvasContainer.BorderBrush <- SolidColorBrush(Color.FromArgb(120uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        
        let displayCanvas = DisplayCanvas.createDisplayCanvas displays onDisplayChanged
        canvasContainer.Child <- displayCanvas
        
        mainPanel.Children.Add(canvasContainer)
        
        // Create status bar at bottom
        let statusBar = Border()
        statusBar.Height <- 35.0
        statusBar.CornerRadius <- CornerRadius(0.0, 0.0, 12.0, 12.0)
        statusBar.Background <- SolidColorBrush(Color.FromArgb(180uy, colors.Surface.R, colors.Surface.G, colors.Surface.B)) :> IBrush
        statusBar.BorderBrush <- SolidColorBrush(Color.FromArgb(100uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        statusBar.BorderThickness <- Thickness(0.0, 1.0, 0.0, 0.0)
        
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
    
    let createMainWindow (world: World) (adapter: IPlatformAdapter) =
        let window = Window()
        window.Title <- "DisplaySwitch-Pro"
        window.Width <- 1200.0
        window.Height <- 700.0
        
        window.TransparencyLevelHint <- [WindowTransparencyLevel.AcrylicBlur; WindowTransparencyLevel.Blur]
        window.Background <- Brushes.Transparent
        window.ExtendClientAreaToDecorationsHint <- false
        
        mainWindow <- Some window
        
        let content = createMainContentPanel world adapter
        window.Content <- content
        
        window

type App() =
    inherit Application()
    
    static let mutable worldData = None
    static let mutable adapterData = None
    
    static member SetData(world: World, adapter: IPlatformAdapter) =
        worldData <- Some world
        adapterData <- Some adapter
    
    override this.Initialize() =
        this.Styles.Add(FluentTheme())
    
    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            match worldData, adapterData with
            | Some world, Some adapter ->
                let window = GUI.createMainWindow world adapter
                desktop.MainWindow <- window
                window.Show()
                printfn "Window created and shown"
            | _ -> failwith "Application data not set"
        | _ -> 
            printfn "No desktop lifetime found"
        
        base.OnFrameworkInitializationCompleted()

module AppRunner =
    let run (adapter: IPlatformAdapter) (world: World) =
        App.SetData(world, adapter)
        try
            printfn "Starting Avalonia application..."
            let result = 
                AppBuilder
                    .Configure<App>()
                    .UsePlatformDetect()
                    .LogToTrace()
                    .StartWithClassicDesktopLifetime([||])
            printfn "Avalonia application finished with exit code: %d" result
            result
        with
        | ex -> 
            printfn "Error starting Avalonia: %s" ex.Message
            printfn "Stack trace: %s" ex.StackTrace
            1