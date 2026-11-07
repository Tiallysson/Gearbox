using Gearbox.Core;

namespace Gearbox.Modules.Notch.Native;

public sealed class LinkIconStore(GearboxPaths paths)
{
    private readonly string _directory = Path.Combine(paths.CacheDirectory, "link-icons");

    public string PathFor(string linkId) => Path.Combine(_directory, linkId + ".img");

    public bool Exists(string linkId) => File.Exists(PathFor(linkId));

    public async Task WriteAsync(string linkId, byte[] image)
    {
        Directory.CreateDirectory(_directory);
        var path = PathFor(linkId);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, image);
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(string linkId)
    {
        try
        {
            File.Delete(PathFor(linkId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
