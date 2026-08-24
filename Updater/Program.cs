using System.Diagnostics;

namespace TrueAutoHDR.Updater;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            var a = Parse(args);
            if (!a.TryGetValue("wait", out var waitText) ||
                !int.TryParse(waitText, out var pid) ||
                !a.TryGetValue("source", out var source) ||
                !a.TryGetValue("target", out var target) ||
                !a.TryGetValue("restart", out var restart))
                return;

            try
            {
                var process = Process.GetProcessById(pid);
                process.WaitForExit(30000);
            }
            catch { }

            source = Path.GetFullPath(source);
            target = Path.GetFullPath(target);
            if (!Directory.Exists(source) || !Directory.Exists(target)) return;

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                if (relative.StartsWith("..", StringComparison.Ordinal)) continue;
                var destination = Path.GetFullPath(Path.Combine(target, relative));
                if (!destination.StartsWith(target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var temp = destination + ".update-new";
                File.Copy(file, temp, true);
                File.Move(temp, destination, true);
            }

            if (File.Exists(restart))
                Process.Start(new ProcessStartInfo(restart) { UseShellExecute = true });
        }
        catch
        {
            // The main app retains the staged package so a failed update can be diagnosed/retried.
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            var key = args[i].TrimStart('-');
            result[key] = args[i + 1];
        }
        return result;
    }
}
