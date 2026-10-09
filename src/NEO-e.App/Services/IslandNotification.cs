namespace NEO_e.App.Services;

public enum IslandNotificationKind
{
    Info,
    Success,
    Error
}

public sealed record IslandNotification(string Message, IslandNotificationKind Kind);
