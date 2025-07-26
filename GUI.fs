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
    // Theme definitions
    type Theme = 
        | Light
        | Dark
    
    type ThemeColors = {
        Background: Color
        Surface: Color
        Primary: Color
        PrimaryDark: Color
        Secondary: Color
        SecondaryDark: Color
        Text: Color
        TextSecondary: Color
        Border: Color
        DisabledBg: Color
        DisabledBgDark: Color
        CanvasBg: Color
        CanvasBgDark: Color
    }
    
    let getThemeColors theme =
        match theme with
        | Light -> 
            {
                Background = Color.FromRgb(248uy, 250uy, 252uy)
                Surface = Color.FromRgb(255uy, 255uy, 255uy)
                Primary = Color.FromRgb(100uy, 149uy, 237uy)  // Cornflower blue
                PrimaryDark = Color.FromRgb(65uy, 105uy, 225uy)  // Royal blue
                Secondary = Color.FromRgb(52uy, 152uy, 219uy)
                SecondaryDark = Color.FromRgb(41uy, 128uy, 185uy)
                Text = Color.FromRgb(31uy, 41uy, 55uy)
                TextSecondary = Color.FromRgb(107uy, 114uy, 128uy)
                Border = Color.FromRgb(220uy, 225uy, 230uy)
                DisabledBg = Color.FromRgb(220uy, 220uy, 220uy)
                DisabledBgDark = Color.FromRgb(180uy, 180uy, 180uy)
                CanvasBg = Color.FromRgb(245uy, 248uy, 250uy)
                CanvasBgDark = Color.FromRgb(230uy, 235uy, 240uy)
            }
        | Dark -> 
            {
                Background = Color.FromRgb(17uy, 24uy, 39uy)  // Very dark blue
                Surface = Color.FromRgb(31uy, 41uy, 55uy)  // Dark blue-gray
                Primary = Color.FromRgb(96uy, 165uy, 250uy)  // Light blue
                PrimaryDark = Color.FromRgb(59uy, 130uy, 246uy)  // Blue
                Secondary = Color.FromRgb(79uy, 70uy, 229uy)  // Indigo
                SecondaryDark = Color.FromRgb(67uy, 56uy, 202uy)  // Dark indigo
                Text = Color.FromRgb(243uy, 244uy, 246uy)  // Very light gray
                TextSecondary = Color.FromRgb(156uy, 163uy, 175uy)  // Gray
                Border = Color.FromRgb(55uy, 65uy, 81uy)  // Dark gray
                DisabledBg = Color.FromRgb(75uy, 85uy, 99uy)
                DisabledBgDark = Color.FromRgb(55uy, 65uy, 81uy)
                CanvasBg = Color.FromRgb(24uy, 32uy, 47uy)
                CanvasBgDark = Color.FromRgb(17uy, 24uy, 39uy)
            }
    
    let mutable currentTheme = Light
    
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
        let colors = getThemeColors currentTheme
        // Scale factor: 1 pixel = 10 screen pixels
        let scale = 0.1
        let width = float display.Resolution.Width * scale
        let height = float display.Resolution.Height * scale
        
        // Create modern styled display rectangle with gradient and rounded corners
        let rect = Rectangle()
        rect.Width <- width
        rect.Height <- height
        
        // Theme-aware gradient colors
        let enabledGradient = LinearGradientBrush()
        enabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        enabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        enabledGradient.GradientStops.Add(GradientStop(colors.Primary, 0.0))
        enabledGradient.GradientStops.Add(GradientStop(colors.PrimaryDark, 1.0))
        
        let disabledGradient = LinearGradientBrush()
        disabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        disabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        disabledGradient.GradientStops.Add(GradientStop(colors.DisabledBg, 0.0))
        disabledGradient.GradientStops.Add(GradientStop(colors.DisabledBgDark, 1.0))
        
        rect.Fill <- if display.IsEnabled then enabledGradient :> IBrush else disabledGradient :> IBrush
        rect.Stroke <- SolidColorBrush(colors.Border) :> IBrush
        rect.StrokeThickness <- 1.5
        rect.RadiusX <- 8.0  // Rounded corners
        rect.RadiusY <- 8.0
        
        // Create modern styled label with better typography
        let label = TextBlock()
        label.Text <- sprintf "%s\n%dx%d" display.Name display.Resolution.Width display.Resolution.Height
        label.HorizontalAlignment <- HorizontalAlignment.Center
        label.VerticalAlignment <- VerticalAlignment.Center
        label.TextAlignment <- TextAlignment.Center
        label.Foreground <- if currentTheme = Light then Brushes.White :> IBrush else SolidColorBrush(colors.Text) :> IBrush
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
        
        // No checkbox on display rectangle anymore
        
        // Create border container
        let border = Border()
        border.Width <- width
        border.Height <- height
        border.Background <- Brushes.Transparent
        border.Cursor <- new Cursor(StandardCursorType.Hand)
        
        // Add children to a grid
        let grid = Grid()
        grid.Children.Add(rect)
        grid.Children.Add(textShadow)  // Add shadow first
        grid.Children.Add(label)       // Then actual text on top
        
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
            EnableCheckBox = null  // Removed from display
        }
    
    // Create the display arrangement canvas
    let createDisplayCanvas (displays: DisplayInfo list) (onDisplayChanged: DisplayId -> DisplayInfo -> unit) =
        let colors = getThemeColors currentTheme
        let canvas = Canvas()
        
        // Theme-aware gradient background
        let canvasGradient = LinearGradientBrush()
        canvasGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        canvasGradient.EndPoint <- RelativePoint(1.0, 1.0, RelativeUnit.Relative)
        canvasGradient.GradientStops.Add(GradientStop(colors.CanvasBg, 0.0))
        canvasGradient.GradientStops.Add(GradientStop(colors.CanvasBgDark, 1.0))
        
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
            
            // Enable/disable handling moved to display info panel
            
            canvas.Children.Add(visualDisplay.Border)
        
        let scrollViewer = ScrollViewer()
        scrollViewer.Content <- canvas
        scrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Auto
        scrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        scrollViewer
    let createDisplayListView (displays: DisplayInfo list) (onDisplayToggle: DisplayId -> bool -> unit) =
        let colors = getThemeColors currentTheme
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(15.0)
        
        for display in displays do
            let displayCard = Border()
            displayCard.Background <- SolidColorBrush(if currentTheme = Light then Color.FromRgb(249uy, 250uy, 251uy) else colors.Surface) :> IBrush
            displayCard.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
            displayCard.BorderThickness <- Thickness(1.0)
            displayCard.CornerRadius <- CornerRadius(6.0)
            displayCard.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
            displayCard.Padding <- Thickness(12.0, 10.0, 12.0, 10.0)
            displayCard.Cursor <- new Cursor(StandardCursorType.Hand)
            
            let cardContent = Grid()
            cardContent.ColumnDefinitions.Add(ColumnDefinition())
            cardContent.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
            
            let displayContent = StackPanel()
            displayContent.Orientation <- Orientation.Vertical
            Grid.SetColumn(displayContent, 0)
            
            let nameText = TextBlock()
            nameText.Text <- display.Name
            nameText.FontWeight <- FontWeight.SemiBold
            nameText.FontSize <- 13.0
            nameText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
            nameText.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
            displayContent.Children.Add(nameText)
            
            let resolutionText = TextBlock()
            resolutionText.Text <- sprintf "%dx%d" display.Resolution.Width display.Resolution.Height
            resolutionText.FontSize <- 11.0
            resolutionText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            displayContent.Children.Add(resolutionText)
            
            cardContent.Children.Add(displayContent)
            
            // Toggle button
            let toggleButton = Button()
            toggleButton.Content <- if display.IsEnabled then "✓" else "✗"
            toggleButton.Width <- 30.0
            toggleButton.Height <- 30.0
            toggleButton.FontSize <- 14.0
            toggleButton.CornerRadius <- CornerRadius(15.0)
            toggleButton.Background <- 
                if display.IsEnabled then 
                    SolidColorBrush(Color.FromRgb(34uy, 197uy, 94uy)) :> IBrush  // Green
                else 
                    SolidColorBrush(Color.FromRgb(239uy, 68uy, 68uy)) :> IBrush   // Red
            toggleButton.Foreground <- Brushes.White
            toggleButton.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
            Grid.SetColumn(toggleButton, 1)
            
            toggleButton.Click.Add(fun _ ->
                onDisplayToggle display.Id (not display.IsEnabled)
            )
            
            cardContent.Children.Add(toggleButton)
            displayCard.Child <- cardContent
            stackPanel.Children.Add(displayCard)
        
        stackPanel

    let createPresetButtons (presets: string list) (onPresetClick: string -> unit) =
        let colors = getThemeColors currentTheme
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(15.0)
        
        let titleText = TextBlock()
        titleText.Text <- "💾 Saved Presets"
        titleText.FontWeight <- FontWeight.Bold
        titleText.FontSize <- 16.0
        titleText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
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
            
            // Theme-aware button styling
            button.Background <- SolidColorBrush(if currentTheme = Light then colors.Surface else colors.Surface) :> IBrush
            button.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
            button.BorderThickness <- Thickness(1.0)
            button.Foreground <- SolidColorBrush(colors.Text) :> IBrush
            
            button.Click.Add(fun _ -> onPresetClick preset)
            stackPanel.Children.Add(button)
        
        let saveButton = Button()
        saveButton.Content <- "➕ Save Current as Preset"
        saveButton.Margin <- Thickness(0.0, 15.0, 0.0, 0.0)
        saveButton.Height <- 40.0
        saveButton.FontSize <- 14.0
        saveButton.FontWeight <- FontWeight.SemiBold
        saveButton.CornerRadius <- CornerRadius(6.0)
        
        // Theme-aware accent button gradient
        let saveGradient = LinearGradientBrush()
        saveGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        saveGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        saveGradient.GradientStops.Add(GradientStop(colors.Secondary, 0.0))
        saveGradient.GradientStops.Add(GradientStop(colors.SecondaryDark, 1.0))
        saveButton.Background <- saveGradient :> IBrush
        
        saveButton.BorderBrush <- SolidColorBrush(colors.SecondaryDark) :> IBrush
        saveButton.BorderThickness <- Thickness(1.0)
        saveButton.Foreground <- Brushes.White
        saveButton.Click.Add(fun _ -> onPresetClick "SAVE_NEW")
        stackPanel.Children.Add(saveButton)
        
        stackPanel

    let rec createMainWindow (world: World) (adapter: IPlatformAdapter) =
        let colors = getThemeColors currentTheme
        let window = Window()
        window.Title <- "DisplaySwitch-Pro"
        window.Width <- 1200.0
        window.Height <- 700.0
        window.Background <- SolidColorBrush(colors.Background) :> IBrush
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
        
        // Simple data-only approach - no complex UI refresh"
        
        
        let onPresetClick (presetName: string) =
            if presetName = "SAVE_NEW" then
                // Create a simple input dialog for preset name
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
                        // Create configuration from current display states
                        let currentDisplays = currentWorld.Components.ConnectedDisplays |> Map.values |> List.ofSeq
                        let config = {
                            Displays = currentDisplays
                            Name = name
                            CreatedAt = DateTime.Now
                        }
                        currentWorld <- PresetSystem.saveCurrentAsPreset name currentWorld
                        printfn "Saving preset: %s" name
                        dialog.Close()
                        
                        // Preset saved successfully
                        printfn "Preset saved successfully"
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
                dialog.ShowDialog(window) |> ignore
            else
                // Debug preset loading
                printfn "Debug: Loading preset %s" presetName
                printfn "Debug: Available presets: %A" (PresetSystem.listPresets currentWorld |> List.toArray)
                
                match Map.tryFind presetName currentWorld.Components.SavedPresets with
                | Some config ->
                    printfn "Debug: Found preset config with %d displays" config.Displays.Length
                    
                    // Update world state
                    currentWorld <- PresetSystem.loadPreset presetName currentWorld
                    
                    // Update all display positions from the loaded preset
                    for display in config.Displays do
                        printfn "Debug: Setting display %s to position (%d, %d)" display.Id display.Position.X display.Position.Y
                        let updatedComponents = Components.addDisplay display currentWorld.Components
                        currentWorld <- { currentWorld with Components = updatedComponents }
                    
                    // Data updated successfully
                    printfn "Preset loaded - display data updated"
                    
                    printfn "Loading preset: %s" presetName
                | None ->
                    printfn "Debug: Preset %s not found!" presetName
        
        let mainPanel = DockPanel()
        mainPanel.LastChildFill <- true
        mainPanel.Background <- SolidColorBrush(colors.Background) :> IBrush
        mainPanel.Margin <- Thickness(5.0)
        
        // Left side - display info with theme-aware styling
        let infoPanelActual = StackPanel()
        infoPanelActual.Orientation <- Orientation.Vertical
        infoPanelActual.Width <- 200.0
        infoPanelActual.Margin <- Thickness(10.0)
        infoPanelActual.Background <- SolidColorBrush(colors.Surface) :> IBrush
        let infoPanelBorder = Border()
        infoPanelBorder.Child <- infoPanelActual
        infoPanelBorder.Background <- SolidColorBrush(colors.Surface) :> IBrush
        infoPanelBorder.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        infoPanelBorder.BorderThickness <- Thickness(1.0)
        infoPanelBorder.CornerRadius <- CornerRadius(8.0)
        infoPanelBorder.Margin <- Thickness(5.0)
        // Modern border styling without shadows
        
        let infoTitleActual = TextBlock()
        infoTitleActual.Text <- "📺 Display Information"
        infoTitleActual.FontWeight <- FontWeight.Bold
        infoTitleActual.FontSize <- 16.0
        infoTitleActual.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        infoTitleActual.Margin <- Thickness(15.0, 15.0, 15.0, 10.0)
        infoPanelActual.Children.Add(infoTitleActual)
        
        // Simplified display toggle handler
        let onDisplayToggle displayId isEnabled =
            let display = currentWorld.Components.ConnectedDisplays.[displayId]
            let updatedDisplay = { display with IsEnabled = isEnabled }
            let updatedComponents = Components.addDisplay updatedDisplay currentWorld.Components
            currentWorld <- { currentWorld with Components = updatedComponents }
            
            printfn "Display %s %s - updated in data model" displayId (if isEnabled then "enabled" else "disabled")
        
        let displayList = createDisplayListView displays onDisplayToggle
        infoPanelActual.Children.Add(displayList)
        
        DockPanel.SetDock(infoPanelBorder, Dock.Left)
        mainPanel.Children.Add(infoPanelBorder)
        
        // Right side - presets with theme-aware styling
        let presetPanel = createPresetButtons presets onPresetClick
        presetPanel.Width <- 200.0
        let presetPanelBorder = Border()
        presetPanelBorder.Child <- presetPanel
        presetPanelBorder.Background <- SolidColorBrush(colors.Surface) :> IBrush
        presetPanelBorder.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        presetPanelBorder.BorderThickness <- Thickness(1.0)
        presetPanelBorder.CornerRadius <- CornerRadius(8.0)
        presetPanelBorder.Margin <- Thickness(5.0)
        // Modern preset panel styling without shadows
        DockPanel.SetDock(presetPanelBorder, Dock.Right)
        mainPanel.Children.Add(presetPanelBorder)
        
        // Center - visual canvas with theme-aware styling
        let canvasContainer = Border()
        canvasContainer.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        canvasContainer.BorderThickness <- Thickness(1.0)
        canvasContainer.CornerRadius <- CornerRadius(8.0)
        canvasContainer.Margin <- Thickness(5.0)
        canvasContainer.Background <- SolidColorBrush(colors.Surface) :> IBrush
        // Modern canvas styling without shadows
        
        let displayCanvas = createDisplayCanvas displays onDisplayChanged
        canvasContainer.Child <- displayCanvas
        
        mainPanel.Children.Add(canvasContainer)
        
        // Create status bar with theme toggle at bottom
        let statusBar = Border()
        statusBar.Background <- SolidColorBrush(colors.Surface) :> IBrush
        statusBar.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        statusBar.BorderThickness <- Thickness(0.0, 1.0, 0.0, 0.0)
        statusBar.Height <- 35.0
        DockPanel.SetDock(statusBar, Dock.Bottom)
        
        let statusContent = Grid()
        statusContent.Margin <- Thickness(10.0, 5.0, 10.0, 5.0)
        
        // Theme toggle button in status bar
        let themeToggleButton = Button()
        themeToggleButton.Content <- if currentTheme = Light then "🌙" else "☀️"
        themeToggleButton.Width <- 30.0
        themeToggleButton.Height <- 25.0
        themeToggleButton.HorizontalAlignment <- HorizontalAlignment.Right
        themeToggleButton.Background <- SolidColorBrush(colors.Background) :> IBrush
        themeToggleButton.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
        themeToggleButton.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        themeToggleButton.CornerRadius <- CornerRadius(3.0)
        themeToggleButton.FontSize <- 14.0
        
        // Status text
        let statusText = TextBlock()
        statusText.Text <- "DisplaySwitch-Pro - Ready"
        statusText.VerticalAlignment <- VerticalAlignment.Center
        statusText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
        statusText.FontSize <- 11.0
        
        statusContent.Children.Add(statusText)
        statusContent.Children.Add(themeToggleButton)
        statusBar.Child <- statusContent
        
        themeToggleButton.Click.Add(fun _ ->
            // Toggle theme
            currentTheme <- if currentTheme = Light then Dark else Light
            
            // Recreate window with new theme
            let newWindow = createMainWindow world adapter
            
            // Get the application and swap windows
            match Application.Current.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop ->
                let oldWindow = desktop.MainWindow
                desktop.MainWindow <- newWindow
                newWindow.Show()
                oldWindow.Close()
            | _ -> ()
        )
        
        // Add status bar to main panel
        mainPanel.Children.Add(statusBar)
        
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