namespace Gearbox.Modules.Notch.Logic;

public enum SpringIntensity
{
    Gentle,
    Normal,
    Bouncy,
}

public readonly record struct SpringMotion(double Stiffness, double DampingRatio)
{
    private const double SettleTolerance = 0.001;
    private const double MinDampingRatio = 0.05;

    public static SpringMotion For(SpringIntensity intensity) => intensity switch
    {
        SpringIntensity.Gentle => new SpringMotion(170, 1.0),
        SpringIntensity.Bouncy => new SpringMotion(320, 0.6),
        _ => new SpringMotion(260, 0.75),
    };

    public static double MaxOvershoot => Enum.GetValues<SpringIntensity>().Max(i => For(i).PeakOvershoot);

    private double Zeta => Math.Clamp(DampingRatio, MinDampingRatio, 1.0);

    private double Omega => Math.Sqrt(Math.Max(Stiffness, 1.0));

    public double PeakOvershoot => Zeta >= 1.0 ? 0 : Math.Exp(-Math.PI * Zeta / Math.Sqrt(1 - (Zeta * Zeta)));

    public double Progress(double seconds)
    {
        if (seconds <= 0)
        {
            return 0;
        }

        var omega = Omega;
        var zeta = Zeta;
        var decay = Math.Exp(-zeta * omega * seconds);
        if (zeta >= 1.0)
        {
            return 1 - (decay * (1 + (omega * seconds)));
        }

        var damped = omega * Math.Sqrt(1 - (zeta * zeta));
        return 1 - (decay * (Math.Cos(damped * seconds) + (zeta * omega / damped * Math.Sin(damped * seconds))));
    }

    public double Interpolate(double from, double to, double seconds) => from + ((to - from) * Progress(seconds));

    public bool IsSettled(double seconds) => seconds > 0 && Envelope(seconds) < SettleTolerance;

    private double Envelope(double seconds)
    {
        var omega = Omega;
        var zeta = Zeta;
        var decay = Math.Exp(-zeta * omega * seconds);
        return zeta >= 1.0 ? decay * (1 + (omega * seconds)) : decay / Math.Sqrt(1 - (zeta * zeta));
    }
}
