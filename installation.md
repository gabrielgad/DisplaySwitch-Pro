# Installation & Setup

## Overview

This guide provides comprehensive installation procedures for DisplaySwitch-Pro, including basic installation, system integration, and advanced deployment scenarios. The installation process is designed to be simple for end users while providing flexibility for system administrators.

## Prerequisites

### System Requirements
- **Operating System**: Windows 7 or later (Windows 10/11 recommended)
- **Architecture**: x64 (64-bit) or x86 (32-bit)
- **Memory**: 50MB RAM minimum
- **Storage**: 200MB available disk space
- **Permissions**: Standard user account (administrator not required)

### Dependencies
- **.NET Runtime**: Included in self-contained builds
- **Visual C++ Redistributable**: Usually pre-installed on modern Windows
- **Graphics Drivers**: Up-to-date display drivers recommended

## Basic Installation

### Method 1: Portable Installation (Recommended)

1. **Download the executable** from the release page
2. **Choose installation location**:
   ```
   C:\Program Files\DisplayManager\
   ```
   or
   ```
   C:\Users\[Username]\AppData\Local\DisplayManager\
   ```

3. **Create directory structure**:
   ```
   DisplayManager\
   ├── DisplayManager.exe
   ├── README.md
   └── configs\          (created automatically)
   ```

4. **Verify installation**:
   - Double-click `DisplayManager.exe`
   - Main window should appear
   - Check system tray for DisplayManager icon

### Method 2: Installer Package (Future)

```batch
DisplayManager-Setup.exe
├── Extracts files to Program Files
├── Creates Start Menu shortcuts
├── Registers file associations
└── Configures auto-start (optional)
```

## System Integration

### Start Menu Integration

#### Create Start Menu Shortcut
1. **Open Start Menu folder**:
   - Press `Win+R`
   - Type `shell:programs`
   - Press Enter

2. **Create shortcut**:
   - Right-click → New → Shortcut
   - Location: `"C:\Program Files\DisplayManager\DisplayManager.exe"`
   - Name: `Display Manager`

3. **Set icon** (optional):
   - Right-click shortcut → Properties
   - Change Icon → Browse to `DisplayManager.exe`
   - Select built-in icon

#### Create Folder Organization
```
Start Menu\Programs\
└── Display Manager\
    ├── Display Manager.lnk
    ├── PC Mode.lnk
    ├── TV Mode.lnk
    └── Uninstall.lnk
```

### Desktop Shortcuts

#### PC Mode Shortcut
1. **Create shortcut**:
   - Right-click desktop → New → Shortcut
   - Location: `"C:\Program Files\DisplayManager\DisplayManager.exe" pc`
   - Name: `PC Mode (All Displays)`

2. **Set properties**:
   - Right-click shortcut → Properties
   - Shortcut key: `Ctrl+Alt+P` (optional)
   - Run: `Normal window`
   - Comment: `Switch to PC mode with all displays`

#### TV Mode Shortcut
1. **Create shortcut**:
   - Right-click desktop → New → Shortcut
   - Location: `"C:\Program Files\DisplayManager\DisplayManager.exe" tv`
   - Name: `TV Mode`

2. **Set properties**:
   - Right-click shortcut → Properties
   - Shortcut key: `Ctrl+Alt+T` (optional)
   - Run: `Normal window`
   - Comment: `Switch to TV mode (single display)`

### Taskbar Integration

#### Pin to Taskbar
1. Right-click `DisplayManager.exe`
2. Select `Pin to taskbar`
3. Application will be available in taskbar

#### Jump List Configuration
```csharp
// Future enhancement: Custom jump list
private void ConfigureJumpList()
{
    var jumpList = new JumpList();
    jumpList.JumpItems.Add(new JumpTask
    {
        Title = "PC Mode",
        Arguments = "pc",
        Description = "Switch to PC mode (all displays)"
    });
    jumpList.JumpItems.Add(new JumpTask
    {
        Title = "TV Mode",
        Arguments = "tv",
        Description = "Switch to TV mode (single display)"
    });
    JumpList.SetJumpList(Application.Current, jumpList);
}
```

