namespace FolioDesk.Application.Abstractions;

public readonly record struct PhysicalScreenPoint(int X, int Y);

public interface IDesktopIconLocator {
    bool TryLocate(int folderId, out PhysicalScreenPoint position);
}
