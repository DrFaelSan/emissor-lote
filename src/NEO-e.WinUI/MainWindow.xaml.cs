using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NEO_e.WinUI.Services;
using NEO_e.WinUI.ViewModels;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace NEO_e.WinUI;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    private const double DefaultWidth = 1280;
    private const double DefaultHeight = 840;

    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel, IIslandNotifier islandNotifier, PickerService pickerService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        RootGrid.DataContext = viewModel;
        DynamicIsland.Notifier = islandNotifier;

        DynamicIsland.SizeChanged += (_, _) => UpdateIslandInteractiveRegion();
        TitleBarRow.SizeChanged += (_, _) => UpdateIslandInteractiveRegion();
        CertificatesBorder.SizeChanged += (_, _) => UpdateCertificatesBorderClip();

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarRow);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            var scale = GetRasterizationScale();
            TitleStack.Margin = new Thickness(AppWindow.TitleBar.LeftInset / scale, 0, 0, 0);
        }

        pickerService.Initialize(WindowNative.GetWindowHandle(this));
        InitializeWindowSize();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private double GetRasterizationScale()
    {
        var scale = Content?.XamlRoot?.RasterizationScale ?? 0d;
        if (scale <= 0d)
        {
            scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96d;
        }

        return scale > 0d ? scale : 1d;
    }

    private void InitializeWindowSize()
    {
        var scale = GetRasterizationScale();
        var width = (int)(DefaultWidth * scale);
        var height = (int)(DefaultHeight * scale);
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

        AppWindow.Resize(new SizeInt32 { Width = width, Height = height });
        AppWindow.Move(new PointInt32
        {
            X = workArea.X + Math.Max(0, (workArea.Width - width) / 2),
            Y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2),
        });
    }

    private void UpdateCertificatesBorderClip()
    {
        if (CertificatesBorder.ActualWidth <= 0 || CertificatesBorder.ActualHeight <= 0)
        {
            return;
        }

        CertificatesBorder.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, CertificatesBorder.ActualWidth, CertificatesBorder.ActualHeight),
        };
    }

    private void UpdateIslandInteractiveRegion()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        if (DynamicIsland.ActualWidth <= 0 || DynamicIsland.ActualHeight <= 0)
        {
            return;
        }

        var bounds = DynamicIsland.TransformToVisual(RootGrid)
            .TransformBounds(new Rect(0, 0, DynamicIsland.ActualWidth, DynamicIsland.ActualHeight));
        var scale = GetRasterizationScale();
        var region = new RectInt32
        {
            X = (int)Math.Round(bounds.X * scale),
            Y = (int)Math.Round(bounds.Y * scale),
            Width = (int)Math.Round(bounds.Width * scale),
            Height = (int)Math.Round(bounds.Height * scale),
        };

        InputNonClientPointerSource.GetForWindowId(AppWindow.Id)
            .SetRegionRects(NonClientRegionKind.Passthrough, new[] { region });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnLoaded;

        UpdateIslandInteractiveRegion();
        await _viewModel.InitializeAsync();
    }
}
