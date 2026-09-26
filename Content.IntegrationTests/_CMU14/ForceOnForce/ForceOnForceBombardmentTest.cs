#pragma warning disable RA0002 // Inspect effect state and arrange bodies for safety regressions.
using System.Numerics;
using System.Reflection;
using Content.Client.Gameplay;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ForceOnForce;
using Content.Server.Mind;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Client.State;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceBombardmentTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private async Task<EntityUid> GroundViewer()
    {
        var map = await Pair.CreateTestMap();
        EntityUid viewer = default;
        await Server.WaitPost(() =>
        {
            var grid = SEntMan.GetComponent<MapGridComponent>(map.GridCoords.EntityId);
            SEntMan.EnsureComponent<RMCPlanetComponent>(SEntMan.GetComponent<TransformComponent>(grid.Owner).MapUid!.Value);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["RMCFloorVehicleInteriorDarkSterile"].TileId);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -22; x <= 22; x++)
            for (var y = -22; y <= 22; y++) maps.SetTile(grid.Owner, grid, new Vector2i(x, y), floor);
            viewer = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            SEntMan.EnsureComponent<MobStateComponent>(viewer);
            Server.System<MindSystem>().ControlMob(ServerSession!.UserId, viewer);
        });
        await Pair.RunUntilSynced();
        // This fixture uses a dummy ticker without a database round. Only the
        // gameplay UI is needed to receive and display the replicated popup.
        await Client.WaitPost(() => Client.ResolveDependency<IStateManager>().RequestStateChange<GameplayState>());
        return viewer;
    }

    [Test]
    public async Task AlarmDeliversLargeRedPopupOverGroundPlayer()
    {
        var viewer = await GroundViewer();
        await Server.WaitAssertion(() => Assert.That(Server.System<ForceOnForceBombardmentSystem>().ForcePreview(viewer, 0), Is.True));
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            var popups = Client.System<Content.Client.Popups.PopupSystem>().WorldLabels;
            var warning = popups.Single(p => p.Text == Loc.GetString("cmu-fof-bombardment-popup"));
            Assert.That(warning.Type, Is.EqualTo(PopupType.LargeCaution), "the caution popup is rendered in red over the player");
        });
    }

    [Test]
    public async Task EachTypeRetainsItsIdentityHasFourPatternsAndOnlyLaserCurtainSpawnsBeams()
    {
        var viewer = await GroundViewer();
        var system = Server.System<ForceOnForceBombardmentSystem>();
        for (var variant = 0; variant < 4; variant++)
        {
            var variations = new HashSet<int>();
            var impacts = new Dictionary<EntityUid, TimeSpan>();
            var sawBeam = false;
            await Server.WaitAssertion(() => Assert.That(system.ForcePreview(viewer, variant), Is.True));
            for (var sample = 0; sample < 80; sample++)
            {
                await Pair.RunSeconds(.5f);
                await Server.WaitAssertion(() =>
                {
                    Assert.That(SEntMan.EntityQuery<FighterFlybyComponent>().Any(), Is.False, "orbital fire must never summon a jet");
                    var beams = SEntMan.EntityQuery<FighterLaserComponent>().Any();
                    if (variant != 1) Assert.That(beams, Is.False, "shells, meteors and concussion blasts are not laser variants");
                    sawBeam |= beams;
                    foreach (var effect in SEntMan.EntityQuery<ForceOnForceBombardmentVisualComponent>())
                    {
                        Assert.That(effect.Variant, Is.EqualTo(variant), "a selected type must not cycle into another type");
                        variations.Add(effect.Variation);
                        if (effect.Impacted) impacts[effect.Owner] = effect.ImpactAt;
                    }
                });
            }
            Assert.That(variations, Is.EquivalentTo(new[] { 0, 1, 2, 3 }), "each barrage exercises all four internal arrangements");
            Assert.That(impacts.Count, Is.GreaterThan(4), "the arrangements must produce multi-hit salvos, not four isolated flashes");
            var times = impacts.Values.OrderBy(t => t).ToArray();
            var gaps = times.Skip(1).Zip(times, (a, b) => Math.Round((a - b).TotalSeconds, 1)).Distinct();
            Assert.That(gaps.Count(), Is.GreaterThan(1), "salvos should contain both close bursts and irregular pauses");
            Assert.That(sawBeam, Is.EqualTo(variant == 1));
        }
    }

    [Test]
    public async Task MovingIntoAnIncomingMeteorCancelsItsImpact()
    {
        var viewer = await GroundViewer();
        EntityUid incoming = default;
        await Server.WaitAssertion(() =>
        {
            var position = SEntMan.GetComponent<TransformComponent>(viewer).Coordinates.Offset(new Vector2(10, 0));
            typeof(ForceOnForceBombardmentSystem).GetMethod("Present", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Server.System<ForceOnForceBombardmentSystem>(),
                    [position, Vector2.UnitX, 2, SProtoMan.Index<ForceOnForceBombardmentPrototype>("CMUFoFBombardment")]);
            var effect = SEntMan.EntityQuery<ForceOnForceBombardmentVisualComponent>().Single();
            incoming = effect.Owner;
            Assert.That(effect.Impacted, Is.False);
            Assert.That(SEntMan.HasComponent<FighterStrikeVisualComponent>(incoming), Is.False);
            // A bystander walks under the incoming effect after the initial location was chosen.
            var body = SEntMan.SpawnEntity(null, position.Offset(new Vector2(0, 8)));
            SEntMan.EnsureComponent<MobStateComponent>(body);
            Server.System<SharedTransformSystem>().SetCoordinates(body, position);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(incoming), Is.True, "cancel the descent when its landing zone becomes occupied");
            Assert.That(SEntMan.EntityQuery<FighterStrikeVisualComponent>().Any(), Is.False);
            Assert.That(SEntMan.EntityQuery<FighterLaserComponent>().Any(), Is.False);
        });
    }
}