## Auto-Start Configuration

### Windows Startup Integration

#### Method 1: Startup Folder
1. **Open Startup folder**:
   - Press `Win+R`
   - Type `shell:startup`
   - Press Enter

2. **Create shortcut**:
   - Copy `DisplayManager.exe` shortcut to startup folder
   - Application will start minimized to system tray

#### Method 2: Registry Entry
```batch
@echo off
echo Adding DisplayManager to Windows startup...
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "DisplayManager" /t REG_SZ /d "\"C:\Program Files\DisplayManager\DisplayManager.exe\"" /f
echo DisplayManager added to startup.
pause
```

#### Method 3: Task Scheduler
```xml
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Date>2024-01-01T00:00:00</Date>
    <Author>User</Author>
    <Description>Start DisplayManager at login</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions>
    <Exec>
      <Command>C:\Program Files\DisplayManager\DisplayManager.exe</Command>
    </Exec>
  </Actions>
</Task>
```

## File Associations

### Configuration File Association
```batch
@echo off
echo Registering .displayconfig file association...

reg add "HKEY_CURRENT_USER\Software\Classes\.displayconfig" /ve /t REG_SZ /d "DisplayManager.Config" /f
reg add "HKEY_CURRENT_USER\Software\Classes\DisplayManager.Config" /ve /t REG_SZ /d "DisplayManager Configuration" /f
reg add "HKEY_CURRENT_USER\Software\Classes\DisplayManager.Config\shell\open\command" /ve /t REG_SZ /d "\"C:\Program Files\DisplayManager\DisplayManager.exe\" config \"%%1\"" /f
reg add "HKEY_CURRENT_USER\Software\Classes\DisplayManager.Config\DefaultIcon" /ve /t REG_SZ /d "\"C:\Program Files\DisplayManager\DisplayManager.exe\",0" /f

echo File association registered.
pause
```

## Configuration Directory Setup

### Default Configuration Location
```
%APPDATA%\DisplayManager\
├── configs\
│   ├── work_setup.json
│   ├── gaming_setup.json
│   └── backup_configs\
├── logs\
│   └── displaymanager.log
└── settings.json
```

### Custom Configuration Location
```csharp
// Allow custom config directory via environment variable
private string GetConfigPath()
{
    string customPath = Environment.GetEnvironmentVariable("DISPLAYMANAGER_CONFIG");
    if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
        return customPath;
    
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DisplayManager"
    );
}
```

## Network Deployment

### Group Policy Deployment
```batch
REM Deploy via Group Policy software installation
REM Place in network share: \\server\software\DisplayManager\

REM Create deployment batch file
@echo off
echo Installing DisplayManager...

REM Copy files to Program Files
xcopy "\\server\software\DisplayManager\*" "C:\Program Files\DisplayManager\" /Y /E /I

REM Create shortcuts
powershell -Command "& {$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\Display Manager.lnk'); $Shortcut.TargetPath = 'C:\Program Files\DisplayManager\DisplayManager.exe'; $Shortcut.Save()}"

REM Add to startup
reg add "HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run" /v "DisplayManager" /t REG_SZ /d "\"C:\Program Files\DisplayManager\DisplayManager.exe\"" /f

echo Installation complete.
```

