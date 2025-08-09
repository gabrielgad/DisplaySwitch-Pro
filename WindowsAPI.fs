namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices

// Windows API structures for display detection
module WindowsAPI =
    
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

    [<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)>]
    type DEVMODE =
        struct
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
            val mutable dmDeviceName: string
            val mutable dmSpecVersion: uint16
            val mutable dmDriverVersion: uint16
            val mutable dmSize: uint16
            val mutable dmDriverExtra: uint16
            val mutable dmFields: uint32
            val mutable dmPositionX: int
            val mutable dmPositionY: int
            val mutable dmDisplayOrientation: uint32
            val mutable dmDisplayFixedOutput: uint32
            val mutable dmColor: int16
            val mutable dmDuplex: int16
            val mutable dmYResolution: int16
            val mutable dmTTOption: int16
            val mutable dmCollate: int16
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
            val mutable dmFormName: string
            val mutable dmLogPixels: uint16
            val mutable dmBitsPerPel: uint32
            val mutable dmPelsWidth: uint32
            val mutable dmPelsHeight: uint32
            val mutable dmDisplayFlags: uint32
            val mutable dmDisplayFrequency: uint32
            val mutable dmICMMethod: uint32
            val mutable dmICMIntent: uint32
            val mutable dmMediaType: uint32
            val mutable dmDitherType: uint32
            val mutable dmReserved1: uint32
            val mutable dmReserved2: uint32
            val mutable dmPanningWidth: uint32
            val mutable dmPanningHeight: uint32
        end

    type MonitorEnumDelegate = delegate of IntPtr * IntPtr * byref<RECT> * IntPtr -> bool

    // Display device state flags
    module Flags =
        let DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001u
        let DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004u
        let DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008u

    // P/Invoke declarations
    [<DllImport("user32.dll")>]
    extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, 
                                   MonitorEnumDelegate lpfnEnum, IntPtr dwData)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFOEX& lpmi)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool EnumDisplayDevices(string lpDevice, uint32 iDevNum, 
                                  DISPLAY_DEVICE& lpDisplayDevice, uint32 dwFlags)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, DEVMODE& lpDevMode)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern int ChangeDisplaySettings(DEVMODE& lpDevMode, uint32 dwFlags)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern int ChangeDisplaySettingsEx(string lpszDeviceName, DEVMODE& lpDevMode, IntPtr hwnd, uint32 dwflags, IntPtr lParam)

    [<DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "ChangeDisplaySettingsExW")>]
    extern int ChangeDisplaySettingsExNull(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint32 dwflags, IntPtr lParam)

    // CCD (Connecting and Configuring Displays) API structures
    [<StructLayout(LayoutKind.Sequential)>]
    type LUID =
        struct
            val mutable LowPart: uint32
            val mutable HighPart: int32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_RATIONAL =
        struct
            val mutable Numerator: uint32
            val mutable Denominator: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_2DREGION =
        struct
            val mutable cx: uint32
            val mutable cy: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type POINTL =
        struct
            val mutable x: int32
            val mutable y: int32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_PATH_SOURCE_INFO =
        struct
            val mutable adapterId: LUID
            val mutable id: uint32
            val mutable modeInfoIdx: uint32
            val mutable statusFlags: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_PATH_TARGET_INFO =
        struct
            val mutable adapterId: LUID
            val mutable id: uint32
            val mutable modeInfoIdx: uint32
            val mutable outputTechnology: uint32
            val mutable rotation: uint32
            val mutable scaling: uint32
            val mutable refreshRate: DISPLAYCONFIG_RATIONAL
            val mutable scanLineOrdering: uint32
            val mutable targetAvailable: int32
            val mutable statusFlags: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_PATH_INFO =
        struct
            val mutable sourceInfo: DISPLAYCONFIG_PATH_SOURCE_INFO
            val mutable targetInfo: DISPLAYCONFIG_PATH_TARGET_INFO
            val mutable flags: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_SOURCE_MODE =
        struct
            val mutable width: uint32
            val mutable height: uint32
            val mutable pixelFormat: uint32
            val mutable position: POINTL
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_VIDEO_SIGNAL_INFO =
        struct
            val mutable pixelRate: uint64
            val mutable hSyncFreq: DISPLAYCONFIG_RATIONAL
            val mutable vSyncFreq: DISPLAYCONFIG_RATIONAL
            val mutable activeSize: DISPLAYCONFIG_2DREGION
            val mutable totalSize: DISPLAYCONFIG_2DREGION
            val mutable videoStandard: uint32
            val mutable scanLineOrdering: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_TARGET_MODE =
        struct
            val mutable targetVideoSignalInfo: DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        end

    [<StructLayout(LayoutKind.Explicit)>]
    type DISPLAYCONFIG_MODE_INFO_UNION =
        struct
            [<FieldOffset(0)>]
            val mutable targetMode: DISPLAYCONFIG_TARGET_MODE
            [<FieldOffset(0)>]
            val mutable sourceMode: DISPLAYCONFIG_SOURCE_MODE
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_MODE_INFO =
        struct
            val mutable infoType: uint32
            val mutable id: uint32
            val mutable adapterId: LUID
            val mutable modeInfo: DISPLAYCONFIG_MODE_INFO_UNION
        end

    // ChangeDisplaySettings flags
    module CDS =
        let CDS_UPDATEREGISTRY = 0x00000001u
        let CDS_TEST = 0x00000002u
        let CDS_FULLSCREEN = 0x00000004u
        let CDS_GLOBAL = 0x00000008u
        let CDS_SET_PRIMARY = 0x00000010u
        let CDS_VIDEOPARAMETERS = 0x00000020u
        let CDS_ENABLE_UNSAFE_MODES = 0x00000100u
        let CDS_DISABLE_UNSAFE_MODES = 0x00000200u
        let CDS_RESET = 0x40000000u
        let CDS_NORESET = 0x10000000u

    // ChangeDisplaySettings return values
    module DISP =
        let DISP_CHANGE_SUCCESSFUL = 0
        let DISP_CHANGE_RESTART = 1
        let DISP_CHANGE_FAILED = -1
        let DISP_CHANGE_BADMODE = -2
        let DISP_CHANGE_NOTUPDATED = -3
        let DISP_CHANGE_BADFLAGS = -4
        let DISP_CHANGE_BADPARAM = -5
        let DISP_CHANGE_BADDUALVIEW = -6

    // Display orientation values for DEVMODE.dmDisplayOrientation
    module DMDO =
        let DMDO_DEFAULT = 0u
        let DMDO_90 = 1u
        let DMDO_180 = 2u
        let DMDO_270 = 3u

    // CCD API constants
    module QDC =
        let QDC_ALL_PATHS = 0x00000001u
        let QDC_ONLY_ACTIVE_PATHS = 0x00000002u
        let QDC_DATABASE_CURRENT = 0x00000004u

    module SDC =
        let SDC_TOPOLOGY_INTERNAL = 0x00000001u
        let SDC_TOPOLOGY_CLONE = 0x00000002u
        let SDC_TOPOLOGY_EXTEND = 0x00000004u
        let SDC_TOPOLOGY_EXTERNAL = 0x00000008u
        let SDC_APPLY = 0x00000080u
        let SDC_NO_OPTIMIZATION = 0x00000100u
        let SDC_VALIDATE = 0x00000200u
        let SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020u
        let SDC_ALLOW_CHANGES = 0x00000400u
        let SDC_PATH_PERSIST_IF_REQUIRED = 0x00000800u
        let SDC_FORCE_MODE_ENUMERATION = 0x00001000u
        let SDC_ALLOW_PATH_ORDER_CHANGES = 0x00002000u

    module DISPLAYCONFIG_PATH =
        let DISPLAYCONFIG_PATH_ACTIVE = 0x00000001u

    module ERROR =
        let ERROR_SUCCESS = 0
        let ERROR_INVALID_PARAMETER = 87
        let ERROR_NOT_SUPPORTED = 50
        let ERROR_ACCESS_DENIED = 5
        let ERROR_INSUFFICIENT_BUFFER = 122
        let ERROR_GEN_FAILURE = 31

    // CCD (Connecting and Configuring Displays) API functions
    [<DllImport("user32.dll")>]
    extern int GetDisplayConfigBufferSizes(uint32 flags, uint32& numPathArrayElements, uint32& numModeInfoArrayElements)

    [<DllImport("user32.dll")>]
    extern int QueryDisplayConfig(uint32 flags, uint32& numPathArrayElements, 
                                  [<In; Out>] DISPLAYCONFIG_PATH_INFO[] pathArray,
                                  uint32& numModeInfoArrayElements,
                                  [<In; Out>] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
                                  IntPtr currentTopologyId)

    [<DllImport("user32.dll")>]
    extern int SetDisplayConfig(uint32 numPathArrayElements,
                                DISPLAYCONFIG_PATH_INFO[] pathArray,
                                uint32 numModeInfoArrayElements,
                                DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
                                uint32 flags)