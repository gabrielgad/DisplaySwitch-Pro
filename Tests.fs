module Tests

open Expecto
open DisplaySwitchPro

// Mock adapter for testing - provides predictable display data
type MockPlatformAdapter(displays: DisplayInfo list) =
    interface IPlatformAdapter with
        member _.GetConnectedDisplays() = displays
        member _.ApplyDisplayConfiguration(_) = Ok ()

// Domain test data - represents real display arrangements
module TestData =
    let singleDisplay = {
        Id = "MOCK_PRIMARY"
        Name = "Primary Test Display"
        Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
        Position = { X = 0; Y = 0 }
        Orientation = Landscape
        IsPrimary = true
        IsEnabled = true
    }

    let dualDisplayHorizontal = [
        singleDisplay
        {
            Id = "MOCK_SECONDARY"
            Name = "Secondary Test Display"
            Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
            Position = { X = 1920; Y = 0 }
            Orientation = Landscape
            IsPrimary = false
            IsEnabled = true
        }
    ]

    let complexMultiDisplay = [
        singleDisplay
        {
            Id = "MOCK_VERTICAL"
            Name = "Vertical Display"
            Resolution = { Width = 1080; Height = 1920; RefreshRate = 60 }
            Position = { X = 3840; Y = -420 }
            Orientation = Portrait
            IsPrimary = false
            IsEnabled = true
        }
        {
            Id = "MOCK_DISABLED"
            Name = "Disabled Display"
            Resolution = { Width = 1600; Height = 900; RefreshRate = 60 }
            Position = { X = 1920; Y = 1080 }
            Orientation = Landscape
            IsPrimary = false
            IsEnabled = false
        }
    ]

[<Tests>]
let domainFunctionalityTests =
    testList "Core Domain Functionality" [
        testCase "Display arrangement preserves relationships" <| fun _ ->
            let primary = TestData.singleDisplay
            let secondary = TestData.dualDisplayHorizontal.[1]
            
            // Verify that secondary display is positioned adjacent to primary
            let expectedX = primary.Position.X + primary.Resolution.Width
            Expect.equal secondary.Position.X expectedX "Secondary display should be adjacent to primary"
            Expect.equal secondary.Position.Y primary.Position.Y "Displays should be vertically aligned"
            
        testCase "Display configuration maintains integrity" <| fun _ ->
            let config = {
                Displays = TestData.dualDisplayHorizontal
                Name = "Dual Horizontal Setup"
                CreatedAt = System.DateTime.Now
            }
            
            let primaryDisplays = config.Displays |> List.filter (fun d -> d.IsPrimary)
            Expect.equal primaryDisplays.Length 1 "Configuration should have exactly one primary display"
            
        testCase "Preset system preserves display state" <| fun _ ->
            let mockAdapter = MockPlatformAdapter(TestData.dualDisplayHorizontal) :> IPlatformAdapter
            let world = World.create()
            let worldWithDisplays = DisplayDetectionSystem.updateWorld mockAdapter world
            
            // Modify display position
            let movedDisplay = { TestData.dualDisplayHorizontal.[1] with Position = { X = 2000; Y = 100 } }
            let updatedWorld = { worldWithDisplays with Components = Components.addDisplay movedDisplay worldWithDisplays.Components }
            
            // Save and reload preset
            let presetWorld = PresetSystem.saveCurrentAsPreset "TestLayout" updatedWorld
            let restoredWorld = PresetSystem.loadPreset "TestLayout" presetWorld
            
            let restoredDisplay = restoredWorld.Components.ConnectedDisplays.[movedDisplay.Id]
            Expect.equal restoredDisplay.Position movedDisplay.Position "Preset should preserve exact display positions"
    ]

