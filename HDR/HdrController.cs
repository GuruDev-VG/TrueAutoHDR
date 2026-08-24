using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AutoHDR.HDR;

public readonly record struct HdrAggregateState(bool AnyHdrEnabled, int SupportedTargetCount);

public sealed class HdrController
{
    private readonly FileLogger _logger;
    public HdrController(FileLogger logger) => _logger = logger;

    // Compatibility name retained for the rest of the app. The aggregate now
    // intentionally represents only the Windows primary display.
    public HdrAggregateState GetAggregateState()
    {
        var primary = DisplayConfigNative.TryGetPrimaryTarget();
        if (primary is null)
        {
            _logger.Log("Could not resolve the Windows primary display target.");
            return new HdrAggregateState(false, 0);
        }

        var state = DisplayConfigNative.TryGetAdvancedColorState(primary.Value);
        if (state is null || !state.Value.Supported)
        {
            _logger.Log("Windows primary display does not report HDR support.");
            return new HdrAggregateState(false, 0);
        }

        return new HdrAggregateState(state.Value.Enabled, 1);
    }

    // Compatibility name retained so existing watcher code needs no behavioral
    // changes. Only the Windows primary display is modified.
    public void SetHdrOnAllSupportedTargets(bool enabled)
    {
        var primary = DisplayConfigNative.TryGetPrimaryTarget();
        if (primary is null)
        {
            _logger.Log($"HDR {(enabled ? "enable" : "disable")} skipped: primary display target was not found.");
            return;
        }

        var state = DisplayConfigNative.TryGetAdvancedColorState(primary.Value);
        if (state is null || !state.Value.Supported)
        {
            _logger.Log($"HDR {(enabled ? "enable" : "disable")} skipped: primary display is not HDR-capable.");
            return;
        }

        if (state.Value.Enabled == enabled)
        {
            _logger.Log($"Primary display HDR is already {(enabled ? "ON" : "OFF")}; no change needed.");
            return;
        }

        DisplayConfigNative.SetAdvancedColorState(primary.Value, enabled);
        _logger.Log($"Requested HDR={(enabled ? "ON" : "OFF")} on Windows primary display only.");
    }
}

internal static class DisplayConfigNative
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const int ERROR_SUCCESS = 0;
    private const int GET_ADVANCED_COLOR_INFO = 9;
    private const int SET_ADVANCED_COLOR_STATE = 10;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_2DREGION { public uint cx; public uint cy; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_TARGET_MODE { public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTL { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECTL { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public uint pixelFormat;
        public POINTL position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
    {
        public POINTL PathSourceSize;
        public RECTL DesktopImageRegion;
        public RECTL DesktopImageClip;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct MODE_UNION
    {
        [FieldOffset(0)] public DISPLAYCONFIG_TARGET_MODE targetMode;
        [FieldOffset(0)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        [FieldOffset(0)] public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        public MODE_UNION modeInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
    }

    internal readonly record struct Target(LUID AdapterId, uint Id);
    internal readonly record struct ColorState(bool Supported, bool Enabled);


    private const int GET_SOURCE_NAME = 1;
    private const int CCHDEVICENAME = 32;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string viewGdiDeviceName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);


    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setPacket);

    internal static IEnumerable<Target> GetActiveTargets()
    {
        var result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
        if (result != ERROR_SUCCESS) throw new Win32Exception(result);

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (result != ERROR_SUCCESS) throw new Win32Exception(result);

        for (var i = 0; i < (int)pathCount; i++)
            yield return new Target(paths[i].targetInfo.adapterId, paths[i].targetInfo.id);
    }

    internal static Target? TryGetPrimaryTarget()
    {
        // Screen.PrimaryScreen resolves the display Windows marks as "Make this
        // my main display". We then map its GDI name (for example \\.\DISPLAY1)
        // to the corresponding DisplayConfig source and return that path's
        // target, which is what the Advanced Color API operates on.
        var primaryDeviceName = System.Windows.Forms.Screen.PrimaryScreen?.DeviceName;
        if (string.IsNullOrWhiteSpace(primaryDeviceName))
            return null;

        var result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
        if (result != ERROR_SUCCESS) throw new Win32Exception(result);

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (result != ERROR_SUCCESS) throw new Win32Exception(result);

        for (var i = 0; i < (int)pathCount; i++)
        {
            var source = paths[i].sourceInfo;
            var request = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = GET_SOURCE_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = source.adapterId,
                    id = source.id
                },
                viewGdiDeviceName = string.Empty
            };

            if (DisplayConfigGetDeviceInfo(ref request) != ERROR_SUCCESS)
                continue;

            if (string.Equals(request.viewGdiDeviceName, primaryDeviceName, StringComparison.OrdinalIgnoreCase))
                return new Target(paths[i].targetInfo.adapterId, paths[i].targetInfo.id);
        }

        return null;
    }

    internal static ColorState? TryGetAdvancedColorState(Target target)
    {
        var packet = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = GET_ADVANCED_COLOR_INFO,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                adapterId = target.AdapterId,
                id = target.Id
            }
        };

        var result = DisplayConfigGetDeviceInfo(ref packet);
        if (result != ERROR_SUCCESS) return null;

        var supported = (packet.value & 0x1) != 0;
        var enabled = (packet.value & 0x2) != 0;
        var wideColorEnforced = (packet.value & 0x4) != 0;

        // On modern Windows an HDR screen is AdvancedColorSupported and not the
        // SDR/ACM "wide color enforced" case.
        return new ColorState(supported && !wideColorEnforced, enabled && !wideColorEnforced);
    }

    internal static void SetAdvancedColorState(Target target, bool enabled)
    {
        var packet = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = SET_ADVANCED_COLOR_STATE,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                adapterId = target.AdapterId,
                id = target.Id
            },
            value = enabled ? 1u : 0u
        };

        var result = DisplayConfigSetDeviceInfo(ref packet);
        if (result != ERROR_SUCCESS) throw new Win32Exception(result);
    }
}
