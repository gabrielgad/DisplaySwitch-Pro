namespace DisplaySwitchPro

open System

/// Enhanced domain-specific types for improved type safety and validation
/// This module provides strongly-typed wrappers around primitive types to prevent invalid data
module DomainTypes =

    /// Domain-specific error types for better error classification
    type ValidationError =
        | EmptyDisplayId
        | InvalidPixelDimension of value: int * reason: string
        | InvalidRefreshRate of value: int * reason: string
        | InvalidPosition of x: int * y: int * reason: string
        | InvalidConfiguration of reason: string

    /// Strongly-typed display identifier that cannot be empty or null
    type DisplayId = private DisplayId of string

    module DisplayId =
        /// Create a display ID with validation
        let create (id: string) : Result<DisplayId, ValidationError> =
            if String.IsNullOrWhiteSpace(id) then
                Error EmptyDisplayId
            else
                Ok (DisplayId id)

        /// Extract the string value from a DisplayId
        let value (DisplayId id) = id

        /// Try to create a DisplayId, returning None if invalid
        let tryCreate (id: string) =
            match create id with
            | Ok displayId -> Some displayId
            | Error _ -> None

        /// Comparison function for sorting
        let compare (DisplayId id1) (DisplayId id2) = String.Compare(id1, id2)

    /// Strongly-typed pixel dimension that ensures positive values within reasonable limits
    type PixelDimension = private PixelDimension of int

    module PixelDimension =
        /// Create a pixel dimension with validation
        let create (pixels: int) : Result<PixelDimension, ValidationError> =
            if pixels <= 0 then
                Error (InvalidPixelDimension (pixels, "Pixel dimension must be positive"))
            elif pixels > 16384 then
                Error (InvalidPixelDimension (pixels, "Pixel dimension exceeds maximum supported value (16384)"))
            else
                Ok (PixelDimension pixels)

        /// Extract the integer value from a PixelDimension
        let value (PixelDimension pixels) = pixels

        /// Try to create a PixelDimension, returning None if invalid
        let tryCreate (pixels: int) =
            match create pixels with
            | Ok pixelDim -> Some pixelDim
            | Error _ -> None

        /// Create from a potentially unsafe integer, clamping to valid range
        let createSafe (pixels: int) =
            let clampedPixels = Math.Max(1, Math.Min(16384, pixels))
            PixelDimension clampedPixels

        /// Common standard pixel dimensions
        let standard720p = PixelDimension 720
        let standard1080p = PixelDimension 1080
        let standard1440p = PixelDimension 1440
        let standard2160p = PixelDimension 2160

    /// Strongly-typed refresh rate that ensures valid Hz values
    type RefreshRate = private RefreshRate of int

    module RefreshRate =
        /// Create a refresh rate with validation
        let create (hz: int) : Result<RefreshRate, ValidationError> =
            if hz <= 0 then
                Error (InvalidRefreshRate (hz, "Refresh rate must be positive"))
            elif hz > 1000 then
                Error (InvalidRefreshRate (hz, "Refresh rate exceeds reasonable maximum (1000Hz)"))
            elif hz < 24 then
                Error (InvalidRefreshRate (hz, "Refresh rate too low for practical use"))
            else
                Ok (RefreshRate hz)

        /// Extract the integer value from a RefreshRate
        let value (RefreshRate hz) = hz

        /// Try to create a RefreshRate, returning None if invalid
        let tryCreate (hz: int) =
            match create hz with
            | Ok refreshRate -> Some refreshRate
            | Error _ -> None

        /// Create from a potentially unsafe integer, clamping to valid range
        let createSafe (hz: int) =
            let clampedHz = Math.Max(24, Math.Min(1000, hz))
            RefreshRate clampedHz

        /// Common standard refresh rates
        let hz24 = RefreshRate 24
        let hz30 = RefreshRate 30
        let hz60 = RefreshRate 60
        let hz75 = RefreshRate 75
        let hz120 = RefreshRate 120
        let hz144 = RefreshRate 144
        let hz240 = RefreshRate 240

    /// Validated position in virtual desktop coordinate space
    type ValidatedPosition = private ValidatedPosition of x: int * y: int

    module ValidatedPosition =
        /// Create a validated position within Windows coordinate limits
        let create (x: int) (y: int) : Result<ValidatedPosition, ValidationError> =
            if x < -32768 || x > 32767 then
                Error (InvalidPosition (x, y, "X coordinate outside valid range (-32768 to 32767)"))
            elif y < -32768 || y > 32767 then
                Error (InvalidPosition (x, y, "Y coordinate outside valid range (-32768 to 32767)"))
            else
                Ok (ValidatedPosition (x, y))

        /// Extract the coordinate values from a ValidatedPosition
        let value (ValidatedPosition (x, y)) = (x, y)

        /// Create X and Y accessors
        let x (ValidatedPosition (x, _)) = x
        let y (ValidatedPosition (_, y)) = y

        /// Try to create a ValidatedPosition, returning None if invalid
        let tryCreate (x: int) (y: int) =
            match create x y with
            | Ok pos -> Some pos
            | Error _ -> None

        /// Create from potentially unsafe coordinates, clamping to valid range
        let createSafe (x: int) (y: int) =
            let clampedX = Math.Max(-32768, Math.Min(32767, x))
            let clampedY = Math.Max(-32768, Math.Min(32767, y))
            ValidatedPosition (clampedX, clampedY)

        /// Zero position (origin)
        let zero = ValidatedPosition (0, 0)

        /// Calculate distance between two positions
        let distance (pos1: ValidatedPosition) (pos2: ValidatedPosition) =
            let (x1, y1) = value pos1
            let (x2, y2) = value pos2
            let dx = float (x2 - x1)
            let dy = float (y2 - y1)
            Math.Sqrt(dx * dx + dy * dy)

    /// Enhanced resolution type with domain-specific validation
    type ValidatedResolution = private ValidatedResolution of width: PixelDimension * height: PixelDimension * refreshRate: RefreshRate

    module ValidatedResolution =
        /// Create a validated resolution from components
        let create (width: PixelDimension) (height: PixelDimension) (refreshRate: RefreshRate) =
            ValidatedResolution (width, height, refreshRate)

        /// Create a validated resolution from raw integers with validation
        let createFromInts (width: int) (height: int) (refreshRate: int) : Result<ValidatedResolution, ValidationError list> =
            let widthResult = PixelDimension.create width
            let heightResult = PixelDimension.create height
            let refreshResult = RefreshRate.create refreshRate

            match widthResult, heightResult, refreshResult with
            | Ok w, Ok h, Ok r -> Ok (ValidatedResolution (w, h, r))
            | Error e1, Ok _, Ok _ -> Error [e1]
            | Ok _, Error e2, Ok _ -> Error [e2]
            | Ok _, Ok _, Error e3 -> Error [e3]
            | Error e1, Error e2, Ok _ -> Error [e1; e2]
            | Error e1, Ok _, Error e3 -> Error [e1; e3]
            | Ok _, Error e2, Error e3 -> Error [e2; e3]
            | Error e1, Error e2, Error e3 -> Error [e1; e2; e3]

        /// Extract the components from a ValidatedResolution
        let components (ValidatedResolution (w, h, r)) = (w, h, r)

        /// Extract width as PixelDimension
        let width (ValidatedResolution (w, _, _)) = w

        /// Extract height as PixelDimension
        let height (ValidatedResolution (_, h, _)) = h

        /// Extract refresh rate as RefreshRate
        let refreshRate (ValidatedResolution (_, _, r)) = r

        /// Convert to raw integer values
        let toInts (ValidatedResolution (w, h, r)) =
            (PixelDimension.value w, PixelDimension.value h, RefreshRate.value r)

        /// Calculate aspect ratio
        let aspectRatio (ValidatedResolution (w, h, _)) =
            let widthFloat = float (PixelDimension.value w)
            let heightFloat = float (PixelDimension.value h)
            widthFloat / heightFloat

        /// Check if resolution is widescreen (aspect ratio > 1.5)
        let isWidescreen (resolution: ValidatedResolution) =
            aspectRatio resolution > 1.5

        /// Common standard resolutions
        let hd720p60 =
            ValidatedResolution (PixelDimension.createSafe 1280, PixelDimension.createSafe 720, RefreshRate.hz60)
        let fullHd1080p60 =
            ValidatedResolution (PixelDimension.createSafe 1920, PixelDimension.createSafe 1080, RefreshRate.hz60)
        let quadHd1440p60 =
            ValidatedResolution (PixelDimension.createSafe 2560, PixelDimension.createSafe 1440, RefreshRate.hz60)
        let ultraHd4k60 =
            ValidatedResolution (PixelDimension.createSafe 3840, PixelDimension.createSafe 2160, RefreshRate.hz60)

    /// Conversion functions to maintain backward compatibility with existing types
    module Compatibility =
        /// Convert from existing Resolution type to ValidatedResolution
        let fromResolution (res: Resolution) : Result<ValidatedResolution, ValidationError list> =
            ValidatedResolution.createFromInts res.Width res.Height res.RefreshRate

        /// Convert from ValidatedResolution to existing Resolution type
        let toResolution (validatedRes: ValidatedResolution) : Resolution =
            let (width, height, refreshRate) = ValidatedResolution.toInts validatedRes
            { Width = width; Height = height; RefreshRate = refreshRate }

        /// Convert from existing Position type to ValidatedPosition
        let fromPosition (pos: Position) : Result<ValidatedPosition, ValidationError> =
            ValidatedPosition.create pos.X pos.Y

        /// Convert from ValidatedPosition to existing Position type
        let toPosition (validatedPos: ValidatedPosition) : Position =
            let (x, y) = ValidatedPosition.value validatedPos
            { X = x; Y = y }

        /// Convert from string to DisplayId
        let fromString (str: string) : Result<DisplayId, ValidationError> =
            DisplayId.create str

        /// Convert from DisplayId to string
        let toString (displayId: DisplayId) : string =
            DisplayId.value displayId

    /// Validation utilities for collections and complex types
    module ValidationUtils =
        /// Validate a list of items and collect all errors
        let validateAll (validator: 'a -> Result<'b, 'c>) (items: 'a list) : Result<'b list, 'c list> =
            let results = items |> List.map validator
            let errors = results |> List.choose (function Error e -> Some e | Ok _ -> None)
            let successes = results |> List.choose (function Ok s -> Some s | Error _ -> None)

            if List.isEmpty errors then
                Ok successes
            else
                Error errors

        /// Validate that exactly one item in a list satisfies a predicate
        let validateExactlyOne (predicate: 'a -> bool) (items: 'a list) (errorMsg: string) : Result<'a, ValidationError> =
            let matching = items |> List.filter predicate
            match matching with
            | [single] -> Ok single
            | [] -> Error (InvalidConfiguration (sprintf "%s - no matching items found" errorMsg))
            | _ -> Error (InvalidConfiguration (sprintf "%s - multiple matching items found" errorMsg))

        /// Validate that all items in a list are unique according to a key function
        let validateUnique (keySelector: 'a -> 'b) (items: 'a list) (errorMsg: string) : Result<'a list, ValidationError> =
            let keys = items |> List.map keySelector
            let uniqueKeys = keys |> List.distinct
            if List.length keys = List.length uniqueKeys then
                Ok items
            else
                Error (InvalidConfiguration (sprintf "%s - duplicate items found" errorMsg))

        /// Format validation errors for display
        let formatValidationError (error: ValidationError) : string =
            match error with
            | EmptyDisplayId -> "Display ID cannot be empty"
            | InvalidPixelDimension (value, reason) -> sprintf "Invalid pixel dimension %d: %s" value reason
            | InvalidRefreshRate (value, reason) -> sprintf "Invalid refresh rate %d: %s" value reason
            | InvalidPosition (x, y, reason) -> sprintf "Invalid position (%d, %d): %s" x y reason
            | InvalidConfiguration reason -> sprintf "Configuration error: %s" reason

        /// Format multiple validation errors
        let formatValidationErrors (errors: ValidationError list) : string =
            errors
            |> List.map formatValidationError
            |> String.concat "; "