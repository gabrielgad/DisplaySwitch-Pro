namespace DisplaySwitchPro

open System
open Avalonia
open DomainTypes
open EnhancedResult

/// Immutable canvas state management for functional display canvas interactions
/// Provides pure state transformations and centralized canvas state
module CanvasState =

    /// Point in 2D space for canvas coordinates
    type CanvasPoint = {
        X: float
        Y: float
    }

    /// Rectangle in canvas coordinate space
    type CanvasRectangle = {
        X: float
        Y: float
        Width: float
        Height: float
    }

    /// Transform parameters for coordinate conversion
    type TransformParameters = {
        /// Center X coordinate (canvas center maps to Windows 0,0)
        CenterX: float
        /// Center Y coordinate (canvas center maps to Windows 0,0)
        CenterY: float
        /// Scale factor for coordinate conversion
        Scale: float
        /// Canvas bounds for validation
        CanvasBounds: CanvasRectangle
        /// Windows coordinate bounds for validation
        WindowsBounds: CanvasRectangle
    }

    /// Drag operation state for a specific display
    type DragOperation = {
        /// ID of the display being dragged
        DisplayId: DisplayId
        /// Starting position when drag began
        StartPosition: CanvasPoint
        /// Current position during drag
        CurrentPosition: CanvasPoint
        /// When the drag operation started
        StartTime: DateTime
        /// Whether the drag is currently active
        IsActive: bool
        /// Available snap targets for this drag
        SnapTargets: SnapPoint list
        /// Original display position before drag started
        OriginalPosition: CanvasPoint
    }

    /// Snap configuration for display alignment
    and SnapConfiguration = {
        /// Size of the grid for grid snapping
        GridSize: float
        /// Distance threshold for proximity snapping
        ProximityThreshold: float
        /// Whether to snap to edges of other displays
        SnapToEdges: bool
        /// Whether to snap to grid points
        SnapToGrid: bool
        /// Whether to show visual snap guides
        ShowSnapGuides: bool
        /// Whether snapping is enabled at all
        SnapEnabled: bool
    }

    /// Point that can be snapped to
    and SnapPoint = {
        /// Position of the snap point
        Position: CanvasPoint
        /// Type of snap point
        SnapType: SnapType
        /// Target display ID if snapping to another display
        TargetDisplayId: DisplayId option
        /// Strength of the snap attraction
        Strength: SnapStrength
    }

    /// Types of snap points
    and SnapType =
        | Grid of size: float
        | EdgeAlign of edge: Edge * targetBounds: CanvasRectangle
        | CornerAlign of corner: Corner * targetBounds: CanvasRectangle
        | CenterAlign of axis: Axis * targetBounds: CanvasRectangle

    /// Edge types for snapping
    and Edge = Left | Right | Top | Bottom

    /// Corner types for snapping
    and Corner = TopLeft | TopRight | BottomLeft | BottomRight

    /// Axis types for center alignment
    and Axis = Horizontal | Vertical

    /// Strength of snap attraction
    and SnapStrength = Weak | Medium | Strong

    /// Canvas interaction event types
    type CanvasEvent =
        | PointerDown of DisplayId * CanvasPoint
        | PointerMove of DisplayId * CanvasPoint
        | PointerUp of DisplayId * CanvasPoint
        | KeyDown of Key
        | Scroll of CanvasPoint * float
        | DoubleClick of DisplayId * CanvasPoint
        | DisplaySelectionChanged of Set<DisplayId>

    /// Commands that can be executed on the canvas
    type CanvasCommand =
        | StartDrag of DisplayId * CanvasPoint
        | UpdateDrag of DisplayId * CanvasPoint
        | EndDrag of DisplayId * CanvasPoint
        | ToggleSnap
        | ZoomIn of CanvasPoint option
        | ZoomOut of CanvasPoint option
        | SelectDisplay of DisplayId
        | SelectMultiple of Set<DisplayId>
        | ClearSelection
        | ShowContextMenu of DisplayId * CanvasPoint
        | ResetView
        | FitToCanvas

    /// Visual state for rendering a display on the canvas
    type VisualDisplayState = {
        /// Current position on canvas
        Position: CanvasPoint
        /// Opacity for drag feedback
        Opacity: float
        /// Whether display is highlighted
        IsHighlighted: bool
        /// Whether display is selected
        IsSelected: bool
        /// Whether to show snap guides
        ShowSnapGuides: bool
        /// Bounding rectangle for hit testing
        BoundingBox: CanvasRectangle
        /// Whether display is being dragged
        IsDragging: bool
        /// Z-order for layering
        ZIndex: int
    }

    /// History entry for undo/redo functionality
    type HistoryEntry = {
        /// Command that was executed
        Command: CanvasCommand
        /// State before the command was executed
        PreviousState: CanvasState
        /// Timestamp when command was executed
        Timestamp: DateTime
        /// Human-readable description
        Description: string
    }

    /// Interaction history for undo/redo
    and History = {
        /// Past states (most recent first)
        Past: HistoryEntry list
        /// Current state
        Present: CanvasState
        /// Future states for redo (most recent first)
        Future: HistoryEntry list
        /// Maximum number of history entries to keep
        MaxSize: int
    }

    /// Complete immutable canvas state
    and CanvasState = {
        /// Transform parameters for coordinate conversion
        TransformParams: TransformParameters
        /// Active drag operations by display ID
        DragOperations: Map<DisplayId, DragOperation>
        /// Snap configuration
        SnapSettings: SnapConfiguration
        /// Currently selected displays
        SelectedDisplays: Set<DisplayId>
        /// Canvas viewport bounds
        ViewportBounds: CanvasRectangle
        /// Visual state for each display
        DisplayVisuals: Map<DisplayId, VisualDisplayState>
        /// Collision detection enabled
        CollisionDetection: bool
        /// Canvas interaction mode
        InteractionMode: InteractionMode
        /// Last update timestamp
        LastUpdate: DateTime
    }

    /// Canvas interaction modes
    and InteractionMode =
        | Normal
        | DragMode of activeDisplays: Set<DisplayId>
        | SelectionMode of selectionRect: CanvasRectangle option
        | ZoomMode

    /// Core canvas state operations
    module Core =

        /// Create default transform parameters
        let createDefaultTransform (canvasWidth: float) (canvasHeight: float) =
            {
                CenterX = canvasWidth / 2.0
                CenterY = canvasHeight / 2.0
                Scale = 0.1
                CanvasBounds = { X = 0.0; Y = 0.0; Width = canvasWidth; Height = canvasHeight }
                WindowsBounds = { X = -32768.0; Y = -32768.0; Width = 65536.0; Height = 65536.0 }
            }

        /// Create default snap configuration
        let createDefaultSnapConfig () =
            {
                GridSize = 20.0
                ProximityThreshold = 25.0
                SnapToEdges = true
                SnapToGrid = true
                ShowSnapGuides = true
                SnapEnabled = true
            }

        /// Create empty canvas state
        let createEmpty (canvasWidth: float) (canvasHeight: float) =
            {
                TransformParams = createDefaultTransform canvasWidth canvasHeight
                DragOperations = Map.empty
                SnapSettings = createDefaultSnapConfig ()
                SelectedDisplays = Set.empty
                ViewportBounds = { X = 0.0; Y = 0.0; Width = canvasWidth; Height = canvasHeight }
                DisplayVisuals = Map.empty
                CollisionDetection = true
                InteractionMode = Normal
                LastUpdate = DateTime.Now
            }

        /// Create canvas point from coordinates
        let createPoint (x: float) (y: float) = { X = x; Y = y }

        /// Create canvas rectangle from coordinates and dimensions
        let createRectangle (x: float) (y: float) (width: float) (height: float) =
            { X = x; Y = y; Width = width; Height = height }

        /// Check if a point is within a rectangle
        let isPointInRectangle (point: CanvasPoint) (rect: CanvasRectangle) =
            point.X >= rect.X && point.X <= (rect.X + rect.Width) &&
            point.Y >= rect.Y && point.Y <= (rect.Y + rect.Height)

        /// Calculate distance between two points
        let calculateDistance (point1: CanvasPoint) (point2: CanvasPoint) =
            let dx = point2.X - point1.X
            let dy = point2.Y - point1.Y
            Math.Sqrt(dx * dx + dy * dy)

        /// Get center point of a rectangle
        let getRectangleCenter (rect: CanvasRectangle) =
            createPoint (rect.X + rect.Width / 2.0) (rect.Y + rect.Height / 2.0)

        /// Check if two rectangles intersect
        let rectanglesIntersect (rect1: CanvasRectangle) (rect2: CanvasRectangle) =
            not (rect1.X + rect1.Width <= rect2.X ||
                 rect2.X + rect2.Width <= rect1.X ||
                 rect1.Y + rect1.Height <= rect2.Y ||
                 rect2.Y + rect2.Height <= rect1.Y)

    /// State transition functions
    module StateTransitions =

        /// Start a drag operation for a display
        let startDrag (displayId: DisplayId) (startPos: CanvasPoint) (state: CanvasState) : CanvasState =
            let dragOp = {
                DisplayId = displayId
                StartPosition = startPos
                CurrentPosition = startPos
                StartTime = DateTime.Now
                IsActive = true
                SnapTargets = []  // Will be calculated separately
                OriginalPosition = startPos
            }

            let updatedVisuals =
                Map.change displayId (function
                    | Some visual -> Some { visual with IsDragging = true; Opacity = 0.7 }
                    | None -> None) state.DisplayVisuals

            { state with
                DragOperations = Map.add displayId dragOp state.DragOperations
                DisplayVisuals = updatedVisuals
                InteractionMode = DragMode (Set.singleton displayId)
                LastUpdate = DateTime.Now }

        /// Update a drag operation with new position
        let updateDrag (displayId: DisplayId) (currentPos: CanvasPoint) (state: CanvasState) : CanvasState =
            match Map.tryFind displayId state.DragOperations with
            | Some dragOp when dragOp.IsActive ->
                let updated = { dragOp with CurrentPosition = currentPos }

                let updatedVisuals =
                    Map.change displayId (function
                        | Some visual -> Some { visual with Position = currentPos }
                        | None -> None) state.DisplayVisuals

                { state with
                    DragOperations = Map.add displayId updated state.DragOperations
                    DisplayVisuals = updatedVisuals
                    LastUpdate = DateTime.Now }
            | _ -> state

        /// End a drag operation
        let endDrag (displayId: DisplayId) (finalPos: CanvasPoint) (state: CanvasState) : CanvasState =
            match Map.tryFind displayId state.DragOperations with
            | Some dragOp ->
                let updatedVisuals =
                    Map.change displayId (function
                        | Some visual -> Some { visual with IsDragging = false; Opacity = 1.0; Position = finalPos }
                        | None -> None) state.DisplayVisuals

                { state with
                    DragOperations = Map.remove displayId state.DragOperations
                    DisplayVisuals = updatedVisuals
                    InteractionMode = Normal
                    LastUpdate = DateTime.Now }
            | None -> state

        /// Toggle snap settings
        let toggleSnap (state: CanvasState) : CanvasState =
            let newSnapSettings = { state.SnapSettings with SnapEnabled = not state.SnapSettings.SnapEnabled }
            { state with SnapSettings = newSnapSettings; LastUpdate = DateTime.Now }

        /// Toggle grid snapping
        let toggleGridSnap (state: CanvasState) : CanvasState =
            let newSnapSettings = { state.SnapSettings with SnapToGrid = not state.SnapSettings.SnapToGrid }
            { state with SnapSettings = newSnapSettings; LastUpdate = DateTime.Now }

        /// Toggle edge snapping
        let toggleEdgeSnap (state: CanvasState) : CanvasState =
            let newSnapSettings = { state.SnapSettings with SnapToEdges = not state.SnapSettings.SnapToEdges }
            { state with SnapSettings = newSnapSettings; LastUpdate = DateTime.Now }

        /// Select a display
        let selectDisplay (displayId: DisplayId) (state: CanvasState) : CanvasState =
            let updatedVisuals =
                state.DisplayVisuals
                |> Map.map (fun id visual ->
                    { visual with IsSelected = (id = displayId) })

            { state with
                SelectedDisplays = Set.singleton displayId
                DisplayVisuals = updatedVisuals
                LastUpdate = DateTime.Now }

        /// Select multiple displays
        let selectMultiple (displayIds: Set<DisplayId>) (state: CanvasState) : CanvasState =
            let updatedVisuals =
                state.DisplayVisuals
                |> Map.map (fun id visual ->
                    { visual with IsSelected = Set.contains id displayIds })

            { state with
                SelectedDisplays = displayIds
                DisplayVisuals = updatedVisuals
                LastUpdate = DateTime.Now }

        /// Clear all selections
        let clearSelection (state: CanvasState) : CanvasState =
            let updatedVisuals =
                state.DisplayVisuals
                |> Map.map (fun _ visual ->
                    { visual with IsSelected = false })

            { state with
                SelectedDisplays = Set.empty
                DisplayVisuals = updatedVisuals
                LastUpdate = DateTime.Now }

        /// Add a display to the canvas state
        let addDisplay (display: DisplayInfo) (position: CanvasPoint) (state: CanvasState) : CanvasState =
            let visualState = {
                Position = position
                Opacity = 1.0
                IsHighlighted = false
                IsSelected = false
                ShowSnapGuides = false
                BoundingBox = Core.createRectangle position.X position.Y (float display.Resolution.Width * state.TransformParams.Scale) (float display.Resolution.Height * state.TransformParams.Scale)
                IsDragging = false
                ZIndex = 0
            }

            { state with
                DisplayVisuals = Map.add display.Id visualState state.DisplayVisuals
                LastUpdate = DateTime.Now }

        /// Remove a display from the canvas state
        let removeDisplay (displayId: DisplayId) (state: CanvasState) : CanvasState =
            { state with
                DisplayVisuals = Map.remove displayId state.DisplayVisuals
                DragOperations = Map.remove displayId state.DragOperations
                SelectedDisplays = Set.remove displayId state.SelectedDisplays
                LastUpdate = DateTime.Now }

        /// Update transform parameters
        let updateTransform (newTransform: TransformParameters) (state: CanvasState) : CanvasState =
            { state with
                TransformParams = newTransform
                LastUpdate = DateTime.Now }

        /// Zoom in at a specific point
        let zoomIn (centerPoint: CanvasPoint option) (factor: float) (state: CanvasState) : CanvasState =
            let newScale = Math.Min(2.0, state.TransformParams.Scale * factor)
            let newTransform = { state.TransformParams with Scale = newScale }
            updateTransform newTransform state

        /// Zoom out at a specific point
        let zoomOut (centerPoint: CanvasPoint option) (factor: float) (state: CanvasState) : CanvasState =
            let newScale = Math.Max(0.05, state.TransformParams.Scale / factor)
            let newTransform = { state.TransformParams with Scale = newScale }
            updateTransform newTransform state

        /// Reset view to default
        let resetView (state: CanvasState) : CanvasState =
            let defaultTransform = Core.createDefaultTransform state.ViewportBounds.Width state.ViewportBounds.Height
            updateTransform defaultTransform state

    /// Query functions for reading state
    module Queries =

        /// Check if a display is currently being dragged
        let isDisplayBeingDragged (displayId: DisplayId) (state: CanvasState) : bool =
            Map.containsKey displayId state.DragOperations

        /// Get all displays that are currently being dragged
        let getDraggingDisplays (state: CanvasState) : DisplayId list =
            state.DragOperations |> Map.keys |> List.ofSeq

        /// Check if any displays are currently being dragged
        let hasActiveDrags (state: CanvasState) : bool =
            not (Map.isEmpty state.DragOperations)

        /// Check if a display is selected
        let isDisplaySelected (displayId: DisplayId) (state: CanvasState) : bool =
            Set.contains displayId state.SelectedDisplays

        /// Get all selected displays
        let getSelectedDisplays (state: CanvasState) : DisplayId list =
            Set.toList state.SelectedDisplays

        /// Get the number of selected displays
        let getSelectedDisplayCount (state: CanvasState) : int =
            Set.count state.SelectedDisplays

        /// Get visual state for a specific display
        let getDisplayVisual (displayId: DisplayId) (state: CanvasState) : VisualDisplayState option =
            Map.tryFind displayId state.DisplayVisuals

        /// Get all display visuals
        let getAllDisplayVisuals (state: CanvasState) : Map<DisplayId, VisualDisplayState> =
            state.DisplayVisuals

        /// Find display at a specific canvas point
        let findDisplayAt (point: CanvasPoint) (state: CanvasState) : DisplayId option =
            state.DisplayVisuals
            |> Map.tryFindKey (fun _ visual ->
                Core.isPointInRectangle point visual.BoundingBox)

        /// Get displays within a rectangular selection
        let getDisplaysInSelection (selectionRect: CanvasRectangle) (state: CanvasState) : DisplayId list =
            state.DisplayVisuals
            |> Map.filter (fun _ visual ->
                Core.rectanglesIntersect selectionRect visual.BoundingBox)
            |> Map.keys
            |> List.ofSeq

        /// Check if snap is currently enabled
        let isSnapEnabled (state: CanvasState) : bool =
            state.SnapSettings.SnapEnabled

        /// Check if grid snap is enabled
        let isGridSnapEnabled (state: CanvasState) : bool =
            state.SnapSettings.SnapToGrid && state.SnapSettings.SnapEnabled

        /// Check if edge snap is enabled
        let isEdgeSnapEnabled (state: CanvasState) : bool =
            state.SnapSettings.SnapToEdges && state.SnapSettings.SnapEnabled

    /// Validation functions
    module Validation =

        /// Validate that a canvas point is within bounds
        let validatePointInBounds (point: CanvasPoint) (bounds: CanvasRectangle) : Result<CanvasPoint, string> =
            if Core.isPointInRectangle point bounds then
                Ok point
            else
                Error (sprintf "Point (%.1f, %.1f) is outside bounds" point.X point.Y)

        /// Validate that a display position doesn't cause collisions (if collision detection is enabled)
        let validateNoCollisions (displayId: DisplayId) (position: CanvasPoint) (displaySize: CanvasPoint) (state: CanvasState) : Result<CanvasPoint, string> =
            if not state.CollisionDetection then
                Ok position
            else
                let proposedRect = Core.createRectangle position.X position.Y displaySize.X displaySize.Y
                let hasCollision =
                    state.DisplayVisuals
                    |> Map.exists (fun id visual ->
                        id <> displayId && Core.rectanglesIntersect proposedRect visual.BoundingBox)

                if hasCollision then
                    Error "Position would cause collision with another display"
                else
                    Ok position

        /// Validate canvas state consistency
        let validateStateConsistency (state: CanvasState) : Result<CanvasState, string list> =
            let errors = [
                // Check that all dragging displays have visual state
                for dragDisplayId in Map.keys state.DragOperations do
                    if not (Map.containsKey dragDisplayId state.DisplayVisuals) then
                        yield sprintf "Dragging display %A has no visual state" dragDisplayId

                // Check that all selected displays have visual state
                for selectedDisplayId in state.SelectedDisplays do
                    if not (Map.containsKey selectedDisplayId state.DisplayVisuals) then
                        yield sprintf "Selected display %A has no visual state" selectedDisplayId

                // Check that visual states are consistent with drag states
                for (id, visual) in Map.toList state.DisplayVisuals do
                    let isDragging = Map.containsKey id state.DragOperations
                    if visual.IsDragging <> isDragging then
                        yield sprintf "Visual drag state inconsistent for display %A" id

                // Check that visual states are consistent with selection states
                for (id, visual) in Map.toList state.DisplayVisuals do
                    let isSelected = Set.contains id state.SelectedDisplays
                    if visual.IsSelected <> isSelected then
                        yield sprintf "Visual selection state inconsistent for display %A" id
            ]

            if List.isEmpty errors then
                Ok state
            else
                Error errors

    /// History management for undo/redo functionality
    module History =

        /// Create empty history with current state
        let createEmpty (currentState: CanvasState) (maxSize: int) = {
            Past = []
            Present = currentState
            Future = []
            MaxSize = maxSize
        }

        /// Add a history entry for a command execution
        let addEntry (command: CanvasCommand) (previousState: CanvasState) (currentState: CanvasState) (history: History) =
            let entry = {
                Command = command
                PreviousState = previousState
                Timestamp = DateTime.Now
                Description = sprintf "Executed %A" command
            }

            {
                Past = entry :: (List.take (history.MaxSize - 1) history.Past)
                Present = currentState
                Future = []  // Clear future when new action is taken
                MaxSize = history.MaxSize
            }

        /// Undo the last action
        let undo (history: History) : History option =
            match history.Past with
            | entry :: remainingPast ->
                let newFutureEntry = {
                    Command = entry.Command
                    PreviousState = history.Present
                    Timestamp = DateTime.Now
                    Description = sprintf "Undo %s" entry.Description
                }
                Some {
                    Past = remainingPast
                    Present = entry.PreviousState
                    Future = newFutureEntry :: history.Future
                    MaxSize = history.MaxSize
                }
            | [] -> None

        /// Redo the next action
        let redo (history: History) : History option =
            match history.Future with
            | entry :: remainingFuture ->
                let newPastEntry = {
                    Command = entry.Command
                    PreviousState = history.Present
                    Timestamp = DateTime.Now
                    Description = sprintf "Redo %s" entry.Description
                }
                Some {
                    Past = newPastEntry :: history.Past
                    Present = entry.PreviousState  // This contains the "future" state
                    Future = remainingFuture
                    MaxSize = history.MaxSize
                }
            | [] -> None

        /// Check if undo is available
        let canUndo (history: History) : bool =
            not (List.isEmpty history.Past)

        /// Check if redo is available
        let canRedo (history: History) : bool =
            not (List.isEmpty history.Future)

        /// Get the number of available undo operations
        let undoCount (history: History) : int =
            List.length history.Past

        /// Get the number of available redo operations
        let redoCount (history: History) : int =
            List.length history.Future