# Display Configuration API

## Overview

The Display Configuration API module provides low-level access to Windows display management functionality through the Windows Display Configuration API. This module handles direct communication with the Windows graphics subsystem to query, modify, and apply display configurations.

## Windows API Integration

### Core API Functions

#### GetDisplayConfigBufferSizes
**Location**: `DisplayManagerGUI.cs:451-454`
```csharp
[DllImport("user32.dll")]
static extern int GetDisplayConfigBufferSizes(
    QueryDisplayConfigFlags flags,
    out uint numPathArrayElements,
    out uint numModeInfoArrayElements);
```
**Purpose**: Determines buffer sizes needed for display configuration queries.

#### QueryDisplayConfig
**Location**: `DisplayManagerGUI.cs:457-463`
```csharp
[DllImport("user32.dll")]
static extern int QueryDisplayConfig(
    QueryDisplayConfigFlags flags,
    ref uint numPathArrayElements,
    [Out] DisplayConfigPathInfo[] pathInfoArray,
    ref uint numModeInfoArrayElements,
    [Out] DisplayConfigModeInfo[] modeInfoArray,
    out DisplayConfigTopologyId currentTopologyId);
```
**Purpose**: Retrieves current display configuration information.

#### SetDisplayConfig
**Location**: `DisplayManagerGUI.cs:466-471`
```csharp
[DllImport("user32.dll")]
static extern int SetDisplayConfig(
    uint numPathArrayElements,
    [In] DisplayConfigPathInfo[] pathInfoArray,
    uint numModeInfoArrayElements,
    [In] DisplayConfigModeInfo[] modeInfoArray,
    SetDisplayConfigFlags flags);
```
**Purpose**: Applies new display configuration settings.

#### DisplayConfigGetDeviceInfo
**Location**: `DisplayManagerGUI.cs:474-477`
```csharp
[DllImport("user32.dll")]
static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName deviceName);

[DllImport("user32.dll")]
static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName deviceName);
```
**Purpose**: Retrieves detailed device information for displays.

## Data Structures

### Fundamental Types

#### LUID (Logical Unit Identifier)
**Location**: `DisplayManagerGUI.cs:505-509`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct LUID
{
    public uint LowPart;
    public int HighPart;
}
```
**Purpose**: Unique identifier for display adapters.

#### DisplayConfigRational
**Location**: `DisplayManagerGUI.cs:544-548`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigRational
{
    public uint Numerator;
    public uint Denominator;
}
```
**Purpose**: Represents fractional values (refresh rates, scaling factors).

### Path Information

#### DisplayConfigPathInfo
**Location**: `DisplayManagerGUI.cs:512-517`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo sourceInfo;
    public DisplayConfigPathTargetInfo targetInfo;
    public uint flags;
}
```
**Purpose**: Describes connection between graphics adapter and display.

#### DisplayConfigPathSourceInfo
**Location**: `DisplayManagerGUI.cs:520-526`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigPathSourceInfo
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;
}
```
**Purpose**: Source (graphics adapter) information for display path.

#### DisplayConfigPathTargetInfo
**Location**: `DisplayManagerGUI.cs:529-541`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigPathTargetInfo
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint outputTechnology;
    public uint rotation;
    public uint scaling;
    public DisplayConfigRational refreshRate;
    public uint scanLineOrdering;
    public bool targetAvailable;
    public uint statusFlags;
}
```
**Purpose**: Target (display device) information for display path.

### Mode Information

#### DisplayConfigModeInfo
**Location**: `DisplayManagerGUI.cs:551-557`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigModeInfo
{
    public uint infoType;
    public uint id;
    public LUID adapterId;
    public DisplayConfigModeInfoUnion modeInfo;
}
```
**Purpose**: Container for display mode information.

