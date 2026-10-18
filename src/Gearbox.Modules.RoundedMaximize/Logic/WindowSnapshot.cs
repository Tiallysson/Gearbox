namespace Gearbox.Modules.RoundedMaximize.Logic;

/// <summary>Fotografia de uma janela com tudo o que a regra de elegibilidade precisa.</summary>
public sealed record WindowSnapshot(
    nint Hwnd,
    bool IsVisible,
    bool IsTopLevel,
    bool HasCaption,
    bool HasMaximizeBox,
    bool IsToolWindow,
    bool IsCloaked,
    uint ProcessId,
    string? ProcessName);
