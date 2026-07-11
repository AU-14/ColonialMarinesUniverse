using System.Reflection;
using Content.Client._RMC14.Xenonids.Hud;
using Content.Shared._RMC14.Synth;
using NUnit.Framework;

namespace Content.Tests.Client._RMC14.Xenonids;

[TestFixture]
public sealed class XenoHudOverlayTest
{
    [Test]
    public void HumanHealthIconsDoNotHideSynthIconFromXenos()
    {
        var synth = new SynthComponent();
        SetSynthField(synth, nameof(SynthComponent.UseHumanHealthIcons), true);

        Assert.That(XenoHudOverlay.ShouldDrawSynthIcon(synth), Is.True);
    }

    [Test]
    public void ExplicitHideFlagSuppressesSynthIconFromXenos()
    {
        var synth = new SynthComponent();
        SetSynthField(synth, nameof(SynthComponent.HideXenoSynthIcon), true);

        Assert.That(XenoHudOverlay.ShouldDrawSynthIcon(synth), Is.False);
    }

    private static void SetSynthField(SynthComponent synth, string field, bool value)
    {
        typeof(SynthComponent)
            .GetField(field, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(synth, value);
    }
}
