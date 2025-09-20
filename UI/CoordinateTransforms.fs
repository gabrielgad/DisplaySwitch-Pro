namespace DisplaySwitchPro

open System
open CanvasState
open DomainTypes
open EnhancedResult

/// Pure coordinate transformation functions for display canvas
/// Provides bidirectional coordinate conversion between Windows and Canvas coordinate systems
module CoordinateTransforms =

    /// Coordinate system enumeration
    type CoordinateSystem =
        | Windows  // Windows virtual desktop coordinates (-32768 to +32767)
        | Canvas   // Canvas UI coordinates (0.0 to canvas dimensions)

    /// Enhanced transform configuration with validation
    type Transform = {
        /// Scale factor for coordinate conversion
        Scale: float
        /// X offset (canvas center X coordinate)
        OffsetX: float
        /// Y offset (canvas center Y coordinate)
        OffsetY: float
        /// Canvas bounds for validation
        CanvasBounds: CanvasRectangle
        /// Windows bounds for validation
        WindowsBounds: CanvasRectangle
        /// Minimum allowed scale
        MinScale: float
        /// Maximum allowed scale
        MaxScale: float
        /// Whether to clamp coordinates to bounds
        ClampToBounds: bool
    }

    /// Transformation result with validation information
    type TransformResult<'T> = {
        /// The transformed value
        Value: 'T
        /// Whether the value was clamped to bounds
        WasClamped: bool
        /// Original value before any clamping
        OriginalValue: 'T
        /// Validation warnings (non-fatal)
        Warnings: string list
    }

    /// Core transformation functions
    module Core =

        /// Create a transform with validation
        let createTransform (canvasWidth: float) (canvasHeight: float) (scale: float) : Result<Transform, string> =
            if canvasWidth <= 0.0 || canvasHeight <= 0.0 then
                Error "Canvas dimensions must be positive"
            elif scale <= 0.0 then
                Error "Scale must be positive"
            else
                Ok {
                    Scale = scale
                    OffsetX = canvasWidth / 2.0
                    OffsetY = canvasHeight / 2.0
                    CanvasBounds = { X = 0.0; Y = 0.0; Width = canvasWidth; Height = canvasHeight }
                    WindowsBounds = { X = -32768.0; Y = -32768.0; Width = 65536.0; Height = 65536.0 }
                    MinScale = 0.01
                    MaxScale = 10.0
                    ClampToBounds = true
                }

        /// Create a transform with custom bounds
        let createTransformWithBounds (canvasWidth: float) (canvasHeight: float) (scale: float)
                                     (windowsBounds: CanvasRectangle) (clampToBounds: bool) : Result<Transform, string> =
            if canvasWidth <= 0.0 || canvasHeight <= 0.0 then
                Error "Canvas dimensions must be positive"
            elif scale <= 0.0 then
                Error "Scale must be positive"
            else
                Ok {
                    Scale = scale
                    OffsetX = canvasWidth / 2.0
                    OffsetY = canvasHeight / 2.0
                    CanvasBounds = { X = 0.0; Y = 0.0; Width = canvasWidth; Height = canvasHeight }
                    WindowsBounds = windowsBounds
                    MinScale = 0.01
                    MaxScale = 10.0
                    ClampToBounds = clampToBounds
                }

        /// Validate that a point is within the specified bounds
        let validatePointInBounds (point: CanvasPoint) (bounds: CanvasRectangle) : bool =
            point.X >= bounds.X && point.X <= (bounds.X + bounds.Width) &&
            point.Y >= bounds.Y && point.Y <= (bounds.Y + bounds.Height)

        /// Clamp a point to the specified bounds
        let clampPointToBounds (point: CanvasPoint) (bounds: CanvasRectangle) : CanvasPoint =
            {
                X = Math.Max(bounds.X, Math.Min(bounds.X + bounds.Width, point.X))
                Y = Math.Max(bounds.Y, Math.Min(bounds.Y + bounds.Height, point.Y))
            }

        /// Transform a single point between coordinate systems
        let transformPoint (transform: Transform) (fromSystem: CoordinateSystem) (point: CanvasPoint) : TransformResult<CanvasPoint> =
            let transformedPoint =
                match fromSystem with
                | Windows ->
                    // Windows (0,0) maps to canvas center (OffsetX, OffsetY)
                    {
                        X = point.X * transform.Scale + transform.OffsetX
                        Y = point.Y * transform.Scale + transform.OffsetY
                    }
                | Canvas ->
                    // Canvas center (OffsetX, OffsetY) maps to Windows (0,0)
                    {
                        X = (point.X - transform.OffsetX) / transform.Scale
                        Y = (point.Y - transform.OffsetY) / transform.Scale
                    }

            let targetBounds =
                match fromSystem with
                | Windows -> transform.CanvasBounds
                | Canvas -> transform.WindowsBounds

            let warnings = []
            let isInBounds = validatePointInBounds transformedPoint targetBounds

            let (finalPoint, wasClamped, updatedWarnings) =
                if not isInBounds && transform.ClampToBounds then
                    let clampedPoint = clampPointToBounds transformedPoint targetBounds
                    let warning = sprintf "Point (%.1f, %.1f) was clamped to bounds" transformedPoint.X transformedPoint.Y
                    (clampedPoint, true, warning :: warnings)
                elif not isInBounds then
                    let warning = sprintf "Point (%.1f, %.1f) is outside bounds but clamping is disabled" transformedPoint.X transformedPoint.Y
                    (transformedPoint, false, warning :: warnings)
                else
                    (transformedPoint, false, warnings)

            {
                Value = finalPoint
                WasClamped = wasClamped
                OriginalValue = transformedPoint
                Warnings = updatedWarnings
            }

        /// Transform a rectangle between coordinate systems
        let transformRectangle (transform: Transform) (fromSystem: CoordinateSystem) (rect: CanvasRectangle) : TransformResult<CanvasRectangle> =
            let topLeft = { X = rect.X; Y = rect.Y }
            let bottomRight = { X = rect.X + rect.Width; Y = rect.Y + rect.Height }

            let transformedTopLeft = transformPoint transform fromSystem topLeft
            let transformedBottomRight = transformPoint transform fromSystem bottomRight

            let transformedRect = {
                X = transformedTopLeft.Value.X
                Y = transformedTopLeft.Value.Y
                Width = Math.Abs(transformedBottomRight.Value.X - transformedTopLeft.Value.X)
                Height = Math.Abs(transformedBottomRight.Value.Y - transformedTopLeft.Value.Y)
            }

            let allWarnings = transformedTopLeft.Warnings @ transformedBottomRight.Warnings
            let wasClamped = transformedTopLeft.WasClamped || transformedBottomRight.WasClamped

            {
                Value = transformedRect
                WasClamped = wasClamped
                OriginalValue = transformedRect
                Warnings = allWarnings
            }

        /// Transform a list of points
        let transformPoints (transform: Transform) (fromSystem: CoordinateSystem) (points: CanvasPoint list) : TransformResult<CanvasPoint list> =
            let results = points |> List.map (transformPoint transform fromSystem)
            let transformedPoints = results |> List.map (fun r -> r.Value)
            let allWarnings = results |> List.collect (fun r -> r.Warnings)
            let anyWasClamped = results |> List.exists (fun r -> r.WasClamped)
            let originalPoints = results |> List.map (fun r -> r.OriginalValue)

            {
                Value = transformedPoints
                WasClamped = anyWasClamped
                OriginalValue = originalPoints
                Warnings = allWarnings |> List.distinct
            }

        /// Validate transform parameters
        let validateTransform (transform: Transform) : Result<Transform, string list> =
            let errors = [
                if transform.Scale < transform.MinScale then
                    yield sprintf "Scale %.3f is below minimum %.3f" transform.Scale transform.MinScale
                if transform.Scale > transform.MaxScale then
                    yield sprintf "Scale %.3f exceeds maximum %.3f" transform.Scale transform.MaxScale
                if transform.CanvasBounds.Width <= 0.0 || transform.CanvasBounds.Height <= 0.0 then
                    yield "Canvas bounds must have positive dimensions"
                if transform.WindowsBounds.Width <= 0.0 || transform.WindowsBounds.Height <= 0.0 then
                    yield "Windows bounds must have positive dimensions"
            ]

            if List.isEmpty errors then
                Ok transform
            else
                Error errors

    /// Display-specific coordinate operations
    module Display =

        /// Calculate display bounds in canvas coordinates
        let calculateDisplayBounds (display: DisplayInfo) (transform: Transform) : TransformResult<CanvasRectangle> =
            let windowsPos = { X = float display.Position.X; Y = float display.Position.Y }
            let windowsRect = {
                X = windowsPos.X
                Y = windowsPos.Y
                Width = float display.Resolution.Width
                Height = float display.Resolution.Height
            }

            Core.transformRectangle transform Windows windowsRect

        /// Calculate all display bounds for a list of displays
        let calculateAllDisplayBounds (displays: DisplayInfo list) (transform: Transform) : Map<DisplayId, TransformResult<CanvasRectangle>> =
            displays
            |> List.filter (fun d -> d.IsEnabled)
            |> List.map (fun d -> (d.Id, calculateDisplayBounds d transform))
            |> Map.ofList

        /// Find display at a specific canvas point
        let findDisplayAt (point: CanvasPoint) (displayBounds: Map<DisplayId, TransformResult<CanvasRectangle>>) : DisplayId option =
            displayBounds
            |> Map.tryFindKey (fun _ boundsResult ->
                let bounds = boundsResult.Value
                Core.validatePointInBounds point bounds)

        /// Calculate the total bounds of all displays in canvas coordinates
        let calculateTotalDisplayBounds (displays: DisplayInfo list) (transform: Transform) : TransformResult<CanvasRectangle> option =
            let enabledDisplays = displays |> List.filter (fun d -> d.IsEnabled)
            if List.isEmpty enabledDisplays then
                None
            else
                let displayBounds = enabledDisplays |> List.map (fun d -> calculateDisplayBounds d transform)
                let allBounds = displayBounds |> List.map (fun r -> r.Value)

                let minX = allBounds |> List.map (fun b -> b.X) |> List.min
                let minY = allBounds |> List.map (fun b -> b.Y) |> List.min
                let maxX = allBounds |> List.map (fun b -> b.X + b.Width) |> List.max
                let maxY = allBounds |> List.map (fun b -> b.Y + b.Height) |> List.max

                let totalBounds = {
                    X = minX
                    Y = minY
                    Width = maxX - minX
                    Height = maxY - minY
                }

                let allWarnings = displayBounds |> List.collect (fun r -> r.Warnings) |> List.distinct
                let anyWasClamped = displayBounds |> List.exists (fun r -> r.WasClamped)

                Some {
                    Value = totalBounds
                    WasClamped = anyWasClamped
                    OriginalValue = totalBounds
                    Warnings = allWarnings
                }

        /// Convert canvas coordinates to display position for a specific display
        let canvasToDisplayPosition (canvasPoint: CanvasPoint) (transform: Transform) : Result<Position, string> =
            let result = Core.transformPoint transform Canvas canvasPoint

            if not (List.isEmpty result.Warnings) then
                let warningsStr = String.concat "; " result.Warnings
                Error (sprintf "Coordinate transformation warnings: %s" warningsStr)
            else
                let windowsPoint = result.Value
                ValidatedPosition.create (int (Math.Round(windowsPoint.X))) (int (Math.Round(windowsPoint.Y)))
                |> Result.map Compatibility.toPosition
                |> Result.mapError (fun validationError ->
                    ValidationUtils.formatValidationError validationError)

        /// Convert display position to canvas coordinates
        let displayToCanvasPosition (position: Position) (transform: Transform) : TransformResult<CanvasPoint> =
            let windowsPoint = { X = float position.X; Y = float position.Y }
            Core.transformPoint transform Windows windowsPoint

    /// Zoom and scale operations
    module Zoom =

        /// Calculate new transform for zoom operation
        let calculateZoomTransform (currentTransform: Transform) (zoomFactor: float) (zoomCenter: CanvasPoint option) : Result<Transform, string> =
            let newScale = currentTransform.Scale * zoomFactor

            if newScale < currentTransform.MinScale || newScale > currentTransform.MaxScale then
                Error (sprintf "Zoom would result in invalid scale %.3f (valid range: %.3f - %.3f)"
                       newScale currentTransform.MinScale currentTransform.MaxScale)
            else
                match zoomCenter with
                | None ->
                    // Zoom from canvas center - no offset adjustment needed
                    Ok { currentTransform with Scale = newScale }
                | Some center ->
                    // Zoom from specific point - adjust offsets to keep the zoom center stationary
                    let oldWorldPoint = Core.transformPoint currentTransform Canvas center
                    let newTransform = { currentTransform with Scale = newScale }
                    let newWorldPoint = Core.transformPoint newTransform Canvas center

                    let deltaX = oldWorldPoint.Value.X - newWorldPoint.Value.X
                    let deltaY = oldWorldPoint.Value.Y - newWorldPoint.Value.Y

                    Ok { newTransform with
                           OffsetX = newTransform.OffsetX + (deltaX * newScale)
                           OffsetY = newTransform.OffsetY + (deltaY * newScale) }

        /// Zoom in by a factor
        let zoomIn (currentTransform: Transform) (factor: float) (center: CanvasPoint option) : Result<Transform, string> =
            calculateZoomTransform currentTransform factor center

        /// Zoom out by a factor
        let zoomOut (currentTransform: Transform) (factor: float) (center: CanvasPoint option) : Result<Transform, string> =
            calculateZoomTransform currentTransform (1.0 / factor) center

        /// Zoom to fit all displays within the canvas
        let zoomToFitDisplays (displays: DisplayInfo list) (canvasWidth: float) (canvasHeight: float) (margin: float) : Result<Transform, string> =
            let enabledDisplays = displays |> List.filter (fun d -> d.IsEnabled)
            if List.isEmpty enabledDisplays then
                Error "Cannot zoom to fit - no enabled displays"
            else
                // Calculate total bounds in Windows coordinates
                let positions = enabledDisplays |> List.map (fun d -> d.Position)
                let sizes = enabledDisplays |> List.map (fun d -> d.Resolution)

                let minX = positions |> List.map (fun p -> p.X) |> List.min |> float
                let minY = positions |> List.map (fun p -> p.Y) |> List.min |> float
                let maxX = (List.zip positions sizes) |> List.map (fun (p, s) -> p.X + s.Width) |> List.max |> float
                let maxY = (List.zip positions sizes) |> List.map (fun (p, s) -> p.Y + s.Height) |> List.max |> float

                let totalWidth = maxX - minX
                let totalHeight = maxY - minY
                let centerX = (minX + maxX) / 2.0
                let centerY = (minY + maxY) / 2.0

                // Calculate scale to fit with margin
                let availableWidth = canvasWidth - (2.0 * margin)
                let availableHeight = canvasHeight - (2.0 * margin)
                let scaleX = availableWidth / totalWidth
                let scaleY = availableHeight / totalHeight
                let scale = Math.Min(scaleX, scaleY)

                // Calculate offsets to center the displays
                let offsetX = canvasWidth / 2.0 - centerX * scale
                let offsetY = canvasHeight / 2.0 - centerY * scale

                Core.createTransform canvasWidth canvasHeight scale
                |> Result.map (fun transform ->
                    { transform with OffsetX = offsetX; OffsetY = offsetY })

        /// Reset zoom to default (1:1 scale with primary display centered)
        let resetZoom (displays: DisplayInfo list) (canvasWidth: float) (canvasHeight: float) (defaultScale: float) : Result<Transform, string> =
            let primaryDisplay = displays |> List.tryFind (fun d -> d.IsPrimary && d.IsEnabled)
            match primaryDisplay with
            | Some primary ->
                // Center the primary display
                let offsetX = canvasWidth / 2.0 - (float primary.Position.X * defaultScale)
                let offsetY = canvasHeight / 2.0 - (float primary.Position.Y * defaultScale)

                Core.createTransform canvasWidth canvasHeight defaultScale
                |> Result.map (fun transform ->
                    { transform with OffsetX = offsetX; OffsetY = offsetY })
            | None ->
                // No primary display, just center at origin
                Core.createTransform canvasWidth canvasHeight defaultScale

    /// Pan operations for moving the view
    module Pan =

        /// Calculate new transform for pan operation
        let calculatePanTransform (currentTransform: Transform) (deltaX: float) (deltaY: float) : Transform =
            { currentTransform with
                OffsetX = currentTransform.OffsetX + deltaX
                OffsetY = currentTransform.OffsetY + deltaY }

        /// Pan by pixel amounts
        let panBy (currentTransform: Transform) (deltaX: float) (deltaY: float) : Transform =
            calculatePanTransform currentTransform deltaX deltaY

        /// Pan to center a specific Windows coordinate
        let panToCenter (currentTransform: Transform) (windowsPoint: CanvasPoint) : Transform =
            let desiredCanvasCenter = {
                X = currentTransform.CanvasBounds.Width / 2.0
                Y = currentTransform.CanvasBounds.Height / 2.0
            }

            let currentCanvasPoint = Core.transformPoint currentTransform Windows windowsPoint
            let deltaX = desiredCanvasCenter.X - currentCanvasPoint.Value.X
            let deltaY = desiredCanvasCenter.Y - currentCanvasPoint.Value.Y

            calculatePanTransform currentTransform deltaX deltaY

        /// Pan to make a specific display visible and centered
        let panToDisplay (currentTransform: Transform) (display: DisplayInfo) : Transform =
            let displayCenter = {
                X = float display.Position.X + (float display.Resolution.Width / 2.0)
                Y = float display.Position.Y + (float display.Resolution.Height / 2.0)
            }
            panToCenter currentTransform displayCenter

    /// Utility functions for coordinate calculations
    module Utils =

        /// Calculate the distance between two points in the same coordinate system
        let calculateDistance (point1: CanvasPoint) (point2: CanvasPoint) : float =
            let dx = point2.X - point1.X
            let dy = point2.Y - point1.Y
            Math.Sqrt(dx * dx + dy * dy)

        /// Calculate the angle between two points (in radians)
        let calculateAngle (point1: CanvasPoint) (point2: CanvasPoint) : float =
            let dx = point2.X - point1.X
            let dy = point2.Y - point1.Y
            Math.Atan2(dy, dx)

        /// Convert angle from radians to degrees
        let radiansToDegrees (radians: float) : float =
            radians * 180.0 / Math.PI

        /// Convert angle from degrees to radians
        let degreesToRadians (degrees: float) : float =
            degrees * Math.PI / 180.0

        /// Check if a point is approximately equal to another point within tolerance
        let approximatelyEqual (point1: CanvasPoint) (point2: CanvasPoint) (tolerance: float) : bool =
            Math.Abs(point1.X - point2.X) <= tolerance && Math.Abs(point1.Y - point2.Y) <= tolerance

        /// Round point coordinates to integer values
        let roundPoint (point: CanvasPoint) : CanvasPoint =
            { X = Math.Round(point.X); Y = Math.Round(point.Y) }

        /// Interpolate between two points
        let interpolatePoints (point1: CanvasPoint) (point2: CanvasPoint) (t: float) : CanvasPoint =
            {
                X = point1.X + (point2.X - point1.X) * t
                Y = point1.Y + (point2.Y - point1.Y) * t
            }

        /// Get the bounding rectangle for a list of points
        let getBoundingRectangle (points: CanvasPoint list) : CanvasRectangle option =
            if List.isEmpty points then
                None
            else
                let xs = points |> List.map (fun p -> p.X)
                let ys = points |> List.map (fun p -> p.Y)
                let minX = List.min xs
                let minY = List.min ys
                let maxX = List.max xs
                let maxY = List.max ys

                Some {
                    X = minX
                    Y = minY
                    Width = maxX - minX
                    Height = maxY - minY
                }

    /// Validation and testing utilities
    module Testing =

        /// Test that coordinate transformation is bidirectional (reversible)
        let testBidirectionalTransformation (transform: Transform) (originalPoint: CanvasPoint) (tolerance: float) : bool =
            let toCanvas = Core.transformPoint transform Windows originalPoint
            let backToWindows = Core.transformPoint transform Canvas toCanvas.Value
            Utils.approximatelyEqual originalPoint backToWindows.Value tolerance

        /// Test transformation with a range of points
        let testTransformationRange (transform: Transform) (testPoints: CanvasPoint list) (tolerance: float) : (CanvasPoint * bool) list =
            testPoints
            |> List.map (fun point ->
                (point, testBidirectionalTransformation transform point tolerance))

        /// Generate test points for validation
        let generateTestPoints (bounds: CanvasRectangle) (count: int) : CanvasPoint list =
            let random = System.Random()
            [1..count]
            |> List.map (fun _ -> {
                X = bounds.X + random.NextDouble() * bounds.Width
                Y = bounds.Y + random.NextDouble() * bounds.Height
            })

        /// Validate that a transform produces consistent results
        let validateTransformConsistency (transform: Transform) : Result<unit, string> =
            let testPoints = [
                { X = 0.0; Y = 0.0 }  // Origin
                { X = 1920.0; Y = 1080.0 }  // Common resolution
                { X = -1920.0; Y = -1080.0 }  // Negative coordinates
            ]

            let allValid = testPoints |> List.forall (testBidirectionalTransformation transform >> id <| 0.001)

            if allValid then
                Ok ()
            else
                Error "Transform failed bidirectional consistency test"