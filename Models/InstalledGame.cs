namespace AutoHDR.Models;

public sealed record InstalledGame(string Store, string StoreId, string Name, string InstallDirectory)
{
    public string Key => $"{Store}:{StoreId}";
    public bool IsSteam => Store.Equals("Steam", StringComparison.OrdinalIgnoreCase);
}
