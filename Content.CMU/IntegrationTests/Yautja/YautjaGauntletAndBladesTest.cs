#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaGauntletAndBladesTest : GameTest
{
    private EntityUid _hunter;
    private EntityUid _victim;
    private EntityUid _weapon;

    // player drives a yautja holding the weapon, a human stands one tile east
    private async Task SetupHunter(string weapon)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var minds = SEntMan.System<SharedMindSystem>();
            _hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            _victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            _weapon = SEntMan.SpawnEntity(weapon, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            var mind = minds.GetOrCreateMind(Pair.Player!.UserId).Owner;
            minds.TransferTo(mind, _hunter);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(_hunter, _weapon), Is.True);
            SEntMan.System<SharedCombatModeSystem>().SetInCombatMode(_hunter, true);
        });

        await RunTicksSync(10);
    }

    private NetCoordinates VictimCoords()
    {
        return SEntMan.GetNetCoordinates(SEntMan.GetComponent<TransformComponent>(_victim).Coordinates);
    }

    private float VictimDamage()
    {
        return SEntMan.System<DamageableSystem>().GetTotalDamage(_victim).Float();
    }

    [Test]
    public async Task WristBladesNeverWideSwingButStillHit()
    {
        await SetupHunter("CMUYautjaWristBlades");

        NetEntity weaponNet = default;
        NetEntity victimNet = default;
        NetCoordinates coords = default;
        await Server.WaitPost(() =>
        {
            weaponNet = SEntMan.GetNetEntity(_weapon);
            victimNet = SEntMan.GetNetEntity(_victim);
            coords = VictimCoords();
        });

        // straight from the client, skipping the client-side guard, like a modified client would
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(
            new HeavyAttackEvent(weaponNet, new List<NetEntity> { victimNet }, coords)));
        await RunTicksSync(10);

        await Server.WaitAssertion(() =>
            Assert.That(VictimDamage(), Is.Zero, "wrist blades wide swung"));

        await RunSeconds(1.5f);

        // control: the same pipeline does land a normal click, so the zero above means something
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new LightAttackEvent(victimNet, weaponNet, coords)));
        await RunTicksSync(10);

        await Server.WaitAssertion(() =>
            Assert.That(VictimDamage(), Is.GreaterThan(0f), "wrist blade click didn't land, the wide check proves nothing"));
    }

    [Test]
    public async Task ChainGauntletShoveWhileHeldThrowsTheTarget()
    {
        await SetupHunter("CMUYautjaChainGauntlet");

        NetEntity victimNet = default;
        NetCoordinates coords = default;
        Vector2 start = default;
        await Server.WaitPost(() =>
        {
            Assert.That(SComp<MeleeWeaponComponent>(_weapon).HeldAltDisarm, Is.True,
                "right click with the gauntlet has to shove or the throw finisher can't be reached");

            var gauntlet = SComp<YautjaChainGauntletComponent>(_weapon);
            gauntlet.ComboCounter = gauntlet.DisarmFinisherComboRequired;
            gauntlet.ComboExpiresAt = SGameTiming.CurTime + TimeSpan.FromSeconds(30);
            victimNet = SEntMan.GetNetEntity(_victim);
            coords = VictimCoords();
            start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(_victim);
        });

        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new DisarmAttackEvent(victimNet, coords)));
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var gauntlet = SComp<YautjaChainGauntletComponent>(_weapon);
            var moved = (SEntMan.System<SharedTransformSystem>().GetWorldPosition(_victim) - start).Length();
            Assert.Multiple(() =>
            {
                Assert.That(gauntlet.ComboCounter, Is.Zero, "the finisher didn't fire off a held-gauntlet shove");
                Assert.That(moved, Is.GreaterThan(1f), "the target wasn't thrown");
            });
        });
    }

    [Test]
    public async Task ChainWhipComesBackOffTheGauntlet()
    {
        await SetupHunter("CMUYautjaChainGauntlet");

        EntityUid whip = default;
        await Server.WaitAssertion(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            whip = SEntMan.SpawnEntity("CMUYautjaChainwhip", SEntMan.GetComponent<TransformComponent>(_hunter).Coordinates);
            Assert.That(hands.TryPickupAnyHand(_hunter, whip), Is.True);

            var interaction = SEntMan.System<SharedInteractionSystem>();
            interaction.InteractUsing(_hunter, whip, _weapon, SEntMan.GetComponent<TransformComponent>(_weapon).Coordinates);

            var gauntlet = SEntity<YautjaChainGauntletComponent>(_weapon);
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(gauntlet.Comp.HasChain, Is.True);
                Assert.That(SEntMan.Deleted(whip), Is.False, "wrapping used to delete the whip outright");
                Assert.That(containers.TryGetContainingContainer(whip, out var container) &&
                            container.Owner == _weapon, Is.True);
            });

            Assert.That(SEntMan.System<YautjaChainGauntletSystem>().TryUnwrapChain(gauntlet, _hunter), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(gauntlet.Comp.HasChain, Is.False);
                Assert.That(hands.IsHolding(_hunter, whip), Is.True, "the same whip should land back in hand");
            });
        });
    }
}
