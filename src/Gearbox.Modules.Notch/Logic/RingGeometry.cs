namespace Gearbox.Modules.Notch.Logic;

/// <summary>
/// Arco de um anel de progresso: começa no topo e anda no sentido horário. Coordenadas no quadrado do anel
/// (0 a <c>size</c>), já descontada metade da espessura do traço para ele não ser cortado na borda.
/// </summary>
public readonly record struct RingArc(double StartX, double StartY, double EndX, double EndY, double Radius, bool IsLargeArc)
{
    /// <summary>0%: não desenha o arco (só o trilho).</summary>
    public bool IsEmpty { get; init; }

    /// <summary>100%: um arco não fecha um círculo (início = fim); desenha o círculo inteiro.</summary>
    public bool IsFull { get; init; }
}

public static class RingGeometry
{
    public static RingArc For(double percent, double size, double thickness)
    {
        var p = double.IsNaN(percent) ? 0 : Math.Clamp(percent, 0, 100);
        var radius = Math.Max(0, (size - thickness) / 2);
        var center = size / 2;
        var startX = center;
        var startY = center - radius;

        if (p <= 0)
        {
            return new RingArc(startX, startY, startX, startY, radius, false) { IsEmpty = true };
        }

        if (p >= 99.95)
        {
            return new RingArc(startX, startY, startX, startY, radius, true) { IsFull = true };
        }

        var angle = p / 100 * 2 * Math.PI;
        return new RingArc(
            startX,
            startY,
            center + (radius * Math.Sin(angle)),
            center - (radius * Math.Cos(angle)),
            radius,
            IsLargeArc: p > 50);
    }
}
