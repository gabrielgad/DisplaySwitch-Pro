# Windows Primary Display API - Correct Usage Documentation

## The Problem with Our Current Implementation

Our current `setPrimaryDisplay` function fails because **Windows requires the primary monitor to always be at position (0,0)**. Simply calling `ChangeDisplaySettingsEx` with `CDS_SET_PRIMARY` flag will report success but won't actually change the primary display unless the position is set correctly.

## Key Requirements for Setting Primary Display

1. **Primary monitor MUST be at (0,0)**: The new primary display must be positioned at coordinates (0,0)
2. **All other monitors must be repositioned**: Since the primary display defines the coordinate system origin, all other displays need their positions recalculated relative to the new primary
3. **Multi-step atomic operation**: Use `CDS_NORESET` flag for each monitor update, then apply all changes with a final NULL call

## Correct API Usage Pattern

### Step 1: Get Current Display Positions
```fsharp
// Get current positions of all connected displays
let currentDisplays = DisplayDetection.getConnectedDisplays()
```

### Step 2: Calculate Position Offsets
```fsharp
// Find the display that should become primary
let newPrimaryDisplay = currentDisplays |> List.find (fun d -> d.Id = targetDisplayId)
let currentPrimaryPos = newPrimaryDisplay.Position

// Calculate offset to move new primary to (0,0)
let offsetX = -currentPrimaryPos.X
let offsetY = -currentPrimaryPos.Y
```

### Step 3: Update All Displays (Multi-step process)
```fsharp
// Step 3a: Update each display with CDS_NORESET flag
for display in currentDisplays do
    let newPosition = { 
        X = display.Position.X + offsetX
        Y = display.Position.Y + offsetY 
    }
    
    let flags = 
        if display.Id = targetDisplayId then
            // New primary display at (0,0) with primary flag
            WindowsAPI.CDS.CDS_SET_PRIMARY ||| WindowsAPI.CDS.CDS_UPDATEREGISTRY ||| WindowsAPI.CDS.CDS_NORESET
        else
            // Other displays with adjusted positions
            WindowsAPI.CDS.CDS_UPDATEREGISTRY ||| WindowsAPI.CDS.CDS_NORESET
    
    // Update this display's position and primary status
    ChangeDisplaySettingsEx(display.Id, updatedDevMode, IntPtr.Zero, flags, IntPtr.Zero)

// Step 3b: Apply all changes atomically
ChangeDisplaySettingsEx(null, null, IntPtr.Zero, 0, IntPtr.Zero)
```

## Why Our Current Implementation Fails

**Current Code Problem:**
```fsharp
let applyPrimarySettings displayId (devMode: WindowsAPI.DEVMODE) =
    let mutable modifiedDevMode = devMode
    modifiedDevMode.dmPositionX <- 0  // Sets position to (0,0)
    modifiedDevMode.dmPositionY <- 0  // BUT doesn't adjust other displays!
    // ... calls ChangeDisplaySettingsEx with CDS_SET_PRIMARY
```

**Issues:**
1. ✅ Sets new primary to (0,0) - **correct**
2. ❌ Doesn't reposition other displays - **WRONG**
3. ❌ Doesn't use proper multi-step atomic process - **WRONG**
4. ❌ Creates overlapping displays at (0,0) - **CAUSES CONFLICTS**

## The Fix Required

We need to replace our simple `setPrimaryDisplay` function with a comprehensive `setPrimaryDisplayWithRepositioning` function that:

1. **Gets all current display positions**
2. **Calculates position offsets** to move new primary to (0,0)
3. **Updates ALL displays** using the CDS_NORESET pattern
4. **Applies changes atomically** with final NULL call

## Example Working Implementation

```fsharp
let setPrimaryDisplayWithRepositioning (targetDisplayId: DisplayId) =
    result {
        // Step 1: Get current display configurations
        let! currentDisplays = DisplayDetection.getConnectedDisplays()
        
        // Step 2: Find new primary and calculate offsets
        let newPrimary = currentDisplays |> List.find (fun d -> d.Id = targetDisplayId)
        let offsetX = -newPrimary.Position.X
        let offsetY = -newPrimary.Position.Y
        
        // Step 3: Update each display with adjusted positions
        for display in currentDisplays do
            let! devMode = getCurrentDevMode display.Id
            let mutable updatedDevMode = devMode
            
            // Calculate new position relative to new primary at (0,0)
            updatedDevMode.dmPositionX <- display.Position.X + offsetX
            updatedDevMode.dmPositionY <- display.Position.Y + offsetY
            updatedDevMode.dmFields <- updatedDevMode.dmFields ||| 0x00000020u // DM_POSITION
            
            let flags = 
                if display.Id = targetDisplayId then
                    CDS_SET_PRIMARY ||| CDS_UPDATEREGISTRY ||| CDS_NORESET
                else
                    CDS_UPDATEREGISTRY ||| CDS_NORESET
            
            let result = ChangeDisplaySettingsEx(display.Id, &updatedDevMode, IntPtr.Zero, flags, IntPtr.Zero)
            if result <> DISP_CHANGE_SUCCESSFUL then
                return! Error (sprintf "Failed to update display %s: %s" display.Id (getDisplayChangeErrorMessage result))
        
        // Step 4: Apply all changes atomically
        let finalResult = ChangeDisplaySettingsEx(null, null, IntPtr.Zero, 0, IntPtr.Zero)
        if finalResult <> DISP_CHANGE_SUCCESSFUL then
            return! Error (sprintf "Failed to apply display changes: %s" (getDisplayChangeErrorMessage finalResult))
        
        return ()
    }
```

## References

- [Microsoft ChangeDisplaySettingsEx Documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexa)
- [CodeProject: Set Primary Display](https://www.codeproject.com/Articles/38903/Set-Primary-Display-ChangeDisplaySettingsEx)
- [Stack Overflow: Use Windows API from C# to set primary monitor](https://stackoverflow.com/questions/195267/use-windows-api-from-c-sharp-to-set-primary-monitor)