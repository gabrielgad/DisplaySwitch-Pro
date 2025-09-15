# Display Control Issue Analysis

## Problem Summary

The display control system is failing to enable/disable displays because of a mismatch between the new display ID format and the legacy display number parsing logic.

## Root Cause Analysis

### Issue Location
File: `API/Windows/CCDPathManagement.fs`

### Problem Description
The CCD path management functions are trying to parse display numbers from display IDs, but they expect the old format while receiving the new format:

**Expected Format (Legacy)**: `\\.\DISPLAY3`
**Actual Format (New)**: `Display3`

### Affected Functions

1. **`findDisplayPathBySourceId`** (Line ~115)
   ```fsharp
   let displayNumber =
       if (displayId: string).StartsWith(@"\\.\DISPLAY") then
           let mutable result = 0
           match System.Int32.TryParse((displayId: string).Substring(11), &result) with
           | true -> Some result
           | false -> None
       else None
   ```

2. **`findDisplayPathByDevice`** (Line ~138)
   ```fsharp
   let displayNumber =
       if (displayId: string).StartsWith(@"\\.\DISPLAY") then
           let mutable result = 0
           match System.Int32.TryParse((displayId: string).Substring(11), &result) with
           | true -> Some result
           | false -> None
       else None
   ```

3. **`findInactiveDisplayPath`** (Line ~161)
   - Uses `findDisplayPathBySourceId` internally, inheriting the same issue

### Error Messages in Logs
```
[DEBUG] Strategy CCDTargeted failed: Strategy CCDTargeted failed: Could not parse display number from Display3
[DEBUG] Strategy CCDModePopulation failed: Strategy CCDModePopulation failed: Could not parse display number from Display3
```

## Impact Analysis

### Broken Functionality
- ❌ **Display Enable/Disable**: All CCD-based strategies fail
- ❌ **Multi-Strategy Display Control**: 6 out of 9 strategies fail
- ❌ **TV Hardware Control**: CCD strategies can't activate TVs
- ❌ **Display Mode Changes**: CCD path management broken

### Working Functionality
- ✅ **Display Detection**: Three-way correlation works correctly
- ✅ **UI Display**: Shows all 4 displays with correct numbering
- ✅ **Display Enumeration**: Windows Display numbering algorithm functional
- ✅ **DisplaySwitch Fallback**: Legacy Windows tool still works (partial)

## Historical Context

### Previous Working State
Before the display ID format change, the system used API device names as display IDs:
- Display IDs were `\\.\DISPLAY1`, `\\.\DISPLAY2`, etc.
- CCD path management could parse these directly
- All strategies worked correctly

### Change Impact
The transition to Windows Display Numbers improved user experience but broke internal API compatibility:
- **User-Facing**: Display numbers now show as "1", "2", "3", "4"
- **Internal**: Display IDs changed to "Display1", "Display2", "Display3", "Display4"
- **API Compatibility**: CCD functions still expect old format

## Technical Solution Required

### Option 1: Update CCD Path Management (Recommended)
Modify the display number parsing in `CCDPathManagement.fs` to handle both formats:

```fsharp
let parseDisplayNumber (displayId: string) =
    if displayId.StartsWith("Display") then
        // New format: "Display3" -> 3
        let numberPart = displayId.Substring(7)
        match System.Int32.TryParse(numberPart) with
        | true, result -> Some result
        | false, _ -> None
    elif displayId.StartsWith(@"\\.\DISPLAY") then
        // Legacy format: "\\.\DISPLAY3" -> 3
        let numberPart = displayId.Substring(11)
        match System.Int32.TryParse(numberPart) with
        | true, result -> Some result
        | false, _ -> None
    else None
```

### Option 2: Use API Device Name Mapping
Leverage the existing `getAPIDeviceNameForDisplayId` function to convert new IDs to legacy format before passing to CCD functions.

## Implementation Strategy

1. **Fix CCD Path Management**: Update display number parsing to handle new format
2. **Test All Strategies**: Verify each enable/disable strategy works with new format
3. **Backward Compatibility**: Ensure old format still works if encountered
4. **Cleanup Legacy**: Remove any remaining references to old format where appropriate

## Verification Plan

1. **Basic Functionality**: Test enable/disable of Display3 (the inactive display)
2. **All Strategies**: Verify each CCD strategy can parse display numbers correctly
3. **Edge Cases**: Test with various display configurations
4. **Regression Testing**: Ensure display detection and UI still work correctly

## Related Files to Update

1. **`API/Windows/CCDPathManagement.fs`** - Primary fix location
2. **`API/Windows/WindowsControl.fs`** - May need strategy validation updates
3. **Tests** - Update any tests that verify display control functionality

## Success Criteria

- ✅ Display3 (inactive display) can be enabled successfully
- ✅ All CCD strategies can parse "Display3" format
- ✅ TV hardware control works with new display ID format
- ✅ No regression in display detection or UI functionality
- ✅ System maintains backward compatibility with legacy format