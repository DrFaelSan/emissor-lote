using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NEO_e.WinUI.Services;
using Windows.Foundation;

namespace NEO_e.WinUI.Controls;

public partial class DynamicIslandControl : UserControl
{
    private const double MinimumIslandSize = 40;
    private const double HiddenOffset = -80;
    private const double CollapsedCornerRadius = 20;
    private const double ExpandedCornerRadius = 32;
    private const int NotificationHoldMilliseconds = 5000;

    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(380);
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(180);

    public static readonly DependencyProperty NotifierProperty =
        DependencyProperty.Register(
            nameof(Notifier),
            typeof(IIslandNotifier),
            typeof(DynamicIslandControl),
            new PropertyMetadata(null, OnNotifierChanged));

    private Task _activeTransition = Task.CompletedTask;
    private CancellationTokenSource? _holdCts;
    private bool _isExpanded;
    private bool _hasLoaded;
    private bool _isIslandVisible;
    private bool _notificationHoldActive;
    private bool _isPointerOver;

    public DynamicIslandControl()
    {
        InitializeComponent();
    }

    public IIslandNotifier? Notifier
    {
        get => (IIslandNotifier?)GetValue(NotifierProperty);
        set => SetValue(NotifierProperty, value);
    }

    private static void OnNotifierChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DynamicIslandControl)d;

        if (e.OldValue is IIslandNotifier previous)
            previous.NotificationReceived -= control.OnNotificationReceived;

        if (e.NewValue is IIslandNotifier next)
            next.NotificationReceived += control.OnNotificationReceived;
    }

    private void OnNotificationReceived(IslandNotification notification)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _ = ShowNotificationAsync(notification)
                    .ContinueWith(
                        t => App.DiagLog("island-task hr=0x" + t.Exception!.GetBaseException().HResult.ToString("X8") + " " + t.Exception),
                        TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                App.DiagLog("island-lambda hr=0x" + ex.HResult.ToString("X8") + " " + ex);
            }
        });
    }

    private async Task ShowNotificationAsync(IslandNotification notification)
    {
        ApplyNotification(notification);
        ShowNotificationChip();

        _holdCts?.Cancel();
        _holdCts?.Dispose();
        _holdCts = new CancellationTokenSource();
        var holdToken = _holdCts.Token;
        _notificationHoldActive = true;

        try
        {
            await TransitionToAsync(true);
            if (holdToken.IsCancellationRequested)
                return;

            try
            {
                await Task.Delay(NotificationHoldMilliseconds, holdToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _notificationHoldActive = false;

            if (_isPointerOver)
                return;

            HideNotificationChip();
            await TransitionToAsync(false);
        }
        finally
        {
            if (!holdToken.IsCancellationRequested)
                _notificationHoldActive = false;
        }
    }

    private void OnIslandLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        IslandRoot.IsHitTestVisible = false;
        IslandTransform.Y = HiddenOffset;
    }

    private void OnIslandPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;

        if (!_hasLoaded || !_isIslandVisible)
            return;

        _ = TransitionToAsync(true);
    }

    private void OnIslandPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;

        if (_notificationHoldActive)
            return;

        HideNotificationChip();
        _ = TransitionToAsync(false);
    }

    private Task TransitionToAsync(bool expand)
    {
        var previous = _activeTransition;
        var transition = RunTransitionAsync();
        _activeTransition = transition;
        return transition;

        async Task RunTransitionAsync()
        {
            await previous;

            if (expand && !_isIslandVisible)
            {
                await AnimateTranslateAsync(0, SlideDuration, () =>
                {
                    IslandTransform.Y = 0;
                    _isIslandVisible = true;
                    IslandRoot.IsHitTestVisible = true;
                });
            }

            if (_isExpanded == expand)
            {
                if (!expand)
                    await SlideUpAsync();
                return;
            }

            if (expand)
                await ExpandCoreAsync();
            else
            {
                await CollapseCoreAsync();
                await SlideUpAsync();
            }
        }
    }

    private Task SlideUpAsync()
    {
        if (!_isIslandVisible || _notificationHoldActive || _isPointerOver)
            return Task.CompletedTask;

        return AnimateTranslateAsync(HiddenOffset, SlideDuration, () =>
        {
            IslandTransform.Y = HiddenOffset;
            _isIslandVisible = false;
            IslandRoot.IsHitTestVisible = false;
        });
    }

    private async Task ExpandCoreAsync()
    {
        IslandRoot.CornerRadius = new CornerRadius(ExpandedCornerRadius);

        IslandContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var targetWidth = Math.Max(
            MinimumIslandSize,
            IslandContent.DesiredSize.Width + IslandRoot.Padding.Left + IslandRoot.Padding.Right);
        var targetHeight = Math.Max(
            MinimumIslandSize,
            IslandContent.DesiredSize.Height + IslandRoot.Padding.Top + IslandRoot.Padding.Bottom);

        IslandContent.IsHitTestVisible = true;

        await AnimateSizeAsync(targetWidth, targetHeight, ExpandDuration, () =>
        {
            IslandRoot.Width = double.NaN;
            IslandRoot.Height = double.NaN;
        });

        await AnimateOpacityAsync(IslandContent, 1, FadeInDuration, () => IslandContent.Opacity = 1);

        _isExpanded = true;
    }

    private async Task CollapseCoreAsync()
    {
        IslandContent.IsHitTestVisible = false;

        await AnimateOpacityAsync(IslandContent, 0, FadeOutDuration, () => IslandContent.Opacity = 0);

        if (IslandRoot.ActualWidth > MinimumIslandSize)
        {
            IslandRoot.Width = IslandRoot.ActualWidth;
            IslandRoot.Height = IslandRoot.ActualHeight;
        }

        await AnimateSizeAsync(MinimumIslandSize, MinimumIslandSize, CollapseDuration, () =>
        {
            IslandRoot.Width = MinimumIslandSize;
            IslandRoot.Height = MinimumIslandSize;
        });

        IslandRoot.CornerRadius = new CornerRadius(CollapsedCornerRadius);
        _isExpanded = false;
    }

    private void ApplyNotification(IslandNotification notification)
    {
        NotifyIcon.Text = notification.Kind switch
        {
            IslandNotificationKind.Success => "\uE73E",
            IslandNotificationKind.Error => "\uE711",
            _ => "\uE946"
        };

        NotifyIcon.Foreground = notification.Kind switch
        {
            IslandNotificationKind.Success => (Brush)Resources["IslandAccentBrush"],
            IslandNotificationKind.Error => (Brush)Resources["IslandDangerBrush"],
            _ => (Brush)Resources["IslandTextSecondaryBrush"]
        };

        NotifyText.Text = notification.Message;
    }

    private void ShowNotificationChip() => NotifyChip.Visibility = Visibility.Visible;

    private void HideNotificationChip() => NotifyChip.Visibility = Visibility.Collapsed;

    private Task AnimateTranslateAsync(double targetY, TimeSpan duration, Action commit)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = targetY,
            Duration = duration,
            From = IslandTransform.Y,
            EasingFunction = CreateEasing()
        };
        Storyboard.SetTarget(animation, IslandTransform);
        Storyboard.SetTargetProperty(animation, "Y");
        storyboard.Children.Add(animation);
        return RunStoryboardAsync(storyboard, commit);
    }

    private Task AnimateSizeAsync(double targetWidth, double targetHeight, TimeSpan duration, Action commit)
    {
        var storyboard = new Storyboard();

        var widthAnimation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = duration,
            From = GetAnimationStartValue(IslandRoot.ActualWidth),
            EasingFunction = CreateEasing()
        };
        Storyboard.SetTarget(widthAnimation, IslandRoot);
        Storyboard.SetTargetProperty(widthAnimation, "Width");
        storyboard.Children.Add(widthAnimation);

        var heightAnimation = new DoubleAnimation
        {
            To = targetHeight,
            Duration = duration,
            From = GetAnimationStartValue(IslandRoot.ActualHeight),
            EasingFunction = CreateEasing()
        };
        Storyboard.SetTarget(heightAnimation, IslandRoot);
        Storyboard.SetTargetProperty(heightAnimation, "Height");
        storyboard.Children.Add(heightAnimation);

        return RunStoryboardAsync(storyboard, commit);
    }

    private static double GetAnimationStartValue(double actualSize) =>
        double.IsFinite(actualSize) && actualSize > 0 ? actualSize : MinimumIslandSize;

    private Task AnimateOpacityAsync(UIElement element, double targetOpacity, TimeSpan duration, Action commit)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = duration
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        return RunStoryboardAsync(storyboard, commit);
    }

    private Task RunStoryboardAsync(Storyboard storyboard, Action commit)
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        storyboard.Completed += (_, _) =>
        {
            storyboard.Stop();
            commit();
            completionSource.TrySetResult();
        };

        storyboard.Begin();
        return completionSource.Task;
    }

    private static QuadraticEase CreateEasing() => new() { EasingMode = EasingMode.EaseOut };
}