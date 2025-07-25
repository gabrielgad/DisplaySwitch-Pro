# Installation & Setup

## Overview

This guide provides comprehensive installation procedures for DisplaySwitch-Pro, built with F# and an Entity Component System (ECS) architecture. The functional programming approach enables single binary deployment, immutable state management, and superior cross-platform support with Linux as the primary platform.

## Prerequisites

### System Requirements
- **Operating System (Primary)**: Linux (any modern distribution)
  - Ubuntu 20.04+, Fedora 35+, Arch Linux, etc.
  - Wayland or X11 display server
- **Operating System (Secondary)**: Windows 10/11, macOS 12+
- **Architecture**: x64 (64-bit) or ARM64
- **Memory**: 50MB RAM minimum
- **Storage**: 50MB available disk space (single binary)
- **Permissions**: Standard user account (no root/admin required)

### Dependencies
- **Runtime**: None! Single self-contained binary
- **Display Server**: X11/Wayland on Linux, native APIs on Windows/macOS
- **Graphics Drivers**: Up-to-date display drivers recommended

## Basic Installation

### Method 1: Single Binary Installation (Recommended)

#### Linux (Primary Platform)
1. **Download the binary**:
   ```bash
   wget https://github.com/displayswitch-pro/releases/latest/download/displayswitch-pro-linux-x64
   chmod +x displayswitch-pro-linux-x64
   ```

2. **Choose installation location**:
   ```bash
   # System-wide (requires sudo)
   sudo mv displayswitch-pro-linux-x64 /usr/local/bin/displayswitch-pro
   
   # User-local (recommended)
   mkdir -p ~/.local/bin
   mv displayswitch-pro-linux-x64 ~/.local/bin/displayswitch-pro
   ```

3. **Verify installation**:
   ```bash
   displayswitch-pro --version
   displayswitch-pro --help
   ```

#### Windows
1. **Download the executable**:
   - Get `displayswitch-pro-win-x64.exe` from releases
   
2. **Place in preferred location**:
   ```
   C:\Program Files\DisplaySwitchPro\
   └── displayswitch-pro.exe
   ```

3. **Add to PATH** (optional):
   - System Properties → Environment Variables → PATH

### Method 2: Package Managers

#### Linux Package Managers
```bash
# Arch Linux (AUR)
yay -S displayswitch-pro

# Ubuntu/Debian (via PPA)
sudo add-apt-repository ppa:displayswitch-pro/stable
sudo apt update
sudo apt install displayswitch-pro

# Fedora/RHEL
sudo dnf copr enable displayswitch-pro/stable
sudo dnf install displayswitch-pro

# Nix/NixOS
nix-env -iA nixpkgs.displayswitch-pro
```

#### macOS (Homebrew)
```bash
brew tap displayswitch-pro/tap
brew install displayswitch-pro
```

## System Integration

### Linux Desktop Integration

#### Desktop Entry
Create `/usr/share/applications/displayswitch-pro.desktop`:
```ini
[Desktop Entry]
Name=DisplaySwitch Pro
Comment=Display configuration manager with ECS architecture
Exec=/usr/local/bin/displayswitch-pro
Icon=displayswitch-pro
Type=Application
Categories=System;Settings;HardwareSettings;
Keywords=display;monitor;screen;configuration;
Actions=PCMode;TVMode;Settings;

[Desktop Action PCMode]
Name=PC Mode (All Displays)
Exec=/usr/local/bin/displayswitch-pro pc

[Desktop Action TVMode]
Name=TV Mode (Single Display)
Exec=/usr/local/bin/displayswitch-pro tv

[Desktop Action Settings]
Name=Open Settings
Exec=/usr/local/bin/displayswitch-pro --settings
```

#### System Service (systemd)
Create `~/.config/systemd/user/displayswitch-pro.service`:
```ini
[Unit]
Description=DisplaySwitch Pro - ECS Display Manager
After=graphical-session.target

[Service]
Type=simple
ExecStart=/usr/local/bin/displayswitch-pro --daemon
Restart=on-failure
RestartSec=5

# F# ECS benefits: Single process, low memory footprint
MemoryMax=100M
CPUQuota=10%

[Install]
WantedBy=default.target
```

