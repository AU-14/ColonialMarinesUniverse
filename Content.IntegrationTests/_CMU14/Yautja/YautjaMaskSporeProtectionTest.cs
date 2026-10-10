#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

// clan masks had the GasMask tag and a filter slot but not the spore protection the popper and spore clouds
// actually check, so a single popper could infect a hunter
[TestFixture]
public sealed class YautjaMaskSporeProtectionTest : GameTest
{
    [Test]
    public async Task EveryYautjaMaskBlocksSpores()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var checkedMasks = 0;
            Assert.Multiple(() =>
            {
                foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryGetComponent<YautjaMaskComponent>(out _, factory))
                        continue;

                    checkedMasks++;
                    Assert.That(proto.TryGetComponent<MycotoxinProtectionComponent>(out var protection, factory), Is.True,
                        $"{proto.ID} doesn't protect against spores");
                    Assert.That(protection?.FullProtection, Is.True, $"{proto.ID} should be a full seal");
                }
            });
            Assert.That(checkedMasks, Is.GreaterThan(0));
        });
    }
}
