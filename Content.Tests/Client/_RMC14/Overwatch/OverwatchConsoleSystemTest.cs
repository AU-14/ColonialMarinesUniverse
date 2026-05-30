using Content.Client._RMC14.Overwatch;
using NUnit.Framework;

namespace Content.Tests.Client._RMC14.Overwatch;

[TestFixture]
public sealed class OverwatchConsoleSystemTest
{
    [Test]
    public void RelayedAudioSourceIsIgnored()
    {
        var relayedSource = new OverwatchRelayedSoundComponent();
        typeof(OverwatchRelayedSoundComponent)
            .GetField(nameof(OverwatchRelayedSoundComponent.IsRelayAudioSource))!
            .SetValue(relayedSource, true);

        Assert.Multiple(() =>
        {
            Assert.That(OverwatchConsoleSystem.IsRelayAudioSource(null), Is.False);
            Assert.That(OverwatchConsoleSystem.IsRelayAudioSource(new OverwatchRelayedSoundComponent()), Is.False);
            Assert.That(OverwatchConsoleSystem.IsRelayAudioSource(relayedSource), Is.True);
        });
    }
}
