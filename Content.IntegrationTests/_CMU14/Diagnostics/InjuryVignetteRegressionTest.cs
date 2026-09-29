using Content.Client.DamageOverlay;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DamageOverlay;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Robust.Client.Graphics;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class InjuryVignetteRegressionTest
{
    [Test]
    public async Task InjuredHumanRegistersAndUpdatesDamageOverlay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        NetEntity netHuman = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            netHuman = entities.GetNetEntity(human);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, human);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 50;
            entities.System<DamageableSystem>().TryChangeDamage(human, damage, ignoreResistances: true);
            Assert.That(entities.GetComponent<DamageOverlayComponent>(human).PainLevel, Is.GreaterThan(0));
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(pair.Client.ResolveDependency<IOverlayManager>().HasOverlay<DamageOverlay>(), Is.True);
            var humanClient = pair.Client.EntMan.GetEntity(netHuman);
            var damage = pair.Client.EntMan.GetComponent<DamageOverlayComponent>(humanClient);
            Assert.That(damage.PainLevel, Is.GreaterThan(0));
            var pain = pair.Client.EntMan.GetComponent<PainShockComponent>(humanClient);
            pain.Tier = PainTier.None;
            var overlay = pair.Client.ResolveDependency<IOverlayManager>().GetOverlay<DamageOverlay>();
            overlay.UpdateLevels(damage);
            Assert.That(overlay.PainLevel, Is.EqualTo(damage.PainLevel).And.GreaterThan(0),
                "a zero effective pain tier must not erase the injury vignette");
        });
        await pair.CleanReturnAsync();
    }
}
