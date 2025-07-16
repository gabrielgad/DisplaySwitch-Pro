# Troubleshooting

## Overview

This comprehensive troubleshooting guide addresses common issues, error conditions, and diagnostic procedures for DisplaySwitch-Pro. The guide is organized by problem category with step-by-step solutions and advanced diagnostic techniques.

## Common Issues

### Application Won't Start

#### Issue: "DisplayManager is already running"
**Symptoms**: Error message when trying to launch the application
**Cause**: Previous instance still running in background

**Solution**:
1. Check system tray for DisplayManager icon
2. Right-click icon → Exit
3. If icon not visible:
   ```cmd
   taskkill /F /IM DisplayManager.exe
   ```
4. Restart application

#### Issue: Application crashes on startup
**Symptoms**: Application starts then immediately closes
**Cause**: Corrupted configuration or missing dependencies

**Solution**:
1. Check Event Viewer for error details:
   - Win+R → `eventvwr.msc`
   - Navigate to Windows Logs → Application
   - Look for DisplayManager errors

2. Reset configuration:
   ```cmd
   rmdir /S /Q "%APPDATA%\DisplayManager"
   ```

3. Reinstall .NET runtime if using framework-dependent build
4. Run as Administrator if permission issues

#### Issue: "Could not load file or assembly" error
**Symptoms**: .NET assembly loading errors
**Cause**: Missing .NET runtime or corrupted installation

**Solution**:
1. Download and install .NET 6.0 Runtime from Microsoft
2. Use self-contained build instead
3. Repair Visual C++ Redistributables

### Display Configuration Issues

#### Issue: Display not switching properly
**Symptoms**: Mode change appears to succeed but displays don't change
**Cause**: Driver issues, hardware conflicts, or Windows display service problems

**Diagnostic Steps**:
1. Check display drivers:
   ```cmd
   dxdiag
   ```
   Look for driver issues in Display tab

2. Verify display connections:
   - Check all cables are securely connected
   - Test with different cables if available
   - Try different ports on graphics card

3. Check Windows display settings:
   - Right-click desktop → Display settings
   - Verify displays are detected
   - Test manual switching in Windows

**Solution**:
1. Update graphics drivers from manufacturer
2. Restart Windows display service:
   ```cmd
   net stop "Windows Display Driver Model"
   net start "Windows Display Driver Model"
   ```
3. Disable and re-enable displays in Device Manager

#### Issue: TV/External display not detected
**Symptoms**: DisplayManager shows only internal display
**Cause**: Hardware connection, driver, or EDID issues

**Solution**:
1. Force display detection:
   ```cmd
   DisplayManager.exe
   ```
   Click Refresh button multiple times

2. Check display connection:
   - Verify cable is HDMI/DisplayPort compatible
   - Try different input on TV
   - Test with different cable

3. Update display drivers and restart system

4. Check TV settings:
   - Ensure TV is set to correct input
   - Enable HDMI-CEC if available
   - Check TV's display/video settings

#### Issue: Resolution not applying correctly
**Symptoms**: Display switches but uses wrong resolution
**Cause**: Driver limitations or display capability issues

**Solution**:
1. Check supported resolutions in Windows Display Settings
2. Update graphics drivers
3. Verify display cable supports desired resolution
4. Check display specifications for maximum resolution

### System Tray Issues

#### Issue: System tray icon not appearing
**Symptoms**: No DisplayManager icon in system tray
**Cause**: Windows tray settings or application startup issues

**Solution**:
1. Check tray icon visibility:
   - Right-click taskbar → Taskbar settings
   - Select "Turn system icons on or off"
   - Ensure notification area is enabled

2. Check hidden icons:
   - Click arrow next to system tray
   - Look for DisplayManager in hidden icons
   - Drag to main tray area

3. Restart application:
   ```cmd
   taskkill /F /IM DisplayManager.exe
   DisplayManager.exe
   ```

#### Issue: Tray menu not responding
**Symptoms**: Right-click menu doesn't appear or doesn't work
**Cause**: Application hang or Windows UI issues

**Solution**:
1. Restart application
2. Check for Windows updates
3. Run Windows System File Checker:
   ```cmd
   sfc /scannow
   ```

### Configuration Management Issues

#### Issue: Configuration files not saving
**Symptoms**: Save operation appears successful but files don't exist
**Cause**: Permission issues or disk space problems

**Solution**:
1. Check available disk space
2. Verify permissions on config directory:
   ```cmd
   icacls "%APPDATA%\DisplayManager"
   ```
3. Run application as Administrator
4. Check antivirus software isn't blocking file operations

#### Issue: Configuration files corrupted
**Symptoms**: Error loading saved configurations
**Cause**: Corrupted JSON files or version incompatibility

**Solution**:
1. Validate JSON format:
   ```cmd
   type "%APPDATA%\DisplayManager\config.json"
   ```
