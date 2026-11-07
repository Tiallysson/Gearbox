namespace Gearbox.Interop.Geometry;

public readonly record struct PixelCircle(int CenterX, int CenterY, int Radius)
{
    public bool IsEmpty => Radius <= 0;
}
