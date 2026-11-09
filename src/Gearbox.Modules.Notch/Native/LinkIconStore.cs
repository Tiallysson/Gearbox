using Gearbox.Core;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Native;

public sealed class LinkIconStore(GearboxPaths paths)
{
    private readonly string _directory = Path.Combine(paths.CacheDirectory, "link-icons");

    public string PathFor(string linkId) =>
        QuickLink.IsSafeId(linkId)
            ? Path.Combine(_directory, linkId + ".img")
            : throw new ArgumentException("Id de link inválido.", nameof(linkId));

    public bool Exists(string linkId) => QuickLink.IsSafeId(linkId) && File.Exists(PathFor(linkId));

    public async Task WriteAsync(string linkId, byte[] image)
    {
        if (!QuickLink.IsSafeId(linkId))
        {
            return;
        }

        Directory.CreateDirectory(_directory);
        var path = PathFor(linkId);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, image);
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(string linkId)
    {
        if (!QuickLink.IsSafeId(linkId))
        {
            return;
        }

        try
        {
            File.Delete(PathFor(linkId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
