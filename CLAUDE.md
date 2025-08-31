# DisplaySwitch-Pro Development Guide

## Project Overview
DisplaySwitch-Pro is an F# application built with Avalonia UI for managing multiple display configurations. The project uses functional programming principles with a domain-focused architecture.

## Build Commands

### Build the project
```bash
dotnet build
```

### Run the application
```bash
dotnet run
```

## Test Commands

### Run all tests
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Development Workflow

1. **Code** - Make changes to F# files
2. **Test** - Run `dotnet test` to ensure functionality
3. **Build** - Run `dotnet build` to check compilation
4. **Manual Test** - Run `dotnet run` to test GUI functionality

## Project Structure

### Core Domain
- `Core/Types.fs` - Core domain types and data structures
- `AppState.fs` - Application state management

### API Domain - Display Operations
- `API/WindowsAPI.fs` - Windows API P/Invoke declarations
- `API/DisplayConfigurationAPI.fs` - CCD API wrapper functions
- `API/DisplayDetection.fs` - Display enumeration and detection
- `API/DisplayControl.fs` - High-level display operations
- `API/PlatformAdapter.fs` - Cross-platform abstraction

### UI Domain - User Interface
- `UI/Theme.fs` - UI theming system
- `UI/UIComponents.fs` - Reusable UI components
- `UI/DisplayCanvas.fs` - Visual display arrangement
- `UI/UIState.fs` - UI state management
- `UI/MainContentPanel.fs` - Main UI layout
- `UI/WindowManager.fs` - Window management
- `UI/GUI.fs` - Avalonia UI implementation
- `UI/ApplicationRunner.fs` - Application lifecycle

### Entry Point
- `Program.fs` - Application entry point

## Key Features

### Display Management
- **Enable/Disable Displays**: Complete display control including TV hardware support
- **Resolution & Refresh Rate**: Full Windows API integration for display mode changes
- **Primary Display Selection**: Set any display as the primary display
- **Real-time Display Detection**: Automatic detection of connected displays and capabilities

### Windows CCD API Integration
- **Advanced Display Configuration**: Using Windows Connecting and Configuring Displays API
- **TV Hardware Control**: Proper TV power on/off functionality
- **Target ID Mapping**: Automatic hardware identification and mapping
- **Multi-Strategy Approach**: 8+ different strategies for display activation
- **Comprehensive Validation**: Multi-method display state verification

### User Interface
- **Avalonia UI Framework**: Modern cross-platform UI with dark/light themes
- **Display Canvas**: Visual arrangement of displays with drag-and-drop
- **Theme-aware Styling**: All controls adapt to current theme
- **Real-time Preview**: Live preview of display changes before applying

## Current Status

### ✅ Working Features
- Display enable/disable functionality (fixed with CCD API breakthrough)
- TV hardware control (Samsung Q80A tested successfully)
- Display detection and enumeration
- Basic UI with theme support
- Windows API integration

### 🚧 Next Implementation Priorities
1. **Configuration Presets** - Save/load display configurations
2. **Settings Persistence** - Remember user preferences
3. **Hotplug Detection** - Auto-respond to display events
4. **System Tray Integration** - Background operation
5. **Keyboard Shortcuts** - Quick display switching
6. **Command Line Interface** - Scriptable operations

## Troubleshooting

### Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Runtime Issues
- Check display detection in `API/DisplayDetection.fs`
- Verify Avalonia UI initialization in `Program.fs`
- Review console output for any error messages
- For TV issues, see `docs/TV_FIX_BREAKTHROUGH.md`