### Silent Installation Script
```powershell
# Silent installation script for enterprise deployment
param(
    [string]$InstallPath = "C:\Program Files\DisplayManager",
    [switch]$CreateShortcuts = $true,
    [switch]$AutoStart = $true,
    [switch]$AllUsers = $false
)

# Create installation directory
New-Item -ItemType Directory -Path $InstallPath -Force

# Copy application files
Copy-Item -Path "DisplayManager.exe" -Destination $InstallPath -Force
Copy-Item -Path "README.md" -Destination $InstallPath -Force

# Create shortcuts if requested
if ($CreateShortcuts) {
    $WshShell = New-Object -comObject WScript.Shell
    
    # Start menu shortcut
    $StartMenuPath = if ($AllUsers) { "$env:ALLUSERSPROFILE\Microsoft\Windows\Start Menu\Programs" } else { "$env:APPDATA\Microsoft\Windows\Start Menu\Programs" }
    $Shortcut = $WshShell.CreateShortcut("$StartMenuPath\Display Manager.lnk")
    $Shortcut.TargetPath = "$InstallPath\DisplayManager.exe"
    $Shortcut.Description = "Display configuration manager"
    $Shortcut.Save()
    
    # Desktop shortcuts
    $DesktopPath = if ($AllUsers) { "$env:PUBLIC\Desktop" } else { "$env:USERPROFILE\Desktop" }
    
    # PC Mode shortcut
    $PCShortcut = $WshShell.CreateShortcut("$DesktopPath\PC Mode.lnk")
    $PCShortcut.TargetPath = "$InstallPath\DisplayManager.exe"
    $PCShortcut.Arguments = "pc"
    $PCShortcut.Description = "Switch to PC mode (all displays)"
    $PCShortcut.Save()
    
    # TV Mode shortcut
    $TVShortcut = $WshShell.CreateShortcut("$DesktopPath\TV Mode.lnk")
    $TVShortcut.TargetPath = "$InstallPath\DisplayManager.exe"
    $TVShortcut.Arguments = "tv"
    $TVShortcut.Description = "Switch to TV mode (single display)"
    $TVShortcut.Save()
}

# Add to startup if requested
if ($AutoStart) {
    $RegistryPath = if ($AllUsers) { "HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run" } else { "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" }
    Set-ItemProperty -Path $RegistryPath -Name "DisplayManager" -Value "`"$InstallPath\DisplayManager.exe`"" -Force
}

Write-Host "DisplayManager installation complete!" -ForegroundColor Green
Write-Host "Installation path: $InstallPath" -ForegroundColor Yellow
if ($CreateShortcuts) { Write-Host "Shortcuts created" -ForegroundColor Yellow }
if ($AutoStart) { Write-Host "Auto-start enabled" -ForegroundColor Yellow }
```

## Uninstallation

### Manual Uninstallation
1. **Stop the application**:
   - Right-click system tray icon → Exit
   - Or close main window

2. **Remove files**:
   - Delete installation directory
   - Delete configuration directory (optional)

3. **Remove shortcuts**:
   - Delete Start Menu shortcuts
   - Delete Desktop shortcuts
   - Remove from taskbar

4. **Remove registry entries**:
   - Remove startup entry
   - Remove file associations

### Automated Uninstallation Script
```batch
@echo off
echo Uninstalling DisplayManager...

REM Stop the application
taskkill /F /IM DisplayManager.exe 2>nul

REM Remove files
if exist "C:\Program Files\DisplayManager\" (
    rmdir /S /Q "C:\Program Files\DisplayManager\"
)

REM Remove shortcuts
del "%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\Display Manager.lnk" 2>nul
del "%PUBLIC%\Desktop\PC Mode.lnk" 2>nul
del "%PUBLIC%\Desktop\TV Mode.lnk" 2>nul
del "%USERPROFILE%\Desktop\PC Mode.lnk" 2>nul
del "%USERPROFILE%\Desktop\TV Mode.lnk" 2>nul

REM Remove registry entries
reg delete "HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run" /v "DisplayManager" /f 2>nul
reg delete "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "DisplayManager" /f 2>nul
reg delete "HKEY_CURRENT_USER\Software\Classes\.displayconfig" /f 2>nul
reg delete "HKEY_CURRENT_USER\Software\Classes\DisplayManager.Config" /f 2>nul

REM Optional: Remove configuration directory
echo.
echo Do you want to remove configuration files? (Y/N)
set /p choice=
if /i "%choice%"=="Y" (
    rmdir /S /Q "%APPDATA%\DisplayManager\" 2>nul
    echo Configuration files removed.
) else (
    echo Configuration files preserved.
)

