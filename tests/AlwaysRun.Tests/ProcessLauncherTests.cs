using System.Diagnostics;
using System.IO;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using AlwaysRun.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlwaysRun.Tests;

public class ProcessLauncherTests
{
    private readonly ProcessLauncher _sut;
    private readonly ILogger<ProcessLauncher> _logger;

    public ProcessLauncherTests()
    {
        _logger = Substitute.For<ILogger<ProcessLauncher>>();
        _sut = new ProcessLauncher(_logger);
    }

    [Fact]
    public void Start_WhenFileNotFound_ShouldReturnFailure()
    {
        // Arrange
        var app = new ManagedAppConfig
        {
            Id = Guid.NewGuid(),
            DisplayName = "Missing App",
            FilePath = @"C:\NonExistent\app.exe",
            AppType = AppType.Exe,
            WorkingDirectory = @"C:\NonExistent"
        };

        // Act
        var result = _sut.Start(app);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void Start_WhenExe_ShouldUseFileDirectly()
    {
        // Arrange - use cmd.exe which exists on all Windows systems
        var app = new ManagedAppConfig
        {
            Id = Guid.NewGuid(),
            DisplayName = "CMD Test",
            FilePath = @"C:\Windows\System32\cmd.exe",
            AppType = AppType.Exe,
            Arguments = "/c echo test",
            WorkingDirectory = @"C:\Windows\System32"
        };

        // Act
        var result = _sut.Start(app);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Clean up the started process
        try
        {
            result.Value!.Kill();
            result.Value.Dispose();
        }
        catch { }
    }

    [Fact]
    public void Start_WhenPs1WithBypass_ShouldIncludeExecutionPolicy()
    {
        // This test validates the ProcessStartInfo construction
        // We can't easily start a .ps1 without a real file, but we can verify the launch fails
        // with the expected error (file not found, not execution policy)

        // Arrange
        var tempScript = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.ps1");
        try
        {
            // Create a simple test script
            File.WriteAllText(tempScript, "Write-Host 'Test'");

            var app = new ManagedAppConfig
            {
                Id = Guid.NewGuid(),
                DisplayName = "PS Bypass Test",
                FilePath = tempScript,
                AppType = AppType.PowerShell,
                Arguments = "-arg1 value1",
                WorkingDirectory = Path.GetTempPath(),
                UsePowerShellBypass = true
            };

            // Act
            var result = _sut.Start(app);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.StartInfo.FileName.Should().Be("powershell.exe");
            result.Value.StartInfo.Arguments.Should().Contain("-ExecutionPolicy Bypass");
            result.Value.StartInfo.Arguments.Should().Contain("-File");
            result.Value.StartInfo.Arguments.Should().Contain(tempScript);
            result.Value.StartInfo.Arguments.Should().Contain("-arg1 value1");

            // Clean up
            try
            {
                result.Value.Kill();
                result.Value.Dispose();
            }
            catch { }
        }
        finally
        {
            if (File.Exists(tempScript))
            {
                File.Delete(tempScript);
            }
        }
    }

    [Fact]
    public void Start_WhenPs1WithoutBypass_ShouldNotIncludeExecutionPolicy()
    {
        // Arrange
        var tempScript = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.ps1");
        try
        {
            File.WriteAllText(tempScript, "Write-Host 'Test'");

            var app = new ManagedAppConfig
            {
                Id = Guid.NewGuid(),
                DisplayName = "PS No Bypass Test",
                FilePath = tempScript,
                AppType = AppType.PowerShell,
                WorkingDirectory = Path.GetTempPath(),
                UsePowerShellBypass = false
            };

            // Act
            var result = _sut.Start(app);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.StartInfo.Arguments.Should().NotContain("-ExecutionPolicy");

            // Clean up
            try
            {
                result.Value.Kill();
                result.Value.Dispose();
            }
            catch { }
        }
        finally
        {
            if (File.Exists(tempScript))
            {
                File.Delete(tempScript);
            }
        }
    }

    [Fact]
    public void Start_WhenBatch_ShouldUseCmdExe()
    {
        // Arrange
        var tempBatch = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.bat");
        try
        {
            File.WriteAllText(tempBatch, "@echo off\necho Test");

            var app = new ManagedAppConfig
            {
                Id = Guid.NewGuid(),
                DisplayName = "Batch Test",
                FilePath = tempBatch,
                AppType = AppType.Batch,
                Arguments = "arg1 arg2",
                WorkingDirectory = Path.GetTempPath()
            };

            // Act
            var result = _sut.Start(app);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.StartInfo.FileName.Should().Be("cmd.exe");
            result.Value.StartInfo.Arguments.Should().StartWith("/c");
            result.Value.StartInfo.Arguments.Should().Contain("arg1 arg2");

            // Clean up
            try
            {
                result.Value.Kill();
                result.Value.Dispose();
            }
            catch { }
        }
        finally
        {
            if (File.Exists(tempBatch))
            {
                File.Delete(tempBatch);
            }
        }
    }

    [Fact]
    public void Start_WhenWorkingDirectoryNotSpecified_ShouldUseFileDirectory()
    {
        // Arrange
        var tempExe = @"C:\Windows\System32\cmd.exe";

        var app = new ManagedAppConfig
        {
            Id = Guid.NewGuid(),
            DisplayName = "WorkDir Test",
            FilePath = tempExe,
            AppType = AppType.Exe,
            Arguments = "/c exit",
            WorkingDirectory = string.Empty // Empty, should default to file directory
        };

        // Act
        var result = _sut.Start(app);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.StartInfo.WorkingDirectory.Should().Be(@"C:\Windows\System32");

        // Clean up
        try
        {
            result.Value.Kill();
            result.Value.Dispose();
        }
        catch { }
    }
}
