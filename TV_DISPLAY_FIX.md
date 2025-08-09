# TV Display Enable Fix Documentation

## Problem Analysis

### Root Cause
The TV display enable functionality fails because the current mode selection algorithm picks `4096x2160` (Cinema 4K) instead of `3840x2160` (standard UHD), causing Windows driver failures.

**Current problematic code** (line 1221-1224):
```fsharp
let preferredMode = 
    availableModes
    |> List.sortByDescending (fun m -> (m.Width * m.Height, m.RefreshRate))
    |> List.head
```

**Debug evidence:**
- TV enumeration shows: `3840x2160 @ 60Hz, 59Hz, 50Hz, 100Hz, 120Hz, 119Hz...`
- Algorithm selects: `4096x2160 @ 120Hz` ❌ (doesn't exist in enumerated modes)
- Error result: `ChangeDisplaySettingsEx failed: Display driver failed (-1)`

### Why This Happens
1. **Multiple resolutions available**: TV supports both Cinema 4K (`4096x2160`) and UHD (`3840x2160`)
2. **Wrong prioritization**: Current algorithm picks highest pixel count without considering standard vs exotic resolutions
3. **Driver compatibility**: `4096x2160` exists but isn't properly supported at requested refresh rates

## Proposed Solution

### 1. Smart Mode Selection Function
Create a new `selectBestModeForDisplay` function that:
- **Prioritizes standard resolutions** over exotic ones
- **Prefers safe refresh rates** (60Hz, 75Hz) over high rates (120Hz, 144Hz)
- **Validates selected modes** exist in enumerated list
- **Provides graceful fallbacks**

### 2. Standard Resolution Priority List
```fsharp
let standardResolutions = [
    (3840, 2160); (1920, 1080); (2560, 1440); (1920, 1200); 
    (1680, 1050); (1600, 1200); (1280, 1024); (1024, 768)
]
```

### 3. Safe Refresh Rate Priority
```fsharp
let preferredRefreshRates = [60; 75; 120; 144; 59; 50]
```

### 4. Multiple Fallback Strategies
When primary approach fails, try:
1. **Test-then-apply**: Use `CDS_TEST` flag first (like Windows Settings)
2. **NULL auto-detect**: Let Windows choose optimal mode
3. **1080p fallback**: Use universally compatible resolution

## Implementation Location

**File**: `WindowsDisplaySystem.fs`
**Target line**: Around line 1221-1224 in the `setDisplayEnabled` function

**Replace this:**
```fsharp
// Find the best mode - prefer highest resolution, then highest refresh rate
let preferredMode = 
    availableModes
    |> List.sortByDescending (fun m -> (m.Width * m.Height, m.RefreshRate))
    |> List.head
```

**With this:**
```fsharp
// Use smart mode selection to find the best mode
match selectBestModeForDisplay availableModes with
| Some preferredMode ->
```

## Expected Results

### Before Fix
- ❌ Selects: `4096x2160 @ 120Hz`
- ❌ Result: "Display driver failed (-1)"

### After Fix  
- ✅ Selects: `3840x2160 @ 60Hz`
- ✅ Result: TV enables successfully
- ✅ Fallbacks: Multiple strategies if primary fails

## Key Benefits

1. **Hardware Compatibility**: Works with diverse TV/monitor hardware
2. **Dynamic Detection**: Automatically finds best supported mode
3. **Safe Defaults**: Prioritizes modes most likely to work
4. **Robust Fallbacks**: Multiple strategies prevent total failure
5. **Future-Proof**: Will work with new display technologies

## Testing Strategy

1. **Test with current TV setup**: Should select `3840x2160 @ 60Hz`
2. **Debug logging**: Detailed output showing mode selection process
3. **Fallback validation**: Ensure all strategies are attempted
4. **Cross-hardware testing**: Verify works with different display types