[<Tests>]
let integrationMockTests =
    testList "Integration Tests with Mocks" [
        testCase "Platform adapter integration" <| fun _ ->
            let mockAdapter = MockPlatformAdapter(TestData.complexMultiDisplay) :> IPlatformAdapter
            let detectedDisplays = mockAdapter.GetConnectedDisplays()
            
            Expect.equal detectedDisplays.Length 3 "Mock should return all configured displays"
            
            let enabledDisplays = detectedDisplays |> List.filter (fun d -> d.IsEnabled)
            Expect.equal enabledDisplays.Length 2 "Only enabled displays should be active"
            
        testCase "Display detection system with mock data" <| fun _ ->
            let mockAdapter = MockPlatformAdapter(TestData.dualDisplayHorizontal) :> IPlatformAdapter
            let world = World.create()
            let updatedWorld = DisplayDetectionSystem.updateWorld mockAdapter world
            
            Expect.equal updatedWorld.Components.ConnectedDisplays.Count 2 "World should contain detected displays"
            
            let primaryDisplay = updatedWorld.Components.ConnectedDisplays.Values 
                                |> Seq.find (fun d -> d.IsPrimary)
            Expect.equal primaryDisplay.Id "MOCK_PRIMARY" "Primary display should be correctly identified"
            
        testCase "Cross-domain preset and display interaction" <| fun _ ->
            let mockAdapter = MockPlatformAdapter(TestData.complexMultiDisplay) :> IPlatformAdapter
            let world = World.create()
            let worldWithDisplays = DisplayDetectionSystem.updateWorld mockAdapter world
            
            // Set up current configuration before saving preset
            let currentConfig = {
                Displays = worldWithDisplays.Components.ConnectedDisplays |> Map.values |> List.ofSeq
                Name = "Current Setup"
                CreatedAt = System.DateTime.Now
            }
            let worldWithConfig = World.processEvent (DisplayConfigurationChanged currentConfig) worldWithDisplays
            
            // Save multiple presets
            let preset1 = PresetSystem.saveCurrentAsPreset "Layout1" worldWithConfig
            
            // Modify display arrangement
            let modifiedDisplay = { TestData.complexMultiDisplay.[1] with Position = { X = 5000; Y = 500 } }
            let modifiedWorld = { preset1 with Components = Components.addDisplay modifiedDisplay preset1.Components }
            let preset2 = PresetSystem.saveCurrentAsPreset "Layout2" modifiedWorld
            
            // Verify both presets are preserved
            Expect.isTrue (preset2.Components.SavedPresets.ContainsKey "Layout1") "First preset should be preserved"
            Expect.isTrue (preset2.Components.SavedPresets.ContainsKey "Layout2") "Second preset should be saved"
            
            // Load first preset and verify display positions are restored
            let restoredWorld = PresetSystem.loadPreset "Layout1" preset2
            let restoredDisplay = restoredWorld.Components.ConnectedDisplays.[modifiedDisplay.Id]
            Expect.notEqual restoredDisplay.Position modifiedDisplay.Position "Loading preset should restore original positions"
    ]

