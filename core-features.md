# Core Features

## Overview

The core features of DisplaySwitch-Pro provide the fundamental display switching functionality that allows users to quickly toggle between different display configurations. This module handles the primary use case of switching between PC mode (multiple monitors) and TV mode (single display).

## Key Features

### Display Mode Switching
- **PC Mode**: Activates all connected displays in extended configuration
- **TV Mode**: Activates only the external display (typically a TV)
- **Real-time switching**: Instant configuration changes without restart
- **Visual feedback**: Status updates and notifications for mode changes

### Display Detection
- **Automatic detection**: Scans and identifies all connected displays
- **Real-time information**: Shows current display configuration
- **Display properties**: Resolution, refresh rate, position, and status
- **Friendly naming**: User-readable display names

## Implementation Details

### Core Classes

#### DisplayManager.DisplayMode Enum
```csharp
public enum DisplayMode
{
    PCMode,    // All displays active (extended)
    TVMode     // Single external display only
}
```

#### SetDisplayMode Method
Located in `DisplayManagerGUI.cs:779-795`

```csharp
public static void SetDisplayMode(DisplayMode mode)
{
    SetDisplayConfigFlags flags = SetDisplayConfigFlags.Apply;
    
    switch (mode)
    {
        case DisplayMode.PCMode:
            SetDisplayConfig(0, null, 0, null, 
                flags | (SetDisplayConfigFlags)DisplayConfigTopologyId.Extend);
            break;
            
        case DisplayMode.TVMode:
            SetDisplayConfig(0, null, 0, null, 
                flags | (SetDisplayConfigFlags)DisplayConfigTopologyId.External);
            break;
    }
}
```

#### GetCurrentConfiguration Method
Located in `DisplayManagerGUI.cs:668-758`

This method:
- Queries Windows Display Configuration API
- Retrieves active and inactive display paths
- Collects display properties (resolution, position, refresh rate)
- Returns structured DisplayConfig object

### Mode Detection Logic

#### TV Detection
Located in `DisplayManagerGUI.cs:800-804`

```csharp
bool hasTV = config.Displays.Any(d => 
    d.FriendlyName.ToLower().Contains("tv") || 
    d.FriendlyName.ToLower().Contains("hdmi") ||
    d.FriendlyName.ToLower().Contains("samsung") ||
    d.FriendlyName.ToLower().Contains("lg"));
```

#### Current Mode Determination
Located in `DisplayManagerGUI.cs:286-287`

```csharp
string mode = activeCount == 1 ? "TV Mode" : "PC Mode";
```

## Usage Examples

### Basic Mode Switching
```csharp
// Switch to PC mode (all displays)
DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);

// Switch to TV mode (single external display)
DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.TVMode);
```

### Display Information Retrieval
```csharp
// Get current display configuration
var config = DisplayManager.GetCurrentConfiguration();

// Check display count
int activeDisplays = config.Displays.Count(d => d.IsActive);

// Get display details
foreach (var display in config.Displays)
{
    Console.WriteLine($"Display: {display.FriendlyName}");
    Console.WriteLine($"Active: {display.IsActive}");
    Console.WriteLine($"Resolution: {display.Width}x{display.Height}");
}
```

## Configuration Options

### Custom TV Detection
To add support for additional TV brands or models:

```csharp
// Modify the TV detection logic
bool hasTV = config.Displays.Any(d => 
    d.FriendlyName.ToLower().Contains("tv") || 
    d.FriendlyName.ToLower().Contains("your_tv_brand") ||
    d.DeviceName == "\\\\.\\DISPLAY4");  // Or specific display
```

### Additional Display Modes
To add new display modes:

```csharp
public enum DisplayMode
{
    PCMode,
    TVMode,
    GamingMode,    // New mode
    WorkMode       // New mode
}
```

## Error Handling

### Common Scenarios
- **Display not found**: Graceful fallback to available displays
- **API failures**: Retry logic with user notification
- **Invalid configuration**: Reset to safe default mode

### Error Messages
- Configuration API errors include specific error codes
- User-friendly messages displayed in status bar
- Detailed logging for troubleshooting

## Performance Considerations

### Optimization Techniques
- **Lazy loading**: Display information loaded on demand
- **Caching**: Avoid repeated API calls
- **Async operations**: Non-blocking UI during mode switches
- **Minimal API calls**: Efficient use of Windows Display Configuration API

### Response Times
- Mode switching: ~2 seconds including settling time
- Display detection: ~500ms for typical setups
- Configuration loading: ~100ms from cached data

## Dependencies

### Windows APIs
- `user32.dll` - Display configuration functions
- Windows Display Configuration API
- GDI+ for display enumeration

### Related Components
- [GUI Components](gui-components.md) - UI for mode switching
- [System Tray](system-tray.md) - Quick access to core features
- [Keyboard Shortcuts](keyboard-shortcuts.md) - Hotkey triggers
- [Configuration Management](config-management.md) - Persistent settings

## Testing

### Test Scenarios
1. **Single display setup** - Verify TV mode behavior
2. **Multiple display setup** - Verify PC mode behavior
3. **Mixed display types** - TV + monitor combinations
4. **Display hotplug** - Connect/disconnect during operation

### Validation Points
- Correct display activation/deactivation
- Proper resolution and refresh rate settings
- Position and orientation preservation
- Status reporting accuracy

## Future Enhancements

### Planned Features
- **Custom display arrangements** - User-defined layouts
- **Profile-based switching** - Named display configurations
- **Automatic detection** - Context-aware mode switching
- **Multi-monitor TV support** - Advanced TV configurations