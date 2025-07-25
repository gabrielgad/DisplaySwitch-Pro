# Build System - F# & .NET 8

## Overview

DisplaySwitch-Pro uses a modern F# and .NET 8 build system optimized for functional programming, cross-platform deployment, and high-performance native compilation. The build system supports Linux and Windows targets with comprehensive functional testing using property-based testing frameworks.

## Prerequisites

### Required Software
- **Linux (Ubuntu 20.04+, Fedora 35+) or Windows 10+** (development and target)
- **.NET 8 SDK** - Download from https://dotnet.microsoft.com/download/dotnet/8.0
- **F# 8.0** (included with .NET 8 SDK)
- **Optional**: 
  - JetBrains Rider (excellent F# support)
  - Visual Studio 2022 17.8+ (Community edition is free)
  - VS Code with Ionide F# extension

### Verification Commands
```bash
# Check .NET 8 SDK installation
dotnet --version
# Should show 8.0.x

# Verify F# compiler
dotnet fsc --help

# List available SDKs (should include .NET 8)
dotnet --list-sdks

# Check target frameworks
dotnet --list-runtimes
```

## F# Project Configuration

### Main Project File
**File**: `DisplaySwitch-Pro.fsproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <UseAppHost>true</UseAppHost>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>partial</TrimMode>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- F# source files in compilation order -->
    <Compile Include="Types.fs" />
    <Compile Include="Events.fs" />
    <Compile Include="Components.fs" />
    <Compile Include="Systems/DisplayDetection.fs" />
    <Compile Include="Systems/Configuration.fs" />
    <Compile Include="Systems/EventSourcing.fs" />
    <Compile Include="Platform/IPlatformAdapter.fs" />
    <Compile Include="Platform/LinuxAdapter.fs" />
    <Compile Include="Platform/WindowsAdapter.fs" />
    <Compile Include="UI/Views.fs" />
    <Compile Include="UI/App.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <!-- F# and functional programming packages -->
    <PackageReference Include="FSharp.Core" Version="8.0.100" />
    <PackageReference Include="FSharp.Data" Version="6.3.0" />
    <PackageReference Include="FSharp.Control.Reactive" Version="5.0.5" />
    <PackageReference Include="Avalonia" Version="11.0.7" />
    <PackageReference Include="Avalonia.FuncUI" Version="0.5.3" />
    <PackageReference Include="System.Reactive" Version="6.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>

  <!-- Linux-specific native dependencies -->
  <ItemGroup Condition="'$(RuntimeIdentifier)' == 'linux-x64'">
    <PackageReference Include="Tmds.DBus" Version="0.15.0" />
  </ItemGroup>

  <!-- Windows-specific native dependencies -->
  <ItemGroup Condition="'$(RuntimeIdentifier)' == 'win-x64'">
    <PackageReference Include="PInvoke.User32" Version="0.7.124" />
  </ItemGroup>
</Project>
```

### Test Project Configuration
**File**: `DisplaySwitch-Pro.Tests.fsproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Tests/PropertyTests.fs" />
    <Compile Include="Tests/UnitTests.fs" />
    <Compile Include="Tests/IntegrationTests.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="FsCheck" Version="2.16.5" />
    <PackageReference Include="FsCheck.Xunit" Version="2.16.5" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="FsUnit.xUnit" Version="5.6.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="DisplaySwitch-Pro.fsproj" />
  </ItemGroup>
</Project>
```

### Key Configuration Properties
- **OutputType**: `Exe` - Cross-platform console/GUI application
- **TargetFramework**: `net8.0` - Latest .NET with F# 8.0 features
- **PublishSingleFile**: `true` - Single executable with all dependencies
- **SelfContained**: `true` - No external .NET runtime required
- **PublishTrimmed**: `true` - Remove unused assemblies for smaller size
- **TrimMode**: `partial` - Safe trimming for F# applications
- **PublishReadyToRun**: `true` - AOT compilation for faster startup

## Cross-Platform Build Methods

### Method 1: F# CLI Build

#### Setup Project
```bash
# Create F# project directory
mkdir DisplaySwitch-Pro
cd DisplaySwitch-Pro

# Initialize F# console project
dotnet new console -lang F# -n DisplaySwitch-Pro

# Or create project files manually (see Project Configuration above)
```

#### Build Commands
```bash
# Build debug version (with F# compiler optimizations)
dotnet build

# Build release version (full optimizations)
dotnet build -c Release

# Cross-platform builds
dotnet build -c Release -r linux-x64
dotnet build -c Release -r win-x64
dotnet build -c Release -r osx-x64

# Clean all build artifacts
dotnet clean
```

#### Cross-Platform Publish Commands
```bash
# Linux x64 (Ubuntu, Fedora, etc.)
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true

# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true

# ARM64 (Apple Silicon, Raspberry Pi)
dotnet publish -c Release -r linux-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true

# Optimized for size (aggressive trimming)
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true \
  -p:TrimMode=full -p:EnableCompressionInSingleFile=true
```

#### F# Interactive Development
```bash
# Start F# REPL for interactive development
dotnet fsi

# Load and test modules interactively
#load "Types.fs";;
#load "Components.fs";;
open DisplaySwitchPro.Types;;

# Test functions in REPL
let testDisplay = { EntityId = DisplayId.New(); FriendlyName = "Test"; DevicePath = "/dev/test"; IsConnected = true; ManufacturerInfo = None }
```

#### Output Locations
- **Debug Build**: `bin/Debug/net8.0/DisplaySwitch-Pro` (Linux) / `DisplaySwitch-Pro.exe` (Windows)
- **Release Build**: `bin/Release/net8.0/DisplaySwitch-Pro`
- **Linux Published**: `bin/Release/net8.0/linux-x64/publish/DisplaySwitch-Pro`
- **Windows Published**: `bin/Release/net8.0/win-x64/publish/DisplaySwitch-Pro.exe`

### Method 2: IDE Development

#### JetBrains Rider (Recommended for F#)
1. Open Rider → **New Solution** → **F# Console Application**
2. Project Name: `DisplaySwitch-Pro`
3. Framework: `.NET 8.0`
4. Enable F# Interactive window for REPL development
5. **Build Menu** → **Build Solution** (Ctrl+Shift+F9)
6. **Run** → **Debug/Run** (F5/Ctrl+F5)

#### Visual Studio 2022
1. **Create New Project** → **Console App** → **F#**
2. Project Name: `DisplaySwitch-Pro`
3. Framework: `.NET 8.0`
4. **Build Menu** → **Build Solution** (Ctrl+Shift+B)
5. **Debug Menu** → **Start with/without Debugging**

#### VS Code with Ionide
```bash
# Install Ionide F# extension
code --install-extension Ionide.Ionide-fsharp

# Open project
code DisplaySwitch-Pro

# Use integrated terminal for builds
dotnet build
dotnet run
```

### Method 3: Cross-Platform Build Scripts

#### Linux/macOS Shell Script  
**File**: `build.sh`
```bash
#!/bin/bash
set -e

echo "Building DisplaySwitch-Pro with F# and .NET 8..."

# Check prerequisites
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET 8 SDK not found. Please install from https://dotnet.microsoft.com/"
    exit 1
fi

# Verify .NET 8
DOTNET_VERSION=$(dotnet --version | cut -d'.' -f1)
if [ "$DOTNET_VERSION" -lt "8" ]; then
    echo "Error: .NET 8 required, found: $(dotnet --version)"
    exit 1
fi

echo "Using .NET SDK: $(dotnet --version)"

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean

# Restore dependencies
echo "Restoring F# dependencies..."
dotnet restore

# Run tests with property-based testing
echo "Running FsCheck property tests..."
dotnet test --configuration Release --logger "console;verbosity=normal"

# Build for current platform
echo "Building optimized release..."
dotnet build --configuration Release --no-restore

# Cross-platform publishing
echo "Publishing cross-platform binaries..."

# Linux x64
echo "  Building Linux x64..."
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true \
  --output ./dist/linux-x64

# Windows x64  
echo "  Building Windows x64..."
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true \
  --output ./dist/win-x64

echo ""
echo "Build complete! 🎉"
echo "Linux binary: ./dist/linux-x64/DisplaySwitch-Pro"
echo "Windows binary: ./dist/win-x64/DisplaySwitch-Pro.exe"

# Show file sizes
echo ""
echo "Binary sizes:"
ls -lh ./dist/*/DisplaySwitch-Pro*
```

#### PowerShell Script (Windows)
**File**: `build.ps1`
```powershell
Write-Host "Building DisplaySwitch-Pro with F# and .NET 8..." -ForegroundColor Green

# Check if .NET 8 SDK is installed
try {
    $dotnetVersion = dotnet --version
    if ($LASTEXITCODE -ne 0) { throw }
} catch {
    Write-Error ".NET 8 SDK not found. Please install from https://dotnet.microsoft.com/"
    exit 1
}

$majorVersion = [int]($dotnetVersion.Split('.')[0])
if ($majorVersion -lt 8) {
    Write-Error ".NET 8 required, found: $dotnetVersion"
    exit 1
}

Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Yellow

# Clean and restore
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean

Write-Host "Restoring F# dependencies..." -ForegroundColor Yellow
dotnet restore

# Run property-based tests
Write-Host "Running FsCheck property tests..." -ForegroundColor Yellow
dotnet test --configuration Release --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed!"
    exit 1
}

# Build optimized release
Write-Host "Building optimized F# release..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore

# Cross-platform publishing
Write-Host "Publishing cross-platform binaries..." -ForegroundColor Yellow

# Windows x64
Write-Host "  Building Windows x64..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true `
  --output ./dist/win-x64

# Linux x64
Write-Host "  Building Linux x64..." -ForegroundColor Cyan  
dotnet publish -c Release -r linux-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true `
  --output ./dist/linux-x64

Write-Host ""
Write-Host "Build complete! 🎉" -ForegroundColor Green
Write-Host "Windows binary: ./dist/win-x64/DisplaySwitch-Pro.exe" -ForegroundColor Cyan
Write-Host "Linux binary: ./dist/linux-x64/DisplaySwitch-Pro" -ForegroundColor Cyan

# Show file sizes
Write-Host ""
Write-Host "Binary sizes:" -ForegroundColor Yellow
Get-ChildItem -Path "./dist/*/DisplaySwitch-Pro*" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.FullName): $size MB" -ForegroundColor White
}
```

## F# Build Configurations

### Debug Configuration
**Purpose**: F# interactive development and testing
**Characteristics**:
- F# debugging symbols and metadata included
- Tail call optimization disabled for debugging
- All F# compiler checks enabled
- Faster incremental compilation
- Hot reload support in IDEs

**Build Command**:
```bash
dotnet build -c Debug
```

### Release Configuration  
**Purpose**: Production deployment with F# optimizations
**Characteristics**:
- Full F# compiler optimizations enabled
- Tail call optimization active
- Dead code elimination
- Inlining and partial application optimizations
- No debugging symbols

**Build Command**:
```bash
dotnet build -c Release
```

### Cross-Platform Publish Configuration
**Purpose**: Optimized native binaries
**Characteristics**:
- Ahead-of-time (AOT) compilation where possible
- Assembly trimming removes unused .NET libraries
- Single file deployment with compression
- Platform-specific optimizations
- Ready-to-run images for faster startup

**Build Commands**:
```bash
# Linux optimized
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true

# Windows optimized  
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRun=true
```

## Advanced Build Options

### Runtime Identifiers
```bash
# Windows 64-bit
-r win-x64

# Windows 32-bit
-r win-x86

# Windows ARM64
-r win-arm64
```

### Trimming Options
```bash
# Enable assembly trimming (smaller size)
-p:PublishTrimmed=true

# Aggressive trimming (more size reduction, potential compatibility issues)
-p:PublishTrimmed=true -p:TrimMode=partial
```

### Compression
```bash
# Enable compression (smaller file, slower startup)
-p:EnableCompressionInSingleFile=true
```

### Icon and Metadata
```xml
<!-- In .csproj file -->
<PropertyGroup>
  <ApplicationIcon>app.ico</ApplicationIcon>
  <AssemblyTitle>Display Manager</AssemblyTitle>
  <AssemblyDescription>Display configuration switching utility</AssemblyDescription>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <Company>Your Company</Company>
  <Product>DisplaySwitch-Pro</Product>
  <Copyright>© 2024 Your Company</Copyright>
</PropertyGroup>
```

## Build Output Analysis

### File Size Comparison
- **Debug build**: ~2-3 MB
- **Release build**: ~1-2 MB
- **Self-contained**: ~150-200 MB (includes .NET runtime)
- **Single file**: ~150-200 MB (compressed)
- **Trimmed**: ~50-100 MB (reduced runtime)

### Startup Performance
- **Regular build**: ~200-500ms
- **Ready-to-run**: ~100-200ms (faster startup)
- **Trimmed**: ~150-300ms (smaller memory footprint)

## Continuous Integration

### GitHub Actions Example
**File**: `.github/workflows/build.yml`
```yaml
name: Build and Release

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 6.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore -c Release
      
    - name: Publish
      run: dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
      
    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: DisplayManager
        path: bin/Release/net6.0-windows/win-x64/publish/DisplayManager.exe
```

## Troubleshooting Build Issues

### Common Problems

#### .NET SDK Not Found
**Error**: `'dotnet' is not recognized as an internal or external command`
**Solution**: Install .NET 6.0 SDK from https://dotnet.microsoft.com/

#### Invalid Runtime Identifier
**Error**: `The runtime identifier 'win-x64' is invalid`
**Solution**: Use correct RID format, check available RIDs with `dotnet --info`

#### Missing Windows Forms
**Error**: `The type or namespace name 'Form' could not be found`
**Solution**: Add `<UseWindowsForms>true</UseWindowsForms>` to project file

#### Large File Size
**Issue**: Published executable is very large
**Solution**: Use trimming options:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### Build Performance Tips

#### Faster Builds
- Use `--no-restore` if dependencies haven't changed
- Use `--no-build` for publish if already built
- Enable parallel builds with `-m` flag

#### Smaller Outputs
- Enable trimming for production builds
- Use compression for single-file publishing
- Remove unused dependencies

## Deployment Preparation

### Code Signing (Optional)
```bash
# Sign the executable (requires code signing certificate)
signtool sign /f "certificate.pfx" /p "password" /t "http://timestamp.verisign.com/scripts/timstamp.dll" DisplayManager.exe
```

### Installer Creation
Consider using tools like:
- **Inno Setup** - Free installer creator
- **WiX Toolset** - Windows Installer XML
- **Advanced Installer** - Commercial installer solution

### Distribution Package
Create a release package containing:
- `DisplayManager.exe` (main executable)
- `README.md` (user documentation)
- `LICENSE` (license file)
- `CHANGELOG.md` (version history)

## Integration Points

### Related Components
- **[Installation](installation.md)**: Deployment and setup procedures
- **[Core Features](core-features.md)**: Application functionality being built
- **[Troubleshooting](troubleshooting.md)**: Runtime issues and solutions

### Development Workflow
1. **Code Changes** → Build System → Testing → Deployment
2. **Version Control** → Continuous Integration → Automated Builds
3. **Release Management** → Build Artifacts → Distribution