using System.IO;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using AlwaysRun.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlwaysRun.Tests;

public class ValidationServiceTests
{
    private readonly ValidationService _sut;
    private readonly ILogger<ValidationService> _logger;

    public ValidationServiceTests()
    {
        _logger = Substitute.For<ILogger<ValidationService>>();
        _sut = new ValidationService(_logger);
    }

    [Theory]
    [InlineData(".exe", AppType.Exe)]
    [InlineData(".EXE", AppType.Exe)]
    [InlineData(".ps1", AppType.PowerShell)]
    [InlineData(".PS1", AppType.PowerShell)]
    [InlineData(".bat", AppType.Batch)]
    [InlineData(".BAT", AppType.Batch)]
    [InlineData(".cmd", AppType.Batch)]
    [InlineData(".CMD", AppType.Batch)]
    public void ResolveAppType_ShouldReturnCorrectType(string extension, AppType expectedType)
    {
        // Arrange
        var filePath = $@"C:\test\app{extension}";

        // Act
        var result = _sut.ResolveAppType(filePath);

        // Assert
        result.Should().Be(expectedType);
    }

    [Fact]
    public void ValidateManagedApp_WhenFileExists_ShouldReturnSuccess()
    {
        // Arrange - use a known existing file
        var filePath = @"C:\Windows\System32\cmd.exe";

        // Act
        var result = _sut.ValidateManagedApp(filePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateManagedApp_WhenFileNotFound_ShouldReturnFailure()
    {
        // Arrange
        var filePath = @"C:\NonExistent\fake.exe";

        // Act
        var result = _sut.ValidateManagedApp(filePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ValidateManagedApp_WhenPathEmpty_ShouldReturnFailure()
    {
        // Act
        var result = _sut.ValidateManagedApp(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void ValidateManagedApp_WhenUnsupportedExtension_ShouldReturnFailure()
    {
        // Arrange - create a temp file with unsupported extension
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xyz");
        try
        {
            File.WriteAllText(tempFile, "test");

            // Act
            var result = _sut.ValidateManagedApp(tempFile);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Unsupported");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ValidateDisplayName_WhenValid_ShouldReturnSuccess()
    {
        // Act
        var result = _sut.ValidateDisplayName("My Application");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateDisplayName_WhenEmpty_ShouldReturnFailure()
    {
        // Act
        var result = _sut.ValidateDisplayName(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void ValidateDisplayName_WhenNull_ShouldReturnFailure()
    {
        // Act
        var result = _sut.ValidateDisplayName(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateDisplayName_WhenTooLong_ShouldReturnFailure()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act
        var result = _sut.ValidateDisplayName(longName);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("100");
    }
}