Enable service:
```bash
systemctl --user enable displayswitch-pro.service
systemctl --user start displayswitch-pro.service
```

### Keyboard Shortcuts

#### Global Hotkeys (Cross-Platform)
The ECS architecture allows consistent hotkey handling across platforms:

```yaml
# ~/.config/displayswitch-pro/hotkeys.yaml
hotkeys:
  pc_mode:
    keys: ["Ctrl", "Alt", "P"]
    action: "switch_mode pc"
    description: "Switch to PC mode (all displays)"
  
  tv_mode:
    keys: ["Ctrl", "Alt", "T"]
    action: "switch_mode tv"
    description: "Switch to TV mode (single display)"
  
  cycle_displays:
    keys: ["Ctrl", "Alt", "D"]
    action: "cycle_displays"
    description: "Cycle through display configurations"
  
  reload_config:
    keys: ["Ctrl", "Alt", "R"]
    action: "reload_config"
    description: "Reload configuration (functional hot-reload)"
```

#### Desktop Environment Integration
```bash
# GNOME
gsettings set org.gnome.settings-daemon.plugins.media-keys custom-keybindings "['/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/custom0/']"
gsettings set org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/custom0/ name 'PC Mode'
gsettings set org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/custom0/ command 'displayswitch-pro pc'
gsettings set org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/custom0/ binding '<Ctrl><Alt>p'

# KDE Plasma
# Use System Settings → Shortcuts → Custom Shortcuts
```

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

### Linux Auto-Start

#### Method 1: XDG Autostart (Desktop Environments)
Create `~/.config/autostart/displayswitch-pro.desktop`:
```ini
[Desktop Entry]
Type=Application
Exec=/usr/local/bin/displayswitch-pro --daemon
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
Name=DisplaySwitch Pro
Comment=Start DisplaySwitch Pro on login
```

#### Method 2: Shell Profile
Add to `~/.bashrc` or `~/.zshrc`:
```bash
# Start DisplaySwitch Pro daemon if not running
if ! pgrep -x "displayswitch-pro" > /dev/null; then
    displayswitch-pro --daemon &
fi
```

#### Method 3: Window Manager Integration
```bash
# i3/Sway
exec --no-startup-id displayswitch-pro --daemon

# AwesomeWM
awful.spawn.with_shell("displayswitch-pro --daemon")

# bspwm
bspc rule -a displayswitch-pro state=floating
displayswitch-pro --daemon &
```

### Windows Auto-Start
```powershell
# PowerShell script for Windows
$Action = New-ScheduledTaskAction -Execute "displayswitch-pro.exe" -Argument "--daemon"
$Trigger = New-ScheduledTaskTrigger -AtLogOn
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName "DisplaySwitchPro" -Action $Action -Trigger $Trigger -Settings $Settings
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

### Cross-Platform Configuration Locations

#### Linux (Primary)
```
~/.config/displayswitch-pro/
├── config.toml           # Main configuration (immutable)
├── profiles/             # Display profiles
│   ├── work.toml
│   ├── gaming.toml
│   └── presentation.toml
├── events/               # Event history (append-only)
│   └── 2025-01-25.log   # Daily event logs
└── state/                # Current state snapshots
    └── current.toml      # Latest state
```

#### Configuration Benefits with F# ECS
```fsharp
// Immutable configuration with type safety
type Config = {
    Version: string
    Profiles: Profile list
    Hotkeys: Hotkey list
    EventStore: EventStoreConfig
}

// Event sourcing for configuration changes
type ConfigEvent =
    | ProfileAdded of Profile
    | ProfileRemoved of string
    | HotkeyChanged of Hotkey
    | SettingUpdated of string * obj

// Pure function for config updates
let updateConfig (config: Config) (event: ConfigEvent) : Config =
    match event with
    | ProfileAdded profile -> 
        { config with Profiles = profile :: config.Profiles }
    | ProfileRemoved name ->
        { config with Profiles = config.Profiles |> List.filter (fun p -> p.Name <> name) }
    | HotkeyChanged hotkey ->
        { config with Hotkeys = updateHotkey config.Hotkeys hotkey }
    | SettingUpdated (key, value) ->
        updateSetting config key value
