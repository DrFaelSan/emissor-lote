namespace NEO_e.WinUI.Services;

public enum IslandNotificationKind
{
    Info,
    Success,
    Error
}

public sealed record IslandNotification(string Message, IslandNotificationKind Kind);