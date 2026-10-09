using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NEO_e.App.Services;

namespace NEO_e.App.Controls;

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
        Dispatcher.InvokeAsync(() => _ = ShowNotificationAsync(notification));
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

            if (IsMouseOver)
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

    private void OnIslandMouseEnter(object sender, MouseEventArgs e)
    {
        if (!_hasLoaded || !_isIslandVisible)
            return;

        _ = TransitionToAsync(true);
    }

    private void OnIslandMouseLeave(object sender, MouseEventArgs e)
    {
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
        if (!_isIslandVisible || _notificationHoldActive || IsMouseOver)
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
            IslandNotificationKind.Success => GetBrush("IslandAccentBrush"),
            IslandNotificationKind.Error => GetBrush("IslandDangerBrush"),
            _ => GetBrush("IslandTextSecondaryBrush")
        };

        NotifyText.Text = notification.Message;
    }

    private static Brush GetBrush(string resourceKey) =>
        System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.White;

    private void ShowNotificationChip() => NotifyChip.Visibility = Visibility.Visible;

    private void HideNotificationChip() => NotifyChip.Visibility = Visibility.Collapsed;

    private Task AnimateTranslateAsync(double targetY, TimeSpan duration, Action commit)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation(targetY, duration)
        {
            From = IslandTransform.Y,
            EasingFunction = CreateEasing()
        };
        Storyboard.SetTarget(animation, IslandTransform);
        Storyboard.SetTargetProperty(animation, new PropertyPath(TranslateTransform.YProperty));
        storyboard.Children.Add(animation);
        return RunStoryboardAsync(storyboard, commit);
    }

    private Task AnimateSizeAsync(double targetWidth, double targetHeight, TimeSpan duration, Action commit)
    {
        var storyboard = new Storyboard();

        var widthAnimation = new DoubleAnimation(targetWidth, duration)
        {
            From = GetAnimationStartValue(IslandRoot.ActualWidth),
            EasingFunction = CreateEasing()
        };
        Storyboard.SetTarget(widthAnimation, IslandRoot);
        Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(FrameworkElement.WidthProperty));
        storyboard.Children.Add(widthAnimation);

        var heightAnimation = new DoubleAnimation(targetHeight, duration)
        {
            From = GetAnimationStartValue(IslandRoot.ActualHeight),
            EasingFunction = CreateEasing()
        };
        Storyboard.SetTarget(heightAnimation, IslandRoot);
        Storyboard.SetTargetProperty(heightAnimation, new PropertyPath(FrameworkElement.HeightProperty));
        storyboard.Children.Add(heightAnimation);

        return RunStoryboardAsync(storyboard, commit);
    }

    private static double GetAnimationStartValue(double actualSize) =>
        double.IsFinite(actualSize) && actualSize > 0 ? actualSize : MinimumIslandSize;

    private Task AnimateOpacityAsync(UIElement element, double targetOpacity, TimeSpan duration, Action commit)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation(targetOpacity, duration);
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
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

        storyboard.Begin(this);
        return completionSource.Task;
    }

    private static QuadraticEase CreateEasing() => new() { EasingMode = EasingMode.EaseOut };
}
