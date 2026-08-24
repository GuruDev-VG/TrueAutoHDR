using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

public interface IGameSource
{
    string StoreName { get; }
    IReadOnlyList<InstalledGame> GetInstalledGames(bool forceRefresh = false);
    InstalledGame? IdentifyByExecutable(string executablePath);
}
