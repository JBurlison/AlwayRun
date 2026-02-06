using System.Windows;
using AlwaysRun.ViewModels;

namespace AlwaysRun.Views;

/// <summary>
/// Interaction logic for AddEditDialog.xaml
/// </summary>
public partial class AddEditDialog : Window
{
    public AddEditDialog(AddEditAppViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
