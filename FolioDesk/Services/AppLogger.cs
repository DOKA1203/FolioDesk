using System.Diagnostics;
using System.IO;
using System.Text;

namespace FolioDesk.Services;

public static class AppLogger {
    private const long MaxLogBytes = 512 * 1024;
    private static readonly object Lock = new();
    private static string? _logPath;

    public static void Initialize(string dataFolder) {
        _logPath = Path.Combine(dataFolder, "logs", "FolioDesk.log");
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception = null) {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var line = exception is null
            ? $"{timestamp} [{level}] {message}"
            : $"{timestamp} [{level}] {message} Exception={Flatten(exception)}";

        Debug.WriteLine(line);

        var logPath = _logPath;
        if (logPath is null) return;

        try {
            lock (Lock) {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                if (File.Exists(logPath) && new FileInfo(logPath).Length > MaxLogBytes)
                    File.Move(logPath, logPath + ".old", overwrite: true);
                using var sw = new StreamWriter(logPath, append: true, Encoding.UTF8);
                sw.WriteLine(line);
            }
        }
        catch {
            // Logging must never interrupt app workflows.
        }
    }

    private static string Flatten(Exception exception) =>
        exception.ToString()
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}