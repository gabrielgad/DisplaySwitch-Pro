namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Themes.Fluent

// Pure functional GUI creation - avoiding all OOP violations
module GUI =
    let createDisplayListView (displays: DisplayInfo list) =
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(10.0)
        
        let titleText = TextBlock()
        titleText.Text <- "Connected Displays:"
        titleText.FontWeight <- FontWeight.Bold
        titleText.FontSize <- 16.0
        titleText.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        stackPanel.Children.Add(titleText)
        
        for display in displays do
            let displayText = TextBlock()
            displayText.Text <- sprintf "%s (%dx%d)" display.Name display.Resolution.Width display.Resolution.Height
            displayText.Margin <- Thickness(0.0, 0.0, 0.0, 5.0)
            stackPanel.Children.Add(displayText)
        
        stackPanel

    let createPresetButtons (presets: string list) (onPresetClick: string -> unit) =
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(10.0)
        
        let titleText = TextBlock()
        titleText.Text <- "Saved Presets:"
        titleText.FontWeight <- FontWeight.Bold
        titleText.FontSize <- 16.0
        titleText.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        stackPanel.Children.Add(titleText)
        
        for preset in presets do
            let button = Button()
            button.Content <- preset
            button.Margin <- Thickness(0.0, 0.0, 0.0, 5.0)
            button.HorizontalAlignment <- HorizontalAlignment.Stretch
            button.Click.Add(fun _ -> onPresetClick preset)
            stackPanel.Children.Add(button)
        
        let saveButton = Button()
        saveButton.Content <- "Save Current as Preset"
        saveButton.Margin <- Thickness(0.0, 10.0, 0.0, 0.0)
        saveButton.Background <- Brushes.LightBlue
        saveButton.Click.Add(fun _ -> onPresetClick "SAVE_NEW")
        stackPanel.Children.Add(saveButton)
        
        stackPanel

    let createMainWindow (world: World) (adapter: IPlatformAdapter) =
        let window = Window()
        window.Title <- "DisplaySwitch-Pro"
        window.Width <- 600.0
        window.Height <- 400.0
        
        // Get current data
        let displays = world.Components.ConnectedDisplays |> Map.values |> List.ofSeq
        let presets = PresetSystem.listPresets world
        
        let onPresetClick (presetName: string) =
            if presetName = "SAVE_NEW" then
                let timestamp = DateTime.Now.ToString("HH:mm:ss")
                let name = sprintf "Config_%s" timestamp
                printfn "Saving preset: %s" name
            else
                printfn "Loading preset: %s" presetName
        
        let mainPanel = DockPanel()
        mainPanel.LastChildFill <- true
        
        let displayPanel = createDisplayListView displays
        DockPanel.SetDock(displayPanel, Dock.Left)
        mainPanel.Children.Add(displayPanel)
        
        let presetPanel = createPresetButtons presets onPresetClick
        DockPanel.SetDock(presetPanel, Dock.Right)
        mainPanel.Children.Add(presetPanel)
        
        let statusText = TextBlock()
        statusText.Text <- "Ready"
        statusText.HorizontalAlignment <- HorizontalAlignment.Center
        statusText.VerticalAlignment <- VerticalAlignment.Center
        statusText.FontSize <- 14.0
        mainPanel.Children.Add(statusText)
        
        window.Content <- mainPanel
        window

// Simple Avalonia Application
type App() =
    inherit Application()
    
    // Store our data for the application
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

// Application runner  
module AppRunner =
    let run (adapter: IPlatformAdapter) (world: World) =
        App.SetData(world, adapter)
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime([||])