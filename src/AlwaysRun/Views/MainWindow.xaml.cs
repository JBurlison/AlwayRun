using System.ComponentModel;
using System.Windows;
using AlwaysRun.ViewModels;

namespace AlwaysRun.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _forceClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.ShowAddEditDialogRequested += OnShowAddEditDialogRequested;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnShowAddEditDialogRequested(object? sender, AddEditAppViewModel vm)
    {
        var dialog = new AddEditDialog(vm)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_forceClose)
        {
            // Allow close during shutdown
            return;
        }

        if (!_viewModel.ExitOnClose)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            Hide();
            return;
        }

        // Exit on close is enabled - perform shutdown
        e.Cancel = true;
        _forceClose = true;
        await _viewModel.ShutdownAsync();
        Close();
    }

    /// <summary>
    /// Forces the window to close (for application shutdown).
    /// </summary>
    public async Task ForceCloseAsync()
    {
        _forceClose = true;
        await _viewModel.ShutdownAsync();
        Close();
    }

    /// <summary>
    /// Shows the window and brings it to the foreground.
    /// </summary>
    public void BringToForeground()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }
}
