# GUI Components - Functional Reactive UI Architecture

## Overview

The GUI components in DisplaySwitch-Pro are built using Avalonia.FuncUI, implementing a pure functional reactive approach to user interface development. The UI acts as a pure system that reads world state from the ECS (Entity Component System) and produces immutable view descriptions, similar to React's virtual DOM pattern.

## Architectural Principles

### Pure UI Functions
The UI is composed of pure functions that transform application state into view descriptions:

```fsharp
// Pure view function - no side effects
let view (state: WorldState) (dispatch: Msg -> unit) =
    DockPanel.create [
        DockPanel.children [
            menuBar state dispatch
            displayList state
            controlPanel state dispatch
            statusBar state
        ]
    ]
```

### Immutable State Management
All UI state is immutable and flows unidirectionally:

```fsharp
type DisplayState = {
    Displays: DisplayInfo list
    ActiveMode: DisplayMode
    IsRefreshing: bool
    LastError: string option
}

// State transitions are pure functions
let updateState (msg: Msg) (state: DisplayState) : DisplayState =
    match msg with
    | RefreshDisplays -> { state with IsRefreshing = true }
    | DisplaysRefreshed displays -> 
        { state with Displays = displays; IsRefreshing = false }
    | SetMode mode -> { state with ActiveMode = mode }
    | ErrorOccurred err -> { state with LastError = Some err }
```

## Component Architecture

### Root Component
The root component orchestrates the entire UI as a pure function:

```fsharp
module App =
    type Model = {
        DisplayState: DisplayState
        WindowState: WindowState
        TrayState: TrayState
    }
    
    let init () : Model * Cmd<Msg> =
        let initialModel = {
            DisplayState = DisplayState.empty
            WindowState = WindowState.default'
            TrayState = TrayState.default'
        }
        initialModel, Cmd.ofMsg RefreshDisplays
    
    let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
        match msg with
        | DisplayMsg dmsg ->
            let newDisplayState = DisplayState.update dmsg model.DisplayState
            { model with DisplayState = newDisplayState }, Cmd.none
        | WindowMsg wmsg ->
            let newWindowState = WindowState.update wmsg model.WindowState
            { model with WindowState = newWindowState }, Cmd.none
        | Effect effect ->
            model, Cmd.ofEffect effect
```

### Display List Component
Pure functional component for rendering display information:

```fsharp
module DisplayList =
    let private displayItem (display: DisplayInfo) =
        Border.create [
            Border.padding 8.0
            Border.margin (0.0, 2.0)
            Border.background (if display.IsActive then Brushes.LightBlue else Brushes.LightGray)
            Border.child (
                StackPanel.create [
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.text display.FriendlyName
                            TextBlock.fontWeight FontWeight.Bold
                        ]
                        TextBlock.create [
                            TextBlock.text $"Resolution: {display.Width}x{display.Height} @ {display.RefreshRate}Hz"
                        ]
                        TextBlock.create [
                            TextBlock.text $"Position: ({display.PositionX}, {display.PositionY})"
                        ]
                    ]
                ]
            )
        ]
    
    let view (displays: DisplayInfo list) =
        ScrollViewer.create [
            ScrollViewer.content (
                ItemsControl.create [
                    ItemsControl.items displays
                    ItemsControl.itemTemplate (DataTemplateView<DisplayInfo>.create displayItem)
                ]
            )
        ]
```

### Control Panel Component
Mode switching controls as pure functions:

```fsharp
module ControlPanel =
    let private modeButton mode currentMode dispatch =
        Button.create [
            Button.content (DisplayMode.toString mode)
            Button.classes [ 
                if currentMode = mode then "active" 
            ]
            Button.onClick (fun _ -> dispatch (SetMode mode))
        ]
    
    let view (state: DisplayState) (dispatch: Msg -> unit) =
        StackPanel.create [
            StackPanel.orientation Orientation.Horizontal
            StackPanel.spacing 10.0
            StackPanel.children [
                modeButton PCMode state.ActiveMode dispatch
                modeButton TVMode state.ActiveMode dispatch
                
                Button.create [
                    Button.content "Refresh"
                    Button.isEnabled (not state.IsRefreshing)
                    Button.onClick (fun _ -> dispatch RefreshDisplays)
                ]
            ]
        ]
```

