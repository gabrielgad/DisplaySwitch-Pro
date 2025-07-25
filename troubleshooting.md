# Troubleshooting

## Overview

This comprehensive troubleshooting guide addresses common issues, error conditions, and diagnostic procedures for DisplaySwitch-Pro built with F# and Entity Component System (ECS) architecture. The functional programming approach enables superior debugging capabilities including event replay, time-travel debugging, and immutable state inspection.

## Common Issues

### Application Won't Start

#### Issue: "DisplaySwitch-Pro is already running"
**Symptoms**: Error message when trying to launch the application
**Cause**: Previous instance still running in background

**Linux Solution**:
```bash
# Check if running
ps aux | grep displayswitch-pro

# Stop gracefully
displayswitch-pro --stop

# Force stop if needed
pkill -f displayswitch-pro

# Check systemd service
systemctl --user status displayswitch-pro.service
systemctl --user stop displayswitch-pro.service
```

**Windows Solution**:
```powershell
# Check if running
Get-Process displayswitch-pro -ErrorAction SilentlyContinue

# Stop gracefully
displayswitch-pro.exe --stop

# Force stop
Stop-Process -Name "displayswitch-pro" -Force
```

#### Issue: Application crashes on startup
**Symptoms**: Application starts then immediately closes
**Cause**: Corrupted event store or invalid display configuration

**F# ECS Debugging Solution**:
1. **Check event log integrity**:
   ```bash
   displayswitch-pro --validate-events
   ```

2. **Replay events with debug output**:
   ```bash
   displayswitch-pro --replay-events --debug
   ```

3. **Reset to last known good state**:
   ```bash
   displayswitch-pro --rollback-to-checkpoint
   ```

4. **Full event store reset** (last resort):
   ```bash
   # Backup current events
   cp -r ~/.config/displayswitch-pro/events ~/.config/displayswitch-pro/events.backup
   
   # Reset event store
   displayswitch-pro --reset-events
   ```

#### Issue: "Binary execution failed" or "Permission denied"
**Symptoms**: Single binary won't execute
**Cause**: Missing execute permissions or unsupported architecture

**Linux Solution**:
```bash
# Check file permissions
ls -la ~/.local/bin/displayswitch-pro

# Add execute permission
chmod +x ~/.local/bin/displayswitch-pro

# Check architecture compatibility
file ~/.local/bin/displayswitch-pro
uname -m

# Check dependencies (should be none!)
ldd ~/.local/bin/displayswitch-pro
```

**macOS Solution**:
```bash
# Remove quarantine attribute
xattr -d com.apple.quarantine displayswitch-pro

# Allow in System Preferences → Security & Privacy
```

### Display Configuration Issues

#### Issue: Display not switching properly
**Symptoms**: Mode change appears to succeed but displays don't change
**Cause**: Display server issues, driver problems, or hardware conflicts

**ECS Diagnostic Steps**:
1. **Check component state**:
   ```bash
   # View current ECS state
   displayswitch-pro --dump-components
   
   # Show display entities
   displayswitch-pro --list-displays --verbose
   ```

2. **Verify display server connection**:
   ```bash
   # X11
   echo $DISPLAY
   xrandr --query
   
   # Wayland
   echo $WAYLAND_DISPLAY
   wlr-randr
   
   # Test direct API access
   displayswitch-pro --test-display-api
   ```

3. **Event replay for debugging**:
   ```bash
   # Show recent display events
   displayswitch-pro --show-events --filter=display --last=10
   
   # Replay specific event sequence
   displayswitch-pro --replay-events --from="2025-01-25 10:00" --to="2025-01-25 10:05"
   ```

**F# ECS Solution**:
1. **Component state inspection**:
   ```bash
   # Check display component integrity
   displayswitch-pro --validate-display-components
   
   # Reset display component state
   displayswitch-pro --reset-component display
   
   # Force component reinitialization
   displayswitch-pro --reinit-components
   ```

2. **Update graphics drivers and restart display server**:
   ```bash
   # Linux - restart display manager
   sudo systemctl restart gdm  # or sddm, lightdm
   
   # Or just restart display server
   sudo systemctl restart display-manager
   ```

3. **Event-driven recovery**:
   ```bash
   # Create recovery checkpoint
   displayswitch-pro --create-checkpoint "before-display-fix"
   
   # Apply fix with rollback capability
   displayswitch-pro --apply-fix display-not-switching --allow-rollback
   ```

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

### F# ECS Debug Mode

#### Functional Debugging with Event Sourcing
The ECS architecture enables powerful debugging through immutable event history:

```fsharp
// Event-based debug logging
type DebugEvent = {
    Timestamp: DateTime
    EntityId: EntityId option
    Component: ComponentType option
    System: SystemType option
    Event: string
    Data: Map<string, obj>
    StackTrace: string option
}

// Pure debug state
type DebugState = {
    Events: DebugEvent list
    Filter: DebugEvent -> bool
    Level: LogLevel
    OutputChannels: OutputChannel list
}

// Debug event processing
let processDebugEvent (state: DebugState) (event: DebugEvent) : DebugState =
    if state.Filter event then
        let newEvents = event :: state.Events
        { state with Events = newEvents |> List.take 1000 } // Keep last 1000
    else
        state
```

