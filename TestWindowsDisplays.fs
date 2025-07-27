open System
open System.Runtime.InteropServices

// Windows API structures
[<StructLayout(LayoutKind.Sequential)>]
type RECT = 
    struct
        val mutable left: int
        val mutable top: int
        val mutable right: int
        val mutable bottom: int
    end

[<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)>]
type MONITORINFOEX =
    struct
        val mutable cbSize: int
        val mutable rcMonitor: RECT
        val mutable rcWork: RECT
        val mutable dwFlags: uint32
        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
        val mutable szDevice: string
    end

[<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)>]
type DISPLAY_DEVICE =
    struct
        val mutable cb: int
        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
        val mutable DeviceName: string
        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
        val mutable DeviceString: string
        val mutable StateFlags: uint32
        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
        val mutable DeviceID: string
        [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
        val mutable DeviceKey: string
    end

// Delegate for monitor enumeration callback
type MonitorEnumDelegate = delegate of IntPtr * IntPtr * byref<RECT> * IntPtr -> bool

// Display device state flags
module DisplayDeviceFlags =
    let DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001u
    let DISPLAY_DEVICE_MULTI_DRIVER = 0x00000002u
    let DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004u
    let DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008u
    let DISPLAY_DEVICE_VGA_COMPATIBLE = 0x00000010u
    let DISPLAY_DEVICE_REMOVABLE = 0x00000020u
    let DISPLAY_DEVICE_ACTIVE = 0x00000001u

// Windows API functions
module WinAPI =
    [<DllImport("user32.dll")>]
    extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, 
                                   MonitorEnumDelegate lpfnEnum, IntPtr dwData)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFOEX& lpmi)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool EnumDisplayDevices(string lpDevice, uint32 iDevNum, 
                                  DISPLAY_DEVICE& lpDisplayDevice, uint32 dwFlags)

// Test module
module TestDisplays =
    let mutable monitorCount = 0
    
    // Monitor enumeration callback
    let monitorCallback = 
        MonitorEnumDelegate(fun hMonitor hdcMonitor lprcMonitor dwData ->
            monitorCount <- monitorCount + 1
            printfn "=== Monitor %d ===" monitorCount
            printfn "Handle: %A" hMonitor
            printfn "Bounds from enum: (%d, %d, %d, %d)" 
                lprcMonitor.left lprcMonitor.top lprcMonitor.right lprcMonitor.bottom
            
            // Get detailed monitor info
            let mutable monitorInfo = MONITORINFOEX()
            monitorInfo.cbSize <- Marshal.SizeOf(typeof<MONITORINFOEX>)
            
            let result = WinAPI.GetMonitorInfo(hMonitor, &monitorInfo)
            if result then
                printfn "Device: %s" monitorInfo.szDevice
                printfn "Monitor bounds: (%d, %d) - (%d, %d)" 
                    monitorInfo.rcMonitor.left monitorInfo.rcMonitor.top
                    monitorInfo.rcMonitor.right monitorInfo.rcMonitor.bottom
                printfn "Work area: (%d, %d) - (%d, %d)" 
                    monitorInfo.rcWork.left monitorInfo.rcWork.top
                    monitorInfo.rcWork.right monitorInfo.rcWork.bottom
                printfn "Resolution: %d x %d" 
                    (monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left)
                    (monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top)
                printfn "Primary: %b" ((monitorInfo.dwFlags &&& 1u) = 1u)
                printfn "Flags: 0x%08X" monitorInfo.dwFlags
            else
                printfn "Failed to get monitor info"
            
            printfn ""
            true // Continue enumeration
        )
    
    let enumerateAllDisplayDevices() =
        printfn "=== ALL DISPLAY DEVICES (Including Inactive) ==="
        let mutable deviceIndex = 0u
        let mutable continueEnum = true
        
        while continueEnum do
            let mutable displayDevice = DISPLAY_DEVICE()
            displayDevice.cb <- Marshal.SizeOf(typeof<DISPLAY_DEVICE>)
            
            let result = WinAPI.EnumDisplayDevices(null, deviceIndex, &displayDevice, 0u)
            if result then
                printfn "\n--- Display Device %d ---" deviceIndex
                printfn "Device Name: %s" displayDevice.DeviceName
                printfn "Device String: %s" displayDevice.DeviceString
                printfn "Device ID: %s" displayDevice.DeviceID
                printfn "State Flags: 0x%08X" displayDevice.StateFlags
                
                // Decode state flags
                let isAttached = (displayDevice.StateFlags &&& DisplayDeviceFlags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u
                let isPrimary = (displayDevice.StateFlags &&& DisplayDeviceFlags.DISPLAY_DEVICE_PRIMARY_DEVICE) <> 0u
                let isMirror = (displayDevice.StateFlags &&& DisplayDeviceFlags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u
                let isRemovable = (displayDevice.StateFlags &&& DisplayDeviceFlags.DISPLAY_DEVICE_REMOVABLE) <> 0u
                
                printfn "  Attached to Desktop: %b" isAttached
                printfn "  Primary Device: %b" isPrimary
                printfn "  Mirroring Driver: %b" isMirror
                printfn "  Removable: %b" isRemovable
                printfn "  Status: %s" (if isAttached then "ACTIVE" else "INACTIVE/DISABLED")
                
                deviceIndex <- deviceIndex + 1u
            else
                continueEnum <- false
        
        printfn "\nTotal display devices found: %d" deviceIndex

    let runTest() =
        printfn "Testing Windows Display Detection"
        printfn "=================================="
        printfn "Platform: %s" (Environment.OSVersion.Platform.ToString())
        printfn "OS Version: %s" (Environment.OSVersion.ToString())
        printfn ""
        
        // First: Get ALL display devices (including inactive)
        enumerateAllDisplayDevices()
        
        printfn "\n=== ACTIVE MONITORS (EnumDisplayMonitors) ==="
        monitorCount <- 0
        let success = WinAPI.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorCallback, IntPtr.Zero)
        
        printfn "Enumeration result: %b" success
        printfn "Total active monitors: %d" monitorCount
        
        if monitorCount = 0 then
            printfn "WARNING: No monitors detected - this may indicate an issue with the API calls"

[<EntryPoint>]
let main argv =
    try
        TestDisplays.runTest()
        printfn "\nPress Enter to exit..."
        Console.ReadLine() |> ignore
        0
    with
    | ex -> 
        printfn "Error: %s" ex.Message
        printfn "Stack trace: %s" ex.StackTrace
        1