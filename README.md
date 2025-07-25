# DisplaySwitch-Pro

A revolutionary cross-platform display configuration manager built with F# using Entity Component System (ECS) architecture and pure functional programming principles. Experience instant, reliable display switching between PC mode (multiple monitors) and TV mode (single display) with unprecedented performance and maintainability.

## 🌟 Why DisplaySwitch-Pro?

Traditional display managers suffer from state management complexity, platform-specific code, and difficult-to-track bugs. DisplaySwitch-Pro takes a radically different approach:

- **Pure Functional Core**: All business logic is implemented as pure functions with immutable data structures
- **ECS Architecture**: Display configurations are entities with composable components (Display, Position, Resolution, RefreshRate)
- **Event Sourcing**: Complete audit trail of all configuration changes, enabling time-travel debugging
- **Cross-Platform**: Native Linux and Windows support through platform adapters
- **Reactive UI**: Built with Avalonia.FuncUI for functional reactive programming

## 🚀 Quick Start

```bash
# Run on Linux
./DisplaySwitch-Pro

# Run on Windows
DisplaySwitch-Pro.exe

# Command line usage
./DisplaySwitch-Pro pc    # Switch to PC mode
./DisplaySwitch-Pro tv    # Switch to TV mode

# Advanced usage with event replay
./DisplaySwitch-Pro --replay-events  # Replay all configuration changes
```

## 📋 Core Features

| Feature | Description | Documentation |
|---------|-------------|---------------|
| **Pure ECS Systems** | DisplayDetectionSystem, ConfigurationSystem with zero side effects | [Core Features](core-features.md) |
| **Immutable Components** | Display, Position, Resolution, RefreshRate entities | [Core Features](core-features.md) |
| **Event Sourcing** | Complete history of all display configuration changes | [Core Features](core-features.md) |
| **Platform Adapters** | Linux X11/Wayland and Windows API isolation | [Core Features](core-features.md) |
| **Functional UI** | Avalonia.FuncUI reactive interface with no mutable state | [GUI Components](gui-components.md) |
| **Cross-Platform CLI** | F# script-friendly command interface | [CLI Interface](cli-interface.md) |

## 🔧 System Integration

### Installation & Setup
- **Cross-Platform Binaries** - Native Linux and Windows executables
- **Zero Dependencies** - Self-contained .NET 8 deployment
- **Systemd Integration** - Linux service support
- **Desktop Environment Integration** - Works with GNOME, KDE, Windows Explorer

📖 **[Complete Installation Guide](installation.md)**

### Build System
- **.NET 8** - Latest functional programming features and performance
- **F# 8.0** - Advanced type system and computation expressions
- **Cross-Platform Builds** - Linux x64, Windows x64, ARM64 support
- **Functional Test Suite** - Property-based testing with FsCheck

📖 **[Build System Documentation](build-system.md)**

## 🏗️ ECS/FP Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│               Functional Reactive UI Layer                  │
├─────────────────┬─────────────────┬─────────────────────────┤
│ Avalonia.FuncUI │   Event Stream  │     CLI Interface       │
│ - Pure Views    │ - Observables   │   - F# Scripts          │
│ - No Mutations  │ - Message Flow  │   - Pipeline Support    │
│ - Type Safety   │ - Hot Reload    │   - Computation Expr.   │
└─────────────────┴─────────────────┴─────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                     ECS Core Systems                        │
├─────────────────┬─────────────────┬─────────────────────────┤
│ Detection System│Configuration Sys│   Event Sourcing        │
│ - Pure Functions│ - State Machines│   - Event Store         │
│ - No Side Effect│ - Validations   │   - Time Travel         │
│ - Composition   │ - Transformations│   - Audit Trail         │
└─────────────────┴─────────────────┴─────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                    ECS Components                           │
├─────────────────┬─────────────────┬─────────────────────────┤
│     Display     │    Position     │     Resolution          │
│ - EntityId      │ - X, Y Coords   │ - Width, Height         │
│ - FriendlyName  │ - IsPrimary     │ - RefreshRate           │
│ - DevicePath    │ - Rotation      │ - ColorDepth            │
└─────────────────┴─────────────────┴─────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                   Platform Adapters                         │
├─────────────────────────────────────────────────────────────┤
│      Linux (X11/Wayland)        │      Windows API         │
│  - xrandr Integration            │ - Display Config API     │
│  - Wayland Protocol             │ - Multi-Monitor Support   │
│  - EDID Parsing                 │ - Hardware Detection      │
└─────────────────────────────────────────────────────────────┘
```

## 🔌 Functional Architecture Benefits

### Pure Function Composition
```
Events → Systems → Components → World State
  ↓        ↓          ↓           ↓
