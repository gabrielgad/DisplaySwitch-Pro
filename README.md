# DisplaySwitch-Pro

A clean, functional F# application for managing display configurations with preset support.

## Features
- Detect connected displays
- Save display configurations as presets
- Load and switch between presets
- Simple GUI interface using Avalonia

## Architecture
- Entity-Component-System (ECS) design
- Pure functional F# 
- Event-driven state management
- Cross-platform support (Windows/Linux/macOS)

## Building
```bash
dotnet build
dotnet run
```

## Status
✅ Core ECS system working  
✅ Display detection (mock)  
✅ Preset save/load  
✅ Basic GUI  
🚧 Visual display arrangement (in progress)  
🚧 Real display detection (planned)  

## Requirements
- .NET 8.0 SDK
- Avalonia UI framework