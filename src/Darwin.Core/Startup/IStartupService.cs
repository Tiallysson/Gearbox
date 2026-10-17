namespace Darwin.Core.Startup;

/// <summary>Liga e desliga a inicialização do Darwin junto com o Windows.</summary>
public interface IStartupService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    /// <summary>Se estiver ligado mas apontando para outro executável (app movido), corrige o caminho.</summary>
    void RefreshPathIfEnabled();
}
