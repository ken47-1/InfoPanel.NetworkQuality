namespace InfoPanel.NetworkQuality.Logging;

public static class Logger
{
    private static string LogFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "InfoPanel.NetworkQuality");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "log.txt");
        }
    }

    public static void Log(string message)
    {
        try
        {
            var logPath = LogFilePath;
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(logPath, entry + Environment.NewLine);

            var info = new FileInfo(logPath);
            if (info.Exists && info.Length > 1024 * 1024)
            {
                var backup = Path.ChangeExtension(logPath, ".old.txt");
                File.Copy(logPath, backup, true);
                File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Log rotated{Environment.NewLine}");
            }
        }
        catch { }
    }
}
