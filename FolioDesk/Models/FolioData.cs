using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolioDesk.Models;

public class FolioDataManager {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private FolioData Data { get; } = LoadData();

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

        return folder;
    }

    public void AddFileToFolder(int folderId, FolioItem item) {
        var folder = GetFolioFolder(folderId);
        if (folder == null) return;

        item.Order = folder.Files.Count;
        folder.Files.Add(item);
        
        SaveData();
    }
    

    public FolioFolder? GetFolioFolder(int folderId) {
        return Data.Folders.FirstOrDefault(f => f.Id == folderId);
    }

    public void DeleteFolioFolder(int folderId) {
        Data.Folders.RemoveAll(f => f.Id == folderId);
        SaveData();
    }

    public void ReorderFiles(int folderId, IList<FolioItem> orderedItems) {
        var folder = GetFolioFolder(folderId);
        if (folder == null) return;

        for (int i = 0; i < orderedItems.Count; i++) {
            orderedItems[i].Order = i;
        }
        SaveData();
    }

    private int GetLastId() {
        if (Data.Folders.Count == 0) return 0;
        var folderMaxId = Data.Folders.Max(f => f.Id);
        return folderMaxId;
    }

    private static FolioData LoadData() {
        var path = Path.Combine(App.DataFolder, "folio.json");
        if (!File.Exists(path)) return new FolioData();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FolioData>(json) ?? new FolioData();
    }

    private void SaveData() {
        Directory.CreateDirectory(App.DataFolder);
        var json = JsonSerializer.Serialize(Data, Options);
        File.WriteAllText(Path.Combine(App.DataFolder, "folio.json"), json);
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
}
