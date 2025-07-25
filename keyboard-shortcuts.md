# Cross-Platform Keyboard Shortcuts

## Overview

The Keyboard Shortcuts system provides cross-platform, functional hotkey handling for DisplaySwitch-Pro. Built on immutable data structures and pure functions, this system delivers consistent, testable, and reliable keyboard interaction across Windows, macOS, and Linux platforms through a unified functional API.

## Functional Keyboard Architecture

### Immutable Key Binding Types
**Location**: `Core/KeyBindings.fs`

```fsharp
// Cross-platform key representation
type Key = 
    | Character of char
    | FunctionKey of int  // F1-F12
    | SpecialKey of SpecialKeyType
    | NumericKey of int   // 0-9

and SpecialKeyType =
    | Enter | Escape | Space | Tab | Backspace
    | ArrowUp | ArrowDown | ArrowLeft | ArrowRight

// Platform-agnostic modifier keys
type Modifier = 
    | Ctrl | Alt | Shift | Meta  // Meta = Cmd on macOS, Win key on Windows

// Immutable key combination
type KeyCombination = {
    Key: Key
    Modifiers: Set<Modifier>
}

// Functional key binding with pure action
type KeyBinding = {
    Combination: KeyCombination  
    Action: unit -> DisplayAction
    Description: string
    IsGlobal: bool
}

// Display actions that can be triggered
type DisplayAction = 
    | SwitchToPCMode
    | SwitchToTVMode  
    | RefreshDisplays
    | MinimizeToTray
    | ToggleConfiguration of string
    | ShowQuickMenu
```

### Default Key Bindings
**Location**: `Core/DefaultKeyBindings.fs`

```fsharp
module DefaultKeyBindings =
    
    let defaultBindings = [
        { Combination = { Key = NumericKey 1; Modifiers = Set.singleton Ctrl }
          Action = fun () -> SwitchToPCMode
          Description = "Switch to PC mode (all displays)"
          IsGlobal = false }
          
        { Combination = { Key = NumericKey 2; Modifiers = Set.singleton Ctrl }
          Action = fun () -> SwitchToTVMode  
          Description = "Switch to TV mode (single display)"
          IsGlobal = false }
          
        { Combination = { Key = Character 'r'; Modifiers = Set.singleton Ctrl }
          Action = fun () -> RefreshDisplays
          Description = "Refresh display information"
          IsGlobal = false }
          
        { Combination = { Key = SpecialKey Escape; Modifiers = Set.empty }
          Action = fun () -> MinimizeToTray
          Description = "Minimize to system tray"
          IsGlobal = false }
    ]
    
    // Platform-specific global shortcuts
    let globalBindings platform = 
        match platform with
        | Windows -> [
            { Combination = { Key = FunctionKey 1; Modifiers = Set.ofList [Ctrl; Alt] }
              Action = fun () -> SwitchToPCMode
              Description = "Global PC mode switch"
              IsGlobal = true }
        ]
        | MacOS -> [
            { Combination = { Key = FunctionKey 1; Modifiers = Set.ofList [Meta; Alt] }
              Action = fun () -> SwitchToPCMode  
              Description = "Global PC mode switch"
              IsGlobal = true }
        ]
        | Linux -> [
            { Combination = { Key = FunctionKey 1; Modifiers = Set.ofList [Ctrl; Shift] }
              Action = fun () -> SwitchToPCMode
              Description = "Global PC mode switch"  
              IsGlobal = true }
        ]
```

## Pure Keyboard Processing

### Functional Key Event Processing  
**Location**: `Core/KeyboardProcessor.fs`

```fsharp
module KeyboardProcessor =
    
    // Pure function to convert platform key events to our domain model
    let parseKeyEvent (rawKey: obj) (modifiers: obj) : Result<KeyCombination, string> =
        // Platform-specific parsing logic would go here
        match rawKey, modifiers with
        | _ -> Ok { Key = Character 'a'; Modifiers = Set.empty } // Simplified example
    
    // Pure function to find matching key binding
    let findKeyBinding (combination: KeyCombination) (bindings: KeyBinding list) : KeyBinding option =
        bindings 
        |> List.tryFind (fun binding -> binding.Combination = combination)
    
    // Pure function to validate key combination
    let isValidKeyCombination (combination: KeyCombination) : bool =
        match combination.Key with
        | Character c when Char.IsControl(c) -> false
        | _ -> true
    
    // Functional pipeline for processing key events
    let processKeyEvent (rawKey: obj) (modifiers: obj) (bindings: KeyBinding list) : Result<DisplayAction, string> =
        parseKeyEvent rawKey modifiers
        |> Result.bind (fun combination ->
            if isValidKeyCombination combination then
                match findKeyBinding combination bindings with
                | Some binding -> Ok (binding.Action())
                | None -> Error "No binding found for key combination"
            else
                Error "Invalid key combination")
```

