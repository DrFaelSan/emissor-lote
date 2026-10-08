using NEO_e.App.ViewModels;
using System;
using System.Windows;

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
        StateChanged += OnWindowStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.InitializeAsync();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is null)
            return;

        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        BorderThickness = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