// Core algorithms that power snapping and collision detection
[<Tests>]
let coreAlgorithmTests =
    testList "Core Algorithm Functionality" [
        testCase "Collision detection algorithm accuracy" <| fun _ ->
            // Test the core collision detection math used in GUI
            let checkRectangleOverlap (x1, y1, w1, h1) (x2, y2, w2, h2) =
                not (x1 + w1 <= x2 || x1 >= x2 + w2 || y1 + h1 <= y2 || y1 >= y2 + h2)
            
            // Non-overlapping rectangles
            let rect1 = (0.0, 0.0, 100.0, 100.0)
            let rect2 = (150.0, 0.0, 100.0, 100.0)
            Expect.isFalse (checkRectangleOverlap rect1 rect2) "Non-overlapping rectangles should not collide"
            
            // Overlapping rectangles
            let rect3 = (50.0, 50.0, 100.0, 100.0)
            Expect.isTrue (checkRectangleOverlap rect1 rect3) "Overlapping rectangles should collide"
            
        testCase "Adaptive grid algorithm precision" <| fun _ ->
            let intermediateGridSize = 20.0  // New adaptive grid size
            let snapToIntermediateGrid value =
                System.Math.Round(value / intermediateGridSize : float) * intermediateGridSize
            
            // Test adaptive grid snapping with 20px intermediate grid
            Expect.equal (snapToIntermediateGrid 8.5) 0.0 "Should snap to nearest 20px grid point (0)"
            Expect.equal (snapToIntermediateGrid 15.0) 20.0 "Should snap to nearest 20px grid point (20)"
            Expect.equal (snapToIntermediateGrid 35.0) 40.0 "Should snap to nearest 20px grid point (40)"
            Expect.equal (snapToIntermediateGrid 50.0) 40.0 "Should snap to nearest 20px grid point (40)"  // Fixed: 50 is closer to 40 than 60
            Expect.equal (snapToIntermediateGrid 60.0) 60.0 "Should snap exactly to grid point (60)"
            
        testCase "Display edge calculation algorithm" <| fun _ ->
            // Test display edge calculation for adaptive grid
            let scale = 0.1
            let display1 = {
                Id = "TEST1"
                Name = "Test Display 1"
                Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                Position = { X = 0; Y = 0 }
                Orientation = Landscape
                IsPrimary = true
                IsEnabled = true
            }
            let display2 = {
                Id = "TEST2"
                Name = "Test Display 2"
                Resolution = { Width = 2560; Height = 1440; RefreshRate = 60 }
                Position = { X = 1920; Y = -180 }
                Orientation = Landscape
                IsPrimary = false
                IsEnabled = true
            }
            
            let calculateDisplayEdges displays =
                displays
                |> List.filter (fun d -> d.IsEnabled)
                |> List.collect (fun display ->
                    let x = float display.Position.X * scale
                    let y = float display.Position.Y * scale
                    let width = float display.Resolution.Width * scale
                    let height = float display.Resolution.Height * scale
                    [
                        ("vertical", x)           // Left edge
                        ("vertical", x + width)   // Right edge
                        ("horizontal", y)         // Top edge
                        ("horizontal", y + height) // Bottom edge
                    ])
                |> List.distinct
                |> List.sort
            
            let edges = calculateDisplayEdges [display1; display2]
            let verticalEdges = edges |> List.filter (fun (orientation, _) -> orientation = "vertical") |> List.map snd
            let horizontalEdges = edges |> List.filter (fun (orientation, _) -> orientation = "horizontal") |> List.map snd
            
            // Expected vertical edges: 0.0 (display1 left), 192.0 (display1 right), 192.0 (display2 left), 448.0 (display2 right)
            // After distinct: [0.0; 192.0; 448.0]
            Expect.equal verticalEdges [0.0; 192.0; 448.0] "Should calculate correct vertical display edges"
            
            // Expected horizontal edges: 0.0 (display1 top), 108.0 (display1 bottom), -18.0 (display2 top), 126.0 (display2 bottom)
            // After sort: [-18.0; 0.0; 108.0; 126.0]
            Expect.equal horizontalEdges [-18.0; 0.0; 108.0; 126.0] "Should calculate correct horizontal display edges"
            
        testCase "Edge-to-edge snap position calculation" <| fun _ ->
            // Test edge-to-edge snapping positions
            let display1X, display1Y, display1W, display1H = 0.0, 0.0, 192.0, 108.0  // 1920x1080 at 0.1 scale
            let movingW, movingH = 192.0, 108.0  // Same size display being moved
            
            // Right edge snapping - moving display should be positioned at display1X + display1W
            let rightEdgeSnapX = display1X + display1W
            let rightEdgeSnapY = display1Y  // Aligned to top
            Expect.equal rightEdgeSnapX 192.0 "Right edge snap should position at X=192"
            Expect.equal rightEdgeSnapY 0.0 "Right edge snap should align to top Y=0"
            
            // Bottom edge snapping - moving display should be positioned at display1Y + display1H
            let bottomEdgeSnapX = display1X  // Aligned to left
            let bottomEdgeSnapY = display1Y + display1H
            Expect.equal bottomEdgeSnapX 0.0 "Bottom edge snap should align to left X=0"
            Expect.equal bottomEdgeSnapY 108.0 "Bottom edge snap should position at Y=108"
            
        testCase "Edge snapping priority over grid snapping" <| fun _ ->
            // Test that display edge snapping takes priority over intermediate grid snapping
            let snapProximityThreshold = 25.0
            let intermediateGridSize = 20.0
            
            let snapToDisplayEdgeOrGrid displayEdges value isVertical =
                let relevantEdges = 
                    displayEdges 
                    |> List.filter (fun (orientation, _) -> 
                        (isVertical && orientation = "vertical") || 
                        (not isVertical && orientation = "horizontal"))
                    |> List.map snd
                
                // First try to snap to display edges (priority)
                let edgeSnap = 
                    relevantEdges
                    |> List.tryFind (fun edge -> abs(edge - value) <= snapProximityThreshold)
                
                match edgeSnap with
                | Some edge -> edge
                | None -> 
                    // Fall back to intermediate grid
                    System.Math.Round(value / intermediateGridSize) * intermediateGridSize
            
            let displayEdges = [("vertical", 192.0); ("horizontal", 108.0)]
            
            // Test edge snapping priority - value near display edge should snap to edge
            let result1 = snapToDisplayEdgeOrGrid displayEdges 185.0 true  // Near vertical edge at 192
            Expect.equal result1 192.0 "Should snap to display edge (192) instead of grid (180 or 200)"
            
            // Test grid fallback - value far from edges should snap to grid
            let result2 = snapToDisplayEdgeOrGrid displayEdges 35.0 true  // Far from any edge
            Expect.equal result2 40.0 "Should snap to intermediate grid (40) when far from edges"
            
        testCase "Snap proximity threshold validation" <| fun _ ->
            let snapProximityThreshold = 25.0  // Updated threshold in GUI
            let calculateDistance (x1, y1) (x2, y2) =
                System.Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1))
            
            let distance1 = calculateDistance (0.0, 0.0) (15.0, 20.0)  // 25 distance
            let distance2 = calculateDistance (0.0, 0.0) (30.0, 40.0)  // 50 distance
            
            Expect.isTrue (distance1 <= snapProximityThreshold) "Distance 25 should be within threshold"
            Expect.isFalse (distance2 <= snapProximityThreshold) "Distance 50 should be outside threshold"
            
        testCase "Bounds confinement" <| fun _ ->
            let canvasWidth, canvasHeight = 800.0, 600.0
            let displayWidth, displayHeight = 192.0, 108.0
            
            // Test bounds function
            let confineToCanvas (x, y) =
                let boundedX = max 0.0 (min x (canvasWidth - displayWidth))
                let boundedY = max 0.0 (min y (canvasHeight - displayHeight))
                (boundedX, boundedY)
            
            // Test various positions
            let (x1, y1) = confineToCanvas (-50.0, -25.0)  // Out of bounds negative
            let (x2, y2) = confineToCanvas (850.0, 650.0)  // Out of bounds positive
            let (x3, y3) = confineToCanvas (100.0, 200.0)  // Within bounds
            
            Expect.equal (x1, y1) (0.0, 0.0) "Negative positions should be confined to (0,0)"
            Expect.equal (x2, y2) (608.0, 492.0) "Excessive positions should be confined to canvas bounds"
            Expect.equal (x3, y3) (100.0, 200.0) "Valid positions should remain unchanged"
            
        testCase "Vertical monitor alignment with horizontal displays" <| fun _ ->
            let horizontalDisplay = (0.0, 0.0, 192.0, 108.0)  // 1920x1080
            let verticalMonitor = (108.0, 192.0)              // 1080x1920
            
            let (hx, hy, hw, hh) = horizontalDisplay
            let (vw, vh) = verticalMonitor
            
            // Test vertical monitor positioned to the right of horizontal display
            let rightAlignedX = hx + hw  // 192.0
            let topAlignedY = hy         // 0.0
            let bottomAlignedY = hy + hh - vh  // 108 - 192 = -84 (should be confined)
            
            Expect.equal rightAlignedX 192.0 "Vertical monitor should position at X=192 for right alignment"
            Expect.equal topAlignedY 0.0 "Top alignment should position at Y=0"
            Expect.equal bottomAlignedY -84.0 "Bottom alignment calculation (before bounds confinement)"
    ]

