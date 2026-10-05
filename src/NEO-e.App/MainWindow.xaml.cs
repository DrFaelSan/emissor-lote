using Microsoft.Win32;
using NEO_e.App.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace NEO_e.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ChooseCertificatesFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Escolher pasta de certificados" };
        if (dialog.ShowDialog() == true)
            ((MainWindowViewModel)DataContext).SetCertificateFolder(dialog.FolderName);
    }

    private void ChooseDestinationFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Escolher pasta de destino" };
        if (dialog.ShowDialog() == true)
            ((MainWindowViewModel)DataContext).SetDestinationFolder(dialog.FolderName);
    }

    private void CertificatePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && passwordBox.DataContext is CertificateRowViewModel row)
            ((MainWindowViewModel)DataContext).SetPassword(row, passwordBox.Password);
    }
}