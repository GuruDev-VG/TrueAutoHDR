using Microsoft.Win32;

namespace AutoHDR.UI;

public sealed class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrueAutoHDR";
    private const string LegacyValueName = "AutoHDR";
    private readonly FileLogger _logger;

    public StartupManager(FileLogger logger) => _logger = logger;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(ValueName) as string;
            var legacy = key?.GetValue(LegacyValueName) as string;
            var expected = BuildCommandLine();
            if (string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(legacy?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
                return true;

            var exe = Environment.ProcessPath;
            var oldCommand = string.IsNullOrWhiteSpace(exe) ? null : $"\"{exe}\"";
            if (string.Equals(value?.Trim(), oldCommand, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(legacy?.Trim(), oldCommand, StringComparison.OrdinalIgnoreCase))
            {
                // Seamlessly migrate pre-v0.7 startup entries to the lightweight path.
                SetEnabled(true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not read Windows startup setting: {ex.Message}");
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null) throw new InvalidOperationException("Could not open the Windows Run registry key.");

            if (enabled)
            {
                var command = BuildCommandLine();
                key.SetValue(ValueName, command, RegistryValueKind.String);
                key.DeleteValue(LegacyValueName, false);
                _logger.Log($"Windows startup enabled: {command}");
            }
            else
            {
                key.DeleteValue(ValueName, false);
                key.DeleteValue(LegacyValueName, false);
                _logger.Log("Windows startup disabled.");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not change Windows startup setting: {ex.Message}");
            return false;
        }
    }

    private static string BuildCommandLine()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            throw new InvalidOperationException("Could not determine the TrueAuto HDR executable path.");
        return $"\"{exe}\" --startup";
    }
}
