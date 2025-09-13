# Display Compacting with Centroid Logic

## Overview
The display compacting system should respect the primary display as the centroid at position (0,0) when arranging displays. This prevents Windows from incorrectly reassigning the primary display.

## Core Principle
**Windows automatically assigns the display at position (0,0) as the primary display.** Therefore, our compacting logic must ensure the intended primary display remains at (0,0).

## Implementation Logic

### 1. Primary Display Detection
- Find the display marked as `IsPrimary = true` in the preset
- This display should be the anchor/centroid for all positioning

### 2. Centroid-Based Compacting
```fsharp
let compactDisplayPositions (displayPositions: (DisplayId * Position * DisplayInfo) list) =
    // Find the primary display - it should be the centroid at (0,0)
    let primaryDisplay = displayPositions |> List.tryFind (fun (_, _, info) -> info.IsPrimary)
    
    match primaryDisplay with
    | Some (primaryId, primaryPos, _) ->
        if primaryPos.X = 0 && primaryPos.Y = 0 then
            // Primary is already at (0,0) - no compacting needed
            displayPositions  
        else
            // Offset ALL displays to center the primary at (0,0)
            let offsetX = -primaryPos.X
            let offsetY = -primaryPos.Y
            displayPositions |> List.map (fun (id, pos, info) ->
                (id, { X = pos.X + offsetX; Y = pos.Y + offsetY }, info))
    | None ->
        // No primary found - use first display as anchor
        match displayPositions with
        | (_, firstPos, _) :: _ ->
            let offsetX = -firstPos.X
            let offsetY = -firstPos.Y
            displayPositions |> List.map (fun (id, pos, info) ->
                (id, { X = pos.X + offsetX; Y = pos.Y + offsetY }, info))
        | [] -> []
```

### 3. Coordinate Validation
After compacting, validate that all coordinates are within Windows limits:
- X coordinates: -32,768 to +32,767
- Total display width must not exceed these limits

### 4. Why This Matters
- **Preset Integrity**: Presets define exact intended positions - compacting should only translate, not rearrange
- **Primary Display Stability**: Moving the wrong display to (0,0) causes Windows to reassign primary status
- **User Expectations**: The display marked as primary in settings should remain primary after application

## Key Differences from Previous Logic
- **Old Logic**: Eliminated negative coordinates by finding min X/Y and shifting everything
- **New Logic**: Centers around the primary display, preserving relative positioning
- **Benefit**: Primary display assignment remains stable and predictable

## Edge Cases
1. **No Primary Display**: Use first display as anchor
2. **Coordinate Overflow**: Apply additional shift to fit Windows limits while preserving primary at (0,0)
3. **Single Display**: No compacting needed

## Testing Scenarios
1. Primary at (0,0) - should require no changes
2. Primary at negative coordinates - should shift all displays to center primary at (0,0)  
3. Primary at positive coordinates - should shift all displays to center primary at (0,0)
4. Multiple displays with complex arrangement - should preserve relative spacing while centering primary

This logic ensures that preset restoration maintains the intended primary display while keeping all displays positioned correctly relative to each other.