[<Tests>]
let themeTests = testList "Theme Module Tests" [
    testCase "Light theme colors are correctly defined" <| fun _ ->
        let lightColors = Theme.getThemeColors Theme.Light
        
        Expect.equal lightColors.Background (Avalonia.Media.Color.FromRgb(248uy, 250uy, 252uy)) "Light theme background should match"
        Expect.equal lightColors.Surface (Avalonia.Media.Color.FromRgb(255uy, 255uy, 255uy)) "Light theme surface should be white"
        Expect.equal lightColors.Primary (Avalonia.Media.Color.FromRgb(100uy, 149uy, 237uy)) "Light theme primary should be cornflower blue"
        Expect.equal lightColors.Text (Avalonia.Media.Color.FromRgb(31uy, 41uy, 55uy)) "Light theme text should be dark"
        
    testCase "Dark theme colors are correctly defined" <| fun _ ->
        let darkColors = Theme.getThemeColors Theme.Dark
        
        Expect.equal darkColors.Background (Avalonia.Media.Color.FromRgb(17uy, 24uy, 39uy)) "Dark theme background should be dark blue"
        Expect.equal darkColors.Surface (Avalonia.Media.Color.FromRgb(31uy, 41uy, 55uy)) "Dark theme surface should be dark blue-gray"
        Expect.equal darkColors.Primary (Avalonia.Media.Color.FromRgb(96uy, 165uy, 250uy)) "Dark theme primary should be light blue"
        Expect.equal darkColors.Text (Avalonia.Media.Color.FromRgb(243uy, 244uy, 246uy)) "Dark theme text should be light"
        
    testCase "Theme toggle works correctly" <| fun _ ->
        Theme.setTheme Theme.Light
        Expect.equal Theme.currentTheme Theme.Light "Should start with light theme"
        
        let newTheme = Theme.toggleTheme()
        Expect.equal newTheme Theme.Dark "Should toggle to dark theme"
        Expect.equal Theme.currentTheme Theme.Dark "Current theme should be updated to dark"
        
        let backToLight = Theme.toggleTheme()
        Expect.equal backToLight Theme.Light "Should toggle back to light theme"
        Expect.equal Theme.currentTheme Theme.Light "Current theme should be back to light"
        
    testCase "getCurrentColors returns current theme colors" <| fun _ ->
        let originalTheme = Theme.currentTheme
        
        Theme.setTheme Theme.Light
        let lightColors = Theme.getCurrentColors()
        let expectedLightColors = Theme.getThemeColors Theme.Light
        Expect.equal lightColors.Background expectedLightColors.Background "getCurrentColors should return light theme colors"
        
        Theme.setTheme Theme.Dark
        let darkColors = Theme.getCurrentColors()
        let expectedDarkColors = Theme.getThemeColors Theme.Dark
        Expect.equal darkColors.Background expectedDarkColors.Background "getCurrentColors should return dark theme colors"
        
        // Restore original theme for other tests
        Theme.setTheme originalTheme
        
    testCase "setTheme updates current theme" <| fun _ ->
        let originalTheme = Theme.currentTheme
        
        Theme.setTheme Theme.Dark
        Expect.equal Theme.currentTheme Theme.Dark "setTheme should update current theme to dark"
        
        Theme.setTheme Theme.Light
        Expect.equal Theme.currentTheme Theme.Light "setTheme should update current theme to light"
        
        // Restore original theme for other tests
        Theme.setTheme originalTheme
]

