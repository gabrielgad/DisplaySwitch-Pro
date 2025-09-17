# Core Domain Analysis - DisplaySwitch-Pro

## Overview

The Core Domain serves as the foundation layer for DisplaySwitch-Pro, containing fundamental types, validation logic, logging infrastructure, and functional error handling. This analysis evaluates the current functional programming implementation and provides specific improvement recommendations.

## Current Architecture

### Files Analyzed
- `/Core/Types.fs` - Core domain types and data structures
- `/Core/Logging.fs` - Logging infrastructure with configurable levels
- `/Core/ResultBuilder.fs` - Functional error handling with computation expressions
- `/AppState.fs` - Application state management

### Functional Programming Assessment

**Strengths:**
- ✅ Well-defined discriminated unions (`DisplayOrientation`, `DisplayEvent`)
- ✅ Immutable record types for all domain entities
- ✅ Pure validation functions with Result types
- ✅ Comprehensive helper functions in `DisplayHelpers` module
- ✅ Railway-oriented programming with computation expressions

**Current FP Score: 8/10**

## Critical Issues Identified

### 1. Type System Design Gaps

**Problem:** Primitive types used where domain-specific types would improve safety
```fsharp
// Current - using primitive types
type DisplayId = string
type Resolution = { Width: int; Height: int; RefreshRate: int }
```

**Impact:** Allows invalid data at runtime, reduces compile-time safety

**Solution:** Domain-specific types with validation
```fsharp
type DisplayId = private DisplayId of string
module DisplayId =
    let create (id: string) =
        if String.IsNullOrWhiteSpace(id) then
            Error "DisplayId cannot be empty"
        else
            Ok (DisplayId id)
    let value (DisplayId id) = id

type PixelDimension = private PixelDimension of int
module PixelDimension =
    let create (pixels: int) =
        if pixels <= 0 then Error "Pixel dimension must be positive"
        elif pixels > 16384 then Error "Pixel dimension exceeds maximum"
        else Ok (PixelDimension pixels)
```

### 2. Result Composition Limitations

**Problem:** Manual Result composition in validation chains
```fsharp
// Current - manual composition in Types.fs
let validateConfiguration (config: DisplayConfiguration) : Result<DisplayConfiguration, string> =
    config.Displays
    |> List.map validateDisplayInfo
    |> List.fold (fun acc result ->
        match acc, result with
        | Ok displays, Ok display -> Ok (display :: displays)
        | Error e, _ -> Error e
        | _, Error e -> Error e
    ) (Ok [])
```

**Impact:** Verbose, error-prone, loses detailed error information

**Solution:** Enhanced Result combinators with traverse functions
```fsharp
module Result =
    let traverse f list =
        List.foldBack (fun item acc ->
            Result.bind (fun items ->
                f item |> Result.map (fun item' -> item' :: items)
            ) acc
        ) list (Ok [])

let validateConfiguration (config: DisplayConfiguration) : Result<DisplayConfiguration, ValidationError list> =
    result {
        let! validatedDisplays = config.Displays |> Result.traverse validateDisplayInfo
        let! _ = validateExactlyOnePrimary validatedDisplays
        let! _ = validateUniquePositions validatedDisplays
        return { config with Displays = validatedDisplays }
    }
```

### 3. State Management Concerns

**Problem:** Mixed I/O operations with pure state transformations in `AppState.fs`
```fsharp
// Current - mixing I/O with state (AppState.fs line 47)
let savePreset (name: string) (config: DisplayConfiguration) (state: AppState) =
    let namedConfig = { config with Name = name; CreatedAt = DateTime.Now }
    Logging.logVerbosef "AppState: Saving preset '%s' with %d displays" name namedConfig.Displays.Length
    { state with
        SavedPresets = Map.add name namedConfig state.SavedPresets
        LastUpdate = DateTime.Now }
```

**Impact:** Reduces testability, mixes concerns, hidden side effects

**Solution:** Separate pure state transformations from side effects
```fsharp
module AppStateTransforms =
    let savePreset (name: string) (config: DisplayConfiguration) (state: AppState) =
        let namedConfig = { config with Name = name; CreatedAt = DateTime.Now }
        { state with
            SavedPresets = Map.add name namedConfig state.SavedPresets
            LastUpdate = DateTime.Now }

module AppStateEffects =
    let savePresetWithLogging name config state =
        let newState = AppStateTransforms.savePreset name config state
        Logging.logVerbosef "AppState: Saving preset '%s' with %d displays" name config.Displays.Length
        newState
```

### 4. Logging System Improvements

**Problem:** Imperative logging with mutable state
```fsharp
// Current - imperative with mutation (Logging.fs line 15)
let mutable private currentLogLevel = LogLevel.Normal
```

**Impact:** Not thread-safe, not functional, global mutable state

**Solution:** Functional logging with environment parameter
```fsharp
type LogConfig = {
    Level: LogLevel
    Output: string -> unit
    Formatter: LogLevel -> string -> string
}

module FunctionalLogging =
    let defaultFormatter level message =
        let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
        let levelStr = match level with
                      | LogLevel.Error -> "ERROR"
                      | LogLevel.Normal -> "INFO"
                      | LogLevel.Verbose -> "DEBUG"
        sprintf "[%s] [%s] %s" timestamp levelStr message

    let log config level message =
        if int level <= int config.Level then
            config.Formatter level message |> config.Output
```

