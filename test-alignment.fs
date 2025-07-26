// Test display alignment and snapping behavior
open System

let testAlignment () =
    printfn "=== Testing Display Alignment ==="
    
    // Test grid snapping (50px = 500 display pixels)
    let gridPixelSize = 50.0
    let snapToGrid value = Math.Round(value / gridPixelSize) * gridPixelSize
    
    printfn "\n1. Grid Snapping (50px grid):"
    let testPositions = [125.0; 175.0; 225.0; 275.0]
    for pos in testPositions do
        let snapped = snapToGrid pos
        printfn "   %.0f -> %.0f" pos snapped
    
    // Test edge-to-edge positioning (0px gap)
    printfn "\n2. Edge-to-Edge Positioning (0px gap):"
    let display1 = (0.0, 0.0, 192.0, 108.0)  // 1920x1080 at scale 0.1
    let (d1x, d1y, d1w, d1h) = display1
    
    // Display 2 to the right of Display 1 (touching)
    let display2RightX = d1x + d1w  // Should be 192.0
    let display2RightY = d1y        // Should be 0.0
    printfn "   Display 2 right of Display 1: (%.0f, %.0f)" display2RightX display2RightY
    printfn "   Gap between displays: %.0f px (should be 0)" (display2RightX - (d1x + d1w))
    
    // Vertical monitor alignment
    printfn "\n3. Vertical Monitor Alignment:"
    let verticalW, verticalH = 108.0, 192.0  // 1080x1920 at scale 0.1
    
    // Vertical monitor to the right of Display 2
    let verticalX = display2RightX + 192.0  // Right of Display 2
    let verticalY = d1y + (d1h - verticalH) / 2.0  // Centered vertically
    printfn "   Vertical monitor right of Display 2: (%.0f, %.0f)" verticalX verticalY
    printfn "   Vertical centering offset: %.0f px" ((d1h - verticalH) / 2.0)
    
    // Check Y alignment when centered
    let display1CenterY = d1y + d1h / 2.0
    let verticalCenterY = verticalY + verticalH / 2.0
    printfn "   Display 1 center Y: %.0f" display1CenterY
    printfn "   Vertical monitor center Y: %.0f" verticalCenterY
    printfn "   Centers aligned: %b" (Math.Abs(display1CenterY - verticalCenterY) < 1.0)
    
    // Collision detection test
    printfn "\n4. Collision Detection:"
    let checkOverlap (x1, y1, w1, h1) (x2, y2, w2, h2) =
        let epsilon = 0.1
        not (x1 + w1 - epsilon <= x2 || 
             x1 + epsilon >= x2 + w2 ||
             y1 + h1 - epsilon <= y2 || 
             y1 + epsilon >= y2 + h2)
    
    // Test no overlap when touching
    let touching1 = (0.0, 0.0, 192.0, 108.0)
    let touching2 = (192.0, 0.0, 192.0, 108.0)  // Exactly touching
    let overlaps = checkOverlap touching1 touching2
    printfn "   Touching displays overlap: %b (should be false)" overlaps
    
    // Test overlap detection
    let overlap1 = (0.0, 0.0, 192.0, 108.0)
    let overlap2 = (191.0, 0.0, 192.0, 108.0)  // 1px overlap
    let overlaps2 = checkOverlap overlap1 overlap2
    printfn "   Overlapping displays detected: %b (should be true)" overlaps2

[<EntryPoint>]
let main args =
    testAlignment ()
    0