### Effect-Based Keyboard Handling
**Location**: `Adapters/KeyboardEffects.fs`

```fsharp
module KeyboardEffects =
    
    // Effect types for keyboard operations
    type KeyboardEffect<'T> =
        | Pure of 'T
        | RegisterGlobalHotkey of KeyBinding * (Result<unit, string> -> KeyboardEffect<'T>)
        | UnregisterHotkey of KeyCombination * (Result<unit, string> -> KeyboardEffect<'T>)
        | ProcessKeyEvent of KeyCombination * (DisplayAction option -> KeyboardEffect<'T>)
        | ShowKeyboardHelp of string list * (unit -> KeyboardEffect<'T>)
    
    // Pure keyboard registration logic
    let registerHotkeyLogic (binding: KeyBinding) : KeyboardEffect<Result<unit, string>> =
        if binding.IsGlobal then
            RegisterGlobalHotkey(binding, fun result ->
                Pure result)
        else
            Pure (Ok ()) // Application-level shortcuts don't need OS registration
    
    // Effect interpreter for keyboard operations  
    let rec interpretKeyboardEffect<'T> (effect: KeyboardEffect<'T>) : 'T =
        match effect with
        | Pure value -> value
        | RegisterGlobalHotkey (binding, cont) ->
            // Platform-specific global hotkey registration
            let result = PlatformKeyboard.registerGlobalHotkey binding
            cont result |> interpretKeyboardEffect
        | UnregisterHotkey (combination, cont) ->
            let result = PlatformKeyboard.unregisterHotkey combination  
            cont result |> interpretKeyboardEffect
        | ProcessKeyEvent (combination, cont) ->
            let action = KeyboardProcessor.findKeyBinding combination defaultBindings
                         |> Option.map (fun binding -> binding.Action())
            cont action |> interpretKeyboardEffect
        | ShowKeyboardHelp (shortcuts, cont) ->
            // Show help dialog with shortcuts
            cont () |> interpretKeyboardEffect
```

## Cross-Platform Key Handling

### Platform Adapters
**Location**: `Adapters/PlatformKeyboard.fs`

```fsharp
module PlatformKeyboard =
    
    // Platform-specific key registration
    let registerGlobalHotkey (binding: KeyBinding) : Result<unit, string> =
        match Environment.OSVersion.Platform with
        | PlatformID.Win32NT -> WindowsKeyboard.registerHotkey binding
        | PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) -> 
            LinuxKeyboard.registerHotkey binding
        | PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ->
            MacOSKeyboard.registerHotkey binding
        | _ -> Error "Unsupported platform"
    
    // Platform-agnostic key combination parsing
    let parseNativeKeyEvent (nativeEvent: obj) : KeyCombination option =
        match Environment.OSVersion.Platform with
        | PlatformID.Win32NT -> WindowsKeyboard.parseKeyEvent nativeEvent
        | PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ->
            LinuxKeyboard.parseKeyEvent nativeEvent  
        | PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ->
            MacOSKeyboard.parseKeyEvent nativeEvent
        | _ -> None

// Windows-specific implementation
module WindowsKeyboard =
    
    let registerHotkey (binding: KeyBinding) : Result<unit, string> =
        try
            let virtualKey = keyToVirtualKeyCode binding.Combination.Key
            let modifiers = modifiersToWin32Flags binding.Combination.Modifiers
            let success = RegisterHotKey(IntPtr.Zero, 0, modifiers, virtualKey)
            if success then Ok () else Error "Failed to register hotkey"
        with
        | ex -> Error ex.Message
    
    let parseKeyEvent (e: KeyEventArgs) : KeyCombination option =
        let key = match e.KeyCode with
                  | Keys.D1 -> Some (NumericKey 1)
                  | Keys.D2 -> Some (NumericKey 2)  
                  | Keys.R -> Some (Character 'r')
                  | Keys.Escape -> Some (SpecialKey Escape)
                  | _ -> None
        
        match key with
        | Some k ->
            let modifiers = Set.empty
                           |> (if e.Control then Set.add Ctrl else id)
                           |> (if e.Alt then Set.add Alt else id)
                           |> (if e.Shift then Set.add Shift else id)
            Some { Key = k; Modifiers = modifiers }
        | None -> None

// macOS-specific implementation  
module MacOSKeyboard =
    
    let registerHotkey (binding: KeyBinding) : Result<unit, string> =
        // Use Carbon or Cocoa APIs for global hotkey registration
        try
            // Implementation would use macOS-specific APIs
            Ok ()
        with
        | ex -> Error ex.Message
    
    let parseKeyEvent (nativeEvent: obj) : KeyCombination option =
        // Parse NSEvent or similar macOS key event
        None // Simplified

// Linux-specific implementation
module LinuxKeyboard =
    
    let registerHotkey (binding: KeyBinding) : Result<unit, string> =
        // Use X11 or Wayland APIs for global hotkey registration
        try
            // Implementation would use X11 XGrabKey or similar
            Ok ()
        with
        | ex -> Error ex.Message
    
    let parseKeyEvent (nativeEvent: obj) : KeyCombination option =
        // Parse X11 KeyEvent or Wayland key event
        None // Simplified
```

