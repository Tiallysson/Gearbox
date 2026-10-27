namespace Gearbox.Modules.Notch.Logic;

/// <summary>Página visível na ilha aberta. O módulo só lê discos e sensores quando a página deles está na tela.</summary>
public enum NotchPage
{
    Media,
    Controls,

    /// <summary>Escolha da saída de áudio (dentro da aba "Controles").</summary>
    Devices,
    Storage,
    System,
}
