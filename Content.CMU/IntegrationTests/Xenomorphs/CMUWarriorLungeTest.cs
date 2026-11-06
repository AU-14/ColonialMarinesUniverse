using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUWarriorLungeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LungeReachesVictimOnTranslatedRotatedGrid()
    {
        var map = await Pair.CreateTestMap();
        EntityUid warrior = default;
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalPosition(map.Grid.Owner, new Vector2(30, 40));
            transform.SetLocalRotation(map.Grid.Owner, Angle.FromDegrees(90));
            warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            Lunge(warrior, victim);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<PullerComponent>(warrior).Pulling, Is.EqualTo(victim),
                "A grid's position and rotation must not change which direction the warrior lunges."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task BarricadeOnlyBlocksLungeWhenItIsBeforeVictim(bool inFront)
    {
        var map = await Pair.CreateTestMap();
        EntityUid warrior = default;
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var plating = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = 0; x <= 5; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), plating);
            warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            var barricade = SEntMan.SpawnEntity("CMBarricadeMetal", map.GridCoords.Offset(new Vector2(inFront ? 1.5f : 4.5f, 0.5f)));
            Server.System<SharedTransformSystem>().SetLocalRotation(barricade, Angle.FromDegrees(90));
            Lunge(warrior, victim);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<PullerComponent>(warrior).Pulling,
                Is.EqualTo(inFront ? null : (EntityUid?) victim),
                "Only a barricade on the path to the victim may cancel the grab."));
    }

    [Test]
    public async Task LungeDoesNotRewindMovingVictimToItsStartingPosition()
    {
        var map = await Pair.CreateTestMap();
        EntityUid warrior = default;
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            Lunge(warrior, victim);
            Server.System<SharedTransformSystem>().SetCoordinates(victim,
                map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<PullerComponent>(warrior).Pulling, Is.EqualTo(victim));
            Assert.That(Server.System<SharedTransformSystem>().GetWorldPosition(victim).X,
                Is.LessThan(Server.System<SharedTransformSystem>().ToMapCoordinates(
                    map.GridCoords.Offset(new Vector2(2, 0.5f))).Position.X),
                "Contact must not teleport the victim back to the position stored when the lunge began.");
        });
    }

    private void Lunge(EntityUid warrior, EntityUid victim)
    {
        var ev = new XenoLungeActionEvent
        {
            Entity = victim,
            Target = SEntMan.GetComponent<TransformComponent>(victim).Coordinates,
        };
        SEntMan.EventBus.RaiseLocalEvent(warrior, ev);
    }
}