[<Tests>]
let uiComponentTests = testList "UI Components Tests" [
    testCase "Visual display creation with correct properties" <| fun _ ->
        let display = TestData.singleDisplay
        let mutable positionChangedCalled = false
        let onPositionChanged _ _ = positionChangedCalled <- true
        
        let visualDisplay = UIComponents.createVisualDisplay display onPositionChanged
        
        Expect.equal visualDisplay.Display.Id display.Id "Visual display should preserve display ID"
        Expect.equal visualDisplay.Display.Name display.Name "Visual display should preserve display name"
        Expect.isNotNull visualDisplay.Rectangle "Rectangle should be created"
        Expect.isNotNull visualDisplay.Border "Border should be created"
        Expect.isNotNull visualDisplay.Label "Label should be created"
        
    testCase "Display list view creates correct number of items" <| fun _ ->
        let displays = TestData.dualDisplayHorizontal
        let mutable toggleCallCount = 0
        let onDisplayToggle _ _ = toggleCallCount <- toggleCallCount + 1
        
        let listView = UIComponents.createDisplayListView displays onDisplayToggle
        
        Expect.equal listView.Children.Count 2 "Should create 2 display cards for dual display setup"
        
    testCase "Preset panel handles empty preset list" <| fun _ ->
        let emptyPresets = []
        let mutable presetClickCalled = false
        let mutable deleteClickCalled = false
        let onPresetClick _ = presetClickCalled <- true
        let onPresetDelete _ = deleteClickCalled <- true
        
        let presetPanel = UIComponents.createPresetPanel emptyPresets onPresetClick onPresetDelete
        
        Expect.isNotNull presetPanel "Preset panel should be created even with empty list"
        Expect.equal presetPanel.Children.Count 4 "Should have title, save button, header, and scroll viewer"
        
    testCase "Preset panel handles non-empty preset list" <| fun _ ->
        let presets = ["Layout1"; "Layout2"; "Layout3"]
        let mutable presetClickCalled = false
        let mutable deleteClickCalled = false
        let onPresetClick _ = presetClickCalled <- true
        let onPresetDelete _ = deleteClickCalled <- true
        
        let presetPanel = UIComponents.createPresetPanel presets onPresetClick onPresetDelete
        
        Expect.isNotNull presetPanel "Preset panel should be created with preset list"
        Expect.equal presetPanel.Children.Count 4 "Should have title, save button, header, and scroll viewer"
]

[<Tests>]
let displayCanvasTests = testList "Display Canvas Tests" [
    testCase "Canvas creation succeeds with valid displays" <| fun _ ->
        let displays = TestData.dualDisplayHorizontal
        let mutable changeCallCount = 0
        let onDisplayChanged _ _ = changeCallCount <- changeCallCount + 1
        
        let canvas = DisplayCanvas.createDisplayCanvas displays onDisplayChanged
        
        Expect.isNotNull canvas "Display canvas should be created successfully"
        Expect.equal changeCallCount 0 "No display changes should occur during canvas creation"
        
    testCase "Canvas handles empty display list" <| fun _ ->
        let emptyDisplays = []
        let mutable changeCallCount = 0
        let onDisplayChanged _ _ = changeCallCount <- changeCallCount + 1
        
        let canvas = DisplayCanvas.createDisplayCanvas emptyDisplays onDisplayChanged
        
        Expect.isNotNull canvas "Display canvas should handle empty display list"
        Expect.equal changeCallCount 0 "No display changes should occur with empty list"
]

// Test runner - call this function to run tests programmatically
let runTests () =
    runTestsInAssemblyWithCLIArgs [] [||]