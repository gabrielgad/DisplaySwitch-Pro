# Primary Display Fix Analysis

## Issue Summary

The `setPrimaryDisplay` function in `WindowsControl.fs` is failing to set the primary display due to two critical issues:

1. **Incorrect Windows API usage** - `dmFields` cleared to 0 preventing position changes
2. **Architectural violation** - Function doing too much, violating single responsibility principle

## Current Implementation Problems

### Location: `API/Windows/WindowsControl.fs` lines 242-331

#### Problem 1: Windows API Bug
**Line 307:** `updatedDevMode.dmFields <- 0u`

This tells Windows "don't change ANY display settings" but then we're trying to:
- Set position to (0,0) for new primary
- Apply the `CDS_SET_PRIMARY` flag

**Should be:** `updatedDevMode.dmFields <- updatedDevMode.dmFields ||| 0x00000020u // DM_POSITION`

#### Problem 2: Single Responsibility Principle Violation

The `setPrimaryDisplay` function performs **4 different responsibilities**:

1. ✅ **Setting primary flag** (core responsibility)
2. ❌ **Position calculation** (lines 256-281) - should use existing pure functions
3. ❌ **Display repositioning** (lines 283-317) - should use existing pipeline
4. ❌ **Atomic application** (lines 321-328) - should use existing pipeline

## Solution Architecture

### Refactor to Proper Function Composition:

```fsharp
// 1. Simple function that ONLY sets primary flag
let setPrimaryDisplayFlag (displayId: DisplayId) =
    // Only handle CDS_SET_PRIMARY flag with correct dmFields

// 2. Compose the pipeline using existing functions
let setPrimaryDisplay (displayId: DisplayId) =
    result {
        // Step 1: Set primary flag only (with correct dmFields)
        let! () = setPrimaryDisplayFlag displayId

        // Step 2: Get current display state and use existing pure functions
        let allDisplays = DisplayDetection.getConnectedDisplays()
        let activeDisplays = allDisplays |> List.filter (fun d -> d.IsEnabled)
        let displayPositions = activeDisplays |> List.map (fun d -> (d.Id, d.Position, d))
        let compactedPositions = compactDisplayPositions displayPositions

        // Step 3: Apply using existing atomic pipeline
        return! applyMultipleDisplayPositionsWithInfo compactedPositions
    }
```

## Implementation Requirements

1. **Fix the dmFields bug** - include DM_POSITION flag
2. **Use existing pure functions** - leverage `compactDisplayPositions`
3. **Use existing pipeline** - leverage `applyMultipleDisplayPositionsWithInfo`
4. **Ensure build succeeds** - compile and verify no errors