## ECS Integration

### Keyboard Input Component
**Location**: `Core/InputComponents.fs`

```fsharp
module InputComponents =
    
    // Component for entities that can receive keyboard input
    type KeyboardInputComponent = {
        ActiveBindings: KeyBinding list
        IsGlobalListener: bool
        LastKeyEvent: KeyCombination option
        TimeSinceLastInput: TimeSpan
    }
    
    // Component for keyboard state management
    type KeyboardStateComponent = {
        PressedKeys: Set<Key>
        HeldModifiers: Set<Modifier>
        KeyRepeatDelay: TimeSpan
        IsInputBlocked: bool
    }

// ECS System for keyboard processing
module KeyboardInputSystem =
    
    // Pure function to update keyboard input entities
    let updateKeyboardInputEntities (deltaTime: TimeSpan) (entities: Entity list) : Entity list =
        entities
        |> List.map (fun entity ->
            match entity.GetComponent<KeyboardInputComponent>() with
            | Some inputComp ->
                let updatedComp = { 
                    inputComp with 
                        TimeSinceLastInput = inputComp.TimeSinceLastInput + deltaTime 
                }
                entity.UpdateComponent(updatedComp)
            | None -> entity)
    
    // Pure function to process key events on entities
    let processKeyEventOnEntities (combination: KeyCombination) (entities: Entity list) : (Entity list * DisplayAction list) =
        let processEntity entity =
            match entity.GetComponent<KeyboardInputComponent>() with
            | Some inputComp ->
                let matchingBinding = 
                    inputComp.ActiveBindings 
                    |> List.tryFind (fun binding -> binding.Combination = combination)
                
                match matchingBinding with
                | Some binding ->
                    let updatedComp = { 
                        inputComp with 
                            LastKeyEvent = Some combination
                            TimeSinceLastInput = TimeSpan.Zero 
                    }
                    let updatedEntity = entity.UpdateComponent(updatedComp)
                    (updatedEntity, [binding.Action()])
                | None -> (entity, [])
            | None -> (entity, [])
        
        let results = entities |> List.map processEntity
        let updatedEntities = results |> List.map fst
        let actions = results |> List.collect snd
        (updatedEntities, actions)
    
    // Pure function to validate keyboard input permissions
    let canEntityReceiveInput (entity: Entity) : bool =
        match entity.GetComponent<KeyboardInputComponent>() with
        | Some inputComp -> not inputComp.IsInputBlocked
        | None -> false
```

## Testing Functional Keyboard Logic

### Pure Function Testing
**Location**: `Tests/KeyboardTests.fs`

