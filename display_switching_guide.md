# Windows Display Switching API Architecture Guide

## The Fundamental Problem

Windows display numbering appears simple (`\\.\DISPLAY1`, `\\.\DISPLAY2`) but these are **session-based identifiers** that change when displays connect/disconnect. Professional display switching applications need **persistent hardware-based identification**.

## Multi-Layer Display Architecture

Windows uses three distinct identification layers:

### 1. Session Layer (What You See)
- **Display Names**: `\\.\DISPLAY1`, `\\.\DISPLAY2`, etc.
- **Behavior**: Changes when displays connect/disconnect
- **Problem**: Not reliable for persistent configuration

### 2. Hardware Layer (What Windows Uses Internally)
- **EDID-Based IDs**: Manufacturer + Product + Serial combinations
- **Behavior**: Persistent across reconnections
- **Solution**: True hardware identification

### 3. Registry Layer (Configuration Storage)
- **Registry Keys**: `GraphicsDrivers\Configuration` and `GraphicsDrivers\Connectivity`
- **Device Paths**: SetupAPI device interface names
- **Purpose**: Maps session IDs to hardware IDs

## Core API Functions You Need

### Primary Enumeration APIs

**EnumDisplayDevices** (Two-Call Pattern Required)
- **First Call**: `EnumDisplayDevices(null, index, device, 0)` - Gets display adapters
- **Second Call**: `EnumDisplayDevices(adapterName, index, device, EDD_GET_DEVICE_INTERFACE_NAME)` - Gets monitors
- **Critical**: Must use `EDD_GET_DEVICE_INTERFACE_NAME` flag on second call for hardware linking

**EnumDisplayMonitors**
- Purpose: Gets `HMONITOR` handles for geometric operations
- Use with `GetMonitorInfo` for position/size information
- Good for: Spatial relationships, not hardware identification

**EnumDisplaySettings**
- Purpose: Retrieves current/available display modes
- Modes: `ENUM_CURRENT_SETTINGS` (-1) or `ENUM_REGISTRY_SETTINGS` (-2)
- Use: Getting current resolution, refresh rate, color depth

### Configuration APIs

**ChangeDisplaySettingsEx**
- Purpose: Applies new display settings
- **Multi-Display Pattern**: Update registry first with `CDS_UPDATEREGISTRY | CDS_NORESET`, then apply with null device
- **Single Display**: Direct call with `CDS_UPDATEREGISTRY`

## Hardware Identification Strategy

### The EDID Approach (Most Reliable)

**SetupAPI Functions Needed:**
1. `SetupDiGetClassDevs` - Get monitor device info set using `GUID_DEVINTERFACE_MONITOR`
2. `SetupDiEnumDeviceInterfaces` - Enumerate monitor interfaces
3. `SetupDiGetDeviceInterfaceDetail` - Get device paths
4. `SetupDiOpenDevRegKey` - Access registry for EDID data

**EDID Data Structure:**
- **Manufacturer Code**: 3-character ID (bytes 8-9, packed 5-bit letters)
- **Product Code**: 2-byte identifier (bytes 10-11)
- **Serial Number**: 4-byte unique ID (bytes 12-15)
- **Physical Size**: Width/height in centimeters (bytes 21-22)
- **Monitor Name**: Text descriptors at offsets 54, 72, 90, 108

**Creating Persistent IDs:**
- Format: `"{ManufacturerCode}_{ProductCode:X4}_{SerialNumber:X8}"`
- Example: `"DEL_4072_12345678"` for a Dell monitor
- These IDs survive reconnections, driver updates, port changes

### Correlation Logic

**Linking Session to Hardware:**
1. Use `EnumDisplayDevices` with `EDD_GET_DEVICE_INTERFACE_NAME` to get device interface paths
2. Use `SetupDiGetDeviceInterfaceDetail` to get matching device paths from monitor enumeration
3. Match paths to correlate session displays with EDID hardware data
4. Build internal mapping: `SessionDisplayName → EDID Hardware ID`

## Critical Structure Initialization

### DISPLAY_DEVICE Structure
- **cb Field**: Must equal `sizeof(DISPLAY_DEVICE)` exactly
- **Common Error**: Forgetting to initialize cb leads to empty strings
- **Size**: 840 bytes (Unicode) or 224 bytes (ANSI)

### DEVMODE Structure
- **dmSize Field**: Must equal `sizeof(DEVMODE)`
- **dmDriverExtra**: Usually 0 for display devices
- **dmFields**: Bitmask indicating which fields are valid
- **Union Handling**: For displays, ignore printer-specific union members

