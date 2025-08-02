# Windows Display Enable/Disable Implementation

## Overview
This document explains how DisplaySwitch-Pro implements display enable/disable functionality using the Windows CCD (Connecting and Configuring Displays) API.

## Background
The Windows CCD API provides low-level control over display configuration through the following key functions:
- `GetDisplayConfigBufferSizes` - Gets required buffer sizes for display paths and modes
- `QueryDisplayConfig` - Retrieves current display configuration
- `SetDisplayConfig` - Applies new display configuration

## Key Concepts

### Display Paths
A display path represents a connection from a graphics source (GPU output) to a target (monitor). Each path contains:
- **Source Info**: Graphics adapter and output ID
- **Target Info**: Display adapter, ID, and configuration
- **Flags**: Including the `DISPLAYCONFIG_PATH_ACTIVE` flag (0x00000001)

### Display Modes
Display modes define the resolution, refresh rate, and other properties for both source and target.

## Implementation Details

### Disabling a Display
Successfully implemented using the following approach:

1. **Query Active Paths**: Get only the currently active display paths using `QDC_ONLY_ACTIVE_PATHS`
2. **Find Target Display**: Match the display ID (e.g., `\\.\DISPLAY3`) to a path using source ID mapping
3. **Clear Active Flag**: Remove the `DISPLAYCONFIG_PATH_ACTIVE` flag from the path
4. **Apply Configuration**: Call `SetDisplayConfig` with the modified paths

```fsharp
// Clear the active flag to disable
updatedPath.flags <- updatedPath.flags &&& ~~~DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
```

### Enabling a Display (Current Issue)
The current implementation attempts to re-enable by:
1. Getting ALL paths (including inactive) using `QDC_ALL_PATHS`
2. Finding the inactive path for the display
3. Setting the `DISPLAYCONFIG_PATH_ACTIVE` flag
4. Applying the configuration

**Problem**: Simply setting the active flag is insufficient. The path needs:
- Valid mode indices for both source and target
- Proper resolution and refresh rate configuration
- Correct positioning relative to other displays

## Why Current Re-Enable Fails

When a display is disabled:
- The path remains in the configuration but inactive
- Mode information may be cleared or invalid
- Position data is lost

When attempting to re-enable:
- Setting only the active flag doesn't provide Windows enough information
- The path lacks valid mode indices
- No position is specified for the re-enabled display

## Solution Required

To properly re-enable a display:
1. Set the active flag
2. Ensure valid mode indices are set in the path
3. Configure proper display mode (resolution, refresh rate)
4. Set appropriate position coordinates
5. Use `SDC_USE_SUPPLIED_DISPLAY_CONFIG` flag to force Windows to use our configuration

## Windows API Flags Used

### QueryDisplayConfig Flags
- `QDC_ALL_PATHS` (0x00000001) - Get all paths including inactive
- `QDC_ONLY_ACTIVE_PATHS` (0x00000002) - Get only active paths

### SetDisplayConfig Flags
- `SDC_APPLY` (0x00000080) - Apply the configuration
- `SDC_USE_SUPPLIED_DISPLAY_CONFIG` (0x00000020) - Use supplied configuration
- `SDC_ALLOW_CHANGES` (0x00000400) - Allow Windows to make adjustments

### Path Flags
- `DISPLAYCONFIG_PATH_ACTIVE` (0x00000001) - Indicates path is active/enabled