echo.
echo DisplayManager uninstallation complete!
pause
```

## Troubleshooting Installation

### Common Installation Issues

#### Permission Denied
**Problem**: Cannot write to Program Files directory
**Solution**: 
- Install to user directory instead: `%LOCALAPPDATA%\DisplayManager\`
- Or run installer as Administrator

#### Missing Dependencies
**Problem**: Application fails to start
**Solution**:
- Use self-contained build (includes .NET runtime)
- Install Visual C++ Redistributable

#### Already Running Error
**Problem**: "DisplayManager is already running" message
**Solution**:
- Check system tray for existing instance
- End process in Task Manager
- Restart installation

### Verification Steps

#### Post-Installation Checklist
1. **File Presence**:
   ```batch
   dir "C:\Program Files\DisplayManager\DisplayManager.exe"
   ```

2. **Startup Test**:
   ```batch
   "C:\Program Files\DisplayManager\DisplayManager.exe" --version
   ```

3. **CLI Functionality**:
   ```batch
   "C:\Program Files\DisplayManager\DisplayManager.exe" pc
   "C:\Program Files\DisplayManager\DisplayManager.exe" tv
   ```

4. **Configuration Directory**:
   ```batch
   dir "%APPDATA%\DisplayManager"
   ```

## Enterprise Deployment

### System Center Configuration Manager (SCCM)
```xml
<!-- Application definition for SCCM -->
<Application>
  <Name>DisplayManager</Name>
  <Version>1.0</Version>
  <InstallCommandLine>powershell.exe -ExecutionPolicy Bypass -File "install.ps1" -AllUsers -AutoStart</InstallCommandLine>
  <UninstallCommandLine>powershell.exe -ExecutionPolicy Bypass -File "uninstall.ps1" -AllUsers</UninstallCommandLine>
  <DetectionMethod>
    <File>
      <Path>C:\Program Files\DisplayManager</Path>
      <FileName>DisplayManager.exe</FileName>
    </File>
  </DetectionMethod>
</Application>
```

### Microsoft Intune Deployment
```json
{
  "displayName": "DisplayManager",
  "description": "Display configuration management tool",
  "publisher": "Your Company",
  "installCommandLine": "powershell.exe -ExecutionPolicy Bypass -File install.ps1",
  "uninstallCommandLine": "powershell.exe -ExecutionPolicy Bypass -File uninstall.ps1",
  "applicableArchitectures": ["x64", "x86"],
  "minimumSupportedOperatingSystem": {
    "windows10_1607": true
  },
  "detectionRules": [
    {
      "ruleType": "file",
      "path": "C:\\Program Files\\DisplayManager",
      "fileOrFolderName": "DisplayManager.exe",
      "check32BitOn64System": false,
      "operationType": "exists"
    }
  ]
}
```

## Future Installation Enhancements

### Planned Features
- **MSI Installer**: Windows Installer package
- **Chocolatey Package**: Package manager support
- **Winget Support**: Windows Package Manager
- **Auto-Update**: Built-in update mechanism
- **Custom Installer**: Branded installation experience

### Installation Metrics
```csharp
// Track installation success
private void LogInstallation()
{
    var installInfo = new
    {
        Version = Application.ProductVersion,
        InstallPath = Application.ExecutablePath,
        InstallTime = DateTime.Now,
        OSVersion = Environment.OSVersion.VersionString,
        Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
        UserName = Environment.UserName,
        MachineName = Environment.MachineName
    };
    
    // Log to file or telemetry service
    LogInstallationInfo(installInfo);
}
```

## Integration Points

### Related Components
- **[Build System](build-system.md)**: Produces installable artifacts
- **[Configuration Management](config-management.md)**: Sets up configuration directories
- **[System Tray](system-tray.md)**: Provides always-available access after installation
- **[Troubleshooting](troubleshooting.md)**: Handles installation-related issues

### Post-Installation Flow
1. **Installation** → File Deployment → Registry Setup → Shortcut Creation
2. **First Launch** → Configuration Directory Creation → Initial Setup
3. **User Onboarding** → Feature Discovery → Configuration Testing
4. **System Integration** → Startup Configuration → Usage Monitoring