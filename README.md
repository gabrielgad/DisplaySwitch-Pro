# DisplaySwitch-Pro

A Windows application for seamless display configuration management, providing instant switching between PC mode (multiple monitors) and TV mode (single display) with comprehensive automation and customization features.

## 🚀 Quick Start

```bash
# Download and run
DisplayManager.exe

# Command line usage
DisplayManager.exe pc    # Switch to PC mode
DisplayManager.exe tv    # Switch to TV mode
```

## 📋 Core Features

| Feature | Description | Documentation |
|---------|-------------|---------------|
| **Display Switching** | One-click toggle between PC and TV modes | [Core Features](core-features.md) |
| **System Tray** | Always-available background operation | [System Tray](system-tray.md) |
| **GUI Interface** | Intuitive Windows Forms application | [GUI Components](gui-components.md) |
| **Configuration Management** | Save/load custom display setups | [Configuration Management](config-management.md) |
| **Keyboard Shortcuts** | Instant access via hotkeys (Ctrl+1, Ctrl+2) | [Keyboard Shortcuts](keyboard-shortcuts.md) |
| **Command Line Interface** | Automation and scripting support | [CLI Interface](cli-interface.md) |

## 🔧 System Integration

### Installation & Setup
- **Portable Installation** - No installer required
- **Auto-Start Support** - Launch at Windows startup
- **Desktop Shortcuts** - Quick access via shortcuts
- **Start Menu Integration** - Professional installation experience

📖 **[Complete Installation Guide](installation.md)**

### Build System
- **.NET 6.0** - Modern framework with self-contained deployment
- **Single File Executable** - No dependencies required
- **Cross-Platform Ready** - Windows 7+ support
- **Multiple Build Methods** - CLI, Visual Studio, automated scripts

📖 **[Build System Documentation](build-system.md)**

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    User Interface Layer                     │
├─────────────────┬─────────────────┬─────────────────────────┤
│   GUI Components│  System Tray    │   CLI Interface         │
│   - Main Window │  - Context Menu │   - Command Processing │
│   - Buttons     │  - Notifications│   - Automation Support │
│   - Status Bar  │  - Quick Access │   - Exit Codes         │
└─────────────────┴─────────────────┴─────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                  Application Logic Layer                    │
├─────────────────┬─────────────────┬─────────────────────────┤
│  Core Features  │ Config Management│  Keyboard Shortcuts    │
│  - Mode Switch  │ - Save/Load     │  - Hotkey Registration │
│  - Detection    │ - JSON Storage  │  - Event Handling      │
│  - Validation   │ - Backup/Restore│  - Global Access       │
└─────────────────┴─────────────────┴─────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                   System Interface Layer                    │
├─────────────────────────────────────────────────────────────┤
│              Windows Display Configuration API              │
│  - Display Enumeration    - Mode Application               │
│  - Hardware Detection     - Resolution Management          │
│  - Topology Control       - Multi-Monitor Support          │
└─────────────────────────────────────────────────────────────┘
```

## 🔌 Integration Points

### Cross-Component Communication
```
GUI Components ←→ Core Features ←→ Display API
     ↕                ↕               ↕
System Tray   ←→ Config Mgmt   ←→ Keyboard Shortcuts
     ↕                ↕               ↕
CLI Interface ←→ Troubleshooting ←→ Advanced Features
```

Each component is designed for:
- **Loose Coupling** - Minimal dependencies between components
- **Event-Driven** - Components communicate via events
- **Extensible** - Easy to add new features
- **Testable** - Components can be tested independently

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

# 2. Read component documentation
# Start with core-features.md for main functionality
# Then gui-components.md for UI understanding

# 3. Make changes and test
dotnet run
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