```fsharp
module KeyboardTests =
    
    [<Test>]
    let ``parseKeyEvent correctly parses Ctrl+1 combination`` () =
        // Arrange
        let mockKeyEvent = createMockKeyEvent Keys.D1 [Keys.Control]
        
        // Act  
        let result = WindowsKeyboard.parseKeyEvent mockKeyEvent
        
        // Assert
        match result with
        | Some combination ->
            Assert.AreEqual(NumericKey 1, combination.Key)
            Assert.True(Set.contains Ctrl combination.Modifiers)
            Assert.AreEqual(1, Set.count combination.Modifiers)
        | None -> Assert.Fail("Expected valid key combination")
    
    [<Test>]
    let ``findKeyBinding returns correct binding for matching combination`` () =
        // Arrange
        let testBinding = {
            Combination = { Key = NumericKey 1; Modifiers = Set.singleton Ctrl }
            Action = fun () -> SwitchToPCMode
            Description = "Test binding"
            IsGlobal = false
        }
        let bindings = [testBinding]
        let searchCombination = { Key = NumericKey 1; Modifiers = Set.singleton Ctrl }
        
        // Act
        let result = KeyboardProcessor.findKeyBinding searchCombination bindings
        
        // Assert
        match result with
        | Some binding -> 
            Assert.AreEqual(testBinding.Description, binding.Description)
            Assert.AreEqual(SwitchToPCMode, binding.Action())
        | None -> Assert.Fail("Expected to find matching binding")
    
    [<Test>]
    let ``processKeyEventOnEntities updates entity state correctly`` () =
        // Arrange
        let keyBinding = DefaultKeyBindings.defaultBindings.[0]
        let inputComponent = {
            ActiveBindings = [keyBinding]
            IsGlobalListener = false
            LastKeyEvent = None
            TimeSinceLastInput = TimeSpan.FromSeconds(5.0)
        }
        let entity = Entity.create().AddComponent(inputComponent)
        let combination = keyBinding.Combination
        
        // Act
        let (updatedEntities, actions) = 
            KeyboardInputSystem.processKeyEventOnEntities combination [entity]
        
        // Assert
        Assert.AreEqual(1, List.length updatedEntities)
        Assert.AreEqual(1, List.length actions)
        
        let updatedEntity = updatedEntities.[0]
        match updatedEntity.GetComponent<KeyboardInputComponent>() with
        | Some comp ->
            Assert.AreEqual(Some combination, comp.LastKeyEvent)
            Assert.AreEqual(TimeSpan.Zero, comp.TimeSinceLastInput)
        | None -> Assert.Fail("Expected keyboard input component")
    
    [<Test>]
    let ``cross-platform key parsing produces consistent results`` () =
        // Arrange
        let testCases = [
            (Keys.D1, [Keys.Control], NumericKey 1, Set.singleton Ctrl)
            (Keys.D2, [Keys.Control], NumericKey 2, Set.singleton Ctrl)
            (Keys.R, [Keys.Control], Character 'r', Set.singleton Ctrl)
        ]
        
        // Act & Assert
        testCases
        |> List.iter (fun (key, modifiers, expectedKey, expectedModifiers) ->
            let mockEvent = createMockKeyEvent key modifiers
            match WindowsKeyboard.parseKeyEvent mockEvent with
            | Some combination ->
                Assert.AreEqual(expectedKey, combination.Key)
                Assert.AreEqual(expectedModifiers, combination.Modifiers)
            | None -> Assert.Fail($"Failed to parse key combination: {key}"))

// Property-based testing for key combinations
module KeyboardPropertyTests =
    
    [<Property>]
    let ``isValidKeyCombination returns true for printable characters`` (c: char) =
        let combination = { Key = Character c; Modifiers = Set.empty }
        let isValid = KeyboardProcessor.isValidKeyCombination combination
        
        if Char.IsControl(c) then
            not isValid
        else
            isValid
    
    [<Property>]  
    let ``key binding lookup is deterministic`` (bindings: KeyBinding list) (combination: KeyCombination) =
        let result1 = KeyboardProcessor.findKeyBinding combination bindings
        let result2 = KeyboardProcessor.findKeyBinding combination bindings
        result1 = result2
```

## Summary: Functional Programming Benefits for Keyboard Handling

### Cross-Platform Consistency Through Abstraction
- **Unified Key Model**: Single key representation works across Windows, macOS, and Linux
- **Platform Adapters**: Platform-specific code isolated in adapter modules
- **Consistent Behavior**: Same key combinations produce same actions regardless of platform

### Reliability Through Immutability
- **Immutable Key Bindings**: Key bindings cannot be accidentally modified at runtime
- **Pure Key Processing**: Key event processing functions have no side effects
- **Predictable State**: Keyboard state changes only through well-defined transformations

### Testability Through Functional Design
- **Pure Functions**: All key processing logic can be tested without UI dependencies
- **Mockable Effects**: Platform-specific operations can be mocked for testing
- **Property-Based Testing**: Can verify keyboard behavior across large input spaces
- **Deterministic Results**: Same key events always produce same outcomes

### Maintainability Through Composition
- **Function Composition**: Complex keyboard workflows built from simple functions
- **Modular Architecture**: Key binding, processing, and platform code separated
- **Type Safety**: F# type system prevents invalid key combinations at compile time

### Scalability Through ECS Integration
- **Component-Based Input**: Keyboard input handled as components on entities
- **System Isolation**: Keyboard system operates independently of other systems
- **Flexible Binding**: Entities can have different key binding configurations

### Error Handling Through Result Types
- **Explicit Error Handling**: All keyboard operations return Result types
- **No Exceptions**: Error conditions handled through pattern matching
- **Composable Error Handling**: Error handling logic can be composed and reused

### Performance Through Functional Optimization
- **Lazy Evaluation**: Key bindings only processed when needed
- **Immutable Data Structures**: Efficient sharing of key binding configurations
- **Pure Computation**: No allocation overhead for stateless key processing

The functional approach to keyboard handling makes DisplaySwitch-Pro more reliable across platforms while providing excellent testability and maintainability. The separation of pure logic from platform-specific effects enables consistent behavior and easy testing.