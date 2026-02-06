# AlwaysRun

A Windows desktop application that monitors and automatically restarts your applications, scripts, and batch files.

## Features

- **Auto-start on Windows login** - Launches automatically when you log in via Registry Run key
- **Process monitoring** - Monitors managed processes and automatically restarts them if they exit
- **Multiple file types** - Supports executables (`.exe`), PowerShell scripts (`.ps1`), and batch files (`.bat`, `.cmd`)
- **Configurable restart delay** - Set custom initial delay per application before restart (with exponential backoff on repeated failures)
- **Pause/Resume** - Pause monitoring on individual applications (e.g., for updates) and resume when ready
- **System tray** - Minimizes to system tray; double-click to restore
- **Per-app settings** - Arguments, working directory, PowerShell execution policy bypass

## Screenshots

The main window displays all managed applications with their status, last start/exit times, and exit codes.

## Requirements

- Windows 10/11
- .NET 10.0 Runtime (or self-contained build)

## Installation

### Option 1: Framework-dependent (smaller size, requires .NET runtime)
```powershell
dotnet publish src/AlwaysRun/AlwaysRun.csproj -c Release -r win-x64 --self-contained false -o publish
```

### Option 2: Self-contained (larger size, no runtime required)
```powershell
dotnet publish src/AlwaysRun/AlwaysRun.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```

## Usage

### Adding an Application

1. Click **Add** in the toolbar
2. Browse to select an executable, PowerShell script, or batch file
3. Set a display name and optional arguments
4. Configure the restart delay (default: 2 seconds)
5. For PowerShell scripts, optionally enable "Bypass execution policy"
6. Click **Save**

### Managing Applications

| Action | Description |
|--------|-------------|
| **Start** | Manually start a stopped application |
| **Stop** | Stop a running application |
| **Pause** | Pause monitoring (won't auto-restart) |
| **Resume** | Resume monitoring and start the application |
| **Edit** | Modify application settings |
| **Remove** | Remove from monitoring |

### Restart Behavior

When a monitored application exits:
1. Waits for the configured restart delay (per-app setting)
2. Attempts to restart
3. If it crashes again quickly, applies exponential backoff (2x delay each time, max 5 minutes)
4. If the app runs healthy for 60+ seconds, the backoff resets

### Settings

- **Start with Windows** - Toggle auto-start on login
- **Exit on close** - When unchecked, closing the window minimizes to system tray

## Configuration

Configuration is stored at:
```
%APPDATA%\AlwaysRun\config.json
```

Logs are stored at:
```
%APPDATA%\AlwaysRun\logs\
```

## Building from Source

```powershell
# Clone the repository
git clone https://github.com/JBurlison/AlwayRun.git
cd AlwayRun

# Build
dotnet build AlwaysRun.sln

# Run tests
dotnet test AlwaysRun.sln

# Run the application
dotnet run --project src/AlwaysRun
```

## Project Structure

```
AlwaysRun/
├── src/AlwaysRun/           # Main WPF application
│   ├── Models/              # Data models and DTOs
│   ├── Services/            # Business logic services
│   ├── ViewModels/          # MVVM view models
│   ├── Views/               # WPF XAML views
│   └── Infrastructure/      # Helpers and utilities
├── tests/AlwaysRun.Tests/   # Unit tests
└── .doc/                    # Specifications and documentation
```

## Technology Stack

- .NET 10.0
- WPF (Windows Presentation Foundation)
- CommunityToolkit.Mvvm (MVVM framework)
- Serilog (structured logging)
- xUnit + FluentAssertions (testing)

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
