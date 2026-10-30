using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Yautja;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.Physics.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaCasterPresentationTest
{
    [Test]
    public async Task ModeSwitchesDrivePredictedAndAuthoritativeShotPresentation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid caster = default;
        NetEntity hunterNet = default;
        NetEntity casterNet = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            hunter = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.EnsureComponent<YautjaComponent>(hunter);
            var bracer = entities.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            Assert.That(entities.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            caster = entities.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);
            var stored = entities.GetComponent<YautjaStoredGearComponent>(caster);
            stored.Bracer = bracer;
            stored.Deployed = true;
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(hunter, caster), Is.True);
            Assert.That(hands.IsHolding(hunter, caster, out var hand), Is.True);
            hands.TrySetActiveHand(hunter, hand);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), hunter);
            hunterNet = entities.GetNetEntity(hunter);
            casterNet = entities.GetNetEntity(caster);
        });
        await pair.ReallyBeIdle(10);

        var sounds = new[] { "pred_plasmacaster_fire.wav", "pulse.wav", "pred_lasercannon.wav", "pulse.wav" };
        for (var mode = 0; mode < 4; mode++)
        {
            if (mode > 0)
                await pair.RunTicksSync(pair.SecondsToTicks(8.2f));
            var expected = (CMUYautjaPlasmaMode) mode;
            var sound = sounds[mode];
            await server.WaitAssertion(() =>
            {
                if (expected == CMUYautjaPlasmaMode.Lethal)
                    server.EntMan.EventBus.RaiseLocalEvent(caster, new UniqueActionEvent(hunter));
                else if (expected != CMUYautjaPlasmaMode.Stun)
                    server.EntMan.EventBus.RaiseLocalEvent(caster, new UseInHandEvent(hunter));
            });
            await pair.ReallyBeIdle(5);
            await client.WaitAssertion(() =>
            {
                var projectile = ShootAndAssert(client.EntMan, client.EntMan.GetEntity(hunterNet),
                    client.EntMan.GetEntity(casterNet), expected, sound);
                client.EntMan.System<CMUYautjaVisualsSystem>().FrameUpdate(0);
                var light = client.EntMan.GetComponent<Robust.Client.GameObjects.PointLightComponent>(projectile);
                var lethal = expected is CMUYautjaPlasmaMode.Lethal or CMUYautjaPlasmaMode.Eradicator;
                Assert.That(lethal ? light.Color.R > light.Color.B : light.Color.B > light.Color.R,
                    Is.True, "The actual shot must illuminate the world red for lethal modes and blue for nonlethal modes.");
            });
            await server.WaitAssertion(() =>
            {
                ShootAndAssert(server.EntMan, hunter, caster, expected, sound);
                Assert.That(server.EntMan.EntityQuery<CMUYautjaEffectComponent>()
                    .Any(effect => effect.Kind == CMUYautjaEffect.PlasmaMuzzle && effect.Target == hunter && effect.PlasmaMode == expected), Is.True);
            });
        }
        await pair.CleanReturnAsync();
    }

    private static EntityUid ShootAndAssert(IEntityManager entities, EntityUid hunter, EntityUid caster,
        CMUYautjaPlasmaMode mode, string sound)
    {
        var gun = entities.GetComponent<GunComponent>(caster);
        var before = entities.EntityQuery<AudioComponent>().Select(audio => audio.Owner).ToHashSet();
        var offset = mode switch
        {
            CMUYautjaPlasmaMode.Stun => new Vector2(8, 0),
            CMUYautjaPlasmaMode.Immobilizer => new Vector2(0, 8),
            CMUYautjaPlasmaMode.Lethal => new Vector2(-8, 0),
            _ => new Vector2(5, -5),
        };
        var target = entities.GetComponent<TransformComponent>(hunter).Coordinates.Offset(offset);
        var fired = entities.System<SharedGunSystem>().AttemptShoot((caster, gun), hunter, target);
        Assert.That(fired, Has.Count.EqualTo(1), $"{mode} must fire a single projectile.");
        var visual = entities.GetComponent<CMUYautjaPlasmaVisualComponent>(fired![0]);
        Assert.That(visual.Mode, Is.EqualTo(mode), "The shot must retain the mode that was actually fired.");
        var flight = entities.GetComponent<PhysicsComponent>(fired[0]).LinearVelocity;
        Assert.That(Vector2.Dot(visual.Direction, Vector2.Normalize(flight)), Is.GreaterThan(.999),
            "The drawn bolt and trail must follow the actual projectile rather than its sprite rotation offset.");
        Assert.That(entities.EntityQuery<AudioComponent>().Where(audio => !before.Contains(audio.Owner))
                .Select(audio => audio.FileName).Any(path => path.EndsWith(sound)),
            Is.True, $"{mode} must create its firing sound through the gun audio path.");
        return fired[0];
    }

    [Test]
    public async Task DeployedCasterReplicatesAimOutsideCombatAndRejectsAnotherCastersInput()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        NetEntity casterNet = default;
        NetEntity otherNet = default;
        MapCoordinates target = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            hunter = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.EnsureComponent<YautjaComponent>(hunter);
            var caster = entities.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);
            var other = entities.SpawnEntity("CMUYautjaPlasmaCaster", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(hunter, caster), Is.True);
            Assert.That(hands.IsHolding(hunter, caster, out var hand), Is.True);
            hands.TrySetActiveHand(hunter, hand);
            entities.System<SharedCombatModeSystem>().SetInCombatMode(hunter, false);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), hunter);
            casterNet = entities.GetNetEntity(caster);
            otherNet = entities.GetNetEntity(other);
            target = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords.Offset(new Vector2(4, 2)));
        });
        await pair.ReallyBeIdle(10);
        await client.WaitPost(() => client.ResolveDependency<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new CMUYautjaCasterAimEvent(casterNet, target)));
        await pair.ReallyBeIdle(5);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<CMUYautjaCasterAimComponent>(hunter).Position,
            Is.EqualTo(target.Position)));
        await client.WaitPost(() => client.ResolveDependency<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new CMUYautjaCasterAimEvent(otherNet, new MapCoordinates(target.Position + Vector2.One, target.MapId))));
        await pair.ReallyBeIdle(5);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<CMUYautjaCasterAimComponent>(hunter).Position,
            Is.EqualTo(target.Position), "A different caster must not redirect this hunter's sight."));
        await pair.CleanReturnAsync();
    }
}
