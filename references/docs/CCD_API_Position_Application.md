# Windows CCD API Position Application Requirements

## Core Issue Analysis

The `SetDisplayConfig` function is failing with `ERROR_INVALID_PARAMETER (87)` because the current implementation violates Windows CCD API requirements for flag usage and configuration consistency.

## CCD API Requirements for Position Changes

### 1. Flag Combination Rules

**Current (Broken) Implementation:**
```fsharp
let flags = WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE
```

**Problems:**
- `SDC_SAVE_TO_DATABASE` requires `SDC_USE_SUPPLIED_DISPLAY_CONFIG` flag
- Missing `SDC_USE_SUPPLIED_DISPLAY_CONFIG` when providing custom path/mode arrays

**Correct Flag Usage:**
```fsharp
let flags = WindowsAPI.SDC.SDC_APPLY ||| 
           WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
           WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| 
           WindowsAPI.SDC.SDC_SAVE_TO_DATABASE
```

### 2. Path/Mode Configuration Consistency

**Requirements:**
- Each source/target ID can only appear once in mode array
- Mode indices in paths must point to valid entries in mode array
- Use `DISPLAYCONFIG_PATH_MODE_IDX_INVALID` for unspecified modes
- Source mode must be specified if target mode is specified

**Current Issue:**
The code modifies positions without validating mode indices:
```fsharp
// Dangerous - no validation of sourceIndex
let mutable sourceMode = modes.[int sourceIndex]
```

**Required Validation:**
```fsharp
if sourceIndex = WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_MODE_IDX_INVALID then
    return! Error "Source mode index is invalid - cannot update position"
```

### 3. Position Update Process

**Correct Sequence:**
1. Call `GetDisplayConfigBufferSizes()` with `QDC_ONLY_ACTIVE_PATHS`
2. Call `QueryDisplayConfig()` to get current configuration
3. Validate all mode indices are valid
4. Modify only position fields in source modes
5. Preserve all other path/mode settings
6. Optional: Validate with `SDC_VALIDATE` flag first
7. Call `SetDisplayConfig()` with correct flags

### 4. Mode Array Structure Rules

**Source Mode Requirements:**
- Must have `DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE` type
- Position field (`POINTL`) specifies desktop coordinates
- Width/height must match current resolution
- Pixel format must be preserved

**Target Mode Requirements:**
- Must have `DISPLAYCONFIG_MODE_INFO_TYPE_TARGET` type  
- Video signal info must match current display capabilities
- Refresh rate must be supported by display

## Current Implementation Issues

### Issue 1: Invalid Flag Combination
**Location:** `API/DisplayConfigurationAPI.fs:465-466, 505-506`

**Problem:** Using `SDC_SAVE_TO_DATABASE` without `SDC_USE_SUPPLIED_DISPLAY_CONFIG`

### Issue 2: Missing Mode Index Validation
**Location:** `API/DisplayConfigurationAPI.fs:453-468`

**Problem:** Accessing mode array without validating indices

### Issue 3: No Pre-Application Validation
**Location:** `API/DisplayConfigurationAPI.fs:484-495`

**Problem:** No validation step before applying configuration

## Required Code Fixes

### Fix 1: Update Flag Usage in DisplayConfigurationAPI.fs

**Lines 465-466 (updateDisplayPosition):**
```fsharp
// OLD:
let flags = WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE

// NEW:
let flags = WindowsAPI.SDC.SDC_APPLY ||| 
           WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
           WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| 
           WindowsAPI.SDC.SDC_SAVE_TO_DATABASE
```

**Lines 505-506 (applyMultiplePositionChanges):**
```fsharp
// Same flag fix as above
```

### Fix 2: Add Mode Index Validation

**Add before line 453:**
```fsharp
// Validate mode indices before accessing array
let validateModeIndex index arrayLength description =
    if index = WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_MODE_IDX_INVALID then
        Error (sprintf "%s mode index is invalid" description)
    elif int index >= arrayLength then
        Error (sprintf "%s mode index %d exceeds array bounds %d" description index arrayLength)
    else
        Ok index
```

### Fix 3: Add Validation Step

**Add before SetDisplayConfig calls:**
```fsharp
// Validate configuration before applying
let validateResult = WindowsAPI.SetDisplayConfig(
    pathCount, pathArray, modeCount, modeArray, 
    WindowsAPI.SDC.SDC_VALIDATE ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG)

if validateResult <> WindowsAPI.ERROR.ERROR_SUCCESS then
    Error "Configuration validation failed before application"
else
    // Proceed with actual application
```

## Common Error Codes and Meanings

- **ERROR_INVALID_PARAMETER (87)**: Most common - flag mismatch or invalid array structure
- **ERROR_NOT_SUPPORTED (50)**: Configuration not supported by hardware
- **ERROR_ACCESS_DENIED (5)**: Insufficient permissions
- **ERROR_GEN_FAILURE (31)**: General hardware/driver failure

## Testing Strategy

### Phase 1: Single Display Position Change
1. Test changing one display position at a time
2. Verify all flags and indices are correct
3. Add extensive logging for debugging

### Phase 2: Two Display Configuration  
1. Test simple two-display arrangements
2. Verify adjacency rules are followed
3. Test both horizontal and vertical arrangements

### Phase 3: Complex Multi-Display Layouts
1. Test 3+ display configurations
2. Test mixed resolution scenarios
3. Test edge cases (displays at coordinate limits)

## Debug Logging Recommendations

Add these debug outputs:
```fsharp
printfn "[DEBUG] Using flags: 0x%08X" flags
printfn "[DEBUG] Path count: %d, Mode count: %d" pathCount modeCount
for i in 0..pathCount-1 do
    let path = pathArray.[i]
    printfn "[DEBUG] Path %d: Source %d -> Target %d, SourceMode: %d, TargetMode: %d" 
        i path.sourceInfo.id path.targetInfo.id path.sourceInfo.modeInfoIdx path.targetInfo.modeInfoIdx
```

## Key Insight

The Windows CCD API requires **exact compliance** with flag combinations and array structure rules. There's no tolerance for missing required flags or invalid indices. The current "Invalid parameter" error is a direct result of violating these strict requirements.