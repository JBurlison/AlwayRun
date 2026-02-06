using System.IO;
using AlwaysRun.Models;
using AlwaysRun.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace AlwaysRun.ViewModels;

/// <summary>
/// View model for the Add/Edit application dialog.
/// </summary>
public sealed partial class AddEditAppViewModel : ViewModelBase
{
    private readonly IValidationService _validationService;
    private readonly ILogger<AddEditAppViewModel> _logger;

    private Guid _editingId = Guid.Empty;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _windowTitle = "Add Application";

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private bool _usePowerShellBypass;

    [ObservableProperty]
    private int _restartDelaySeconds = 2;

    [ObservableProperty]
    private bool _showPowerShellBypass;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool _hasValidationError;

    /// <summary>
    /// Gets or sets the dialog result.
    /// </summary>
    public bool? DialogResult { get; set; }

    /// <summary>
    /// Gets the result configuration after successful save.
    /// </summary>
    public ManagedAppConfig? ResultConfig { get; private set; }

    /// <summary>
    /// Event raised when the dialog should close.
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    public AddEditAppViewModel(
        IValidationService validationService,
        ILogger<AddEditAppViewModel> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    partial void OnIsEditModeChanged(bool value)
    {
        WindowTitle = value ? "Edit Application" : "Add Application";
    }

    partial void OnFilePathChanged(string value)
    {
        UpdateAppType();
        UpdateWorkingDirectoryFromFilePath();
        ValidateForm();
    }

    partial void OnDisplayNameChanged(string value)
    {
        ValidateForm();
    }

    /// <summary>
    /// Loads configuration for editing.
    /// </summary>
    public void LoadFromConfig(ManagedAppConfig config)
    {
        _editingId = config.Id;
        DisplayName = config.DisplayName;
        FilePath = config.FilePath;
        Arguments = config.Arguments;
        WorkingDirectory = config.WorkingDirectory;
        UsePowerShellBypass = config.UsePowerShellBypass;
        RestartDelaySeconds = config.RestartDelaySeconds;
        UpdateAppType();
    }

    [RelayCommand]
    private void Browse()
    {
        _logger.LogDebug("Browse command invoked");

        var dialog = new Win32OpenFileDialog
        {
            Title = "Select Application or Script",
            Filter = "Supported Files|*.exe;*.ps1;*.bat;*.cmd|" +
                     "Executables (*.exe)|*.exe|" +
                     "PowerShell Scripts (*.ps1)|*.ps1|" +
                     "Batch Files (*.bat;*.cmd)|*.bat;*.cmd|" +
                     "All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (!string.IsNullOrEmpty(FilePath))
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;

            // Auto-fill display name if empty
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        _logger.LogDebug("Browse working directory command invoked");

        var dialog = new Win32OpenFolderDialog
        {
            Title = "Select Working Directory"
        };

        if (!string.IsNullOrEmpty(WorkingDirectory) && Directory.Exists(WorkingDirectory))
        {
            dialog.InitialDirectory = WorkingDirectory;
        }
        else if (!string.IsNullOrEmpty(FilePath))
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog() == true)
        {
            WorkingDirectory = dialog.FolderName;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _logger.LogDebug("Save command invoked");

        if (!ValidateForm())
        {
            return;
        }

        var appType = _validationService.ResolveAppType(FilePath);

        ResultConfig = new ManagedAppConfig
        {
            Id = IsEditMode ? _editingId : Guid.NewGuid(),
            DisplayName = DisplayName.Trim(),
            FilePath = FilePath.Trim(),
            Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
            WorkingDirectory = WorkingDirectory.Trim(),
            AppType = appType,
            IsPaused = false,
            UsePowerShellBypass = appType == AppType.PowerShell && UsePowerShellBypass,
            RestartDelaySeconds = Math.Max(1, RestartDelaySeconds),
            LastStartTime = null,
            LastExitTime = null,
            LastExitCode = null
        };

        _logger.LogInformation("Saved app config: {DisplayName}, Type={AppType}, FilePath={FilePath}",
            ResultConfig.DisplayName, ResultConfig.AppType, ResultConfig.FilePath);

        DialogResult = true;
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _logger.LogDebug("Cancel command invoked");
        DialogResult = false;
        CloseRequested?.Invoke(this, false);
    }

    private bool CanSave() => !HasValidationError && !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(FilePath);

    private bool ValidateForm()
    {
        // Validate display name
        var nameResult = _validationService.ValidateDisplayName(DisplayName);
        if (!nameResult.IsSuccess)
        {
            ValidationError = nameResult.Error;
            HasValidationError = true;
            SaveCommand.NotifyCanExecuteChanged();
            return false;
        }

        // Validate file path
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ValidationError = "Please select a file.";
            HasValidationError = true;
            SaveCommand.NotifyCanExecuteChanged();
            return false;
        }

        var fileResult = _validationService.ValidateManagedApp(FilePath);
        if (!fileResult.IsSuccess)
        {
            ValidationError = fileResult.Error;
            HasValidationError = true;
            SaveCommand.NotifyCanExecuteChanged();
            return false;
        }

        ValidationError = null;
        HasValidationError = false;
        SaveCommand.NotifyCanExecuteChanged();
        return true;
    }

    private void UpdateAppType()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ShowPowerShellBypass = false;
            return;
        }

        var appType = _validationService.ResolveAppType(FilePath);
        ShowPowerShellBypass = appType == AppType.PowerShell;
    }

    private void UpdateWorkingDirectoryFromFilePath()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        // Only auto-update if working directory is empty or matches old file's directory
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                WorkingDirectory = directory;
            }
        }
    }
}
