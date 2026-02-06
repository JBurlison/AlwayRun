using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// System tray icon service using Windows Forms NotifyIcon.
/// </summary>
public sealed class TrayService(ILogger<TrayService> logger) : ITrayService
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler? OpenRequested;

    /// <inheritdoc/>
    public event EventHandler? ExitRequested;

    /// <inheritdoc/>
    public event EventHandler? PauseAllRequested;

    /// <inheritdoc/>
    public event EventHandler? ResumeAllRequested;

    /// <inheritdoc/>
    public void Initialize()
    {
        logger.LogDebug("Initializing system tray icon");

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Open AlwaysRun", null, OnOpenClicked);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Pause All", null, OnPauseAllClicked);
        _contextMenu.Items.Add("Resume All", null, OnResumeAllClicked);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, OnExitClicked);

        _notifyIcon = new NotifyIcon
        {
            Text = "AlwaysRun - Process Manager",
            ContextMenuStrip = _contextMenu,
            Visible = false
        };

        // Try to load icon from resources, fallback to system icon
        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;

        logger.LogInformation("System tray icon initialized");
    }

    /// <inheritdoc/>
    public void Show()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = true;
            logger.LogDebug("Tray icon shown");
        }
    }

    /// <inheritdoc/>
    public void Hide()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            logger.LogDebug("Tray icon hidden");
        }
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        logger.LogDebug("Tray icon double-clicked");
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenClicked(object? sender, EventArgs e)
    {
        logger.LogDebug("Open menu item clicked");
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        logger.LogDebug("Exit menu item clicked");
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPauseAllClicked(object? sender, EventArgs e)
    {
        logger.LogDebug("Pause All menu item clicked");
        PauseAllRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnResumeAllClicked(object? sender, EventArgs e)
    {
        logger.LogDebug("Resume All menu item clicked");
        ResumeAllRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;

        logger.LogDebug("Tray service disposed");
    }
}
