using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class SpringMotionTests
{
    [Theory]
    [InlineData(SpringIntensity.Gentle)]
    [InlineData(SpringIntensity.Normal)]
    [InlineData(SpringIntensity.Bouncy)]
    public void Progress_ComecaEmZeroETerminaNoAlvo(SpringIntensity intensity)
    {
        var motion = SpringMotion.For(intensity);

        Assert.Equal(0, motion.Progress(0));
        Assert.Equal(1, motion.Progress(5), precision: 3);
    }

    [Theory]
    [InlineData(SpringIntensity.Gentle)]
    [InlineData(SpringIntensity.Normal)]
    [InlineData(SpringIntensity.Bouncy)]
    public void IsSettled_AssentaEmMenosDeUmSegundo(SpringIntensity intensity)
    {
        var motion = SpringMotion.For(intensity);

        Assert.False(motion.IsSettled(0));
        Assert.False(motion.IsSettled(0.05));
        Assert.True(motion.IsSettled(1.0));
    }

    [Fact]
    public void PeakOvershoot_CresceComAIntensidade()
    {
        var gentle = SpringMotion.For(SpringIntensity.Gentle).PeakOvershoot;
        var normal = SpringMotion.For(SpringIntensity.Normal).PeakOvershoot;
        var bouncy = SpringMotion.For(SpringIntensity.Bouncy).PeakOvershoot;

        Assert.True(gentle <= normal);
        Assert.True(normal < bouncy);
        Assert.Equal(bouncy, SpringMotion.MaxOvershoot);
    }

    [Theory]
    [InlineData(SpringIntensity.Gentle)]
    [InlineData(SpringIntensity.Normal)]
    [InlineData(SpringIntensity.Bouncy)]
    public void Progress_NuncaPassaDoPicoCalculado(SpringIntensity intensity)
    {
        var motion = SpringMotion.For(intensity);
        var peak = Enumerable.Range(0, 2000).Max(i => motion.Progress(i / 1000.0));

        Assert.True(peak <= 1 + motion.PeakOvershoot + 1e-6);
    }

    [Fact]
    public void Interpolate_VaiDoInicioAoFim()
    {
        var motion = SpringMotion.For(SpringIntensity.Normal);

        Assert.Equal(180, motion.Interpolate(180, 420, 0));
        Assert.Equal(420, motion.Interpolate(180, 420, 5), precision: 2);
    }
}