#### Time-Travel Debugging
```bash
# Enable comprehensive debug mode
export DISPLAYSWITCH_PRO_DEBUG=full
displayswitch-pro --debug-mode=comprehensive

# Time-travel debugging - replay system state
displayswitch-pro --time-travel --to="2025-01-25 14:30:00" --show-state

# Step-by-step event replay
displayswitch-pro --step-debug --from-event=12345

# Component state at specific time
displayswitch-pro --component-state --entity=display-1 --time="2025-01-25 14:30:00"
```

#### Functional Debug Commands
```bash
# Event sourcing debug
displayswitch-pro --debug-events --filter="display|input|system"

# Component inspection
displayswitch-pro --inspect-components --show-relationships

# System performance profiling
displayswitch-pro --profile-systems --duration=60s

# Pure function testing (no side effects)
displayswitch-pro --test-mode --dry-run

# State validation
displayswitch-pro --validate-state --check-invariants

# Memory usage (should be low due to functional approach)
displayswitch-pro --memory-profile --show-allocations
```

#### Immutable State Debugging
```bash
# Show complete system state as immutable snapshot
displayswitch-pro --snapshot-state --format=json > system-state.json

# Compare two states for differences
displayswitch-pro --diff-states state1.json state2.json

# Validate state consistency
displayswitch-pro --validate-state-consistency

# Show event causality chain
displayswitch-pro --show-causality --event-id=12345
```

### System Information Collection

#### F# ECS Diagnostic Script
```bash
#!/bin/bash
# Comprehensive diagnostic information collection

OUTPUT_FILE="/tmp/displayswitch-pro-diagnostics-$(date +%Y%m%d-%H%M%S).toml"

echo "# DisplaySwitch-Pro Diagnostic Information" > "$OUTPUT_FILE"
echo "# Generated: $(date)" >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

# F# ECS Architecture Information
echo "[architecture]" >> "$OUTPUT_FILE"
displayswitch-pro --version --detailed >> "$OUTPUT_FILE"
echo "ecs_world_entities = $(displayswitch-pro --count-entities)" >> "$OUTPUT_FILE"
echo "ecs_active_systems = $(displayswitch-pro --list-systems --count)" >> "$OUTPUT_FILE"
echo "event_store_size = $(displayswitch-pro --event-store-stats | grep size)" >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

# System Information
echo "[system]" >> "$OUTPUT_FILE"
echo "os = '$(uname -a)'" >> "$OUTPUT_FILE"
echo "display_server = '$(echo $XDG_SESSION_TYPE)'" >> "$OUTPUT_FILE"
echo "wayland_display = '$(echo $WAYLAND_DISPLAY)'" >> "$OUTPUT_FILE"
echo "x11_display = '$(echo $DISPLAY)'" >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

# Display Configuration (immutable snapshot)
echo "[displays]" >> "$OUTPUT_FILE"
displayswitch-pro --export-display-config --format=toml >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

# Event Store Statistics
echo "[event_store]" >> "$OUTPUT_FILE"
displayswitch-pro --event-store-stats --format=toml >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

# Component Health Check
echo "[components]" >> "$OUTPUT_FILE"
displayswitch-pro --component-health-check --format=toml >> "$OUTPUT_FILE"

echo "Diagnostic information saved to: $OUTPUT_FILE"
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

### F# ECS Error Types

#### Functional Error Handling
```fsharp
type DisplayError =
    | DisplayNotFound of displayId: string
    | InvalidConfiguration of config: DisplayConfig * reason: string
    | DisplayServerError of serverType: string * error: string
    | ComponentNotFound of entityId: EntityId * componentType: ComponentType
    | EventProcessingError of eventId: EventId * error: string
    | StateValidationError of invariant: string * actualState: obj

type SystemError =
    | SystemInitializationFailed of systemName: string * error: string
    | SystemUpdateFailed of systemName: string * deltaTime: float * error: string
    | DependencyResolutionFailed of dependency: string list

type EventError =
    | EventStorageError of event: Event * error: string
    | EventReplayError of eventRange: EventId * EventId * error: string
    | EventValidationError of event: Event * rule: ValidationRule
```

### Error Recovery Strategies
| Error Type | Recovery Strategy | F# ECS Benefit |
|------------|------------------|----------------|
| DisplayNotFound | Auto-refresh display list | Component hot-swapping |
| InvalidConfiguration | Rollback to last valid state | Immutable state history |
| EventProcessingError | Replay from checkpoint | Event sourcing |
| SystemUpdateFailed | Restart system without full restart | System isolation |
| StateValidationError | Validate and fix state | Pure function validation |

### Functional Error Handling
```fsharp
// Railway-oriented programming for error handling
type Result<'T, 'TError> = Ok of 'T | Error of 'TError

