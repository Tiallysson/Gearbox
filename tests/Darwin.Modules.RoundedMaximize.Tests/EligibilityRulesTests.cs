using Darwin.Modules.RoundedMaximize.Logic;

namespace Darwin.Modules.RoundedMaximize.Tests;

public sealed class EligibilityRulesTests
{
    private const uint OwnPid = 100;

    private static readonly WindowSnapshot s_normalWindow = new(
        Hwnd: 0x1234,
        IsVisible: true,
        IsTopLevel: true,
        HasCaption: true,
        HasMaximizeBox: true,
        IsToolWindow: false,
        IsCloaked: false,
        ProcessId: 200,
        ProcessName: "notepad");

    private readonly EligibilityRules _rules = new(OwnPid, ["explorer"]);

    [Fact]
    public void JanelaComum_EhElegivel()
    {
        Assert.Equal(Eligibility.Eligible, _rules.Evaluate(s_normalWindow));
        Assert.True(_rules.IsEligible(s_normalWindow));
    }

    public static TheoryData<WindowSnapshot, Eligibility> Rejeicoes => new()
    {
        { s_normalWindow with { IsVisible = false }, Eligibility.NotVisible },
        { s_normalWindow with { IsTopLevel = false }, Eligibility.NotTopLevel },
        { s_normalWindow with { HasCaption = false }, Eligibility.NoCaption },
        { s_normalWindow with { HasMaximizeBox = false }, Eligibility.NoMaximizeBox },
        { s_normalWindow with { IsToolWindow = true }, Eligibility.ToolWindow },
        { s_normalWindow with { IsCloaked = true }, Eligibility.Cloaked },
        { s_normalWindow with { ProcessId = OwnPid }, Eligibility.OwnProcess },
        { s_normalWindow with { ProcessName = "explorer" }, Eligibility.ExcludedProcess },
        { s_normalWindow with { ProcessName = "Explorer.EXE" }, Eligibility.ExcludedProcess },
    };

    [Theory]
    [MemberData(nameof(Rejeicoes))]
    public void JanelasInelegiveis_SaoRejeitadasComOMotivo(WindowSnapshot window, Eligibility expected)
    {
        Assert.Equal(expected, _rules.Evaluate(window));
    }

    [Fact]
    public void NomeDeProcessoDesconhecido_ContinuaElegivel()
    {
        Assert.True(_rules.IsEligible(s_normalWindow with { ProcessName = null }));
    }

    [Fact]
    public void ListaDeExclusao_AceitaNomesComExtensaoEEspacos()
    {
        var rules = new EligibilityRules(OwnPid, ["  Code.exe ", "\"chrome\"", ""]);

        Assert.True(rules.IsExcluded("code"));
        Assert.True(rules.IsExcluded("CHROME.EXE"));
        Assert.False(rules.IsExcluded("notepad"));
        Assert.False(rules.IsExcluded(null));
    }

    [Theory]
    [InlineData("notepad", "notepad")]
    [InlineData("Notepad.EXE", "notepad")]
    [InlineData("  \"WindowsTerminal.exe\" ", "windowsterminal")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeProcessName(string? input, string expected)
    {
        Assert.Equal(expected, EligibilityRules.NormalizeProcessName(input));
    }
}
