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

            if (a.TryGetValue("restore", out var restoreSource) &&
                a.TryGetValue("target", out var restoreTarget) &&
                a.TryGetValue("restart", out var restoreExe))
            {
                if (a.TryGetValue("wait", out var rollbackWaitText) &&
                    int.TryParse(rollbackWaitText, out var rollbackPid))
                {
                    try
                    {
                        var process = Process.GetProcessById(rollbackPid);
                        process.WaitForExit(30000);
                    }
                    catch { }
                }

                RestoreBackup(restoreSource, restoreTarget);
                if (File.Exists(restoreExe))
                    Process.Start(new ProcessStartInfo(restoreExe) { UseShellExecute = true });
                return;
            }

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

            a.TryGetValue("backup", out var backup);
            if (!string.IsNullOrWhiteSpace(backup))
            {
                backup = Path.GetFullPath(backup);
                Directory.CreateDirectory(backup);
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                if (relative.StartsWith("..", StringComparison.Ordinal)) continue;

                var destination = Path.GetFullPath(Path.Combine(target, relative));
                if (!destination.StartsWith(target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(backup) && File.Exists(destination))
                {
                    var backupFile = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
                    File.Copy(destination, backupFile, true);
                }

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
            // The main app retains the staged package/backup so a failed update
            // can be diagnosed or rolled back.
        }
    }

    private static void RestoreBackup(string source, string target)
    {
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
            var temp = destination + ".rollback-new";
            File.Copy(file, temp, true);
            File.Move(temp, destination, true);
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