## Effect Isolation

All side effects are isolated at system boundaries using the Effect pattern:

```fsharp
type Effect =
    | QueryDisplays of AsyncReplyChannel<DisplayInfo list>
    | ApplyDisplayConfig of DisplayConfig * AsyncReplyChannel<Result<unit, string>>
    | ShowNotification of string
    | MinimizeToTray
    | RestoreFromTray

// Effects are handled by platform adapters, not UI components
let handleEffect (effect: Effect) : Async<unit> =
    async {
        match effect with
        | QueryDisplays replyChannel ->
            let! displays = PlatformAdapter.queryDisplays()
            replyChannel.Reply displays
            
        | ApplyDisplayConfig (config, replyChannel) ->
            let! result = PlatformAdapter.applyConfig config
            replyChannel.Reply result
            
        | ShowNotification message ->
            do! PlatformAdapter.showNotification message
            
        | MinimizeToTray ->
            do! PlatformAdapter.minimizeToTray()
            
        | RestoreFromTray ->
            do! PlatformAdapter.restoreFromTray()
    }
```

## Virtual DOM-like Reconciliation

Avalonia.FuncUI provides efficient reconciliation similar to React:

```fsharp
// View descriptions are compared structurally
type ViewElement =
    | Text of string
    | Element of elementType: string * props: Map<string, obj> * children: ViewElement list

// Only differences are applied to the actual UI
let reconcile (oldView: ViewElement) (newView: ViewElement) : ViewPatch list =
    match oldView, newView with
    | Text oldText, Text newText when oldText = newText -> []
    | Element (oldType, oldProps, oldChildren), Element (newType, newProps, newChildren) 
        when oldType = newType ->
        let propPatches = diffProps oldProps newProps
        let childPatches = diffChildren oldChildren newChildren
        propPatches @ childPatches
    | _ -> [Replace newView]
```

## Testability Benefits

The functional approach dramatically improves testability:

### Pure Function Testing
```fsharp
[<Test>]
let ``Clicking PC Mode button dispatches SetMode PCMode message`` () =
    // Arrange
    let mutable dispatchedMsg = None
    let dispatch msg = dispatchedMsg <- Some msg
    let state = { DisplayState.empty with ActiveMode = TVMode }
    
    // Act
    let view = ControlPanel.view state dispatch
    simulateButtonClick view "PC Mode"
    
    // Assert
    dispatchedMsg |> should equal (Some (SetMode PCMode))
```

### State Transition Testing
```fsharp
[<Test>]
let ``RefreshDisplays message sets IsRefreshing to true`` () =
    // Arrange
    let initialState = DisplayState.empty
    
    // Act
    let newState = DisplayState.update RefreshDisplays initialState
    
    // Assert
    newState.IsRefreshing |> should be True
    newState.Displays |> should equal initialState.Displays
```

### Effect Testing
```fsharp
[<Test>]
let ``SetMode PCMode triggers ApplyDisplayConfig effect`` () =
    // Arrange
    let model = { App.Model.empty with DisplayState = { DisplayState.empty with ActiveMode = TVMode } }
    
    // Act
    let newModel, cmd = App.update (SetMode PCMode) model
    
    // Assert
    cmd |> should contain (Effect (ApplyDisplayConfig (pcModeConfig, _)))
```

## Styling with Functional Approach

Styles are defined as pure data:

```fsharp
module Styles =
    let modeButton isActive =
        Style.create [
            Style.padding 10.0
            Style.margin 5.0
            Style.background (if isActive then Brushes.DodgerBlue else Brushes.LightGray)
            Style.foreground (if isActive then Brushes.White else Brushes.Black)
            Style.borderRadius 5.0
            Style.cursor Cursor.Hand
        ]
    
    let statusBar hasError =
        Style.create [
            Style.background (if hasError then Brushes.LightCoral else Brushes.LightGreen)
            Style.padding 5.0
            Style.textAlignment TextAlignment.Center
        ]
```

## Event Handling

Events are handled through message dispatch, maintaining purity:

