# Display Positioning Analysis

## Problem Discovery

During preset restoration testing, we discovered that applying the "NoTV" preset incorrectly changed the primary display from DISPLAY1 to DISPLAY2, despite the preset clearly specifying DISPLAY1 as primary.

## Root Cause Analysis

### What We Expected
- DISPLAY1: Position (0, 0), Primary=true
- DISPLAY2: Position (-1920, 0), Primary=false  
- DISPLAY3: Position (1920, 0), Primary=false

### What Actually Happened
After preset application:
- DISPLAY2: Position (0, 0), Primary=true ❌ (became primary incorrectly)
- DISPLAY1: Position (1920, 0), Primary=false ❌ 
- DISPLAY3: Position (3840, 0), Primary=false ❌

## Technical Root Cause

### Windows Display Positioning Behavior
1. **Windows supports negative coordinates** - displays can be positioned at (-1920, 0) without issues
2. **Windows automatically assigns primary status** to whichever display is at position (0, 0)
3. **CCD API positioning calls** can change primary display assignment based on positioning

### The Flawed Compacting Logic

The `compactDisplayPositions` function was **overly aggressive**:

```fsharp
// This logic was wrong - eliminates valid negative coordinates
let minX = sortedDisplays |> List.map (fun (_, pos, _) -> pos.X) |> List.min
let shiftX = if minX < 0 then -minX else 0  // Forces elimination of negatives
```

**Problem**: The function assumed negative coordinates needed "fixing" when they're perfectly valid.

**Result**: 
1. Detects DISPLAY2 at (-1920, 0) as "negative coordinate problem"
2. Shifts everything right by 1920 pixels to eliminate negatives
3. DISPLAY2 moves from (-1920, 0) to (0, 0) 
4. Windows sees DISPLAY2 at (0, 0) and makes it primary automatically

## Pipeline Order Analysis

Current preset application order:
1. Disable displays that should be disabled
2. Enable displays that should be enabled  
3. Set primary display (this step can fail)
4. **Apply positions with compacting** ← Problem occurs here

The position compacting was overriding the primary display assignment.

## Key Insights

### When Compacting Should Occur
- **Only when primary display is NOT at (0, 0)** - need to move primary to (0, 0) and offset others
- **Only when there are actual overlaps** between displays
- **Only when coordinates exceed Windows limits** (-32768 to +32767)

### When Compacting Should NOT Occur  
- **When preset positions are already valid** (primary at 0,0, no overlaps, within limits)
- **When negative coordinates are intentional** (displays to the left of primary)

### Presets Are Pre-Defined Correct Positions
**Key realization**: Presets store positions that were already working correctly when saved. The compacting logic should respect these positions unless there's an actual problem to fix.

## Solution Strategy

### Fixed Compacting Logic
1. **Check if primary display is already at (0, 0)** - if yes, check for other issues only
2. **Check for actual overlaps** - only compact if displays truly overlap
3. **Check coordinate limits** - only adjust if exceeding Windows (-32768 to +32767) limits  
4. **Preserve negative coordinates** - they're valid for displays left of primary
5. **Only apply minimal corrections** when problems exist

### Benefits
- Respects preset positions as intended
- Only corrects actual positioning problems
- Maintains user's intended display arrangements
- Eliminates unnecessary coordinate shifting
- Prevents primary display reassignment issues

## Test Case Validation

The "NoTV" preset should apply exactly as stored:
- DISPLAY1 at (0, 0) as primary ✓
- DISPLAY2 at (-1920, 0) to the left ✓
- DISPLAY3 at (1920, 0) to the right ✓
- No compacting needed - positions are already perfect ✓

## Implementation Notes

The fix involves modifying `compactDisplayPositions` in `/API/DisplayControl.fs` to:
1. Add validation checks before applying any transformations
2. Respect primary display positioning from presets
3. Only compact when there are actual geometric or limit violations
4. Preserve valid negative coordinates

This maintains the existing compacting logic for edge cases while preventing unnecessary "corrections" to already-valid preset positions.