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
        
        // Create modern styled display rectangle with gradient and rounded corners
        let rect = Rectangle()
        rect.Width <- width
        rect.Height <- height
        
        // Modern gradient colors
        let enabledGradient = LinearGradientBrush()
        enabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        enabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        enabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(100uy, 149uy, 237uy), 0.0))  // Cornflower blue
        enabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(65uy, 105uy, 225uy), 1.0))   // Royal blue
        
        let disabledGradient = LinearGradientBrush()
        disabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        disabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        disabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(220uy, 220uy, 220uy), 0.0))  // Light gray
        disabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(180uy, 180uy, 180uy), 1.0))  // Darker gray
        
        rect.Fill <- if display.IsEnabled then enabledGradient :> IBrush else disabledGradient :> IBrush
        rect.Stroke <- SolidColorBrush(Color.FromRgb(40uy, 40uy, 40uy)) :> IBrush  // Dark charcoal border
        rect.StrokeThickness <- 1.5
        rect.RadiusX <- 8.0  // Rounded corners
        rect.RadiusY <- 8.0
        
        // Create modern styled label with better typography
        let label = TextBlock()
        label.Text <- sprintf "%s\n%dx%d" display.Name display.Resolution.Width display.Resolution.Height
        label.HorizontalAlignment <- HorizontalAlignment.Center
        label.VerticalAlignment <- VerticalAlignment.Center
        label.TextAlignment <- TextAlignment.Center
        label.Foreground <- Brushes.White
        label.FontWeight <- FontWeight.SemiBold
        label.FontSize <- if width > 100.0 then 12.0 else 10.0
        
        // Add subtle text shadow effect
        let textShadow = TextBlock()
        textShadow.Text <- label.Text
        textShadow.HorizontalAlignment <- label.HorizontalAlignment
        textShadow.VerticalAlignment <- label.VerticalAlignment
        textShadow.TextAlignment <- label.TextAlignment
        textShadow.Foreground <- SolidColorBrush(Color.FromArgb(100uy, 0uy, 0uy, 0uy)) :> IBrush  // Semi-transparent black
        textShadow.FontWeight <- label.FontWeight
        textShadow.FontSize <- label.FontSize
        textShadow.Margin <- Thickness(1.0, 1.0, 0.0, 0.0)  // Offset for shadow effect
        
        // Create modern styled enable/disable checkbox
        let checkBox = CheckBox()
        checkBox.IsChecked <- System.Nullable<bool>(display.IsEnabled)
        checkBox.HorizontalAlignment <- HorizontalAlignment.Right
        checkBox.VerticalAlignment <- VerticalAlignment.Top
        checkBox.Margin <- Thickness(0.0, 8.0, 8.0, 0.0)
        checkBox.Background <- SolidColorBrush(Color.FromArgb(200uy, 255uy, 255uy, 255uy)) :> IBrush  // Semi-transparent white
        checkBox.BorderBrush <- SolidColorBrush(Color.FromRgb(100uy, 100uy, 100uy)) :> IBrush
        
        // Create border container
        let border = Border()
        border.Width <- width
        border.Height <- height
        border.Background <- Brushes.Transparent
        border.Cursor <- new Cursor(StandardCursorType.Hand)
        
        // Add children to a grid with shadow effect
        let grid = Grid()
        grid.Children.Add(rect)
        grid.Children.Add(textShadow)  // Add shadow first
        grid.Children.Add(label)       // Then actual text on top
        grid.Children.Add(checkBox)
        
        // Modern styling without shadows (Effects not available)
        
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
        
        // Modern gradient background
        let canvasGradient = LinearGradientBrush()
        canvasGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        canvasGradient.EndPoint <- RelativePoint(1.0, 1.0, RelativeUnit.Relative)
        canvasGradient.GradientStops.Add(GradientStop(Color.FromRgb(245uy, 248uy, 250uy), 0.0))  // Very light blue-gray
        canvasGradient.GradientStops.Add(GradientStop(Color.FromRgb(230uy, 235uy, 240uy), 1.0))  // Slightly darker blue-gray
        
        canvas.Background <- canvasGradient :> IBrush
        canvas.Width <- 800.0
        canvas.Height <- 600.0
        
        // Adaptive display-edge grid configuration
        let intermediateGridSize = 20.0 // Fine positioning grid (200 display pixels)
        let snapProximityThreshold = 25.0 // Distance within which edge snapping activates
        
        // Calculate all display edge positions for natural grid lines
        let calculateDisplayEdges (allDisplays: DisplayInfo list) =
            let scale = 0.1
            let edges = 
                allDisplays
                |> List.filter (fun d -> d.IsEnabled)
                |> List.collect (fun display ->
                    let x = float display.Position.X * scale
                    let y = float display.Position.Y * scale
                    let width = float display.Resolution.Width * scale
                    let height = float display.Resolution.Height * scale
                    [
                        ("vertical", x)           // Left edge
                        ("vertical", x + width)   // Right edge
                        ("horizontal", y)         // Top edge
                        ("horizontal", y + height) // Bottom edge
                    ])
                |> List.distinct
                |> List.sort
            edges
        
        let snapToDisplayEdgeOrGrid (allDisplays: DisplayInfo list) value isVertical =
            let edges = calculateDisplayEdges allDisplays
            let relevantEdges = 
                edges 
                |> List.filter (fun (orientation, pos) -> 
                    (isVertical && orientation = "vertical") || 
                    (not isVertical && orientation = "horizontal"))
                |> List.map snd
            
            // First try to snap to display edges (priority)
            let edgeSnap = 
                relevantEdges
                |> List.tryFind (fun edge -> Math.Abs(edge - value) <= snapProximityThreshold)
            
            match edgeSnap with
            | Some edge -> edge
            | None -> 
                // Fall back to intermediate grid
                Math.Round(value / intermediateGridSize) * intermediateGridSize
        
        let findNearbyDisplayEdges (movingDisplay: DisplayInfo) (targetPos: float * float) (allDisplays: DisplayInfo list) =
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
                    
                    // Edge-to-edge snapping for seamless navigation (0px gaps)
                    let snapCandidates = [
                        // === HORIZONTAL ALIGNMENT (side by side, touching) ===
                        // Right side attachment - exact edge touching
                        (displayX + displayWidth, displayY)  // Right, top-aligned
                        (displayX + displayWidth, displayY + displayHeight - movingHeight)  // Right, bottom-aligned
                        
                        // Left side attachment - exact edge touching
                        (displayX - movingWidth, displayY)   // Left, top-aligned
                        (displayX - movingWidth, displayY + displayHeight - movingHeight)   // Left, bottom-aligned
                        
                        // === VERTICAL ALIGNMENT (stacked, touching) ===
                        // Below attachment - exact edge touching
                        (displayX, displayY + displayHeight) // Below, left-aligned
                        (displayX + displayWidth - movingWidth, displayY + displayHeight) // Below, right-aligned
                        
                        // Above attachment - exact edge touching
                        (displayX, displayY - movingHeight)  // Above, left-aligned  
                        (displayX + displayWidth - movingWidth, displayY - movingHeight)  // Above, right-aligned
                        
                        // === EDGE ALIGNMENT (same edge positions) ===
                        // Align to same left edge
                        (displayX, displayY)  // Top-left corner match
                        (displayX, displayY + displayHeight - movingHeight)  // Bottom-left corner match
                        
                        // Align to same right edge
                        (displayX + displayWidth - movingWidth, displayY)  // Top-right corner match
                        (displayX + displayWidth - movingWidth, displayY + displayHeight - movingHeight)  // Bottom-right corner match
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
                    
                    // Check if rectangles overlap (strict check - no touching allowed)
                    // Add small epsilon to ensure floating point precision doesn't cause false positives
                    let epsilon = 0.1
                    not (newX + movingWidth - epsilon <= displayX || 
                         newX + epsilon >= displayX + displayWidth ||
                         newY + movingHeight - epsilon <= displayY ||
                         newY + epsilon >= displayY + displayHeight))
        
        let applySnapAndCollision (movingDisplayId: DisplayId) (pos: float * float) (allDisplays: DisplayInfo list) (border: Border) =
            let (x, y) = pos
            let movingWidth = border.Width
            let movingHeight = border.Height
            
            let movingDisplay = allDisplays |> List.find (fun d -> d.Id = movingDisplayId)
            
            // Check for edge snapping first (priority for seamless navigation)
            let snapPoints = findNearbyDisplayEdges movingDisplay (x, y) allDisplays
            
            let (targetX, targetY) = 
                if not (List.isEmpty snapPoints) then
                    // Use edge snap position for seamless navigation
                    List.head snapPoints
                else
                    // Fall back to intermediate grid snapping
                    let gridX = snapToDisplayEdgeOrGrid allDisplays x true
                    let gridY = snapToDisplayEdgeOrGrid allDisplays y false
                    (gridX, gridY)
            
            // Check for collision at target position
            if checkCollision movingDisplayId targetX targetY movingWidth movingHeight allDisplays then
                // Collision detected - find closest valid position
                
                // Try positions in order of preference:
                // 1. Original intermediate grid position
                // 2. Adjacent intermediate grid positions
                // 3. Original position (don't move)
                
                let gridX = snapToDisplayEdgeOrGrid allDisplays x true
                let gridY = snapToDisplayEdgeOrGrid allDisplays y false
                
                let validPositions = [
                    if (gridX, gridY) <> (targetX, targetY) then
                        yield (gridX, gridY)  // Try original grid position
                    
                    // Try adjacent intermediate grid positions
                    yield (gridX - intermediateGridSize, gridY)
                    yield (gridX + intermediateGridSize, gridY)
                    yield (gridX, gridY - intermediateGridSize)
                    yield (gridX, gridY + intermediateGridSize)
                    
                    // Try diagonal intermediate grid positions
                    yield (gridX - intermediateGridSize, gridY - intermediateGridSize)
                    yield (gridX + intermediateGridSize, gridY - intermediateGridSize)
                    yield (gridX - intermediateGridSize, gridY + intermediateGridSize)
                    yield (gridX + intermediateGridSize, gridY + intermediateGridSize)
                    
                    // Last resort: original position
                    yield (x, y)
                ]
                
                // Find first valid position within bounds
                validPositions
                |> List.tryFind (fun (px, py) ->
                    px >= 0.0 && py >= 0.0 &&
                    px + movingWidth <= canvas.Width &&
                    py + movingHeight <= canvas.Height &&
                    not (checkCollision movingDisplayId px py movingWidth movingHeight allDisplays))
                |> Option.defaultValue (Math.Max(0.0, Math.Min(x, canvas.Width - movingWidth)), 
                                       Math.Max(0.0, Math.Min(y, canvas.Height - movingHeight)))
            else
                // No collision - use the target position but ensure it's within bounds
                let boundedX = Math.Max(0.0, Math.Min(targetX, canvas.Width - movingWidth))
                let boundedY = Math.Max(0.0, Math.Min(targetY, canvas.Height - movingHeight))
                (boundedX, boundedY)

        // Grid lines removed - adaptive snapping works without visual guides
        
        // Grid info removed - clean interface with just displays
        
        // Track all visual displays for snapping
        let mutable visualDisplays = []
        
        // Add displays
        let onPositionChanged displayId (x, y) =
            let display = displays |> List.find (fun d -> d.Id = displayId)
            // x and y are already in display coordinates (scaled by 0.1)
            let updatedDisplay = { display with Position = { X = int (x * 10.0); Y = int (y * 10.0) } }
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
                
                // Get current display states from visual displays (they may have moved)
                let currentDisplayStates = 
                    visualDisplays
                    |> List.map (fun vd -> 
                        let currentX = Canvas.GetLeft(vd.Border) 
                        let currentY = Canvas.GetTop(vd.Border)
                        { vd.Display with 
                            Position = { 
                                X = int (currentX / 0.1)  // Convert GUI to display coords
                                Y = int (currentY / 0.1) 
                            }
                        })
                
                // Apply snapping and collision detection only on release
                let (snappedX, snappedY) = applySnapAndCollision displayId (x, y) currentDisplayStates border
                
                // Update visual position to snapped position
                Canvas.SetLeft(border, snappedX)
                Canvas.SetTop(border, snappedY)
                
                // Convert back to display coordinates and update data
                onPositionChanged displayId (snappedX, snappedY)
            
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
            
            // Handle enable/disable with modern gradient styling
            visualDisplay.EnableCheckBox.IsCheckedChanged.Add(fun _ ->
                if visualDisplay.EnableCheckBox.IsChecked.GetValueOrDefault() then
                    let updatedDisplay = { display with IsEnabled = true }
                    let enabledGradient = LinearGradientBrush()
                    enabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
                    enabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
                    enabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(100uy, 149uy, 237uy), 0.0))
                    enabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(65uy, 105uy, 225uy), 1.0))
                    visualDisplay.Rectangle.Fill <- enabledGradient :> IBrush
                    onDisplayChanged display.Id updatedDisplay
                else
                    let updatedDisplay = { display with IsEnabled = false }
                    let disabledGradient = LinearGradientBrush()
                    disabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
                    disabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
                    disabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(220uy, 220uy, 220uy), 0.0))
                    disabledGradient.GradientStops.Add(GradientStop(Color.FromRgb(180uy, 180uy, 180uy), 1.0))
                    visualDisplay.Rectangle.Fill <- disabledGradient :> IBrush
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
        stackPanel.Margin <- Thickness(15.0)
        
        for display in displays do
            let displayCard = Border()
            displayCard.Background <- SolidColorBrush(Color.FromRgb(249uy, 250uy, 251uy)) :> IBrush
            displayCard.BorderBrush <- SolidColorBrush(Color.FromRgb(229uy, 231uy, 235uy)) :> IBrush
            displayCard.BorderThickness <- Thickness(1.0)
            displayCard.CornerRadius <- CornerRadius(6.0)
            displayCard.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
            displayCard.Padding <- Thickness(12.0, 10.0, 12.0, 10.0)
            
            let displayContent = StackPanel()
            displayContent.Orientation <- Orientation.Vertical
            
            let nameText = TextBlock()
            nameText.Text <- display.Name
            nameText.FontWeight <- FontWeight.SemiBold
            nameText.FontSize <- 13.0
            nameText.Foreground <- SolidColorBrush(Color.FromRgb(31uy, 41uy, 55uy)) :> IBrush
            nameText.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
            displayContent.Children.Add(nameText)
            
            let resolutionText = TextBlock()
            resolutionText.Text <- sprintf "%dx%d" display.Resolution.Width display.Resolution.Height
            resolutionText.FontSize <- 11.0
            resolutionText.Foreground <- SolidColorBrush(Color.FromRgb(107uy, 114uy, 128uy)) :> IBrush
            displayContent.Children.Add(resolutionText)
            
            let statusText = TextBlock()
            statusText.Text <- if display.IsEnabled then "✓ Enabled" else "✗ Disabled"
            statusText.FontSize <- 10.0
            statusText.Foreground <- 
                if display.IsEnabled then 
                    SolidColorBrush(Color.FromRgb(34uy, 197uy, 94uy)) :> IBrush  // Green
                else 
                    SolidColorBrush(Color.FromRgb(239uy, 68uy, 68uy)) :> IBrush   // Red
            statusText.Margin <- Thickness(0.0, 4.0, 0.0, 0.0)
            displayContent.Children.Add(statusText)
            
            displayCard.Child <- displayContent
            stackPanel.Children.Add(displayCard)
        
        stackPanel

    let createPresetButtons (presets: string list) (onPresetClick: string -> unit) =
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(15.0)
        
        let titleText = TextBlock()
        titleText.Text <- "💾 Saved Presets"
        titleText.FontWeight <- FontWeight.Bold
        titleText.FontSize <- 16.0
        titleText.Foreground <- SolidColorBrush(Color.FromRgb(55uy, 65uy, 85uy)) :> IBrush
        titleText.Margin <- Thickness(0.0, 0.0, 0.0, 15.0)
        stackPanel.Children.Add(titleText)
        
        for preset in presets do
            let button = Button()
            button.Content <- preset
            button.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
            button.HorizontalAlignment <- HorizontalAlignment.Stretch
            button.Height <- 35.0
            button.FontSize <- 13.0
            button.CornerRadius <- CornerRadius(6.0)
            
            // Modern button gradient
            let buttonGradient = LinearGradientBrush()
            buttonGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
            buttonGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
            buttonGradient.GradientStops.Add(GradientStop(Color.FromRgb(248uy, 250uy, 252uy), 0.0))
            buttonGradient.GradientStops.Add(GradientStop(Color.FromRgb(230uy, 235uy, 240uy), 1.0))
            button.Background <- buttonGradient :> IBrush
            
            button.BorderBrush <- SolidColorBrush(Color.FromRgb(200uy, 210uy, 220uy)) :> IBrush
            button.BorderThickness <- Thickness(1.0)
            button.Foreground <- SolidColorBrush(Color.FromRgb(55uy, 65uy, 85uy)) :> IBrush
            
            button.Click.Add(fun _ -> onPresetClick preset)
            stackPanel.Children.Add(button)
        
        let saveButton = Button()
        saveButton.Content <- "➕ Save Current as Preset"
        saveButton.Margin <- Thickness(0.0, 15.0, 0.0, 0.0)
        saveButton.Height <- 40.0
        saveButton.FontSize <- 14.0
        saveButton.FontWeight <- FontWeight.SemiBold
        saveButton.CornerRadius <- CornerRadius(6.0)
        
        // Accent button gradient
        let saveGradient = LinearGradientBrush()
        saveGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        saveGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        saveGradient.GradientStops.Add(GradientStop(Color.FromRgb(52uy, 152uy, 219uy), 0.0))  // Blue
        saveGradient.GradientStops.Add(GradientStop(Color.FromRgb(41uy, 128uy, 185uy), 1.0))  // Darker blue
        saveButton.Background <- saveGradient :> IBrush
        
        saveButton.BorderBrush <- SolidColorBrush(Color.FromRgb(41uy, 128uy, 185uy)) :> IBrush
        saveButton.BorderThickness <- Thickness(1.0)
        saveButton.Foreground <- Brushes.White
        saveButton.Click.Add(fun _ -> onPresetClick "SAVE_NEW")
        stackPanel.Children.Add(saveButton)
        
        stackPanel

    let createMainWindow (world: World) (adapter: IPlatformAdapter) =
        let window = Window()
        window.Title <- "DisplaySwitch-Pro"
        window.Width <- 1200.0
        window.Height <- 700.0
        window.Background <- SolidColorBrush(Color.FromRgb(248uy, 250uy, 252uy)) :> IBrush  // Very light gray-blue
        window.Icon <- null  // We could add an icon later
        
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
        mainPanel.Background <- SolidColorBrush(Color.FromRgb(248uy, 250uy, 252uy)) :> IBrush
        mainPanel.Margin <- Thickness(5.0)
        
        // Left side - display info with modern styling
        let infoPanel = StackPanel()
        infoPanel.Orientation <- Orientation.Vertical
        infoPanel.Width <- 200.0
        infoPanel.Margin <- Thickness(10.0)
        infoPanel.Background <- SolidColorBrush(Color.FromRgb(255uy, 255uy, 255uy)) :> IBrush
        let infoPanelBorder = Border()
        infoPanelBorder.Child <- infoPanel
        infoPanelBorder.Background <- SolidColorBrush(Color.FromRgb(255uy, 255uy, 255uy)) :> IBrush
        infoPanelBorder.BorderBrush <- SolidColorBrush(Color.FromRgb(220uy, 225uy, 230uy)) :> IBrush
        infoPanelBorder.BorderThickness <- Thickness(1.0)
        infoPanelBorder.CornerRadius <- CornerRadius(8.0)
        infoPanelBorder.Margin <- Thickness(5.0)
        // Modern border styling without shadows
        
        let infoTitle = TextBlock()
        infoTitle.Text <- "📺 Display Information"
        infoTitle.FontWeight <- FontWeight.Bold
        infoTitle.FontSize <- 16.0
        infoTitle.Foreground <- SolidColorBrush(Color.FromRgb(55uy, 65uy, 85uy)) :> IBrush
        infoTitle.Margin <- Thickness(15.0, 15.0, 15.0, 10.0)
        infoPanel.Children.Add(infoTitle)
        
        let displayList = createDisplayListView displays
        infoPanel.Children.Add(displayList)
        
        DockPanel.SetDock(infoPanelBorder, Dock.Left)
        mainPanel.Children.Add(infoPanelBorder)
        
        // Right side - presets with modern styling
        let presetPanel = createPresetButtons presets onPresetClick
        presetPanel.Width <- 200.0
        let presetPanelBorder = Border()
        presetPanelBorder.Child <- presetPanel
        presetPanelBorder.Background <- SolidColorBrush(Color.FromRgb(255uy, 255uy, 255uy)) :> IBrush
        presetPanelBorder.BorderBrush <- SolidColorBrush(Color.FromRgb(220uy, 225uy, 230uy)) :> IBrush
        presetPanelBorder.BorderThickness <- Thickness(1.0)
        presetPanelBorder.CornerRadius <- CornerRadius(8.0)
        presetPanelBorder.Margin <- Thickness(5.0)
        // Modern preset panel styling without shadows
        DockPanel.SetDock(presetPanelBorder, Dock.Right)
        mainPanel.Children.Add(presetPanelBorder)
        
        // Center - visual canvas with modern styling
        let canvasContainer = Border()
        canvasContainer.BorderBrush <- SolidColorBrush(Color.FromRgb(200uy, 210uy, 220uy)) :> IBrush
        canvasContainer.BorderThickness <- Thickness(1.0)
        canvasContainer.CornerRadius <- CornerRadius(8.0)
        canvasContainer.Margin <- Thickness(5.0)
        canvasContainer.Background <- SolidColorBrush(Color.FromRgb(255uy, 255uy, 255uy)) :> IBrush
        // Modern canvas styling without shadows
        
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