namespace Darwin.Core.Modules;

/// <summary>
/// Contrato de uma ferramenta do Darwin. O casco descobre os módulos via
/// <c>IEnumerable&lt;IDarwinModule&gt;</c> e cuida de ligar, desligar e isolar falhas.
/// </summary>
/// <remarks>
/// <see cref="Start"/> e <see cref="Stop"/> são sempre chamados na thread de UI.
/// Qualquer exceção lançada por eles é capturada pelo <see cref="ModuleManager"/>,
/// que loga e desabilita o módulo.
/// </remarks>
public interface IDarwinModule
{
    /// <summary>Identificador estável (usado no nome do arquivo de configurações).</summary>
    string Id { get; }

    /// <summary>Nome exibido na navegação e no menu da bandeja (já traduzido).</summary>
    string DisplayName { get; }

    /// <summary>Descrição curta exibida no cabeçalho da página do módulo (já traduzida).</summary>
    string Description { get; }

    /// <summary>Glifo da fonte Segoe Fluent Icons, por exemplo <c>""</c>.</summary>
    string IconGlyph { get; }

    /// <summary>Indica se o módulo está rodando.</summary>
    bool IsEnabled { get; }

    void Start();

    void Stop();

    /// <summary>
    /// Tipo da página de configurações (um <c>Microsoft.UI.Xaml.Controls.Page</c>).
    /// O casco cria a página pelo contêiner de DI, então ela pode receber dependências no construtor.
    /// </summary>
    Type SettingsPageType { get; }
}
