# DisplaySwitch-Pro

A modern F# application for managing multiple display configurations with Avalonia UI.

## What We Have

### Core Features
- **Display Detection**: Automatic detection of connected displays and their capabilities
- **Enable/Disable Displays**: Working display control including TV hardware support
- **Display Configuration**: Resolution, refresh rate, and orientation management
- **Primary Display Selection**: Set any display as primary
- **Visual UI**: Drag-and-drop display arrangement canvas with theme support

### Technical Implementation
- **F# Functional Architecture**: Pure functions with Result types for error handling
- **Windows CCD API Integration**: Modern display configuration using Connecting and Configuring Displays API
- **Avalonia UI Framework**: Cross-platform modern UI with dark/light themes
- **Comprehensive Validation**: Multi-method display state verification

## What Needs Implementation

### Configuration Management
- **Save/Load Presets**: Store and restore display configurations
- **Auto-Apply on Startup**: Remember and restore preferred display setup
- **Profile Management**: Multiple configuration profiles for different scenarios

### Advanced Display Features
- **Hotplug Detection**: Automatic response to display connect/disconnect events
- **Resolution Optimization**: Intelligent resolution selection based on display capabilities
- **Multi-Display Positioning**: Smart positioning algorithms for complex layouts

### User Experience
- **Settings Persistence**: Remember user preferences and window positions
- **Keyboard Shortcuts**: Quick display switching via hotkeys
- **System Tray Integration**: Background operation with tray controls
- **Command Line Interface**: Scriptable display operations

### Platform Support
- **Linux/macOS Support**: Extend beyond Windows using platform adapters
- **GPU-Specific Optimizations**: Enhanced support for different graphics drivers

## Build & Run

```bash
# Build and run
dotnet build
dotnet run

# Run tests
dotnet test --logger "console;verbosity=detailed"
```

## Architecture

Built with functional programming principles using F# and organized into domain-focused modules (Core, API, UI).