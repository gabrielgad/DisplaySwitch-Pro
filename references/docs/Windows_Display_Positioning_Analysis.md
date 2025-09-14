# Windows Display Positioning Analysis

## Windows CCD API Display Positioning Requirements

### Coordinate System Structure
- **Data Type**: `POINTL` uses 32-bit signed integers for x,y coordinates
- **Practical Range**: -32,768 to +32,767 pixels (despite theoretical 32-bit range)
- **Primary Display**: Always anchored at (0,0) as reference point
- **Desktop Coordinates**: All displays positioned relative to primary display

### Strict Validation Rules
1. **Coordinate Range**: -32,768 ≤ x, y ≤ +32,767
2. **Adjacency**: Displays must touch at edges (no gaps allowed)
3. **No Overlaps**: Display rectangles cannot intersect
4. **Primary Fixed**: Primary display cannot move from (0,0)
5. **Path Integrity**: Each display needs valid CCD path configuration

### Common API Failure Patterns
- `ERROR_INVALID_PARAMETER`: Coordinates outside valid range
- Layout Violations: Gaps or overlaps causing GDI rearrangement
- Path Configuration Errors: Invalid source/target relationships
- Mode Incompatibility: Resolution/position combinations not supported

## Current Implementation Issues

### Problem 1: Aggressive 20px Tolerance
**Location**: `API/DisplayControl.fs:214`
```fsharp
let tolerance = 20  // Too aggressive - groups displays that shouldn't be grouped
```

**Issue**: Algorithm creates its own interpretation instead of preserving user canvas layout.

### Problem 2: Flawed Compacting Logic
**Current Broken Approach**:
```fsharp
// Groups displays by rounded positions (loses precision)
displayPositions
|> List.groupBy (fun (_, pos, _) -> pos.X / tolerance * tolerance)
```

**Result**: Overwrites user intent from canvas positioning.

### Problem 3: Late Coordinate Validation
**Current Implementation**:
```fsharp
// Warning happens after damage is done
if pos.X < -32768 || pos.X > 32767 then
    printfn "[DEBUG] WARNING: Coordinates exceed Windows limits"
```

**Problem**: No prevention of invalid coordinates, shift correction applied too late.

## Root Cause Analysis

The fundamental issue is that the compacting algorithm **ignores user intent from canvas positioning** and instead:

1. **Overwrites Canvas Layout**: Completely disregards where user placed displays
2. **Uses Flawed Math**: Creates compound negative coordinates instead of adjacent placement
3. **Lacks Windows Validation**: Doesn't prevent coordinate range violations upfront

## Recommended Solutions

### 1. Replace Tolerance-Based Grouping
```fsharp
// Preserve canvas order instead of tolerance-based grouping
let extractCanvasOrder (displayPositions: (DisplayId * Position * DisplayInfo) list) =
    displayPositions
    |> List.sortBy (fun (_, pos, _) -> pos.X, pos.Y)  // Sort by canvas position
    |> List.mapi (fun index (id, pos, info) -> (index, id, pos, info))
```

### 2. Implement True Adjacent Compacting
```fsharp
let compactToAdjacentLayout (sortedDisplays: (int * DisplayId * Position * DisplayInfo) list) =
    sortedDisplays
    |> List.fold (fun (currentX, result) (index, id, originalPos, info) ->
        let newPos = { X = currentX; Y = 0 }  // Start with Y=0, adjust later
        let displayWidth = int info.Resolution.Width
        (currentX + displayWidth, (id, newPos, info) :: result)
    ) (0, [])
    |> snd |> List.rev
```

### 3. Add Upfront Coordinate Validation
```fsharp
let validateWindowsCoordinates (positions: (DisplayId * Position * DisplayInfo) list) =
    let coordinateErrors = 
        positions
        |> List.choose (fun (id, pos, info) ->
            let maxX = pos.X + int info.Resolution.Width
            if pos.X < -32768 || pos.X > 32767 || maxX > 32767 then
                Some (sprintf "Display %s coordinates (%d, %d) exceed Windows limits" id pos.X pos.Y)
            else None)
    
    if List.isEmpty coordinateErrors then Ok positions
    else Error (String.concat "; " coordinateErrors)
```

### 4. Canvas-to-Windows Integration Strategy

The canvas should be the **single source of truth** for user intent:

1. **Preserve Spatial Relationships**: Maintain left-to-right, top-to-bottom order from canvas
2. **Apply Minimal Compacting**: Only eliminate gaps, don't rearrange
3. **Validate Before Windows API**: Catch coordinate violations early
4. **Provide User Feedback**: Show when arrangements exceed Windows limits

## Implementation Priority

1. **High Priority**: Fix compacting algorithm to preserve canvas order
2. **High Priority**: Add coordinate validation before CCD API calls  
3. **Medium Priority**: Implement canvas bounds checking during drag operations
4. **Medium Priority**: Add visual feedback for Windows coordinate limits on canvas

## Key Insight

The current 20px tolerance approach fundamentally misunderstands the problem - it should preserve user intent from the canvas, not create its own interpretation of display layout.