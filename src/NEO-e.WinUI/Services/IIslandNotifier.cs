namespace NEO_e.WinUI.Services;

public interface IIslandNotifier
{
    event Action<IslandNotification>? NotificationReceived;

    void Notify(IslandNotification notification);
}