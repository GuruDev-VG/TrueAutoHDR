namespace AutoHDR.Models;

public sealed class HdrGame
{
    // Legacy Steam field kept for compatibility with existing bundled/user JSON.
    public string SteamAppId { get; set; } = "";
    public string Store { get; set; } = "Steam";
    public string StoreId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool NativeHdr { get; set; }
    // HDR10+ Gaming is capability metadata only. It never enables Display Recovery by itself.
    public bool Hdr10PlusGaming { get; set; }
    public string Hdr10PlusSource { get; set; } = "";
    // Capability-only records can carry HDR10+ metadata without becoming a Native HDR decision.
    public bool CapabilityOnly { get; set; }
    public string RawPcgwHdrValue { get; set; } = "";
    public DateTime CheckedUtc { get; set; }
    public string Source { get; set; } = "manual";
    public string? PcgwPage { get; set; }
    // Optional cross-store identity metadata. Existing databases remain valid.
    public Dictionary<string, string>? StoreIds { get; set; }
    public List<string>? Aliases { get; set; }

    public string EffectiveStoreId => !string.IsNullOrWhiteSpace(StoreId) ? StoreId : SteamAppId;
}