#### DisplayConfigSourceMode
**Location**: `DisplayManagerGUI.cs:575-581`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigSourceMode
{
    public uint width;
    public uint height;
    public uint pixelFormat;
    public PointL position;
}
```
**Purpose**: Source mode information (resolution, position).

#### DisplayConfigTargetMode
**Location**: `DisplayManagerGUI.cs:569-572`
```csharp
[StructLayout(LayoutKind.Sequential)]
struct DisplayConfigTargetMode
{
    public DisplayConfigVideoSignalInfo targetVideoSignalInfo;
}
```
**Purpose**: Target mode information (video signal details).

### Device Information

#### DisplayConfigTargetDeviceName
**Location**: `DisplayManagerGUI.cs:610-622`
```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct DisplayConfigTargetDeviceName
{
    public DisplayConfigDeviceInfoHeader header;
    public uint flags;
    public uint outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string monitorFriendlyDeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string monitorDevicePath;
}
```
**Purpose**: Detailed information about display devices.

#### DisplayConfigSourceDeviceName
**Location**: `DisplayManagerGUI.cs:625-630`
```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct DisplayConfigSourceDeviceName
{
    public DisplayConfigDeviceInfoHeader header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string viewGdiDeviceName;
}
```
**Purpose**: Source device naming information.

## Enumerations and Constants

### QueryDisplayConfigFlags
**Location**: `DisplayManagerGUI.cs:480-485`
```csharp
[Flags]
enum QueryDisplayConfigFlags : uint
{
    AllPaths = 0x00000001,
    OnlyActivePaths = 0x00000002,
    DatabaseCurrent = 0x00000004
}
```
**Purpose**: Controls display configuration query behavior.

### SetDisplayConfigFlags
**Location**: `DisplayManagerGUI.cs:488-494`
```csharp
[Flags]
enum SetDisplayConfigFlags : uint
{
    Apply = 0x00000080,
    NoOptimization = 0x00000100,
    SaveToDatabase = 0x00000200,
    UseSuppliedDisplayConfig = 0x00000020
}
```
**Purpose**: Controls display configuration application behavior.

### DisplayConfigTopologyId
**Location**: `DisplayManagerGUI.cs:496-502`
```csharp
enum DisplayConfigTopologyId : uint
{
    Internal = 0x00000001,
    Clone = 0x00000002,
    Extend = 0x00000004,
    External = 0x00000008
}
```
**Purpose**: Defines display topology modes.

## High-Level Data Classes

### DisplayConfig
**Location**: `DisplayManagerGUI.cs:645-650`
```csharp
public class DisplayConfig
{
    public List<DisplayInfo> Displays { get; set; } = new List<DisplayInfo>();
    public string ConfigName { get; set; }
    public DateTime SavedAt { get; set; }
}
```
**Purpose**: Container for complete display configuration.

### DisplayInfo
**Location**: `DisplayManagerGUI.cs:652-664`
```csharp
public class DisplayInfo
{
    public string DeviceName { get; set; }
    public string FriendlyName { get; set; }
    public bool IsActive { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint RefreshRate { get; set; }
    public uint TargetId { get; set; }
    public uint SourceId { get; set; }
}
```
**Purpose**: Simplified display information for application use.

## Implementation Details

### Configuration Query Process
**Location**: `DisplayManagerGUI.cs:668-758`

1. **Buffer Size Query**: Get required buffer sizes
2. **Memory Allocation**: Allocate path and mode arrays
3. **Configuration Query**: Retrieve current configuration
4. **Data Processing**: Extract relevant information
5. **Device Information**: Get friendly names and details
6. **Object Creation**: Build DisplayConfig and DisplayInfo objects

### Error Handling
```csharp
const int ERROR_SUCCESS = 0;

if (error != ERROR_SUCCESS)
    throw new Exception($"API call failed with error {error}");
```

### Memory Management
- **Structured Layout**: Proper memory alignment for interop
- **String Marshaling**: Unicode string handling
- **Array Management**: Safe buffer allocation and cleanup

## Usage Examples

### Basic Configuration Query
```csharp
// Get current display configuration
var config = DisplayManager.GetCurrentConfiguration();

// Access display information
foreach (var display in config.Displays)
{
    Console.WriteLine($"Display: {display.FriendlyName}");
    Console.WriteLine($"Resolution: {display.Width}x{display.Height}");
    Console.WriteLine($"Refresh Rate: {display.RefreshRate}Hz");
    Console.WriteLine($"Position: ({display.PositionX}, {display.PositionY})");
    Console.WriteLine($"Active: {display.IsActive}");
}
```

### Display Mode Application
```csharp
// Apply extended desktop mode
DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);

// Apply single external display mode
DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.TVMode);
```

### Device Information Retrieval
```csharp
// Get target device information
var targetInfo = new DisplayConfigTargetDeviceName();
targetInfo.header.type = 2; // DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME
targetInfo.header.size = (uint)Marshal.SizeOf(typeof(DisplayConfigTargetDeviceName));
targetInfo.header.adapterId = path.targetInfo.adapterId;
targetInfo.header.id = path.targetInfo.id;

int result = DisplayConfigGetDeviceInfo(ref targetInfo);
if (result == ERROR_SUCCESS)
{
    string friendlyName = targetInfo.monitorFriendlyDeviceName;
    string devicePath = targetInfo.monitorDevicePath;
}
```

## Performance Considerations

### Optimization Strategies
- **Minimal API Calls**: Cache configuration data when possible
- **Efficient Queries**: Use appropriate query flags
- **Memory Reuse**: Reuse buffers for repeated queries
- **Async Operations**: Don't block UI thread during API calls

### Typical Performance Metrics
- **Configuration Query**: ~50-100ms
- **Mode Application**: ~500-1000ms
- **Device Information**: ~10-20ms per device

## Error Conditions

### Common Error Codes
- **ERROR_SUCCESS (0)**: Operation completed successfully
- **ERROR_INVALID_PARAMETER**: Invalid input parameters
- **ERROR_NOT_SUPPORTED**: Operation not supported by hardware
- **ERROR_ACCESS_DENIED**: Insufficient permissions
- **ERROR_GEN_FAILURE**: General hardware/driver failure

### Error Handling Strategy
```csharp
try
{
    DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
}
catch (Exception ex)
{
    // Log error details
    LogError($"Display mode change failed: {ex.Message}");
    
    // Show user-friendly message
    MessageBox.Show("Unable to change display mode. Please check your display connections.");
    
    // Attempt recovery
    RefreshDisplayConfiguration();
}
```

## Limitations and Considerations

### Hardware Limitations
- **Driver Support**: Depends on graphics driver capabilities
- **Display Connections**: Limited by available ports
- **Refresh Rate**: Restricted by display and cable specifications
- **Resolution**: Maximum resolution depends on hardware

### Software Limitations
- **Windows Version**: API availability varies by Windows version
- **Administrator Rights**: Some operations may require elevated permissions
- **Driver Compatibility**: Behavior may vary between graphics drivers

## Future Enhancements

### Planned API Extensions
- **HDR Support**: High Dynamic Range configuration
- **Variable Refresh Rate**: Adaptive sync settings
- **Color Management**: Color profile handling
- **Multi-GPU Support**: Configuration across multiple graphics adapters
- **Extended Metadata**: Additional display properties and capabilities

### Integration Points
- **[Core Features](core-features.md)**: High-level display mode switching
- **[Configuration Management](config-management.md)**: Persistent storage of API data
- **[Troubleshooting](troubleshooting.md)**: API error diagnosis and resolution