2. Restore from backup if available
3. Delete corrupted file and recreate:
   ```cmd
   del "%APPDATA%\DisplayManager\corrupted_config.json"
   ```

### Keyboard Shortcuts Issues

#### Issue: Shortcuts not working
**Symptoms**: Ctrl+1, Ctrl+2 don't respond
**Cause**: Focus issues or conflicting applications

**Solution**:
1. Ensure DisplayManager window has focus
2. Check for conflicting applications using same shortcuts
3. Restart DisplayManager
4. Try global hotkeys if implemented

#### Issue: Global hotkeys conflict
**Symptoms**: Hotkeys work for other applications instead
**Cause**: Another application registered the same hotkey first

**Solution**:
1. Close conflicting applications
2. Restart DisplayManager to re-register hotkeys
3. Change hotkey combinations in settings
4. Check Windows hotkey assignments

### Performance Issues

#### Issue: Application using high CPU
**Symptoms**: DisplayManager shows high CPU usage in Task Manager
**Cause**: Excessive polling or background processing

**Solution**:
1. Restart application
2. Check for Windows updates
3. Disable unnecessary features:
   - Auto-refresh intervals
   - Background monitoring
4. Update graphics drivers

#### Issue: Slow display switching
**Symptoms**: Long delay between mode change command and actual switch
**Cause**: Hardware delays or driver issues

**Solution**:
1. Update graphics drivers
2. Check display cable quality
3. Reduce number of displays if possible
4. Restart Windows display service

## Advanced Diagnostics

### Debug Mode Activation

#### Enable Debug Logging
Add debug functionality to track issues:

```csharp
public class DebugLogger
{
    private static string logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DisplayManager", "debug.log");
    
    public static void EnableDebugMode()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        
        // Log application start
        WriteDebugLog("DEBUG MODE ENABLED", "Application started");
        
        // Log system information
        WriteDebugLog("SYSTEM INFO", $"OS: {Environment.OSVersion}");
        WriteDebugLog("SYSTEM INFO", $"User: {Environment.UserName}");
        WriteDebugLog("SYSTEM INFO", $"Machine: {Environment.MachineName}");
    }
    
    public static void WriteDebugLog(string category, string message)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {message}";
            File.AppendAllText(logPath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Fallback to event log
            EventLog.WriteEntry("DisplayManager", $"Debug log failed: {ex.Message}", EventLogEntryType.Warning);
        }
    }
}
```

#### Command Line Debug Options
```cmd
# Enable debug mode
DisplayManager.exe --debug

# Verbose logging
DisplayManager.exe --verbose

# Test mode (no actual display changes)
DisplayManager.exe --test-mode
```

### System Information Collection

#### Display Information Script
```powershell
# Collect comprehensive display information
$OutputFile = "$env:TEMP\DisplayManager_DiagInfo.txt"

Write-Output "DisplayManager Diagnostic Information" > $OutputFile
Write-Output "Generated: $(Get-Date)" >> $OutputFile
Write-Output "=================================" >> $OutputFile
Write-Output "" >> $OutputFile

# System Information
Write-Output "SYSTEM INFORMATION:" >> $OutputFile
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, WindowsBuildLabEx >> $OutputFile
Write-Output "" >> $OutputFile

# Display Adapter Information
Write-Output "DISPLAY ADAPTERS:" >> $OutputFile
Get-WmiObject -Class Win32_VideoController | Select-Object Name, DriverVersion, DriverDate >> $OutputFile
Write-Output "" >> $OutputFile

# Monitor Information
Write-Output "MONITORS:" >> $OutputFile
Get-WmiObject -Class Win32_DesktopMonitor | Select-Object Name, MonitorType, ScreenHeight, ScreenWidth >> $OutputFile
Write-Output "" >> $OutputFile

# Display Configuration
Write-Output "DISPLAY CONFIGURATION:" >> $OutputFile
Get-WmiObject -Class Win32_DisplayConfiguration | Select-Object DeviceName, LogPixels, BitsPerPel >> $OutputFile
Write-Output "" >> $OutputFile

# Running Processes
Write-Output "DISPLAY-RELATED PROCESSES:" >> $OutputFile
Get-Process | Where-Object {$_.ProcessName -like "*display*" -or $_.ProcessName -like "*nvidia*" -or $_.ProcessName -like "*amd*" -or $_.ProcessName -like "*intel*"} | Select-Object ProcessName, Id, CPU >> $OutputFile

Write-Output "Diagnostic information saved to: $OutputFile"
```

### Registry Diagnostic

#### Display Registry Check
```cmd
@echo off
echo Checking display-related registry entries...

echo.
echo DISPLAY DRIVERS:
reg query "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" /s | findstr /i "driverdesc"

echo.
echo DISPLAY DEVICES:
reg query "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\DISPLAY" /s | findstr /i "friendlyname"

echo.
echo GRAPHICS CARDS:
reg query "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Video" /s | findstr /i "device"

pause
```

### Event Log Analysis

