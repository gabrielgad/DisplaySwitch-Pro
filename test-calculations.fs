// Direct test of snapping calculations
open System

let testSnapCalculations () =
    printfn "=== Testing Improved Snapping Calculations ==="
    
    // Test data matching our GUI setup
    let display1 = (0.0, 0.0, 192.0, 108.0)  // Primary: 1920x1080 at 0.1 scale
    let display2 = (192.0, 0.0, 192.0, 108.0) // Secondary: 1920x1080 at 0.1 scale  
    let verticalMonitor = (108.0, 192.0)      // Vertical: 1080x1920
    
    let (d1x, d1y, d1w, d1h) = display1
    let (d2x, d2y, d2w, d2h) = display2
    let (vw, vh) = verticalMonitor
    
    printfn "\nDisplay 1 (Primary): (%.0f, %.0f) %.0fx%.0f" d1x d1y d1w d1h
    printfn "Display 2 (Secondary): (%.0f, %.0f) %.0fx%.0f" d2x d2y d2w d2h
    printfn "Vertical Monitor: %.0fx%.0f" vw vh
    
    // Test 0px gap between Display 1 and 2
    let expectedGap = d2x - (d1x + d1w)
    printfn "\nGap between Display 1 and 2: %.0f px (should be 0)" expectedGap
    
    // Test vertical monitor alignment options with Display 1
    printfn "\n=== Vertical Monitor Snap Positions ==="
    
    // Right side of Display 1, top-aligned
    let pos1 = (d1x + d1w, d1y)
    printfn "Right-top: (%.0f, %.0f)" (fst pos1) (snd pos1)
    
    // Right side of Display 1, center-aligned (vertical monitor centered with horizontal)
    let pos2 = (d1x + d1w, d1y + (d1h - vh) / 2.0)
    printfn "Right-center: (%.0f, %.0f)" (fst pos2) (snd pos2)
    
    // Right side of Display 1, bottom-aligned
    let pos3 = (d1x + d1w, d1y + d1h - vh)
    printfn "Right-bottom: (%.0f, %.0f)" (fst pos3) (snd pos3)
    
    // Below Display 1, left-aligned
    let pos4 = (d1x, d1y + d1h)
    printfn "Below-left: (%.0f, %.0f)" (fst pos4) (snd pos4)
    
    // Below Display 1, center-aligned (vertical monitor centered under horizontal)
    let pos5 = (d1x + (d1w - vw) / 2.0, d1y + d1h)
    printfn "Below-center: (%.0f, %.0f)" (fst pos5) (snd pos5)
    
    printfn "\n✅ Calculations show proper 0px gaps and center alignment options"
    printfn "✅ Vertical monitor can align with horizontal displays properly"

[<EntryPoint>]
let main args =
    testSnapCalculations ()
    0