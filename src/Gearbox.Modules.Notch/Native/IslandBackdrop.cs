using Gearbox.Interop.Windowing;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace Gearbox.Modules.Notch.Native;

internal sealed partial class IslandBackdrop : SystemBackdrop
{
    private static readonly Windows.UI.Color Transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);

    private bool _enabled;
    private Windows.UI.Color _fallback;
    private ICompositionSupportsSystemBackdrop? _target;
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;

    public static bool IsSupported() => DesktopAcrylicController.IsSupported();

    public void Configure(bool enabled, Windows.UI.Color fallback)
    {
        var opaqueFallback = Windows.UI.Color.FromArgb(255, fallback.R, fallback.G, fallback.B);
        if (enabled == _enabled && opaqueFallback == _fallback)
        {
            return;
        }

        _enabled = enabled;
        _fallback = opaqueFallback;
        ReleaseController();
        AttachController();
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (_target is not null && !ReferenceEquals(_target, connectedTarget))
        {
            ReleaseController();
            ReleaseTarget();
        }

        _target = connectedTarget;
        AttachController();
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        ReleaseController();
        ReleaseTarget();
        ReleaseReference(disconnectedTarget);
    }

    private void AttachController()
    {
        if (_target is null || _controller is not null || !_enabled || !IsSupported())
        {
            return;
        }

        TransparentWindow.EnsureSystemDispatcherQueue();
        _configuration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark,
        };
        _controller = CreateController();
        _controller.AddSystemBackdropTarget(_target);
        _controller.SetSystemBackdropConfiguration(_configuration);
    }

    private void ReleaseController()
    {
        if (_controller is not null)
        {
            if (_target is not null)
            {
                _controller.RemoveSystemBackdropTarget(_target);
            }

            _controller.Dispose();
            _controller = null;
        }

        _configuration = null;
    }

    private void ReleaseTarget()
    {
        var target = _target;
        _target = null;
        ReleaseReference(target);
    }

    private static void ReleaseReference(object? target)
    {
        if (target is IWinRTObject winrt)
        {
            winrt.NativeObject.Dispose();
        }
    }

    private DesktopAcrylicController CreateController() => new()
    {
        Kind = DesktopAcrylicKind.Thin,
        TintColor = Transparent,
        TintOpacity = 0,
        LuminosityOpacity = 0,
        FallbackColor = _fallback,
    };
}