#### Windows Event Log Queries
```cmd
# Check for display-related errors
wevtutil qe System /c:50 /f:text /q:"*[System[(EventID=4101 or EventID=4102 or EventID=4103)]]"

# Check for application errors
wevtutil qe Application /c:50 /f:text /q:"*[System[Provider[@Name='DisplayManager']]]"

# Check for hardware errors
wevtutil qe System /c:50 /f:text /q:"*[System[(EventID=219 or EventID=220)]]"
```

### Network Connectivity (if using web features)

#### Network Diagnostic
```cmd
# Test local web server
curl -v http://localhost:8080/api/status

# Check firewall settings
netsh advfirewall firewall show rule name="DisplayManager"

# Test port availability
netstat -an | findstr :8080
```

## Error Code Reference

### Application Error Codes
| Code | Description | Solution |
|------|-------------|----------|
| 1001 | Display configuration API failure | Update graphics drivers |
| 1002 | Configuration file corrupted | Delete and recreate config |
| 1003 | Insufficient permissions | Run as Administrator |
| 1004 | Display hardware not found | Check connections |
| 1005 | Registry access denied | Check UAC settings |

### Windows API Error Codes
| Code | Description | Solution |
|------|-------------|----------|
| 0 | ERROR_SUCCESS | Operation completed successfully |
| 5 | ERROR_ACCESS_DENIED | Run with elevated privileges |
| 87 | ERROR_INVALID_PARAMETER | Check display configuration |
| 1223 | ERROR_CANCELLED | User cancelled operation |

### Common Exception Types
```csharp
// Handle specific exception types
try
{
    DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
}
catch (UnauthorizedAccessException)
{
    ShowError("Permission denied. Try running as Administrator.");
}
catch (ArgumentException ex)
{
    ShowError($"Invalid display configuration: {ex.Message}");
}
catch (Win32Exception ex)
{
    ShowError($"Windows API error: {ex.Message} (Code: {ex.NativeErrorCode})");
}
catch (Exception ex)
{
    ShowError($"Unexpected error: {ex.Message}");
    LogError(ex.ToString());
}
```

## Recovery Procedures

### Complete Application Reset
```cmd
@echo off
echo Resetting DisplayManager to default state...

REM Stop application
taskkill /F /IM DisplayManager.exe 2>nul

REM Remove configuration
if exist "%APPDATA%\DisplayManager" (
    rmdir /S /Q "%APPDATA%\DisplayManager"
)

REM Remove registry entries
reg delete "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "DisplayManager" /f 2>nul

REM Clear temporary files
del /Q "%TEMP%\DisplayManager*" 2>nul

echo Reset complete. Please restart DisplayManager.
pause
```

### System Display Reset
```cmd
# Reset Windows display configuration
DisplayManager.exe --reset-displays

# Or manually via Windows
ms-settings:display
```

### Emergency Display Recovery
```cmd
# Force enable all displays
DisplayManager.exe --emergency-restore

# Reset to Windows default
DisplayManager.exe --windows-default
```

## Contact and Support

### Information to Collect
When reporting issues, please provide:

1. **System Information**:
   - Windows version
   - Graphics card model and driver version
   - Display hardware details

2. **Application Information**:
   - DisplayManager version
   - Error messages
   - Steps to reproduce

3. **Log Files**:
   - `%APPDATA%\DisplayManager\debug.log`
   - Windows Event Viewer errors
   - Diagnostic script output

### Support Channels
- **GitHub Issues**: Report bugs and feature requests
- **Documentation**: Check README and feature documentation
- **Community**: User forums and discussions

### Self-Help Resources
- **Built-in Help**: Press F1 in application
- **Tool Tips**: Hover over buttons for help text
- **Status Messages**: Check application status bar
- **System Tray**: Right-click for quick actions

## Prevention and Maintenance

### Regular Maintenance
1. **Update Graphics Drivers**: Monthly driver updates
2. **Clean Configuration**: Remove old/unused configurations
3. **Check Connections**: Verify display cable integrity
4. **Monitor Performance**: Check CPU/memory usage
5. **Backup Configurations**: Regular config backups

### Best Practices
- **Gradual Changes**: Test configuration changes gradually
- **Backup Before Changes**: Always backup working configurations
- **Document Setup**: Keep notes on working configurations
- **Monitor Updates**: Stay informed about application updates
- **Regular Restart**: Restart application periodically

## Integration Points

### Related Components
- **[Installation](installation.md)**: Installation-related troubleshooting
- **[Build System](build-system.md)**: Build and deployment issues
- **[Configuration Management](config-management.md)**: Configuration file problems
- **[Display API](display-api.md)**: Low-level API errors
- **[System Tray](system-tray.md)**: Tray-related issues

### Troubleshooting Flow
1. **Issue Identification** → Problem Category → Diagnostic Steps
2. **Solution Application** → Verification → Documentation
3. **Prevention Planning** → Monitoring Setup → Maintenance Schedule