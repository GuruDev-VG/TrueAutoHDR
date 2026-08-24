using AutoHDR.Database;
using AutoHDR.GameWatcher;
using AutoHDR.HDR;
using AutoHDR.UI;
using AutoHDR.Updates;

namespace AutoHDR;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            RunApplication(args);
        }
        catch (Exception ex)
        {
            var selfTest = args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase));
            Environment.ExitCode = selfTest ? 13 : 1;
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TrueAutoHDR",
                    "startup_crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  Fatal startup exception: {ex}\r\n");
                if (!selfTest)
                    MessageBox.Show(
                        $"TrueAuto HDR could not start.\n\n{ex.Message}\n\nCrash details were written to:\n{path}",
                        "TrueAuto HDR startup error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    private static void RunApplication(string[] args)
    {
        DpiBootstrap.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();

        var startupMode = args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        var selfTestMode = args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase));
        var portableMode = args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)) ||
                           File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.mode"));

        using var singleInstance = new Mutex(true, @"Local\TrueAutoHDR_SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!startupMode)
                MessageBox.Show("TrueAuto HDR is already running in the notification area.",
                    "TrueAuto HDR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = portableMode
            ? Path.Combine(AppContext.BaseDirectory, "Data")
            : Path.Combine(local, "TrueAutoHDR");
        if (!portableMode) MigrateLegacyData(Path.Combine(local, "AutoHDR"), appData);
        Directory.CreateDirectory(appData);

        var logger = new FileLogger(Path.Combine(appData, "trueautohdr.log"));
        logger.Log($"TrueAuto HDR v1.2.6 starting. mode={(portableMode ? "portable" : "installed")}, startup={startupMode}.");

        Application.ThreadException += (_, e) =>
            ReportFatalError(logger, "WinForms UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ReportFatalError(logger, "Unhandled application exception", ex, showDialog: false);
            else
                logger.Log($"Unhandled non-Exception object: {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Log($"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };
        var settings = new AppSettings(Path.Combine(appData, "settings.json"), logger);
        var startup = new StartupManager(logger);
        settings.SetRunAtStartup(startup.IsEnabled());

        var bundledSeed = Path.Combine(AppContext.BaseDirectory, "Database", "native_hdr_database.json");
        var activeDatabase = Path.Combine(appData, "native_hdr_database.json");
        SeedDatabaseIfNeeded(bundledSeed, activeDatabase, logger);
        var database = new HdrDatabase(Path.Combine(appData, "user_hdr_games.json"), logger, activeDatabase);
        var databaseUpdater = new DatabaseUpdater(database, activeDatabase, Path.Combine(appData, "database_version.txt"), logger);
        var steam = new SteamGameDetector(logger);
        var custom = new CustomGameDetector(Path.Combine(appData, "custom_games.json"), logger);
        var games = new UnifiedGameDetector(steam, custom, logger);
        var community = new Lazy<CommunityHdrSources>(() => new CommunityHdrSources(Path.Combine(AppContext.BaseDirectory, "Database", "community_hdr_names.json"), logger));
        var steamStore = new Lazy<SteamStoreHdrClient>(() => new SteamStoreHdrClient(logger));
        var pcgwHdr = new Lazy<PcgwHdrListClient>(() => new PcgwHdrListClient(logger));
        var hdrSourcesUpdater = new HdrSourcesUpdater(games, database, pcgwHdr.Value, databaseUpdater, logger);
        var appUpdates = new AppUpdateService(appData, logger);
        var hdr = new HdrController(logger);

        if (selfTestMode)
        {
            RunSelfTest(appData, logger, games, database, appUpdates);
            return;
        }

        var watcher = new GameProcessWatcher(games, database, hdr, logger);
        Application.Run(new TrayApplicationContext(watcher, games, database, community, steamStore, pcgwHdr, databaseUpdater, hdrSourcesUpdater, appUpdates, logger, settings, startup, startupMode));
    }

    private static void RunSelfTest(
        string appData,
        FileLogger logger,
        UnifiedGameDetector games,
        HdrDatabase database,
        AppUpdateService appUpdates)
    {
        var failures = new List<string>();

        void Require(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        Require(File.Exists(Path.Combine(AppContext.BaseDirectory, "TrueAutoHDR.exe")),
            "TrueAutoHDR.exe missing from application directory.");
        Require(File.Exists(Path.Combine(AppContext.BaseDirectory, "TrueAutoHDR.Updater.exe")),
            "TrueAutoHDR.Updater.exe missing from application directory.");
        Require(File.Exists(Path.Combine(AppContext.BaseDirectory, "Database", "native_hdr_database.json")),
            "Database/native_hdr_database.json missing.");
        Require(File.Exists(Path.Combine(AppContext.BaseDirectory, "Database", "community_hdr_names.json")),
            "Database/community_hdr_names.json missing.");

        // Exercise constructors/index loading without starting the watcher or tray.
        _ = database.Count;
        _ = games.GetInstalledGames(false);
        _ = appUpdates.GetType().Assembly.GetName().Version;

        if (failures.Count > 0)
        {
            foreach (var failure in failures) logger.Log($"SELF-TEST FAIL: {failure}");
            Environment.ExitCode = 12;
            return;
        }

        logger.Log($"SELF-TEST PASS: version={typeof(Program).Assembly.GetName().Version}, appData={appData}");
        Environment.ExitCode = 0;
    }

    private static void ReportFatalError(FileLogger logger, string context, Exception ex, bool showDialog = true)
    {
        try { logger.Log($"{context}: {ex}"); } catch { }

        if (!showDialog) return;
        try
        {
            MessageBox.Show(
                $"{context}.\n\n{ex.Message}\n\nDetails were written to trueautohdr.log.",
                "TrueAuto HDR",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch { }
    }

    private static void SeedDatabaseIfNeeded(string seed, string active, FileLogger logger)
    {
        try
        {
            if (File.Exists(active)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(active)!);
            if (File.Exists(seed))
            {
                File.Copy(seed, active);
                logger.Log($"Seeded updateable native HDR database: {active}");
            }
            else
            {
                File.WriteAllText(active, "{}");
                logger.Log("Bundled HDR database seed was missing; created an empty active database.");
            }
        }
        catch (Exception ex) { logger.Log($"Could not seed native HDR database: {ex.Message}"); }
    }

    private static void MigrateLegacyData(string legacy, string current)
    {
        try
        {
            if (!Directory.Exists(legacy) || Directory.Exists(current)) return;
            Directory.CreateDirectory(current);
            foreach (var name in new[] { "settings.json", "user_hdr_games.json" })
            {
                var source = Path.Combine(legacy, name);
                var destination = Path.Combine(current, name);
                if (File.Exists(source) && !File.Exists(destination)) File.Copy(source, destination);
            }
        }
        catch { }
    }
}

public sealed class FileLogger
{
    private readonly string _path; private readonly object _sync = new();
    public FileLogger(string path) => _path = path;
    public void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}";
        lock (_sync) File.AppendAllText(_path, line + Environment.NewLine);
        System.Diagnostics.Debug.WriteLine(line);
    }
    public string Path => _path;
}
