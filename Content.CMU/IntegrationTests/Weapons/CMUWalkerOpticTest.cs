using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Visor;
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.IntegrationTests.CMU14.Weapons;

public sealed class CMUWalkerOpticTest : GameTest
{
    [Test]
    public async Task SquadOpticRespectsDisabledWeaponIffAgainstWalker()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var iff = Server.System<GunIFFSystem>();
            var hands = Server.System<SharedHandsSystem>();
            var shooter = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var target = map.GridCoords.Offset(new Vector2(3, 0));
            var victim = SSpawnAtPosition("CMMobHuman", target);
            var popper = SSpawnAtPosition("CMU14XenoPopper", target.Offset(new Vector2(0, 3)));
            iff.SetUserFaction(shooter, "GOVFOR");
            iff.SetUserFaction(victim, "GOVFOR");
            SEntMan.EnsureComponent<CMUSquadVisorWearerComponent>(shooter);

            var rifle = SSpawnAtPosition("WeaponRifleM4SPR", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(shooter, rifle), Is.True);
            var attempt = new AttemptShootEvent(shooter, null, map.GridCoords, target);
            SEntMan.EventBus.RaiseLocalEvent(rifle, ref attempt);
            Assert.That(attempt.Cancelled, Is.True);
            Assert.That(hands.TryDrop(shooter, rifle), Is.True);

            var switchable = SSpawnAtPosition("RMCWeaponRifleM59A", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(shooter, switchable), Is.True);
            var actions = Server.System<SharedActionsSystem>();
            var toggle = actions.GetAction(SComp<IFFToggleComponent>(switchable).Action);
            actions.PerformAction(shooter, toggle!.Value);
            Assert.That(SComp<GunIFFComponent>(switchable).Enabled, Is.False);
            attempt = new AttemptShootEvent(shooter, null, map.GridCoords, target);
            SEntMan.EventBus.RaiseLocalEvent(switchable, ref attempt);
            Assert.That(attempt.Cancelled, Is.False, attempt.Message);
            Assert.That(hands.TryDrop(shooter, switchable), Is.True);

            // BUG-344: once walker'd they're no friend of the optic's either, the dogtag IFF used to come along
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new CMUMycotoxinInjectDoReanimateEvent(victim, popper));
            Assert.That(SEntMan.HasComponent<CMUPathogenWalkerComponent>(victim), Is.True);
            var second = SSpawnAtPosition("WeaponRifleM4SPR", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(shooter, second), Is.True);
            attempt = new AttemptShootEvent(shooter, null, map.GridCoords, target);
            SEntMan.EventBus.RaiseLocalEvent(second, ref attempt);
            Assert.That(attempt.Cancelled, Is.False, attempt.Message);
        });
    }
}
