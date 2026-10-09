namespace NEO_e.App.Services;

public interface IIslandNotifier
{
    event Action<IslandNotification>? NotificationReceived;

    void Notify(IslandNotification notification);
}
