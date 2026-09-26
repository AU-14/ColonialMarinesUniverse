using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Fighter;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.ParaDrop;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared._RMC14.Marines;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Fighter;

[TestFixture]
public sealed class FighterGroundTest : GameTest
{
    private EntityUid _hull;
    private EntityUid _pilot;
    private EntityUid _officer;
    private EntityUid _terrain;
    private EntityCoordinates _site;
    private FighterGroundComponent _ground = null!;
    private Entity<FighterAircraftComponent> _aircraft;
    private FighterSystem System => SEntMan.System<FighterSystem>();
    private SharedTransformSystem Transform => SEntMan.System<SharedTransformSystem>();

    private async Task CreateWorld()
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.GridCoords.EntityId);
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tiles["RMCFloorVehicleInteriorDarkSterile"].TileId);
            for (var x = -12; x <= 12; x++)
            for (var y = -12; y <= 12; y++) maps.SetTile(map.GridCoords.EntityId, grid, new Vector2i(x, y), floor);
            _site = new EntityCoordinates(map.GridCoords.EntityId, new Vector2(.5f, .5f));
            _terrain = Server.Transform(_site.EntityId).MapUid!.Value;
            _hull = SEntMan.SpawnEntity("CMUFighterGround", _site);
            _ground = SEntMan.GetComponent<FighterGroundComponent>(_hull);
            Assert.That(_ground.Aircraft, Is.Not.Null);
            _aircraft = (_ground.Aircraft!.Value, SEntMan.GetComponent<FighterAircraftComponent>(_ground.Aircraft.Value));
            _ground.TakeoffTime = TimeSpan.FromSeconds(.6);
            _ground.LandingTime = TimeSpan.FromSeconds(.6);
            _pilot = SEntMan.SpawnEntity("MobHuman", _site.Offset(new Vector2(2.4f, 0)));
            _officer = SEntMan.SpawnEntity("MobHuman", _site.Offset(new Vector2(-2.4f, 0)));
            SEntMan.EnsureComponent<MarineComponent>(_pilot).Faction = "govfor";
            SEntMan.EnsureComponent<MarineComponent>(_officer).Faction = "govfor";
            Assert.That(System.TryBoardGround(_pilot, _hull), Is.True);
            Assert.That(System.TryBoardGround(_officer, _hull), Is.True);
        });
        await Advance(.4); // Let the ordinary buckle cooldown expire.
    }

    private async Task Advance(double seconds) => await Pair.RunTicksSync(
        (int) Math.Ceiling(seconds * Server.ResolveDependency<IGameTiming>().TickRate));

    private void CleanWorld()
    {
        SEntMan.DeleteEntity(_hull);
        if (!SEntMan.Deleted(_pilot)) SEntMan.DeleteEntity(_pilot);
        if (!SEntMan.Deleted(_officer)) SEntMan.DeleteEntity(_officer);
    }

    [TestCase(MobState.Critical)]
    [TestCase(MobState.Dead)]
    public async Task IncapacitatedPilotCannotTakeOffOrControlCountermeasures(MobState state)
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<MobStateSystem>().ChangeMobState(_pilot, state);
            var combat = SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft);
            combat.Incoming = true;
            combat.IncomingAt = Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(10);
            Assert.That(System.TryTakeoff(_pilot), Is.False);
            Assert.That(System.TryDeployFlares(_pilot), Is.False);
            Assert.That(System.TryCoverSector(_pilot, 0), Is.False);
            Assert.That(_ground.State, Is.EqualTo(FighterGroundState.Grounded));
            CleanWorld();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AircraftCrashesAtLifetimeLimitOrWithBlockedRecoveryAndCrewCanEject(bool blocked)
    {
        await CreateWorld();
        EntityUid? blocker = null;
        await Server.WaitAssertion(() => Assert.That(System.TryTakeoff(_pilot), Is.True));
        await Advance(.9);
        await Server.WaitAssertion(() =>
        {
            var combat = SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft);
            Assert.That(combat.RecoveryDuration, Is.EqualTo(TimeSpan.FromMinutes(3)));
            combat.CrashHitLimit = 3;
            combat.HitsTaken = blocked ? 0 : 2;
            combat.Incoming = true;
            combat.IncomingAt = Server.ResolveDependency<IGameTiming>().CurTime;
            if (blocked) blocker = SEntMan.SpawnEntity("MobHuman", _site);
        });
        await Advance(.3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_ground.State, Is.EqualTo(FighterGroundState.Crashing));
            Assert.That(_aircraft.Comp.GroundStateEndsAt - _aircraft.Comp.GroundStateStartedAt, Is.EqualTo(TimeSpan.FromSeconds(8)));
            Assert.That(System.TryConfirmEjection(_pilot), Is.True, "The crash warning arms the ejection handles for both seats.");
            Assert.That(SEntMan.GetComponent<BuckleComponent>(_pilot).BuckledTo, Is.Null);
            Assert.That(SEntMan.GetComponent<BuckleComponent>(_officer).BuckledTo, Is.Null);
        });
        await Advance(8.2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_ground.State, Is.EqualTo(FighterGroundState.Crashed));
            Assert.That(_aircraft.Comp.ForcedRetreat, Is.True);
            Assert.That(_aircraft.Comp.Flying, Is.False);
            Assert.That(Server.Transform(_hull).MapUid, Is.EqualTo(_terrain));
            Assert.That(Server.Transform(_pilot).MapUid, Is.EqualTo(_terrain), "The parachute descent must finish on the ground map.");
            Assert.That(Server.Transform(_officer).MapUid, Is.EqualTo(_terrain));
            if (blocker is { } uid) SEntMan.DeleteEntity(uid);
            CleanWorld();
        });
    }

    [Test]
    public async Task CompletedRepairKeepsLifetimeDamageAndPreventsEarlyTakeoff()
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            var combat = SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft);
            combat.HitsTaken = 1;
            combat.CrashHitLimit = 4;
            _aircraft.Comp.ForcedRetreat = true;
        });
        await Advance(.2);
        await Server.WaitAssertion(() =>
        {
            var combat = SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft);
            var remaining = combat.RecoveryUntil - Server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(remaining.TotalSeconds, Is.InRange(179, 180));
            Assert.That(System.TryTakeoff(_pilot), Is.False);
            combat.RecoveryUntil = Server.ResolveDependency<IGameTiming>().CurTime;
        });
        await Advance(.2);
        await Server.WaitAssertion(() =>
        {
            var combat = SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft);
            Assert.That(_aircraft.Comp.ForcedRetreat, Is.False);
            Assert.That(combat.HitsTaken, Is.EqualTo(1));
            Assert.That(combat.CrashHitLimit, Is.EqualTo(4));
            Assert.That(System.TryTakeoff(_pilot), Is.True);
            CleanWorld();
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LeavingSeatDetachesCrewFromMovingAircraft(bool forcedMovement)
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            foreach (var crew in new[] { _pilot, _officer })
            {
                if (forcedMovement)
                    Transform.SetLocalPosition(crew, new Vector2(12, 0));
                else
                {
                    var click = new Content.Shared.Interaction.InteractHandEvent(crew, crew);
                    SEntMan.EventBus.RaiseLocalEvent(crew, click);
                    Assert.That(click.Handled, Is.True);
                }

                Assert.That(SEntMan.GetComponent<BuckleComponent>(crew).BuckledTo, Is.Null);
                Assert.That(Server.Transform(crew).ParentUid, Is.EqualTo(Server.Transform(_hull).ParentUid));
                var body = SEntMan.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(crew);
                Assert.That(body.BodyType, Is.Not.EqualTo(Robust.Shared.Physics.BodyType.Static));
                Assert.That(body.CanCollide, Is.True);
            }

            var pilotPosition = Transform.GetWorldPosition(_pilot);
            var officerPosition = Transform.GetWorldPosition(_officer);
            Transform.SetCoordinates(_hull, _site.Offset(new Vector2(20, 0)));
            Transform.SetLocalRotation(_hull, Angle.FromDegrees(90));
            Assert.That(Transform.GetWorldPosition(_pilot), Is.EqualTo(pilotPosition));
            Assert.That(Transform.GetWorldPosition(_officer), Is.EqualTo(officerPosition));
            Assert.That(SEntMan.GetComponent<FighterSeatComponent>(_aircraft.Comp.FrontSeat!.Value).Occupant, Is.Null);
            Assert.That(SEntMan.GetComponent<FighterSeatComponent>(_aircraft.Comp.RearSeat!.Value).Occupant, Is.Null);
            CleanWorld();
        });
    }

    [Test]
    public async Task UndamagedAircraftWaitsForObstructedLandingWithoutNeedingRepair()
    {
        await CreateWorld();
        EntityUid blocker = default;
        await Server.WaitAssertion(() => Assert.That(System.TryTakeoff(_pilot), Is.True));
        await Advance(.9);
        await Server.WaitAssertion(() =>
        {
            blocker = SEntMan.SpawnEntity("MobHuman", _site);
            Assert.That(System.TryReturnToGround(_pilot), Is.True);
        });
        await Advance(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_ground.State, Is.EqualTo(FighterGroundState.Returning));
            Assert.That(Server.Transform(_hull).MapUid, Is.Null);
            Assert.That(SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft).RecoveryUntil, Is.EqualTo(TimeSpan.Zero));
            SEntMan.DeleteEntity(blocker);
        });
        await Advance(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_ground.State, Is.EqualTo(FighterGroundState.Grounded));
            Assert.That(SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft).RecoveryUntil, Is.EqualTo(TimeSpan.Zero));
            Assert.That(System.TryTakeoff(_pilot), Is.True, "An undamaged return must not impose damage repairs.");
            CleanWorld();
        });
    }
}
