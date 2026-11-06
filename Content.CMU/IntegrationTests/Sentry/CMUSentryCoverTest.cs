using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Sentry;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Interaction;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.NPC.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Sentry;

[TestFixture]
public sealed class CMUSentryCoverTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task SentryShootsPastLowCoverButNotWalls()
    {
        var map = await Pair.CreateTestMap();
        EntityUid sentry = default;
        EntityUid hostile = default;
        EntityUid wall = default;
        var initialAmmo = 0;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -1; x <= 7; x++)
            for (var y = -1; y <= 1; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            sentry = SEntMan.SpawnEntity("RMCSentry", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var cover = SEntMan.SpawnEntity("CMBarricadeMetal", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalRotation(sentry, Direction.East.ToAngle());
            transform.SetLocalRotation(cover, Direction.East.ToAngle());
            transform.AnchorEntity(sentry);
            hostile = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5.5f, 0.5f)));
            var factions = Server.System<NpcFactionSystem>();
            factions.ClearFactions(hostile);
            factions.AddFaction(hostile, "OPFOR");
            Server.System<GunIFFSystem>().SetUserFaction(hostile, "OPFOR");
            Assert.That(Server.System<SentryTargetingSystem>().TryApplyDefaultFaction(sentry, "GOVFOR"), Is.True);
            var component = SEntMan.GetComponent<SentryComponent>(sentry);
            Server.System<RMCInteractionSystem>().SetMaxRotation(sentry, Direction.East.ToAngle(), component.MaxDeviation);
            Assert.That(Server.System<SentrySystem>().TrySetMode((sentry, component), SentryMode.On), Is.True);
            initialAmmo = Ammo();
        });

        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(), Is.LessThan(initialAmmo), "Low cover must not prevent acquiring a target beyond the barricade.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(hostile), Is.GreaterThan(FixedPoint2.Zero),
                "Actual projectiles must pass the aligned nearby barricade and reach the target.");
            // The first body is also ballistic cover; keep the next phase limited to the wall under test.
            SEntMan.DeleteEntity(hostile);
            wall = SEntMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(3.5f, 0.5f)));
            // The wall must protect a fresh, otherwise valid target.
            hostile = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5.5f, 0.5f)));
            var factions = Server.System<NpcFactionSystem>();
            factions.ClearFactions(hostile);
            factions.AddFaction(hostile, "OPFOR");
            Server.System<GunIFFSystem>().SetUserFaction(hostile, "OPFOR");
        });

        // Let the firing system refresh its cached LOS before checking that it holds fire.
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => initialAmmo = Ammo());
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo(), Is.EqualTo(initialAmmo), "The same gun must stop firing when a real wall blocks the path.");
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(hostile), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(SEntMan.GetComponent<MobStateComponent>(hostile).CurrentState, Is.EqualTo(MobState.Alive));
            SEntMan.DeleteEntity(wall);
        });
        // A blocked gun can enter the normal idle plan, whose wait lasts up to seven seconds.
        await Pair.RunSeconds(10);
        await Server.WaitAssertion(() => Assert.That(Ammo(), Is.LessThan(initialAmmo),
            "Removing the wall must allow the same still-living target to be acquired again."));

        int Ammo()
        {
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(sentry, ref count);
            return count.Count;
        }
    }
}
