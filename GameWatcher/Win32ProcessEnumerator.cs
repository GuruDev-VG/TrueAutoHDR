using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoHDR.GameWatcher;

internal static class Win32ProcessEnumerator
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumProcesses([Out] uint[] processIds, uint cb, out uint bytesReturned);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    public static HashSet<int> GetProcessIds()
    {
        var buffer = new uint[512];
        while (true)
        {
            if (!EnumProcesses(buffer, (uint)(buffer.Length * sizeof(uint)), out var bytesReturned))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var count = (int)(bytesReturned / sizeof(uint));
            if (count < buffer.Length)
            {
                var result = new HashSet<int>();
                for (var i = 0; i < count; i++)
                    if (buffer[i] != 0) result.Add((int)buffer[i]);
                return result;
            }

            buffer = new uint[buffer.Length * 2];
        }
    }

    public static string? TryGetExecutablePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == 0) return null;

        try
        {
            var capacity = 1024u;
            var buffer = new StringBuilder((int)capacity);
            return QueryFullProcessImageName(handle, 0, buffer, ref capacity) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
