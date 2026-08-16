using System.IO;
using FolioDesk.Application.Abstractions;
using FolioDesk.Services;

namespace FolioDesk.Infrastructure.Files;

public sealed class LocalItemFileStore(string dataFolder) : IItemFileStore {
    private readonly string _iconsRoot = Path.Combine(dataFolder, "icons");

    public bool ItemDirectoryExists(int folderId, string itemName) =>
        Directory.Exists(GetItemDirectory(folderId, itemName));

    public StoredItemFile Store(int folderId, string itemName, string sourcePath) {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException("The item to add was not found.", fullSourcePath);

        var itemDirectory = GetItemDirectory(folderId, itemName);
        Directory.CreateDirectory(itemDirectory);
        var storedPath = Path.Combine(itemDirectory, Path.GetFileName(fullSourcePath));
        var iconPath = Path.Combine(itemDirectory, "icon.png");
        var movedFromDesktop = IsInDesktopDirectory(fullSourcePath);

        if (movedFromDesktop)
            File.Move(fullSourcePath, storedPath, overwrite: false);
        else
            File.Copy(fullSourcePath, storedPath, overwrite: false);

        return new StoredItemFile(fullSourcePath, storedPath, iconPath, itemDirectory, movedFromDesktop);
    }

    public void RollbackStore(StoredItemFile storedItem) {
        try {
            if (storedItem.MovedFromDesktop && File.Exists(storedItem.StoredPath) && !File.Exists(storedItem.SourcePath)) {
                Directory.CreateDirectory(Path.GetDirectoryName(storedItem.SourcePath)!);
                File.Move(storedItem.StoredPath, storedItem.SourcePath, overwrite: false);
            }
            else if (File.Exists(storedItem.StoredPath)) {
                File.Delete(storedItem.StoredPath);
            }
            DeleteDirectoryIfPresent(storedItem.ItemDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Error($"Failed to roll back stored item '{storedItem.StoredPath}'.", ex);
        }
    }

    public ExtractedItemFile MoveToDesktop(string storedPath) {
        var fullStoredPath = Path.GetFullPath(storedPath);
        if (!File.Exists(fullStoredPath))
            throw new FileNotFoundException("The stored item was not found.", fullStoredPath);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var destination = GetUniqueDesktopPath(desktop, Path.GetFileName(fullStoredPath));
        File.Move(fullStoredPath, destination, overwrite: false);
        return new ExtractedItemFile(fullStoredPath, destination, Path.GetDirectoryName(fullStoredPath)!);
    }

    public void RollbackExtraction(ExtractedItemFile extractedItem) {
        try {
            if (File.Exists(extractedItem.DesktopPath) && !File.Exists(extractedItem.StoredPath))
                File.Move(extractedItem.DesktopPath, extractedItem.StoredPath, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Error($"Failed to roll back extracted item '{extractedItem.DesktopPath}'.", ex);
        }
    }

    public void CompleteExtraction(ExtractedItemFile extractedItem) {
        try {
            DeleteDirectoryIfPresent(extractedItem.ItemDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Warning($"Failed to clean extracted item directory '{extractedItem.ItemDirectory}': {ex.Message}");
        }
    }

    public void DeleteFolderStorage(int folderId) {
        var directory = Path.Combine(_iconsRoot, folderId.ToString());
        try {
            DeleteDirectoryIfPresent(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Warning($"Failed to clean folder storage '{directory}': {ex.Message}");
        }
    }

    private string GetItemDirectory(int folderId, string itemName) =>
        Path.Combine(_iconsRoot, folderId.ToString(), itemName);

    private static void DeleteDirectoryIfPresent(string directory) {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static string GetUniqueDesktopPath(string desktopDirectory, string fileName) {
        var candidate = Path.Combine(desktopDirectory, fileName);
        if (!File.Exists(candidate)) return candidate;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var counter = 2; ; counter++) {
            candidate = Path.Combine(desktopDirectory, $"{name} ({counter}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static bool IsInDesktopDirectory(string path) {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(sourceDirectory)) return false;
        return IsSameDirectory(sourceDirectory, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)) ||
               IsSameDirectory(sourceDirectory, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
    }

    private static bool IsSameDirectory(string left, string right) {
        if (string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }
}