```

### Environment Variables
```bash
# Override config location
export DISPLAYSWITCH_PRO_CONFIG="$HOME/.config/displayswitch-pro"

# Enable debug mode
export DISPLAYSWITCH_PRO_DEBUG=1

# Event store location
export DISPLAYSWITCH_PRO_EVENTS="/var/log/displayswitch-pro/events"
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

### Linux Uninstallation

#### Single Binary Removal
```bash
# Stop the service
systemctl --user stop displayswitch-pro.service
systemctl --user disable displayswitch-pro.service

# Remove binary
sudo rm /usr/local/bin/displayswitch-pro
# or
rm ~/.local/bin/displayswitch-pro

# Remove desktop entries
rm ~/.local/share/applications/displayswitch-pro.desktop
rm ~/.config/autostart/displayswitch-pro.desktop

# Optional: Remove configuration and event history
rm -rf ~/.config/displayswitch-pro
```

#### Package Manager Removal
```bash
# Arch Linux
yay -R displayswitch-pro

# Ubuntu/Debian
sudo apt remove displayswitch-pro

# Fedora
sudo dnf remove displayswitch-pro

# Nix
nix-env -e displayswitch-pro
```

### Configuration Preservation
The ECS event store allows easy backup and restoration:
```bash
# Backup event history before uninstall
tar -czf displayswitch-pro-backup.tar.gz ~/.config/displayswitch-pro/events/

# Restore on reinstall
tar -xzf displayswitch-pro-backup.tar.gz -C ~/.config/displayswitch-pro/
```

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
- **Flatpak/Snap**: Universal Linux packages
- **AppImage**: Portable Linux format
- **Reproducible Builds**: Nix flakes support
- **Binary Diff Updates**: Minimal update downloads
- **P2P Updates**: Distributed update mechanism

### Installation Benefits with F# ECS

#### Single Binary Advantages
```fsharp
// Self-contained deployment
type DeploymentInfo = {
    Binary: byte[]
    Checksum: string
    Platform: Platform
    Architecture: Architecture
    CompressedSize: int64
    UncompressedSize: int64
}

// Immutable installation record
type InstallationEvent =
    | Installed of version: string * path: string * timestamp: DateTime
    | Updated of fromVersion: string * toVersion: string * timestamp: DateTime
    | Removed of version: string * timestamp: DateTime

// Pure installation verification
let verifyInstallation (path: string) : Result<InstallationInfo, InstallationError> =
    match File.Exists(path) with
    | false -> Error (BinaryNotFound path)
    | true ->
        let checksum = computeChecksum path
        let version = extractVersion path
        Ok { Path = path; Version = version; Checksum = checksum }
```

#### Cross-Platform Package Generation
```bash
# Single build script for all platforms
./build.sh --release --all-platforms

# Outputs:
# - displayswitch-pro-linux-x64
# - displayswitch-pro-linux-arm64
# - displayswitch-pro-win-x64.exe
# - displayswitch-pro-macos-universal
```

## Integration Points

### Related Components
- **[Build System](build-system.md)**: F# build pipeline for single binary
- **[Configuration Management](config-management.md)**: Immutable config with event sourcing
- **[System Tray](system-tray.md)**: Native tray integration per platform
- **[Troubleshooting](troubleshooting.md)**: Functional debugging with event replay
- **[Advanced Features](advanced-features.md)**: Plugin system via functional composition

### Post-Installation Flow (ECS Architecture)
1. **Installation** → Single Binary → Platform Detection → Service Registration
2. **First Launch** → Initialize ECS World → Load Event History → Restore State
3. **User Onboarding** → Display Profiles → Hotkey Setup → Event Recording
4. **System Integration** → Display Server Connection → Event Stream → State Updates

### Functional Benefits
- **Reproducible State**: Event history allows exact state recreation
- **Time-Travel Debugging**: Replay events to debug issues
- **Zero Configuration**: Smart defaults with override capability
- **Hot Reload**: Configuration changes without restart
- **Crash Recovery**: Automatic state restoration from events