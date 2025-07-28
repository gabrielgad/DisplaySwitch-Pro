# DisplaySwitch-Pro Development Guide

## Project Overview
DisplaySwitch-Pro is an F# application built with Avalonia UI for managing multiple display configurations. The project uses an Entity Component System (ECS) architecture with functional programming principles.

## Build Commands

### Build the project
```bash
dotnet build
```

### Run the application
```bash
dotnet run
```

### Run the visual test
```bash
dotnet run --project . test-visual.fs
```

## Test Commands

### Run all tests
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run tests with Expecto (alternative)
```bash
dotnet run --no-build -- --summary
```

### Run specific test categories
```bash
# Run only collision tests
dotnet test --filter "FullyQualifiedName~collisionTests"

# Run only snapping tests  
dotnet test --filter "FullyQualifiedName~snappingTests"
```

## Development Workflow

1. **Code** - Make changes to F# files
2. **Test** - Run `dotnet test` to ensure functionality
3. **Build** - Run `dotnet build` to check compilation
4. **Manual Test** - Run `dotnet run` to test GUI functionality

## Project Structure

- `Types.fs` - Core domain types and data structures
- `Components.fs` - ECS component system
- `Systems.fs` - Business logic systems (PresetSystem, DisplayDetectionSystem)
- `PlatformAdapter.fs` - Platform-specific display detection
- `WindowsDisplaySystem.fs` - Windows API integration for display management
- `UIComponents.fs` - Reusable UI components and display settings dialog
- `GUI.fs` - Avalonia UI components and display arrangement canvas
- `Tests.fs` - Unit tests using Expecto framework
- `Program.fs` - Application entry point

## Key Features

### Display Snapping System
- **Free Movement**: Displays can be dragged freely during interaction
- **Smart Snapping**: On release, displays snap to nearby edges and align with other displays
- **Collision Prevention**: Displays cannot overlap - system finds nearest valid position
- **Configurable Thresholds**: Snap distance can be adjusted via UI slider (5-30px range)
- **Grid Reference**: Enhanced grid lines provide visual alignment guides

### Display Settings Management
- **Resolution & Refresh Rate**: Complete UI for selecting supported display modes
- **Orientation Controls**: 4-button interface for display rotation (Landscape, Portrait, Flipped variants)
- **Primary Display Toggle**: Set any display as the primary display
- **Real-time Preview**: Shows current settings and selected changes before applying
- **Theme-aware Styling**: All controls adapt to light/dark theme with proper visibility

### Windows API Integration
- **Display Mode Detection**: Full enumeration of supported resolutions and refresh rates
- **Display Configuration**: Complete Windows API calls for changing display settings
- **Error Handling**: Comprehensive error reporting for unsupported modes or driver issues

**⚠️ KNOWN ISSUES:**
- Windows API calls (`ChangeDisplaySettingsEx`) may not work properly in WSL environment
- Display mode changes require proper graphics driver support
- Primary display changes may need elevated privileges on some systems
- Testing should be done on native Windows environment for full functionality

### Test Coverage
- Display system basic operations
- Component system (add/update displays)
- Preset system (save/load configurations)
- Collision detection algorithms
- Snapping logic and threshold validation

## Troubleshooting

### Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Test Issues
```bash
# Verbose test output
dotnet test --logger "console;verbosity=diagnostic"
```

### Runtime Issues
- Check display detection in `PlatformAdapter.fs`
- Verify Avalonia UI initialization in `Program.fs`
- Review console output for any error messages

## Performance Notes
- Display snapping calculations are optimized for real-time interaction
- Collision detection uses efficient bounding box algorithms
- Grid rendering is optimized with separate major/minor line rendering