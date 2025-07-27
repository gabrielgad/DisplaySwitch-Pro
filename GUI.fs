namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
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
        
        printfn "DEBUG: Creating content with displays:"
        for display in displays do
            printfn "  - %s at (%d, %d) enabled: %b" display.Id display.Position.X display.Position.Y display.IsEnabled
        
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
                textBox.SelectAll()
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
        infoPanel.Width <- 220.0
        infoPanel.Margin <- Thickness(15.0)
        infoPanel.Background <- SolidColorBrush(colors.Surface) :> IBrush
        
        let infoPanelBorder = Border()
        infoPanelBorder.Child <- infoPanel
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
        
        let displayList = UIComponents.createDisplayListView displays onDisplayToggle
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
        
        let themeToggleOverlay = Grid()
        themeToggleOverlay.HorizontalAlignment <- HorizontalAlignment.Right
        themeToggleOverlay.VerticalAlignment <- VerticalAlignment.Top
        themeToggleOverlay.Margin <- Thickness(0.0, 10.0, 10.0, 0.0)
        themeToggleOverlay.ZIndex <- 1000
        
        let themeToggleButton = Button()
        themeToggleButton.Content <- if Theme.currentTheme = Theme.Light then "🌙" else "☀️"
        themeToggleButton.Width <- 45.0
        themeToggleButton.Height <- 45.0
        themeToggleButton.CornerRadius <- CornerRadius(22.5)
        themeToggleButton.FontSize <- 18.0
        themeToggleButton.Opacity <- 0.9
        themeToggleButton.BorderThickness <- Thickness(1.0)
        
        let themeToggleGradient = RadialGradientBrush()
        themeToggleGradient.Center <- RelativePoint(0.3, 0.3, RelativeUnit.Relative)
        themeToggleGradient.Radius <- 1.0
        
        if Theme.currentTheme = Theme.Light then
            themeToggleGradient.GradientStops.Add(GradientStop(Color.FromArgb(200uy, 255uy, 255uy, 255uy), 0.0))
            themeToggleGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 240uy, 245uy, 250uy), 1.0))
        else
            themeToggleGradient.GradientStops.Add(GradientStop(Color.FromArgb(200uy, 60uy, 70uy, 85uy), 0.0))
            themeToggleGradient.GradientStops.Add(GradientStop(Color.FromArgb(160uy, 40uy, 50uy, 65uy), 1.0))
        
        themeToggleButton.Background <- themeToggleGradient :> IBrush
        themeToggleButton.BorderBrush <- SolidColorBrush(Color.FromArgb(150uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush
        themeToggleButton.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        ToolTip.SetTip(themeToggleButton, "Toggle theme")
        
        themeToggleOverlay.Children.Add(themeToggleButton)
        
        themeToggleButton.Click.Add(fun _ ->
            Theme.toggleTheme() |> ignore
            refreshMainWindowContent ()
        )
        
        let rootGrid = Grid()
        rootGrid.Children.Add(acrylicBorder)
        rootGrid.Children.Add(themeToggleOverlay)
        
        rootGrid
    
    let createMainWindow (world: World) (adapter: IPlatformAdapter) =
        let window = Window()
        window.Title <- "DisplaySwitch-Pro"
        window.Width <- 1200.0
        window.Height <- 700.0
        
        window.TransparencyLevelHint <- [WindowTransparencyLevel.AcrylicBlur; WindowTransparencyLevel.Blur]
        window.Background <- Brushes.Transparent
        window.ExtendClientAreaToDecorationsHint <- true
        
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
                desktop.MainWindow <- GUI.createMainWindow world adapter
            | _ -> failwith "Application data not set"
        | _ -> ()
        
        base.OnFrameworkInitializationCompleted()

module AppRunner =
    let run (adapter: IPlatformAdapter) (world: World) =
        App.SetData(world, adapter)
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime([||])