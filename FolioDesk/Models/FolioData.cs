using System.Text.Json.Serialization;

namespace FolioDesk.Models;

public sealed class FolioData {
    public List<FolioFolder> Folders { get; init; } = [];
}

public sealed class FolioItem {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public sealed class FolioFolder {
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<FolioItem> Files { get; set; } = [];

    [JsonPropertyName("iconColor")]
    public string IconColor { get; set; } = "#FFD8D8D8";
}
