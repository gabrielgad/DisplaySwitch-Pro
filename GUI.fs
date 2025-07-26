namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Primitives
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Themes.Fluent

// Pure functional GUI creation - avoiding all OOP violations
module GUI =
    // Visual display representation
    type VisualDisplay = {
        Display: DisplayInfo
        Rectangle: Rectangle
        Border: Border
        Label: TextBlock
        EnableCheckBox: CheckBox
    }
    
    // Create a visual representation of a display
    let createVisualDisplay (display: DisplayInfo) (onPositionChanged: DisplayId -> float * float -> unit) =
        // Scale factor: 1 pixel = 10 screen pixels
        let scale = 0.1
        let width = float display.Resolution.Width * scale
        let height = float display.Resolution.Height * scale
        
        // Create display rectangle
        let rect = Rectangle()
        rect.Width <- width
        rect.Height <- height
        rect.Fill <- if display.IsEnabled then Brushes.LightBlue else Brushes.LightGray
        rect.Stroke <- Brushes.Black
        rect.StrokeThickness <- 2.0
        
        // Create label
        let label = TextBlock()
        label.Text <- sprintf "%s\n%dx%d" display.Name display.Resolution.Width display.Resolution.Height
        label.HorizontalAlignment <- HorizontalAlignment.Center
        label.VerticalAlignment <- VerticalAlignment.Center
        label.TextAlignment <- TextAlignment.Center
        
        // Create enable/disable checkbox
        let checkBox = CheckBox()
        checkBox.IsChecked <- System.Nullable<bool>(display.IsEnabled)
        checkBox.HorizontalAlignment <- HorizontalAlignment.Right
        checkBox.VerticalAlignment <- VerticalAlignment.Top
        checkBox.Margin <- Thickness(0.0, 5.0, 5.0, 0.0)
        
        // Create border container
        let border = Border()
        border.Width <- width
        border.Height <- height
        border.Background <- Brushes.Transparent
        border.Cursor <- new Cursor(StandardCursorType.Hand)
        
        // Add children to a grid
        let grid = Grid()
        grid.Children.Add(rect)
        grid.Children.Add(label)
        grid.Children.Add(checkBox)
        border.Child <- grid
        
        // Position on canvas
        Canvas.SetLeft(border, float display.Position.X * scale)
        Canvas.SetTop(border, float display.Position.Y * scale)
        
        // Note: Dragging is handled by the canvas for snapping support
        
        {
            Display = display
            Rectangle = rect
            Border = border
            Label = label
            EnableCheckBox = checkBox
        }
    
    // Create the display arrangement canvas
    let createDisplayCanvas (displays: DisplayInfo list) (onDisplayChanged: DisplayId -> DisplayInfo -> unit) =
        let canvas = Canvas()
        canvas.Background <- Brushes.WhiteSmoke
        canvas.Width <- 800.0
        canvas.Height <- 600.0
        
        // Grid-based snapping configuration
        let mutable snapEnabled = true
        let gridPixelSize = 50.0 // Fixed grid increment in GUI pixels (500 display pixels)
        let snapProximityThreshold = 25.0 // Distance within which edge snapping activates
        
        let snapToGrid value =
            if snapEnabled then
                Math.Round(value / gridPixelSize) * gridPixelSize
            else
                value
        
        let findNearbyDisplayEdges (movingDisplay: DisplayInfo) (targetPos: float * float) (allDisplays: DisplayInfo list) =
            if not snapEnabled then []
            else
                let (targetX, targetY) = targetPos
                let movingWidth = float movingDisplay.Resolution.Width * 0.1
                let movingHeight = float movingDisplay.Resolution.Height * 0.1
                
                let mutable bestSnapPoint = None
                let mutable bestSnapDistance = Double.MaxValue
                
                for display in allDisplays do
                    if display.Id <> movingDisplay.Id && display.IsEnabled then
                        let displayX = float display.Position.X * 0.1
                        let displayY = float display.Position.Y * 0.1
                        let displayWidth = float display.Resolution.Width * 0.1
                        let displayHeight = float display.Resolution.Height * 0.1
                        
                        // Focus on edge-to-edge snapping for proper alignment
                        let snapCandidates = [
                            // Horizontal edge alignment - displays side by side
                            (displayX + displayWidth, displayY)  // Move to right of display, aligned to top
                            (displayX - movingWidth, displayY)   // Move to left of display, aligned to top
                            (displayX + displayWidth, displayY + displayHeight - movingHeight)  // Right, aligned to bottom
                            (displayX - movingWidth, displayY + displayHeight - movingHeight)   // Left, aligned to bottom
                            
                            // Vertical edge alignment - displays stacked
                            (displayX, displayY + displayHeight) // Move below display, aligned to left
                            (displayX, displayY - movingHeight)  // Move above display, aligned to left  
                            (displayX + displayWidth - movingWidth, displayY + displayHeight) // Below, aligned to right
                            (displayX + displayWidth - movingWidth, displayY - movingHeight)  // Above, aligned to right
                            
                            // Corner-to-corner snapping for more complex arrangements
                            (displayX + displayWidth, displayY + displayHeight)  // Bottom-right corner
                            (displayX - movingWidth, displayY - movingHeight)     // Top-left corner
                        ]
                        
                        for (candidateX, candidateY) in snapCandidates do
                            let distanceX = Math.Abs(candidateX - targetX)
                            let distanceY = Math.Abs(candidateY - targetY)
                            let totalDistance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY)
                            
                            // Only snap if within reasonable proximity
                            if totalDistance <= snapProximityThreshold then
                                if totalDistance < bestSnapDistance then
                                    bestSnapDistance <- totalDistance
                                    bestSnapPoint <- Some (candidateX, candidateY)
                
                match bestSnapPoint with
                | Some point -> [point]
                | None -> []

        let checkCollision (movingDisplayId: DisplayId) (newX: float) (newY: float) (movingWidth: float) (movingHeight: float) (allDisplays: DisplayInfo list) =
            allDisplays
            |> List.exists (fun display ->
                if display.Id = movingDisplayId || not display.IsEnabled then false
                else
                    let displayX = float display.Position.X * 0.1
                    let displayY = float display.Position.Y * 0.1
                    let displayWidth = float display.Resolution.Width * 0.1
                    let displayHeight = float display.Resolution.Height * 0.1
                    
                    // Check if rectangles overlap
                    not (newX + movingWidth <= displayX || 
                         newX >= displayX + displayWidth ||
                         newY + movingHeight <= displayY ||
                         newY >= displayY + displayHeight))
        
        let applySnapAndCollision (movingDisplayId: DisplayId) (pos: float * float) (allDisplays: DisplayInfo list) (border: Border) =
            let (x, y) = pos
            let movingWidth = border.Width
            let movingHeight = border.Height
            
            let movingDisplay = allDisplays |> List.find (fun d -> d.Id = movingDisplayId)
            
            // First try snapping to nearby edges
            let snapPoints = findNearbyDisplayEdges movingDisplay (x, y) allDisplays
            
            let (candidateX, candidateY) = 
                if List.isEmpty snapPoints then
                    (snapToGrid x, snapToGrid y)
                else
                    List.head snapPoints
            
            // Check if snapped position causes collision
            if checkCollision movingDisplayId candidateX candidateY movingWidth movingHeight allDisplays then
                // If collision, try to find nearest non-colliding position
                let mutable testX = candidateX
                let mutable testY = candidateY
                let step = 10.0
                
                // Try moving in small increments to find valid position
                let mutable found = false
                let mutable attempts = 0
                
                while not found && attempts < 50 do
                    attempts <- attempts + 1
                    
                    // Try different directions
                    let positions = [
                        (testX + step, testY)
                        (testX - step, testY)
                        (testX, testY + step)
                        (testX, testY - step)
                        (testX + step, testY + step)
                        (testX - step, testY - step)
                    ]
                    
                    match positions |> List.tryFind (fun (px, py) -> 
                        px >= 0.0 && py >= 0.0 && 
                        px + movingWidth <= canvas.Width && 
                        py + movingHeight <= canvas.Height &&
                        not (checkCollision movingDisplayId px py movingWidth movingHeight allDisplays)) with
                    | Some (validX, validY) -> 
                        testX <- validX
                        testY <- validY
                        found <- true
                    | None -> 
                        testX <- testX + step
                        testY <- testY + step
                
                (testX, testY)
            else
                (candidateX, candidateY)

        
        // Grid lines based on snap grid size (50px = 500 display pixels)
        // Major grid lines every 50 pixels (snap grid)
        for i in 0..(int (canvas.Width / gridPixelSize)) do
            let lineV = Line()
            lineV.StartPoint <- Point(float i * gridPixelSize, 0.0)
            lineV.EndPoint <- Point(float i * gridPixelSize, canvas.Height)
            lineV.Stroke <- if i = 0 then Brushes.Gray else Brushes.LightGray
            lineV.StrokeThickness <- if i = 0 then 1.0 else 0.6
            canvas.Children.Add(lineV)
            
        for i in 0..(int (canvas.Height / gridPixelSize)) do
            let lineH = Line()
            lineH.StartPoint <- Point(0.0, float i * gridPixelSize)
            lineH.EndPoint <- Point(canvas.Width, float i * gridPixelSize)
            lineH.Stroke <- if i = 0 then Brushes.Gray else Brushes.LightGray
            lineH.StrokeThickness <- if i = 0 then 1.0 else 0.6
            canvas.Children.Add(lineH)
        
        // Enhanced grid lines with better visual feedback
        // No red snap guides needed - grid provides sufficient visual reference
        
        // Simple snap control
        let snapCheckBox = CheckBox()
        snapCheckBox.Content <- "Grid Snapping"
        snapCheckBox.IsChecked <- System.Nullable<bool>(snapEnabled)
        snapCheckBox.Margin <- Thickness(10.0)
        snapCheckBox.Background <- Brushes.White
        snapCheckBox.Opacity <- 0.9
        Canvas.SetLeft(snapCheckBox, 10.0)
        Canvas.SetTop(snapCheckBox, 10.0)
        snapCheckBox.ZIndex <- 1000
        snapCheckBox.IsCheckedChanged.Add(fun _ ->
            snapEnabled <- snapCheckBox.IsChecked.GetValueOrDefault()
        )
        canvas.Children.Add(snapCheckBox)
        
        // Track all visual displays for snapping
        let mutable visualDisplays = []
        
        // Add displays
        let onPositionChanged displayId (x, y) =
            let display = displays |> List.find (fun d -> d.Id = displayId)
            let snappedX = snapToGrid x
            let snappedY = snapToGrid y
            let updatedDisplay = { display with Position = { X = int snappedX; Y = int snappedY } }
            onDisplayChanged displayId updatedDisplay
        
        for display in displays do
            let mutable currentDisplay = display
            
            let onDragging (border: Border) (currentPos: float * float) =
                let (x, y) = currentPos
                
                // Allow free movement during drag - no snapping or constraints
                // Just ensure we don't go outside reasonable bounds
                let finalX = Math.Max(0.0, Math.Min(x, canvas.Width - border.Width))
                let finalY = Math.Max(0.0, Math.Min(y, canvas.Height - border.Height))
                
                Canvas.SetLeft(border, finalX)
                Canvas.SetTop(border, finalY)
                
            let onDragEnd displayId (border: Border) =
                let x = Canvas.GetLeft(border)
                let y = Canvas.GetTop(border)
                
                // Apply snapping and collision detection only on release
                let (snappedX, snappedY) = applySnapAndCollision displayId (x, y) displays border
                
                // Update visual position to snapped position
                Canvas.SetLeft(border, snappedX)
                Canvas.SetTop(border, snappedY)
                
                // Convert back to display coordinates and update data
                onPositionChanged displayId (snappedX / 0.1, snappedY / 0.1)
            
            // Create visual display with custom drag handling
            let visualDisplay = createVisualDisplay display (fun _ _ -> ())
            
            // Override drag handling for snapping
            let mutable isDragging = false
            let mutable dragStart = Point(0.0, 0.0)
            
            visualDisplay.Border.PointerPressed.Add(fun e ->
                if e.GetCurrentPoint(visualDisplay.Border).Properties.IsLeftButtonPressed then
                    isDragging <- true
                    dragStart <- e.GetPosition(canvas)
                    visualDisplay.Border.Opacity <- 0.7
            )
            
            visualDisplay.Border.PointerReleased.Add(fun e ->
                if isDragging then
                    isDragging <- false
                    visualDisplay.Border.Opacity <- 1.0
                    onDragEnd display.Id visualDisplay.Border
            )
            
            visualDisplay.Border.PointerMoved.Add(fun e ->
                if isDragging then
                    let currentPos = e.GetPosition(canvas)
                    let deltaX = currentPos.X - dragStart.X
                    let deltaY = currentPos.Y - dragStart.Y
                    
                    let newX = Canvas.GetLeft(visualDisplay.Border) + deltaX
                    let newY = Canvas.GetTop(visualDisplay.Border) + deltaY
                    
                    onDragging visualDisplay.Border (newX, newY)
                    dragStart <- currentPos
            )
            
            visualDisplays <- visualDisplay :: visualDisplays
            
            // Handle enable/disable
            visualDisplay.EnableCheckBox.IsCheckedChanged.Add(fun _ ->
                if visualDisplay.EnableCheckBox.IsChecked.GetValueOrDefault() then
                    let updatedDisplay = { display with IsEnabled = true }
                    visualDisplay.Rectangle.Fill <- Brushes.LightBlue
                    onDisplayChanged display.Id updatedDisplay
                else
                    let updatedDisplay = { display with IsEnabled = false }
                    visualDisplay.Rectangle.Fill <- Brushes.LightGray
                    onDisplayChanged display.Id updatedDisplay
            )
            
            canvas.Children.Add(visualDisplay.Border)
        
        let scrollViewer = ScrollViewer()
        scrollViewer.Content <- canvas
        scrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Auto
        scrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        scrollViewer
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
        window.Width <- 1200.0
        window.Height <- 700.0
        
        // Get current data
        let mutable currentWorld = world
        let displays = currentWorld.Components.ConnectedDisplays |> Map.values |> List.ofSeq
        let presets = PresetSystem.listPresets currentWorld
        
        // Handle display changes from canvas
        let onDisplayChanged displayId (updatedDisplay: DisplayInfo) =
            let updatedComponents = Components.addDisplay updatedDisplay currentWorld.Components
            currentWorld <- { currentWorld with Components = updatedComponents }
            printfn "Display %s moved to (%d, %d)" displayId updatedDisplay.Position.X updatedDisplay.Position.Y
        
        let onPresetClick (presetName: string) =
            if presetName = "SAVE_NEW" then
                let timestamp = DateTime.Now.ToString("HH:mm:ss")
                let name = sprintf "Config_%s" timestamp
                
                // Create configuration from current display states
                let currentDisplays = currentWorld.Components.ConnectedDisplays |> Map.values |> List.ofSeq
                let config = {
                    Displays = currentDisplays
                    Name = name
                    CreatedAt = DateTime.Now
                }
                currentWorld <- PresetSystem.saveCurrentAsPreset name currentWorld
                printfn "Saving preset: %s" name
            else
                currentWorld <- PresetSystem.loadPreset presetName currentWorld
                printfn "Loading preset: %s" presetName
        
        let mainPanel = DockPanel()
        mainPanel.LastChildFill <- true
        
        // Left side - display info
        let infoPanel = StackPanel()
        infoPanel.Orientation <- Orientation.Vertical
        infoPanel.Width <- 200.0
        infoPanel.Margin <- Thickness(10.0)
        
        let infoTitle = TextBlock()
        infoTitle.Text <- "Display Information"
        infoTitle.FontWeight <- FontWeight.Bold
        infoTitle.FontSize <- 14.0
        infoTitle.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        infoPanel.Children.Add(infoTitle)
        
        let displayList = createDisplayListView displays
        infoPanel.Children.Add(displayList)
        
        DockPanel.SetDock(infoPanel, Dock.Left)
        mainPanel.Children.Add(infoPanel)
        
        // Right side - presets
        let presetPanel = createPresetButtons presets onPresetClick
        presetPanel.Width <- 200.0
        DockPanel.SetDock(presetPanel, Dock.Right)
        mainPanel.Children.Add(presetPanel)
        
        // Center - visual canvas
        let canvasContainer = Border()
        canvasContainer.BorderBrush <- Brushes.DarkGray
        canvasContainer.BorderThickness <- Thickness(1.0)
        canvasContainer.Margin <- Thickness(5.0)
        
        let displayCanvas = createDisplayCanvas displays onDisplayChanged
        canvasContainer.Child <- displayCanvas
        
        mainPanel.Children.Add(canvasContainer)
        
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