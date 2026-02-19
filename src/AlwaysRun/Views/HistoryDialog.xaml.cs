using System.Windows;
using AlwaysRun.ViewModels;

namespace AlwaysRun.Views;

/// <summary>
/// Interaction logic for HistoryDialog.xaml
/// </summary>
public partial class HistoryDialog : Window
{
    public HistoryDialog(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
