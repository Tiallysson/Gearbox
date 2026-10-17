namespace Darwin.Interop;

/// <summary>Retângulo em pixels físicos (mesmo layout de um <c>RECT</c> do Win32).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromSize(int left, int top, int width, int height) => new(left, top, left + width, top + height);

    /// <summary>Compara com tolerância (o Windows às vezes arredonda 1 px em monitores com escala).</summary>
    public bool NearlyEquals(PixelRect other, int tolerance = 1) =>
        Math.Abs(Left - other.Left) <= tolerance &&
        Math.Abs(Top - other.Top) <= tolerance &&
        Math.Abs(Right - other.Right) <= tolerance &&
        Math.Abs(Bottom - other.Bottom) <= tolerance;

    public override string ToString() => $"({Left},{Top})-({Right},{Bottom}) {Width}x{Height}";
}

/// <summary>Distância entre o retângulo da janela e a moldura visível (bordas invisíveis de redimensionamento).</summary>
public readonly record struct FrameInsets(int Left, int Top, int Right, int Bottom)
{
    public static FrameInsets None => default;
}
