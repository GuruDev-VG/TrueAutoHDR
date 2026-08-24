namespace AutoHDR.Models;

public sealed class HdrGame
{
    // Legacy Steam field kept for compatibility with existing bundled/user JSON.
    public string SteamAppId { get; set; } = "";
    public string Store { get; set; } = "Steam";
    public string StoreId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool NativeHdr { get; set; }
    public string RawPcgwHdrValue { get; set; } = "";
    public DateTime CheckedUtc { get; set; }
    public string Source { get; set; } = "manual";
    public string? PcgwPage { get; set; }
    // Optional cross-store identity metadata. Existing databases remain valid.
    public Dictionary<string, string>? StoreIds { get; set; }
    public List<string>? Aliases { get; set; }

    public string EffectiveStoreId => !string.IsNullOrWhiteSpace(StoreId) ? StoreId : SteamAppId;
}