Input → Transform → Validate → Apply
```

### Key Architectural Principles
- **Immutability** - All state changes create new immutable data structures
- **Pure Functions** - Systems have no side effects, making testing trivial
- **Event Sourcing** - Complete auditability and time-travel debugging
- **Composition** - Complex behaviors emerge from simple, composable functions
- **Type Safety** - F#'s type system prevents entire categories of runtime errors

## 📚 Component Documentation

### Core Components
| Component | Purpose | Key Features | Lines of Code |
|-----------|---------|--------------|---------------|
| [Core Features](core-features.md) | Display switching logic | Mode detection, API integration | ~200 |
| [Display API](display-api.md) | Windows API interface | Low-level display control | ~400 |
| [GUI Components](gui-components.md) | User interface | Forms, buttons, status display | ~300 |
| [System Tray](system-tray.md) | Background operation | Notifications, quick access | ~150 |

### Integration Components
| Component | Purpose | Key Features | Lines of Code |
|-----------|---------|--------------|---------------|
| [Configuration Management](config-management.md) | Settings persistence | JSON storage, backup/restore | ~200 |
| [Keyboard Shortcuts](keyboard-shortcuts.md) | Hotkey support | Global/local shortcuts | ~100 |
| [CLI Interface](cli-interface.md) | Command line access | Automation, scripting | ~150 |

### Development & Deployment
| Component | Purpose | Key Features |
|-----------|---------|--------------|
| [Build System](build-system.md) | Compilation & packaging | Multiple build methods, CI/CD |
| [Installation](installation.md) | Deployment procedures | Setup, integration, shortcuts |
| [Troubleshooting](troubleshooting.md) | Issue resolution | Diagnostics, recovery procedures |
| [Advanced Features](advanced-features.md) | Extended functionality | Plugins, automation, monitoring |

## 🛠️ Development Workflow

### Getting Started
```bash
# 1. Clone and build
git clone https://github.com/user/DisplaySwitch-Pro.git
cd DisplaySwitch-Pro
dotnet build

# 2. Explore F# modules
# Start with core-features.md for ECS architecture
# Then build-system.md for F#/.NET 8 setup

# 3. Run functional tests
dotnet test
# F# property-based tests ensure correctness

# 4. Start development
dotnet run
# Hot reload with Avalonia.FuncUI
```

### Contributing Guidelines
1. **Read Component Docs** - Understand the specific component you're modifying
2. **Follow Architecture** - Maintain separation of concerns between components
3. **Update Documentation** - Keep component docs in sync with code changes
4. **Test Integration** - Verify changes don't break component interactions

## 🔍 Quick Reference

### Most Common Tasks
| Task | Primary Documentation | Supporting Docs |
|------|----------------------|-----------------|
| **Add new display mode** | [Core Features](core-features.md) | [Display API](display-api.md) |
| **Modify UI layout** | [GUI Components](gui-components.md) | [System Tray](system-tray.md) |
| **Add hotkey** | [Keyboard Shortcuts](keyboard-shortcuts.md) | [GUI Components](gui-components.md) |
| **Fix startup issue** | [Troubleshooting](troubleshooting.md) | [Installation](installation.md) |
| **Build deployment** | [Build System](build-system.md) | [Installation](installation.md) |
| **Add CLI command** | [CLI Interface](cli-interface.md) | [Core Features](core-features.md) |

### Code Navigation
```
DisplayManagerGUI.cs (main file):
├── Lines 1-87:     Constructor & Initialization
├── Lines 89-197:   GUI Component Setup          → gui-components.md
├── Lines 199-236:  System Tray Setup           → system-tray.md
├── Lines 238-255:  Keyboard Event Handling     → keyboard-shortcuts.md
├── Lines 257-295:  Display Information Loading → core-features.md
├── Lines 297-347:  Mode Switching Methods      → core-features.md
├── Lines 353-417:  Configuration Management    → config-management.md
├── Lines 447-821:  Display API Structures      → display-api.md
├── Lines 824-881:  CLI & Main Entry Point      → cli-interface.md
```

## 📈 Project Status

### Current Version: 1.0
- ✅ All core components implemented
- ✅ Full documentation coverage
- ✅ Cross-component integration complete
- ✅ Build system operational
- ✅ Installation procedures documented

### Roadmap
- 🔄 **Advanced Features** - Global hotkeys, automation, plugins
- 🔄 **Performance Optimization** - Memory usage, startup time
- 🔄 **Enhanced UI** - Modern styling, dark mode
- 🔄 **Extended API** - RESTful interface, remote control

## 📞 Support

### Self-Help Resources
1. **[Troubleshooting Guide](troubleshooting.md)** - Common issues and solutions
2. **Component Documentation** - Detailed feature explanations
3. **Code Comments** - Inline documentation in source

### Getting Help
- **GitHub Issues** - Bug reports and feature requests
- **Documentation** - Start with the relevant component documentation
- **Code Examples** - Each component doc includes usage examples

## 📄 License

This project documentation and code examples are provided for educational and development purposes. See individual component documentation for specific implementation details and considerations.

---

**Quick Navigation**: [Core Features](core-features.md) | [GUI Components](gui-components.md) | [System Tray](system-tray.md) | [Build System](build-system.md) | [Installation](installation.md) | [Troubleshooting](troubleshooting.md)
