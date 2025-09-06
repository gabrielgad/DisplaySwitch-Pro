namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Media

module DisplayCanvas =
    
    type DragState = {
        IsDragging: bool
        StartPoint: Point
    }
    
    let createDisplayCanvas (displays: DisplayInfo list) (onDisplayPositionUpdate: DisplayId -> DisplayInfo -> unit) (onDisplayDragComplete: DisplayId -> DisplayInfo -> unit) =
        let colors = Theme.getCurrentColors()
        let canvas = Canvas()
        
        let canvasGradient = LinearGradientBrush()
        canvasGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        canvasGradient.EndPoint <- RelativePoint(1.0, 1.0, RelativeUnit.Relative)
        canvasGradient.GradientStops.Add(GradientStop(colors.CanvasBg, 0.0))
        canvasGradient.GradientStops.Add(GradientStop(colors.CanvasBgDark, 1.0))
        
        canvas.Background <- canvasGradient :> IBrush
        
        // Calculate canvas bounds with proper coordinate transformation
        let scale = 0.1
        let padding = 50.0  // Padding around displays
        
        if not displays.IsEmpty then
            let maxX = displays |> List.map (fun d -> d.Position.X + d.Resolution.Width) |> List.max |> float
            let maxY = displays |> List.map (fun d -> d.Position.Y + d.Resolution.Height) |> List.max |> float
            let minX = displays |> List.map (fun d -> d.Position.X) |> List.min |> float
            let minY = displays |> List.map (fun d -> d.Position.Y) |> List.min |> float
            
            // Canvas coordinate system: (0,0) represents the minimum coordinate
            let canvasWidth = (maxX - minX) * scale + (padding * 2.0)
            let canvasHeight = (maxY - minY) * scale + (padding * 2.0)
            
            canvas.Width <- Math.Max(800.0, canvasWidth)
            canvas.Height <- Math.Max(600.0, canvasHeight)
            
            // Store coordinate transformation info for display positioning
            canvas.Tag <- (minX, minY, scale, padding)  // Store transformation parameters
        else
            // Default size when no displays
            canvas.Width <- 800.0
            canvas.Height <- 600.0
            canvas.Tag <- (0.0, 0.0, scale, padding)
        
        let intermediateGridSize = 20.0
        let snapProximityThreshold = 25.0
        
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
                        ("vertical", x)
                        ("vertical", x + width)
                        ("horizontal", y)
                        ("horizontal", y + height)
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
            
            let edgeSnap = 
                relevantEdges
                |> List.tryFind (fun edge -> Math.Abs(edge - value) <= snapProximityThreshold)
            
            match edgeSnap with
            | Some edge -> edge
            | None -> 
                Math.Round(value / intermediateGridSize) * intermediateGridSize
        
        let findNearbyDisplayEdges (movingDisplay: DisplayInfo) (targetPos: float * float) (allDisplays: DisplayInfo list) =
            let (targetX, targetY) = targetPos
            let movingWidth = float movingDisplay.Resolution.Width * 0.1
            let movingHeight = float movingDisplay.Resolution.Height * 0.1
            
            let getSnapCandidatesForDisplay (display: DisplayInfo) =
                let displayX = float display.Position.X * 0.1
                let displayY = float display.Position.Y * 0.1
                let displayWidth = float display.Resolution.Width * 0.1
                let displayHeight = float display.Resolution.Height * 0.1
                
                [
                    (displayX + displayWidth, displayY)
                    (displayX + displayWidth, displayY + displayHeight - movingHeight)
                    (displayX - movingWidth, displayY)
                    (displayX - movingWidth, displayY + displayHeight - movingHeight)
                    (displayX, displayY + displayHeight)
                    (displayX + displayWidth - movingWidth, displayY + displayHeight)
                    (displayX, displayY - movingHeight)
                    (displayX + displayWidth - movingWidth, displayY - movingHeight)
                    (displayX, displayY)
                    (displayX, displayY + displayHeight - movingHeight)
                    (displayX + displayWidth - movingWidth, displayY)
                    (displayX + displayWidth - movingWidth, displayY + displayHeight - movingHeight)
                ]
            
            let calculateDistance (candidateX, candidateY) =
                let distanceX = Math.Abs(candidateX - targetX)
                let distanceY = Math.Abs(candidateY - targetY)
                Math.Sqrt(distanceX * distanceX + distanceY * distanceY)
            
            allDisplays
            |> List.filter (fun display -> display.Id <> movingDisplay.Id && display.IsEnabled)
            |> List.collect getSnapCandidatesForDisplay
            |> List.map (fun point -> (point, calculateDistance point))
            |> List.filter (fun (_, distance) -> distance <= snapProximityThreshold)
            |> List.sortBy snd
            |> List.tryHead
            |> Option.map fst
            |> function
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
            
            let snapPoints = findNearbyDisplayEdges movingDisplay (x, y) allDisplays
            
            let (targetX, targetY) = 
                if not (List.isEmpty snapPoints) then
                    List.head snapPoints
                else
                    let gridX = snapToDisplayEdgeOrGrid allDisplays x true
                    let gridY = snapToDisplayEdgeOrGrid allDisplays y false
                    (gridX, gridY)
            
            if checkCollision movingDisplayId targetX targetY movingWidth movingHeight allDisplays then
                let gridX = snapToDisplayEdgeOrGrid allDisplays x true
                let gridY = snapToDisplayEdgeOrGrid allDisplays y false
                
                let validPositions = [
                    if (gridX, gridY) <> (targetX, targetY) then
                        yield (gridX, gridY)
                    
                    yield (gridX - intermediateGridSize, gridY)
                    yield (gridX + intermediateGridSize, gridY)
                    yield (gridX, gridY - intermediateGridSize)
                    yield (gridX, gridY + intermediateGridSize)
                    yield (gridX - intermediateGridSize, gridY - intermediateGridSize)
                    yield (gridX + intermediateGridSize, gridY - intermediateGridSize)
                    yield (gridX - intermediateGridSize, gridY + intermediateGridSize)
                    yield (gridX + intermediateGridSize, gridY + intermediateGridSize)
                    yield (x, y)
                ]
                
                validPositions
                |> List.tryFind (fun (px, py) ->
                    px >= 0.0 && py >= 0.0 &&
                    px + movingWidth <= canvas.Width &&
                    py + movingHeight <= canvas.Height &&
                    not (checkCollision movingDisplayId px py movingWidth movingHeight allDisplays))
                |> Option.defaultValue (Math.Max(0.0, Math.Min(x, canvas.Width - movingWidth)), 
                                       Math.Max(0.0, Math.Min(y, canvas.Height - movingHeight)))
            else
                let boundedX = Math.Max(0.0, Math.Min(targetX, canvas.Width - movingWidth))
                let boundedY = Math.Max(0.0, Math.Min(targetY, canvas.Height - movingHeight))
                (boundedX, boundedY)

        // Update position during drag (no compacting)
        let onPositionChanged displayId (x, y) =
            // Get coordinate transformation parameters from canvas tag
            let (minX, minY, scale, padding) = 
                match canvas.Tag with
                | :? (float * float * float * float) as transform -> transform
                | _ -> (0.0, 0.0, 0.1, 50.0)  // Fallback values
            
            // Convert canvas coordinates back to Windows coordinates
            let windowsX = int ((x - padding) / scale + minX)
            let windowsY = int ((y - padding) / scale + minY)
            
            // Validate Windows coordinates are within valid ranges (-32768 to +32767)
            let validatedX = Math.Max(-32768, Math.Min(32767, windowsX))
            let validatedY = Math.Max(-32768, Math.Min(32767, windowsY))
            
            if validatedX <> windowsX || validatedY <> windowsY then
                printfn "[DEBUG] Position clamped from (%d, %d) to (%d, %d) for display %s" 
                    windowsX windowsY validatedX validatedY displayId
            
            let display = displays |> List.find (fun d -> d.Id = displayId)
            let updatedDisplay = { display with Position = { X = validatedX; Y = validatedY } }
            onDisplayPositionUpdate displayId updatedDisplay
        
        // Handle drag completion (with compacting)
        let onDragComplete displayId (x, y) =
            printfn "[DEBUG] Drag completed for %s at canvas position (%.1f, %.1f)" displayId x y
            
            // Get coordinate transformation parameters from canvas tag
            let (minX, minY, scale, padding) = 
                match canvas.Tag with
                | :? (float * float * float * float) as transform -> transform
                | _ -> (0.0, 0.0, 0.1, 50.0)  // Fallback values
            
            // Convert canvas coordinates back to Windows coordinates
            let windowsX = int ((x - padding) / scale + minX)
            let windowsY = int ((y - padding) / scale + minY)
            
            // Validate Windows coordinates are within valid ranges (-32768 to +32767)
            let validatedX = Math.Max(-32768, Math.Min(32767, windowsX))
            let validatedY = Math.Max(-32768, Math.Min(32767, windowsY))
            
            let display = displays |> List.find (fun d -> d.Id = displayId)
            let updatedDisplay = { display with Position = { X = validatedX; Y = validatedY } }
            
            // Call the drag completion handler (this will trigger compacting)
            onDisplayDragComplete displayId updatedDisplay
        
        let createVisualDisplayWithHandlers (display: DisplayInfo) (allVisualDisplays: UIComponents.VisualDisplay list ref) =
            let onDragging (border: Border) (currentPos: float * float) =
                let (x, y) = currentPos
                let finalX = Math.Max(0.0, Math.Min(x, canvas.Width - border.Width))
                let finalY = Math.Max(0.0, Math.Min(y, canvas.Height - border.Height))
                
                Canvas.SetLeft(border, finalX)
                Canvas.SetTop(border, finalY)
                
            let onDragEnd displayId (border: Border) =
                let x = Canvas.GetLeft(border)
                let y = Canvas.GetTop(border)
                
                let currentDisplayStates = 
                    !allVisualDisplays
                    |> List.map (fun vd -> 
                        let currentX = Canvas.GetLeft(vd.Border) 
                        let currentY = Canvas.GetTop(vd.Border)
                        { vd.Display with 
                            Position = { 
                                X = int (currentX / 0.1)
                                Y = int (currentY / 0.1) 
                            }
                        })
                
                let (snappedX, snappedY) = applySnapAndCollision displayId (x, y) currentDisplayStates border
                
                Canvas.SetLeft(border, snappedX)
                Canvas.SetTop(border, snappedY)
                
                onDragComplete displayId (snappedX, snappedY)
            
            let visualDisplay = UIComponents.createVisualDisplay display (fun _ _ -> ())
            
            // Get coordinate transformation parameters from canvas tag
            let (minX, minY, scale, padding) = 
                match canvas.Tag with
                | :? (float * float * float * float) as transform -> transform
                | _ -> (0.0, 0.0, 0.1, 50.0)  // Fallback values
            
            // Transform Windows coordinates to canvas coordinates
            let canvasX = (float display.Position.X - minX) * scale + padding
            let canvasY = (float display.Position.Y - minY) * scale + padding
            
            // Ensure the display is within canvas bounds (with validation)
            let boundedX = Math.Max(0.0, Math.Min(canvasX, canvas.Width - visualDisplay.Border.Width))
            let boundedY = Math.Max(0.0, Math.Min(canvasY, canvas.Height - visualDisplay.Border.Height))
            
            Canvas.SetLeft(visualDisplay.Border, boundedX)
            Canvas.SetTop(visualDisplay.Border, boundedY)
            
            // Drag state per visual display
            let dragState = ref { IsDragging = false; StartPoint = Point(0.0, 0.0) }
            
            visualDisplay.Border.PointerPressed.Add(fun e ->
                if e.GetCurrentPoint(visualDisplay.Border).Properties.IsLeftButtonPressed then
                    dragState := { IsDragging = true; StartPoint = e.GetPosition(canvas) }
                    visualDisplay.Border.Opacity <- 0.7
            )
            
            visualDisplay.Border.PointerReleased.Add(fun e ->
                if (!dragState).IsDragging then
                    dragState := { IsDragging = false; StartPoint = Point(0.0, 0.0) }
                    visualDisplay.Border.Opacity <- 1.0
                    onDragEnd display.Id visualDisplay.Border
            )
            
            visualDisplay.Border.PointerMoved.Add(fun e ->
                if (!dragState).IsDragging then
                    let currentPos = e.GetPosition(canvas)
                    let deltaX = currentPos.X - (!dragState).StartPoint.X
                    let deltaY = currentPos.Y - (!dragState).StartPoint.Y
                    
                    let newX = Canvas.GetLeft(visualDisplay.Border) + deltaX
                    let newY = Canvas.GetTop(visualDisplay.Border) + deltaY
                    
                    onDragging visualDisplay.Border (newX, newY)
                    dragState := { !dragState with StartPoint = currentPos }
            )
            
            visualDisplay
        
        let visualDisplays = ref []
        
        displays
        |> List.iter (fun display ->
            let visualDisplay = createVisualDisplayWithHandlers display visualDisplays
            visualDisplays := visualDisplay :: !visualDisplays
            canvas.Children.Add(visualDisplay.Border)
        )
        
        let scrollViewer = ScrollViewer()
        scrollViewer.Content <- canvas
        scrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Auto
        scrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        scrollViewer