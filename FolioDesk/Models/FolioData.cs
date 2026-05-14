using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolioDesk.Services;

namespace FolioDesk.Models;

public class FolioDataManager {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private FolioData Data { get; } = LoadData();
    private static string DataPath => Path.Combine(App.DataFolder, "folio.json");
    private static string BackupPath => Path.Combine(App.DataFolder, "folio.json.bak");
    private static string TempPath => Path.Combine(App.DataFolder, "folio.json.tmp");

    public FolioDataManager() {

    }

    public FolioFolder CreateFolioFolder(string name) {
        var folder = new FolioFolder {
            Id = GetLastId() + 1,
            Name = name,
            Files = []
        };

        Data.Folders.Add(folder);
        SaveData();
        Directory.CreateDirectory(Path.Combine(App.DataFolder, "icons", $"{folder.Id}"));
        AppLogger.Info($"Created folder. Id={folder.Id}, Name='{folder.Name}'.");

        return folder;
    }

    public void AddFileToFolder(int folderId, FolioItem item) {
        var folder = GetFolioFolder(folderId);
        if (folder == null) return;

        item.Order = folder.Files.Count;
        folder.Files.Add(item);
        
        SaveData();
        AppLogger.Info($"Added file to folder. FolderId={folderId}, Name='{item.Name}', Path='{item.Path}'.");
    }
    

    public FolioFolder? GetFolioFolder(int folderId) {
        return Data.Folders.FirstOrDefault(f => f.Id == folderId);
    }

    public void DeleteFolioFolder(int folderId) {
        var removed = Data.Folders.RemoveAll(f => f.Id == folderId);
        SaveData();
        if (removed == 0)
            AppLogger.Warning($"DeleteFolioFolder: FolderId={folderId} not found.");
        else
            AppLogger.Info($"Deleted folder. FolderId={folderId}.");
    }

    public void ReorderFiles(int folderId, IList<FolioItem> orderedItems) {
        var folder = GetFolioFolder(folderId);
        if (folder == null) return;

        for (var i = 0; i < orderedItems.Count; i++) {
            orderedItems[i].Order = i;
        }
        SaveData();
        AppLogger.Info($"Reordered files. FolderId={folderId}, Count={orderedItems.Count}.");
    }

    public void UpdateFolderColor(int folderId, string argbHex) {
        var folder = GetFolioFolder(folderId);
        if (folder == null) return;
        folder.IconColor = argbHex;
        SaveData();
        AppLogger.Info($"Updated folder color. FolderId={folderId}, Color='{argbHex}'.");
    }

    public void RemoveFileFromFolder(int folderId, FolioItem item) {
        var folder = GetFolioFolder(folderId);
        if (folder == null) return;

        folder.Files.Remove(item);
        for (int i = 0; i < folder.Files.Count; i++) {
            folder.Files[i].Order = i;
        }
        SaveData();
        AppLogger.Info($"Removed file from folder. FolderId={folderId}, Name='{item.Name}', Path='{item.Path}'.");
    }

    private int GetLastId() {
        if (Data.Folders.Count == 0) return 0;
        var folderMaxId = Data.Folders.Max(f => f.Id);
        return folderMaxId;
    }

    private static FolioData LoadData() {
        if (TryLoadDataFile(DataPath, out var data)) {
            AppLogger.Info($"Loaded data file '{DataPath}'. FolderCount={data.Folders.Count}.");
            return data;
        }

        if (TryLoadDataFile(BackupPath, out var backupData)) {
            TryRestoreBackup();
            AppLogger.Info($"Loaded backup data file '{BackupPath}'. FolderCount={backupData.Folders.Count}.");
            return backupData;
        }

        AppLogger.Info("No valid data file found. Starting with empty data.");
        return new FolioData();
    }

    private void SaveData() {
        Directory.CreateDirectory(App.DataFolder);
        var json = JsonSerializer.Serialize(Data, Options);
        WriteAllTextDurable(TempPath, json);

        try {
            if (File.Exists(DataPath)) {
                File.Replace(TempPath, DataPath, BackupPath, ignoreMetadataErrors: true);
            }
            else {
                File.Move(TempPath, DataPath, overwrite: true);
            }
            AppLogger.Info($"Saved data file '{DataPath}'. FolderCount={Data.Folders.Count}.");
        }
        finally {
            TryDeleteTempFile();
        }
    }

    private static bool TryLoadDataFile(string path, out FolioData data) {
        data = new FolioData();
        if (!File.Exists(path)) return false;

        try {
            var json = File.ReadAllText(path);
            data = JsonSerializer.Deserialize<FolioData>(json) ?? new FolioData();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            AppLogger.Error($"Failed to load data file '{path}'.", ex);
            return false;
        }
    }

    private static void TryRestoreBackup() {
        try {
            Directory.CreateDirectory(App.DataFolder);
            File.Copy(BackupPath, DataPath, overwrite: true);
            AppLogger.Info($"Restored data file from backup '{BackupPath}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Error("Failed to restore backup data file.", ex);
        }
    }

    private static void TryDeleteTempFile() {
        try {
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Warning($"Failed to delete temp data file: {ex.Message}");
        }
    }

    private static void WriteAllTextDurable(string path, string contents) {
        using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}

public class FolioData {
    public List<FolioFolder> Folders { get; init; } = [];
}

public class FolioItem {

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }
}
public class FolioFolder {
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<FolioItem> Files { get; set; } = [];

    [JsonPropertyName("iconColor")]
    public string IconColor { get; set; } = "#FFD8D8D8";
}
