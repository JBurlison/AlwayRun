using System.IO;
using System.Text.Json;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using AlwaysRun.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlwaysRun.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testAppDataDir;
    private readonly ConfigService _sut;
    private readonly ILogger<ConfigService> _logger;

    public ConfigServiceTests()
    {
        // Create a temporary test directory
        _testAppDataDir = Path.Combine(Path.GetTempPath(), "AlwaysRunTests_" + Guid.NewGuid().ToString("N"));
        _testConfigPath = Path.Combine(_testAppDataDir, "config.json");
        Directory.CreateDirectory(_testAppDataDir);

        _logger = Substitute.For<ILogger<ConfigService>>();
        _sut = new ConfigService(_logger);
    }

    [Fact]
    public async Task LoadAsync_WhenFileMissing_ShouldReturnDefaultConfig()
    {
        // Arrange - ensure file doesn't exist
        if (File.Exists(AppPaths.ConfigFilePath))
        {
            File.Delete(AppPaths.ConfigFilePath);
        }

        // Act
        var config = await _sut.LoadAsync();

        // Assert
        config.Should().NotBeNull();
        config.SchemaVersion.Should().Be(AppConfiguration.CurrentSchemaVersion);
        config.Apps.Should().BeEmpty();
        config.AutoStartEnabled.Should().BeTrue();
        config.ExitOnClose.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateFileWithCorrectContent()
    {
        // Arrange
        var config = new AppConfiguration
        {
            SchemaVersion = 1,
            Apps =
            [
                new ManagedAppConfig
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Test App",
                    FilePath = @"C:\test\app.exe",
                    AppType = AppType.Exe,
                    WorkingDirectory = @"C:\test",
                    IsPaused = false,
                    UsePowerShellBypass = false
                }
            ],
            AutoStartEnabled = true,
            ExitOnClose = true
        };

        // Act
        await _sut.SaveAsync(config);

        // Assert
        File.Exists(AppPaths.ConfigFilePath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(AppPaths.ConfigFilePath);
        content.Should().Contain("Test App");
        content.Should().Contain("app.exe");
    }

    [Fact]
    public async Task SaveAndLoad_ShouldRoundTrip()
    {
        // Arrange
        var originalConfig = new AppConfiguration
        {
            SchemaVersion = 1,
            Apps =
            [
                new ManagedAppConfig
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Roundtrip Test",
                    FilePath = @"C:\path\to\script.ps1",
                    AppType = AppType.PowerShell,
                    Arguments = "-param value",
                    WorkingDirectory = @"C:\path\to",
                    IsPaused = true,
                    UsePowerShellBypass = true,
                    LastStartTime = DateTimeOffset.UtcNow.AddHours(-1),
                    LastExitTime = DateTimeOffset.UtcNow.AddMinutes(-30),
                    LastExitCode = 0
                }
            ],
            AutoStartEnabled = false,
            ExitOnClose = true
        };

        // Act
        await _sut.SaveAsync(originalConfig);
        var loadedConfig = await _sut.LoadAsync();

        // Assert
        loadedConfig.SchemaVersion.Should().Be(originalConfig.SchemaVersion);
        loadedConfig.AutoStartEnabled.Should().Be(originalConfig.AutoStartEnabled);
        loadedConfig.ExitOnClose.Should().Be(originalConfig.ExitOnClose);
        loadedConfig.Apps.Should().HaveCount(1);

        var loadedApp = loadedConfig.Apps[0];
        var originalApp = originalConfig.Apps[0];
        loadedApp.Id.Should().Be(originalApp.Id);
        loadedApp.DisplayName.Should().Be(originalApp.DisplayName);
        loadedApp.FilePath.Should().Be(originalApp.FilePath);
        loadedApp.AppType.Should().Be(originalApp.AppType);
        loadedApp.Arguments.Should().Be(originalApp.Arguments);
        loadedApp.WorkingDirectory.Should().Be(originalApp.WorkingDirectory);
        loadedApp.IsPaused.Should().Be(originalApp.IsPaused);
        loadedApp.UsePowerShellBypass.Should().Be(originalApp.UsePowerShellBypass);
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteAtomicReplace()
    {
        // Arrange - first save
        var config1 = new AppConfiguration
        {
            SchemaVersion = 1,
            Apps = [],
            AutoStartEnabled = true,
            ExitOnClose = false
        };
        await _sut.SaveAsync(config1);

        // Capture original modified time
        var originalTime = File.GetLastWriteTimeUtc(AppPaths.ConfigFilePath);
        await Task.Delay(100); // Ensure time difference

        // Act - second save
        var config2 = config1 with { ExitOnClose = true };
        await _sut.SaveAsync(config2);

        // Assert
        File.GetLastWriteTimeUtc(AppPaths.ConfigFilePath).Should().BeAfter(originalTime);

        // Verify no temp file left behind
        File.Exists(AppPaths.ConfigFilePath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsInvalidJson_ShouldReturnDefaultConfig()
    {
        // Arrange - write invalid JSON
        AppPaths.EnsureDirectoriesExist();
        await File.WriteAllTextAsync(AppPaths.ConfigFilePath, "{ invalid json }");

        // Act
        var config = await _sut.LoadAsync();

        // Assert
        config.Should().NotBeNull();
        config.Apps.Should().BeEmpty();
    }

    public void Dispose()
    {
        // Clean up test directory
        try
        {
            if (Directory.Exists(_testAppDataDir))
            {
                Directory.Delete(_testAppDataDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