## Enhancement Recommendations

### High Priority Improvements

#### 1. Enhanced Type System (Week 1)
- **Add domain-specific types** with validation (DisplayId, PixelDimension, RefreshRate)
- **Implement units of measure** for pixel, hz, and mm measurements
- **Add non-empty list types** for configurations requiring at least one display

#### 2. Result Composition Enhancement (Week 1)
- **Implement traverse functions** for better list validation
- **Add validation combinators** with applicative functors
- **Enhanced ResultBuilder** with list processing capabilities

#### 3. State Management Separation (Week 2)
- **Separate pure functions** from side effects in AppState
- **Implement functional logging** with dependency injection
- **Add state validation** in all transformation functions

### Medium Priority Improvements

#### 4. Advanced Type Safety (Week 3)
```fsharp
// Add discriminated unions for better domain modeling
type DisplayOperationResult =
    | Success of DisplayInfo
    | PartialSuccess of DisplayInfo * warnings: string list
    | Failed of error: string
    | RequiresReboot of DisplayInfo * reason: string

type ValidationError =
    | EmptyName
    | InvalidDimensions of int * int
    | UnsupportedRefreshRate of int
    | DuplicateDisplayId of DisplayId
```

#### 5. Enhanced Validation (Week 3)
```fsharp
module ResultValidation =
    let (<!>) = Result.map
    let (<*>) f x = Result.bind (fun f' -> Result.map f' x) f

    let validateAll validations value =
        validations
        |> List.fold (fun acc validate ->
            match acc, validate value with
            | Ok (), Ok () -> Ok ()
            | Ok (), Error e -> Error [e]
            | Error errs, Ok () -> Error errs
            | Error errs1, Error e -> Error (e :: errs1)
        ) (Ok ())
```

### Low Priority Enhancements

#### 6. Non-Empty List Types (Week 4)
```fsharp
type NonEmptyList<'T> = private NonEmptyList of 'T * 'T list
module NonEmptyList =
    let create head tail = NonEmptyList (head, tail)
    let head (NonEmptyList (h, _)) = h
    let toList (NonEmptyList (h, t)) = h :: t
```

## Implementation Strategy

### Phase 1: Foundation (Days 1-3)
1. **Add domain-specific types** starting with DisplayId and PixelDimension
2. **Implement Result traverse functions** for list validation
3. **Separate pure functions** from side effects in AppState

### Phase 2: Enhancement (Days 4-7)
4. **Implement functional logging** with configuration dependency
5. **Add enhanced validation** with detailed error types
6. **Implement advanced ResultBuilder** features

### Phase 3: Polish (Days 8-10)
7. **Add units of measure** for type safety
8. **Implement non-empty list types** where appropriate
9. **Add comprehensive validation** with applicative functors

## Expected Benefits

### Type Safety Improvements
- **Compile-time prevention** of invalid DisplayIds and pixel dimensions
- **Elimination** of runtime validation errors for basic types
- **Better domain modeling** with discriminated unions

### Functional Programming Enhancement
- **Improved composability** with Result traverse functions
- **Better testability** with pure state transformations
- **Enhanced maintainability** with separated concerns

### Developer Experience
- **Clearer APIs** with domain-specific types
- **Better error messages** with structured validation errors
- **Improved debugging** with functional logging

## Testing Strategy

### Unit Tests for New Types
```fsharp
[<Test>]
let ``DisplayId.create rejects empty string`` () =
    let result = DisplayId.create ""
    Assert.IsTrue(Result.isError result)

[<Test>]
let ``PixelDimension.create accepts valid values`` () =
    let result = PixelDimension.create 1920
    Assert.IsTrue(Result.isOk result)
```

### Property-Based Testing
```fsharp
[<Property>]
let ``validateConfiguration is pure`` (config: DisplayConfiguration) =
    let result1 = validateConfiguration config
    let result2 = validateConfiguration config
    result1 = result2
```

## Performance Considerations

### Memory Usage
- Domain-specific types add minimal overhead
- Result types already in use throughout codebase
- No significant performance impact expected

### CPU Performance
- Validation functions remain O(n) complexity
- Type safety checks happen at compile time
- Logging performance improved with functional approach

## Migration Strategy

### Backward Compatibility
- Implement new types alongside existing ones
- Gradual migration module by module
- Maintain existing API surface during transition

### Risk Mitigation
- Comprehensive test coverage for all new types
- Gradual rollout starting with non-critical components
- Fallback mechanisms for validation failures

## Success Metrics

### Code Quality
- **50% reduction** in type-related runtime errors
- **Improved** code coverage for validation logic
- **Enhanced** compiler error messages

### Maintainability
- **Simplified** validation logic with compose functions
- **Reduced** coupling between modules
- **Improved** debugging with structured logging

## Next Steps

1. **Implement DisplayId and PixelDimension** types first as proof of concept
2. **Add Result traverse functions** to improve validation composability
3. **Separate AppState concerns** to improve testability
4. **Gradually migrate** existing code to use new types
5. **Add comprehensive tests** for all new functionality

This core domain enhancement will provide the foundation for improvements across all other domains while maintaining the excellent functional programming practices already established in the codebase.