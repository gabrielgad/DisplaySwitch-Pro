# Build System

## Overview

The build system for DisplaySwitch-Pro provides multiple methods for compiling and packaging the application, from simple command-line builds to automated deployment packages. The system is designed to create self-contained executables that can run on target systems without requiring separate .NET runtime installation.

## Prerequisites

### Required Software
- **Windows 7 or later** (development and target environment)
- **.NET 6.0 SDK or later** - Download from https://dotnet.microsoft.com/
- **Optional**: Visual Studio 2022 (Community edition is free)

### Verification Commands
```bash
# Check .NET SDK installation
dotnet --version

# List available SDKs
dotnet --list-sdks

# List available runtimes
dotnet --list-runtimes
```

## Project Configuration

### Project File Structure
**File**: `DisplayManager.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net6.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationIcon>app.ico</ApplicationIcon>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>true</PublishReadyToRun>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
</Project>
```

### Key Configuration Properties
- **OutputType**: `WinExe` - Windows application (no console window)
- **TargetFramework**: `net6.0-windows` - Windows-specific .NET 6.0
- **UseWindowsForms**: `true` - Enable Windows Forms support
- **PublishSingleFile**: `true` - Create single executable file
- **SelfContained**: `true` - Include .NET runtime in output
- **RuntimeIdentifier**: `win-x64` - Target 64-bit Windows
- **PublishReadyToRun**: `true` - Pre-compile for faster startup

## Build Methods

### Method 1: Command Line Build (.NET CLI)

#### Setup Project
```bash
# Create project directory
mkdir DisplayManager
cd DisplayManager

# Initialize project (if not exists)
dotnet new winforms -n DisplayManager

# Or create project file manually (see Project Configuration above)
```

#### Build Commands
```bash
# Build debug version
dotnet build

# Build release version (optimized)
dotnet build -c Release

# Build with specific runtime
dotnet build -c Release -r win-x64

# Clean build artifacts
dotnet clean
```

#### Publish Commands
```bash
# Create self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true

# Create single file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Create ready-to-run executable (faster startup)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

# Trim unused assemblies (smaller file size)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

#### Output Locations
- **Debug Build**: `bin\Debug\net6.0-windows\DisplayManager.exe`
- **Release Build**: `bin\Release\net6.0-windows\DisplayManager.exe`
- **Published Single File**: `bin\Release\net6.0-windows\win-x64\publish\DisplayManager.exe`

### Method 2: Visual Studio Build

#### Setup Steps
1. Open Visual Studio 2022
2. Create New Project → Windows Forms App (.NET)
3. Project Name: `DisplayManager`
4. Framework: `.NET 6.0 (Long-term support)`
5. Replace generated code with source code

#### Build Process
1. **Build Menu** → **Build Solution** (Ctrl+Shift+B)
2. **Build Menu** → **Rebuild Solution** (clean + build)
3. **Build Menu** → **Publish DisplayManager** (for deployment)

#### Configuration Manager
- **Debug Configuration**: Development builds with debugging symbols
- **Release Configuration**: Optimized builds for deployment
- **Platform**: x64 for 64-bit Windows, x86 for 32-bit Windows

### Method 3: Automated Build Script

#### Windows Batch Script
**File**: `build.bat`
```batch
@echo off
echo Building Display Manager...

REM Create project file if it doesn't exist
if not exist DisplayManager.csproj (
    echo Creating project file...
    (
    echo ^<Project Sdk="Microsoft.NET.Sdk"^>
    echo   ^<PropertyGroup^>
    echo     ^<OutputType^>WinExe^</OutputType^>
    echo     ^<TargetFramework^>net6.0-windows^</TargetFramework^>
    echo     ^<UseWindowsForms^>true^</UseWindowsForms^>
    echo     ^<PublishSingleFile^>true^</PublishSingleFile^>
    echo     ^<SelfContained^>true^</SelfContained^>
    echo     ^<RuntimeIdentifier^>win-x64^</RuntimeIdentifier^>
    echo     ^<PublishReadyToRun^>true^</PublishReadyToRun^>
    echo   ^</PropertyGroup^>
    echo ^</Project^>
    ) > DisplayManager.csproj
)

REM Build release version
echo Building release version...
dotnet build -c Release

REM Create single file executable
echo Creating single file executable...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

echo.
echo Build complete!
echo Executable location: bin\Release\net6.0-windows\win-x64\publish\DisplayManager.exe
echo File size: 
dir /s "bin\Release\net6.0-windows\win-x64\publish\DisplayManager.exe"
pause
```

#### PowerShell Script
**File**: `build.ps1`
```powershell
Write-Host "Building Display Manager..." -ForegroundColor Green

# Check if .NET SDK is installed
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Error ".NET SDK not found. Please install .NET 6.0 SDK or later."
    exit 1
}

Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Yellow

# Create project file if it doesn't exist
if (-not (Test-Path "DisplayManager.csproj")) {
    Write-Host "Creating project file..." -ForegroundColor Yellow
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net6.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>true</PublishReadyToRun>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
</Project>
"@ | Out-File -FilePath "DisplayManager.csproj" -Encoding utf8
}

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean

# Build release version
Write-Host "Building release version..." -ForegroundColor Yellow
dotnet build -c Release

# Create single file executable
Write-Host "Creating single file executable..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true

# Show results
$outputPath = "bin\Release\net6.0-windows\win-x64\publish\DisplayManager.exe"
if (Test-Path $outputPath) {
    $fileSize = (Get-Item $outputPath).Length
    Write-Host "Build complete!" -ForegroundColor Green
    Write-Host "Executable location: $outputPath" -ForegroundColor Cyan
    Write-Host "File size: $([math]::Round($fileSize / 1MB, 2)) MB" -ForegroundColor Cyan
} else {
    Write-Error "Build failed - output file not found"
}
```

## Build Configurations

### Debug Configuration
**Purpose**: Development and testing
**Characteristics**:
- Debugging symbols included
- No code optimization
- Faster build times
- Larger file size
- Performance not optimized

**Build Command**:
```bash
dotnet build -c Debug
```

### Release Configuration
**Purpose**: Production deployment
**Characteristics**:
- Optimized code
- No debugging symbols
- Smaller file size
- Better performance
- Longer build times

**Build Command**:
```bash
dotnet build -c Release
```

### Publish Configuration
**Purpose**: Deployment packages
**Characteristics**:
- Self-contained runtime
- Single file executable
- Ready-to-run compilation
- Trimmed assemblies (optional)
- Platform-specific

**Build Command**:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
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