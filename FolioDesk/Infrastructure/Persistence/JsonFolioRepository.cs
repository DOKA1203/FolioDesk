using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FolioDesk.Application.Abstractions;
using FolioDesk.Models;
using FolioDesk.Services;

namespace FolioDesk.Infrastructure.Persistence;

public sealed class JsonFolioRepository(string dataFolder) : IFolioRepository {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _dataPath = Path.Combine(dataFolder, "folio.json");
    private readonly string _backupPath = Path.Combine(dataFolder, "folio.json.bak");

    public FolioFolder? GetFolder(int folderId) =>
        LoadData().Folders.FirstOrDefault(folder => folder.Id == folderId);

    public FolioFolder CreateFolder(string name) {
        var data = LoadData();
        var folder = new FolioFolder {
            Id = data.Folders.Count == 0 ? 1 : checked(data.Folders.Max(item => item.Id) + 1),
            Name = name
        };
        data.Folders.Add(folder);
        SaveData(data);
        AppLogger.Info($"Created folder. Id={folder.Id}, Name='{folder.Name}'.");
        return folder;
    }

    public void DeleteFolder(int folderId) {
        var data = LoadData();
        if (data.Folders.RemoveAll(folder => folder.Id == folderId) == 0) return;
        SaveData(data);
        AppLogger.Info($"Deleted folder. FolderId={folderId}.");
    }

    public void AddItem(int folderId, FolioItem item) {
        var data = LoadData();
        var folder = RequireFolder(data, folderId);
        item.Order = folder.Files.Count;
        folder.Files.Add(item);
        SaveData(data);
        AppLogger.Info($"Added file to folder. FolderId={folderId}, Name='{item.Name}', Path='{item.Path}'.");
    }

    public void RemoveItem(int folderId, string itemPath) {
        var data = LoadData();
        var folder = RequireFolder(data, folderId);
        if (folder.Files.RemoveAll(item => PathsEqual(item.Path, itemPath)) == 0)
            throw new InvalidOperationException($"Item '{itemPath}' was not found in folder {folderId}.");
        NormalizeOrder(folder);
        SaveData(data);
        AppLogger.Info($"Removed file from folder. FolderId={folderId}, Path='{itemPath}'.");
    }

    public void ReorderItems(int folderId, IReadOnlyList<string> orderedPaths) {
        var data = LoadData();
        var folder = RequireFolder(data, folderId);
        if (orderedPaths.Count != folder.Files.Count)
            throw new InvalidOperationException("The reordered item count does not match the stored item count.");

        var byPath = folder.Files.ToDictionary(item => NormalizePath(item.Path), StringComparer.OrdinalIgnoreCase);
        var reordered = new List<FolioItem>(orderedPaths.Count);
        foreach (var path in orderedPaths) {
            if (!byPath.Remove(NormalizePath(path), out var item))
                throw new InvalidOperationException($"Item '{path}' was not found while reordering folder {folderId}.");
            reordered.Add(item);
        }

        folder.Files = reordered;
        NormalizeOrder(folder);
        SaveData(data);
        AppLogger.Info($"Reordered files. FolderId={folderId}, Count={reordered.Count}.");
    }

    public void UpdateFolderColor(int folderId, string argbHex) {
        var data = LoadData();
        var folder = RequireFolder(data, folderId);
        folder.IconColor = argbHex;
        SaveData(data);
        AppLogger.Info($"Updated folder color. FolderId={folderId}, Color='{argbHex}'.");
    }

    private FolioData LoadData() {
        if (TryLoadDataFile(_dataPath, out var data)) return data;
        if (TryLoadDataFile(_backupPath, out var backupData)) {
            TryRestoreBackup();
            return backupData;
        }
        AppLogger.Info("No valid data file found. Starting with empty data.");
        return new FolioData();
    }

    private void SaveData(FolioData data) {
        var directory = Path.GetDirectoryName(_dataPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"folio.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        WriteAllTextDurable(tempPath, JsonSerializer.Serialize(data, Options));

        try {
            if (File.Exists(_dataPath))
                File.Replace(tempPath, _dataPath, _backupPath, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, _dataPath);
            AppLogger.Info($"Saved data file '{_dataPath}'. FolderCount={data.Folders.Count}.");
        }
        finally {
            TryDeleteFile(tempPath);
        }
    }

    private bool TryLoadDataFile(string path, out FolioData data) {
        data = new FolioData();
        if (!File.Exists(path)) return false;
        try {
            data = JsonSerializer.Deserialize<FolioData>(File.ReadAllText(path)) ?? new FolioData();
            AppLogger.Info($"Loaded data file '{path}'. FolderCount={data.Folders.Count}.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            AppLogger.Error($"Failed to load data file '{path}'.", ex);
            return false;
        }
    }

    private void TryRestoreBackup() {
        try {
            File.Copy(_backupPath, _dataPath, overwrite: true);
            AppLogger.Info($"Restored data file from backup '{_backupPath}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Error("Failed to restore backup data file.", ex);
        }
    }

    private static FolioFolder RequireFolder(FolioData data, int folderId) =>
        data.Folders.FirstOrDefault(folder => folder.Id == folderId) ??
        throw new InvalidOperationException($"Folder {folderId} was not found.");

    private static void NormalizeOrder(FolioFolder folder) {
        for (var index = 0; index < folder.Files.Count; index++) folder.Files[index].Order = index;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void WriteAllTextDurable(string path, string contents) {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void TryDeleteFile(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Debug.WriteLine($"Failed to delete temporary data file '{path}': {ex.Message}");
        }
    }
}
