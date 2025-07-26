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
            IsPrimary = false
            IsEnabled = true
        }
        {
            Id = "MOCK_DISABLED"
            Name = "Disabled Display"
            Resolution = { Width = 1600; Height = 900; RefreshRate = 60 }
            Position = { X = 1920; Y = 1080 }
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
            
            // Save multiple presets
            let preset1 = PresetSystem.saveCurrentAsPreset "Layout1" worldWithDisplays
            
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
            
        testCase "Grid snapping algorithm precision" <| fun _ ->
            let snapToGrid gridSize value =
                System.Math.Round(value / gridSize : float) * gridSize
            
            // Test various grid sizes and values
            Expect.equal (snapToGrid 10.0 23.7) 20.0 "Should snap to nearest grid point"
            Expect.equal (snapToGrid 25.0 37.5) 50.0 "Should handle exact midpoints"
            Expect.equal (snapToGrid 5.0 12.2) 10.0 "Should work with smaller grid sizes"
            
        testCase "Distance calculation for snap thresholds" <| fun _ ->
            let calculateDistance (x1, y1) (x2, y2) =
                System.Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1))
            
            let distance = calculateDistance (0.0, 0.0) (3.0, 4.0)
            Expect.equal distance 5.0 "3-4-5 triangle should yield distance of 5"
            
            let closeDistance = calculateDistance (100.0, 200.0) (103.0, 204.0)
            Expect.isTrue (closeDistance < 10.0) "Close points should be within snap threshold"
    ]

// Test runner - call this function to run tests programmatically
let runTests () =
    runTestsInAssemblyWithCLIArgs [] [||]