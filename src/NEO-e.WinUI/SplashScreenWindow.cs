using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.Graphics;
using WinRT.Interop;

namespace NEO_e.WinUI;

public sealed class SplashScreenWindow : Microsoft.UI.Xaml.Window
{
    public SplashScreenWindow()
    {
        Title = "NEO-e";
        Content = CreateContent();
        CenterWindow();
    }

    private static Grid CreateContent()
    {
        var primaryBrush = new SolidColorBrush(Color.FromArgb(255, 201, 162, 39));
        var textBrush = new SolidColorBrush(Color.FromArgb(255, 47, 47, 43));
        var secondaryBrush = new SolidColorBrush(Color.FromArgb(255, 122, 119, 109));

        var layout = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 241, 239, 231)),
            Padding = new Thickness(32)
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var logo = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(20),
            Background = primaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 24),
            Child = new TextBlock
            {
                Text = "\uE8A4",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 32,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(logo, 0);
        layout.Children.Add(logo);

        var productName = new TextBlock
        {
            Text = "NEO-e",
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = textBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(productName, 1);
        layout.Children.Add(productName);

        var subtitle = new TextBlock
        {
            Text = "NFS-e em lote",
            FontSize = 13,
            Foreground = secondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 24)
        };
        Grid.SetRow(subtitle, 2);
        layout.Children.Add(subtitle);

        var progress = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(255, 227, 223, 210)),
            Foreground = primaryBrush
        };
        Grid.SetRow(progress, 3);
        layout.Children.Add(progress);

        return layout;
    }

    private void CenterWindow()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        appWindow.Resize(new SizeInt32 { Width = 420, Height = 260 });

        var workArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest).WorkArea;
        appWindow.Move(new PointInt32
        {
            X = workArea.X + Math.Max(0, (workArea.Width - 420) / 2),
            Y = workArea.Y + Math.Max(0, (workArea.Height - 260) / 2)
        });
    }
}