// Pure error handling without exceptions
let setDisplayMode (mode: DisplayMode) : Result<DisplayState, DisplayError> =
    result {
        let! currentState = getCurrentDisplayState()
        let! newConfig = validateDisplayMode mode currentState
        let! updatedState = applyDisplayConfiguration newConfig
        do! persistDisplayState updatedState
        return updatedState
    }

// Error recovery with event sourcing
let recoverFromError (error: DisplayError) : Result<unit, SystemError> =
    match error with
    | DisplayNotFound displayId ->
        refreshDisplayList() |> Result.map ignore
    | InvalidConfiguration (config, reason) ->
        rollbackToLastValidState() |> Result.map ignore
    | EventProcessingError (eventId, error) ->
        replayFromCheckpoint eventId |> Result.map ignore
    | _ ->
        Error (SystemError.UnrecoverableError (sprintf "Cannot recover from: %A" error))

// Debug-friendly error messages
let formatError (error: DisplayError) : string =
    match error with
    | DisplayNotFound id -> 
        sprintf "Display '%s' not found. Available displays: %A" id (getAvailableDisplays())
    | InvalidConfiguration (config, reason) ->
        sprintf "Configuration invalid: %s\nConfig: %A\nSuggested fix: %s" reason config (suggestFix config)
    | DisplayServerError (serverType, error) ->
        sprintf "Display server (%s) error: %s\nRecovery: %s" serverType error (getRecoverySteps serverType)
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

### Information to Collect for F# ECS Issues
When reporting issues, please provide:

1. **System Information**:
   ```bash
   displayswitch-pro --system-info
   ```
   - Linux distribution and version
   - Display server (X11/Wayland)
   - Graphics card and driver version
   - Desktop environment

2. **F# ECS Architecture Information**:
   ```bash
   displayswitch-pro --architecture-info
   ```
   - ECS world state snapshot
   - Active system list
   - Component relationships
   - Event store statistics

3. **Event History**:
   ```bash
   # Export recent events (anonymized)
   displayswitch-pro --export-events --last=100 --anonymize
   ```
   - Event sequence leading to issue
   - Component state changes
   - System transitions

4. **State Validation**:
   ```bash
   displayswitch-pro --validate-state --export-violations
   ```
   - State consistency check results
   - Invariant violations
   - Recovery suggestions

### Support Channels
- **GitHub Issues**: Report bugs with event history
- **Documentation**: F# ECS architecture guides
- **Community**: Functional programming discussions
- **Matrix Chat**: Real-time support for complex issues

### Self-Help Resources with F# ECS
- **Interactive Help**: `displayswitch-pro --help-interactive`
- **State Inspector**: `displayswitch-pro --inspect-state`
- **Event Browser**: `displayswitch-pro --browse-events --interactive`
- **Component Explorer**: `displayswitch-pro --explore-components`
- **Time-Travel Debug**: Replay any issue state
- **Pure Function Tests**: Validate logic without side effects

## Prevention and Maintenance

### F# ECS Maintenance Benefits
1. **Automatic State Validation**: Continuous invariant checking
2. **Event Store Cleanup**: Automated old event archival
3. **Component Health Monitoring**: Automatic component validation
4. **Memory Management**: Functional approach minimizes memory leaks
5. **Configuration Immutability**: No configuration corruption possible

### Functional Best Practices
- **Event-Driven Changes**: All changes go through event system
- **Immutable Configurations**: State can't be corrupted
- **Time-Travel Testing**: Test changes by replaying history
- **Pure Function Validation**: Test logic without side effects
- **Automatic Recovery**: System self-heals from event history
- **Zero-Downtime Updates**: Hot-reload configurations without restart

### Proactive Monitoring
```bash
# Set up automated health checks
displayswitch-pro --health-check --schedule=hourly

# Monitor event store growth
displayswitch-pro --monitor-events --alert-size=1GB

# Validate state consistency
displayswitch-pro --validate-state --schedule=daily

# Component performance monitoring
displayswitch-pro --monitor-components --cpu-threshold=5%
```

## Integration Points

### Related Components
- **[Installation](installation.md)**: Single binary deployment issues
- **[Build System](build-system.md)**: F# compilation and packaging
- **[Configuration Management](config-management.md)**: Immutable config and event sourcing
- **[Display API](display-api.md)**: Cross-platform display server APIs
- **[Advanced Features](advanced-features.md)**: Plugin system and functional composition

### F# ECS Troubleshooting Flow
1. **Issue Detection** → Event Analysis → Component Inspection → System Validation
2. **Root Cause Analysis** → Event Replay → State Reconstruction → Timeline Analysis
3. **Solution Application** → State Rollback → Component Restart → Event Correction
4. **Prevention Implementation** → Invariant Addition → Event Validation → Monitoring Setup

### Functional Debugging Advantages
- **Deterministic Reproduction**: Exact issue recreation from events
- **Time-Travel Debugging**: Debug at any point in application history
- **Pure Function Testing**: Isolated logic testing without side effects
- **Immutable State Inspection**: No race conditions in debugging
- **Event Sourcing**: Complete audit trail of all changes