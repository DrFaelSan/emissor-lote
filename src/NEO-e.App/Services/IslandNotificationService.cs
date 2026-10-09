namespace NEO_e.App.Services;

public sealed class IslandNotificationService : IIslandNotifier
{
    public event Action<IslandNotification>? NotificationReceived;

    public void Notify(IslandNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        NotificationReceived?.Invoke(notification);
    }
}
