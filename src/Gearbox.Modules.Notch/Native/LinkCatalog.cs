using Gearbox.Core.Settings;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Notch.Native;

public sealed class LinkCatalog
{
    private const int IconSize = 64;

    private readonly ISettingsService<LinksSettings> _settings;
    private readonly LinkIconStore _icons;
    private readonly ILogger<LinkCatalog> _logger;
    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _failed = [];
    private List<QuickLink>? _links;

    public LinkCatalog(ISettingsService<LinksSettings> settings, LinkIconStore icons, ILogger<LinkCatalog> logger)
    {
        _settings = settings;
        _icons = icons;
        _logger = logger;
        _settings.Changed += OnSettingsChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<QuickLink> Links => _links ??= QuickLinkList.Safe(_settings.Load().Links);

    public string? IconPath(string linkId) => _icons.Exists(linkId) ? _icons.PathFor(linkId) : null;

    public bool Add(QuickLink link)
    {
        var next = QuickLinkList.Add(Links, link);
        if (next.Count == Links.Count || !Save(next))
        {
            return false;
        }

        _ = StoreIconAsync(link);
        return true;
    }

    public void Remove(string linkId)
    {
        if (Save(QuickLinkList.Remove(Links, linkId)))
        {
            _icons.Delete(linkId);
        }
    }

    public void Move(string linkId, int offset) => Save(QuickLinkList.Move(Links, linkId, offset));

    public void EnsureIcons()
    {
        foreach (var link in Links)
        {
            if (!_icons.Exists(link.Id) && !_failed.Contains(link.Id))
            {
                _ = StoreIconAsync(link);
            }
        }
    }

    private bool Save(List<QuickLink> links)
    {
        try
        {
            _settings.Save(new LinksSettings { Links = links });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar os links do Notch");
            return false;
        }
    }

    private async Task StoreIconAsync(QuickLink link)
    {
        if (!_pending.Add(link.Id))
        {
            return;
        }

        try
        {
            var image = await ReadIconAsync(link);
            if (image is null)
            {
                _failed.Add(link.Id);
                _logger.LogInformation("Sem ícone para o link {Target}", link.Target);
                return;
            }

            if (Links.Any(l => l.Id == link.Id))
            {
                await _icons.WriteAsync(link.Id, image);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _failed.Add(link.Id);
            _logger.LogWarning(ex, "Não foi possível guardar o ícone do link {Target}", link.Target);
        }
        finally
        {
            _pending.Remove(link.Id);
        }
    }

    private static async Task<byte[]?> ReadIconAsync(QuickLink link)
    {
        if (link.Kind == QuickLinkKind.App)
        {
            var icon = await AppIconQueue.GetAsync(link.Target, IconSize);
            return icon is null ? null : await ShellIconEncoder.ToPngAsync(icon);
        }

        return Uri.TryCreate(link.Target, UriKind.Absolute, out var page)
            ? await FaviconFetcher.TryDownloadAsync(page, CancellationToken.None)
            : null;
    }

    private void OnSettingsChanged(object? sender, LinksSettings e)
    {
        _links = QuickLinkList.Safe(e.Links);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