## Multi-Display Configuration Patterns

### Two-Phase Configuration (Multiple Displays)
1. **Phase 1**: For each display, call `ChangeDisplaySettingsEx` with:
   - Device name
   - New settings
   - `CDS_UPDATEREGISTRY | CDS_NORESET` flags
2. **Phase 2**: Call `ChangeDisplaySettingsEx(null, null, CDS_NONE)` to apply all changes

### Enable/Disable Pattern
**To Disable:**
- Set `dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT`
- Set `dmPelsWidth = 0`, `dmPelsHeight = 0`
- Set position to (0,0)
- Use two-phase pattern

**To Enable:**
- Set valid resolution and position
- Ensure position is adjacent to existing display pixel area
- Use two-phase pattern

## Common Pitfalls and Solutions

### Marshaling Issues
**Problem**: Empty strings or access violations
**Solution**: Always initialize structure size fields (`cb`, `dmSize`) before API calls

**Problem**: Unicode/ANSI mismatches
**Solution**: Use `CharSet.Auto` in DllImport declarations

### Display Numbering Confusion
**Problem**: Assuming display numbers are persistent
**Solution**: Always use hardware identification for reliable targeting

**Problem**: Port-based assumptions
**Solution**: Display numbers aren't tied to physical ports - use EDID identification

### Registry Access Issues
**Problem**: Access denied when reading EDID
**Solution**: Use `KEY_READ` permissions, not `KEY_ALL_ACCESS`

**Problem**: Different EDID locations
**Solution**: Use SetupAPI enumeration, not direct registry paths

## Implementation Logic Flow

### Application Startup
1. Enumerate all display adapters using `EnumDisplayDevices(null, ...)`
2. For each adapter, enumerate monitors using `EnumDisplayDevices(adapterName, ..., EDD_GET_DEVICE_INTERFACE_NAME)`
3. Use SetupAPI to get EDID data for each monitor
4. Build hardware ID → session display mapping
5. Store configuration using hardware IDs, not session names

### Configuration Changes
1. Identify target displays by hardware ID, not session number
2. Look up current session names for target hardware
3. Get current settings using `EnumDisplaySettings`
4. Modify required fields in DEVMODE structure
5. Apply using appropriate single/multi-display pattern
6. Verify success and handle rollback if needed

### Hot-Plug Handling
1. Re-enumerate displays when hardware changes detected
2. Update hardware ID → session display mapping
3. Apply saved configurations to newly connected displays
4. Handle cases where expected hardware isn't present

## Advanced Features

### DISPLAYCONFIG API (Windows 7+)
- **QueryDisplayConfig**: Get current topology
- **SetDisplayConfig**: Apply complex multi-display configurations
- **GetDisplayConfigBufferSizes**: Determine buffer requirements
- More powerful than ChangeDisplaySettingsEx for complex scenarios

### Monitor Capabilities
- **GetDeviceCaps**: Query display capabilities
- **EnumDisplaySettings**: Iterate through all supported modes
- **DDC/CI Integration**: Monitor control via display data channel

## Best Practices

### Identification Strategy
- Always use EDID-based hardware identification
- Fall back to device interface names if EDID unavailable
- Never rely solely on display numbers for configuration persistence

### Error Handling
- Validate all structure initializations
- Check return codes from all API calls
- Implement graceful degradation for unsupported scenarios
- Provide user feedback for configuration failures

### Performance Considerations
- Cache EDID information - it doesn't change
- Minimize SetupAPI calls - they're expensive
- Use device change notifications instead of polling

### User Experience
- Always test configuration changes before applying
- Provide rollback mechanisms for failed changes
- Show friendly names from EDID monitor names
- Handle multi-monitor arrangements visually

## Testing Strategy

### Hardware Variations
- Test with different GPU vendors (NVIDIA, AMD, Intel)
- Test with mixed connection types (HDMI, DisplayPort, DVI)
- Test with USB-C/Thunderbolt displays
- Test with docking stations and KVM switches

### Configuration Scenarios
- Single display resolution changes
- Multi-display enable/disable
- Display arrangement modifications
- Primary display switching
- Hot-plug connect/disconnect events

### Edge Cases
- Identical monitors (same EDID data)
- Displays without EDID information
- Legacy displays with incomplete EDID
- Virtual displays and remote desktop scenarios

This architecture provides reliable, hardware-based display identification that works consistently across reconnections, driver updates, and system changes - essential for professional display switching applications.
