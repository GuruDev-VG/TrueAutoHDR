namespace AutoHDR.UI;

internal static class AppIcon
{
    private static readonly Icon BaseIcon = Load();

    public static Icon Create() => (Icon)BaseIcon.Clone();

    private static Icon Load()
    {
        try
        {
            using var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null)
                return (Icon)extracted.Clone();
        }
        catch
        {
            // Fall back to the standard application icon if Windows cannot
            // extract the embedded executable icon (for example under dotnet run).
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
