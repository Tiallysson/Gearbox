namespace Darwin.Modules.RoundedMaximize.Logic;

public enum Eligibility
{
    Eligible,
    NotVisible,
    NotTopLevel,
    NoCaption,
    NoMaximizeBox,
    ToolWindow,
    Cloaked,
    OwnProcess,
    ExcludedProcess,
}

/// <summary>Decide se uma janela maximizada deve virar "pseudo-maximizada".</summary>
public sealed class EligibilityRules
{
    private readonly uint _ownProcessId;
    private readonly HashSet<string> _excluded;

    public EligibilityRules(uint ownProcessId, IEnumerable<string> excludedProcesses)
    {
        _ownProcessId = ownProcessId;
        _excluded = new HashSet<string>(
            excludedProcesses.Select(NormalizeProcessName).Where(n => n.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>"Explorer.EXE " → "explorer". Aceita com ou sem extensão.</summary>
    public static string NormalizeProcessName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim().Trim('"');
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        return trimmed.ToLowerInvariant();
    }

    public bool IsExcluded(string? processName) =>
        processName is not null && _excluded.Contains(NormalizeProcessName(processName));

    public Eligibility Evaluate(WindowSnapshot window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.ProcessId == _ownProcessId)
        {
            return Eligibility.OwnProcess;
        }

        if (!window.IsVisible)
        {
            return Eligibility.NotVisible;
        }

        if (!window.IsTopLevel)
        {
            return Eligibility.NotTopLevel;
        }

        if (!window.HasCaption)
        {
            return Eligibility.NoCaption;
        }

        if (!window.HasMaximizeBox)
        {
            return Eligibility.NoMaximizeBox;
        }

        if (window.IsToolWindow)
        {
            return Eligibility.ToolWindow;
        }

        if (window.IsCloaked)
        {
            return Eligibility.Cloaked;
        }

        // Processo ilegível (ex.: protegido) não tem como estar na lista, então segue elegível.
        if (IsExcluded(window.ProcessName))
        {
            return Eligibility.ExcludedProcess;
        }

        return Eligibility.Eligible;
    }

    public bool IsEligible(WindowSnapshot window) => Evaluate(window) == Eligibility.Eligible;
}