```fsharp
// Messages represent user intentions
type Msg =
    | SetMode of DisplayMode
    | RefreshDisplays
    | DisplaysRefreshed of DisplayInfo list
    | SaveConfiguration
    | LoadConfiguration
    | ErrorOccurred of string
    | DismissError

// Event handlers are simple message dispatchers
let handleKeyDown (dispatch: Msg -> unit) (args: KeyEventArgs) =
    match args.Key, args.Modifiers with
    | Key.D1, KeyModifiers.Control -> dispatch (SetMode PCMode)
    | Key.D2, KeyModifiers.Control -> dispatch (SetMode TVMode)
    | Key.R, KeyModifiers.Control -> dispatch RefreshDisplays
    | Key.Escape, _ -> dispatch MinimizeToTray
    | _ -> ()
```

## Cross-Platform Considerations

The functional UI approach enables easy cross-platform support:

```fsharp
// Platform-specific implementations are injected
type IPlatformUI =
    abstract member CreateTrayIcon : unit -> ITrayIcon
    abstract member ShowNotification : string -> Async<unit>
    abstract member GetPlatformStyles : unit -> Style list

// UI components remain platform-agnostic
let createWindow (platform: IPlatformUI) =
    Window.create [
        Window.title "Display Manager"
        Window.width 500.0
        Window.height 400.0
        Window.styles (platform.GetPlatformStyles())
        Window.content (view model dispatch)
    ]
```

## Performance Optimizations

### Memoization
```fsharp
// Expensive computations are memoized
let memoizedDisplayItem = 
    FuncUI.memo (fun (display: DisplayInfo) -> displayItem display)

// Only re-render when display data changes
let optimizedDisplayList displays =
    ItemsControl.create [
        ItemsControl.items displays
        ItemsControl.itemTemplate (DataTemplateView<DisplayInfo>.create memoizedDisplayItem)
    ]
```

### Selective Updates
```fsharp
// Components can opt out of updates
let staticHeader =
    TextBlock.create [
        TextBlock.text "Display Configuration"
        TextBlock.fontSize 18.0
        TextBlock.key "header" // Stable key prevents re-renders
    ]
```

## Integration with ECS

The UI reads from the ECS world state:

```fsharp
// Query display entities from ECS
let queryDisplays (world: World) : DisplayInfo list =
    world.Query<DisplayComponent, PositionComponent>()
    |> Seq.map (fun (entity, display, position) ->
        {
            FriendlyName = display.Name
            Width = display.Width
            Height = display.Height
            RefreshRate = display.RefreshRate
            PositionX = position.X
            PositionY = position.Y
            IsActive = world.HasComponent<ActiveComponent>(entity)
        })
    |> Seq.toList

// UI subscribes to world changes
let subscribeToWorld (world: World) (dispatch: Msg -> unit) =
    world.OnComponentAdded<DisplayComponent> (fun _ ->
        dispatch RefreshDisplays
    )
    world.OnComponentRemoved<DisplayComponent> (fun _ ->
        dispatch RefreshDisplays
    )
```

## Benefits of Functional Approach

### Predictability
- UI state changes are predictable and traceable
- No hidden mutations or side effects
- Time-travel debugging is possible

### Maintainability
- Components are self-contained pure functions
- Dependencies are explicit through function parameters
- Refactoring is safer with compiler assistance

### Testability
- Pure functions are trivial to test
- No mocking required for most tests
- Property-based testing is natural

### Composability
- Components compose naturally
- Higher-order components are just higher-order functions
- Reusability through function composition

## Migration from Imperative UI

For teams transitioning from Windows Forms or WPF:

### Before (Imperative)
```csharp
private void BtnPCMode_Click(object sender, EventArgs e)
{
    UpdateStatus("Switching to PC Mode...");
    var config = DisplayManager.GetPCModeConfig();
    DisplayManager.ApplyConfiguration(config);
    Thread.Sleep(2000);
    LoadDisplayInfo();
    UpdateStatus("PC Mode activated");
}
```

### After (Functional)
```fsharp
let update msg model =
    match msg with
    | SetMode PCMode ->
        { model with Status = "Switching to PC Mode..." },
        Cmd.batch [
            Cmd.ofEffect (ApplyDisplayConfig (pcModeConfig, replyChannel))
            Cmd.ofAsync (async {
                do! Async.Sleep 2000
                return RefreshDisplays
            })
        ]
```

The functional approach makes the flow explicit and testable without requiring actual display hardware or timing dependencies.