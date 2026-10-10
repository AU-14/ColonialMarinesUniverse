using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Fighter;
using Content.Shared.Buckle;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Fighter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Fighter;

[TestFixture]
public sealed class FighterAirCombatTest : GameTest
{
    private Entity<FighterAircraftComponent> _blue;
    private Entity<FighterAircraftComponent> _red;
    private FighterAirCombatComponent _blueCombat = null!;
    private FighterAirCombatComponent _redCombat = null!;
    private EntityUid _bluePilot;
    private EntityUid _blueOfficer;
    private EntityUid _redPilot;
    private EntityUid _redOfficer;
    private EntityUid _outsider;
    private FighterSystem System => SEntMan.System<FighterSystem>();
    private TimeSpan Now => Server.ResolveDependency<IGameTiming>().CurTime;

    private async Task CreateWorld()
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var terrain = Server.Transform(map.GridCoords.EntityId).MapUid!.Value;
            var origin = Server.Transform(map.GridCoords.EntityId).WorldPosition;
            _blue = System.CreateAircraft(terrain, origin);
            _red = System.CreateAircraft(terrain, origin);
            foreach (var aircraft in new[] { _blue, _red })
            {
                aircraft.Comp.Home = origin;
                aircraft.Comp.Battlefield = new Box2(origin - new Vector2(120), origin + new Vector2(120));
                aircraft.Comp.AirspaceRadius = 250;
                aircraft.Comp.Entry = origin + new Vector2(0, 100);
                aircraft.Comp.Exit = origin - new Vector2(0, 100);
                aircraft.Comp.Position = FighterFlight.HoldingPoint(aircraft.Comp);
                var combat = SEntMan.GetComponent<FighterAirCombatComponent>(aircraft);
                // Outcome tests use exact 0/1 odds; timing and speed are covered separately.
                combat.SlowFlareEvasionMultiplier = 1;
                combat.LateFlareEvasionMultiplier = 1;
                combat.CoverageArmTime = TimeSpan.FromSeconds(.5);
                combat.MissileFlightTime = TimeSpan.FromSeconds(1);
                combat.InterceptCooldown = TimeSpan.FromSeconds(5);
                combat.RecoveryDuration = TimeSpan.FromSeconds(2);
            }
            SEntMan.GetComponent<FighterWeaponsComponent>(_blue).Faction = "govfor";
            SEntMan.GetComponent<FighterWeaponsComponent>(_red).Faction = "opfor";
            _blueCombat = SEntMan.GetComponent<FighterAirCombatComponent>(_blue);
            _redCombat = SEntMan.GetComponent<FighterAirCombatComponent>(_red);
            _bluePilot = Crew(_blue, false);
            _blueOfficer = Crew(_blue, true);
            _redPilot = Crew(_red, false);
            _redOfficer = Crew(_red, true);
            _outsider = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
        });
    }

    private EntityUid Crew(Entity<FighterAircraftComponent> aircraft, bool officer)
    {
        var mob = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(aircraft, Vector2.Zero));
        SEntMan.EnsureComponent<MarineComponent>(mob).Faction = SEntMan.GetComponent<FighterWeaponsComponent>(aircraft).Faction;
        Assert.That(System.Board(mob, aircraft, officer), Is.True);
        return mob;
    }

    private void EnterSector(Entity<FighterAircraftComponent> aircraft)
    {
        aircraft.Comp.Position = aircraft.Comp.Home;
        aircraft.Comp.Progress = .5f;
        aircraft.Comp.Phase = FighterPhase.Pass;
        aircraft.Comp.Flying = true;
        aircraft.Comp.Speed = aircraft.Comp.TargetSpeed = 8;
    }

    private async Task Advance(double seconds)
    {
        var ticks = 0;
        await Server.WaitPost(() => ticks = (int) Math.Ceiling(seconds * Server.ResolveDependency<IGameTiming>().TickRate));
        await Pair.RunTicksSync(ticks);
    }

    private void CleanWorld()
    {
        SEntMan.DeleteEntity(Server.Transform(_blue).MapUid!.Value);
        SEntMan.DeleteEntity(Server.Transform(_red).MapUid!.Value);
        SEntMan.DeleteEntity(_outsider);
    }

    [Test]
    public async Task BothCrewCanCoverOnlyTwoValidSectorsAndUnseatedUsersCannot()
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            Assert.That(System.TryCoverSector(_outsider, 4), Is.False);
            Assert.That(System.TryCoverSector(_bluePilot, -1), Is.False);
            Assert.That(System.TryCoverSector(_bluePilot, 9), Is.False);
            Assert.That(System.TryCoverSector(_bluePilot, 1), Is.True);
            Assert.That(System.TryCoverSector(_blueOfficer, 4), Is.True);
            Assert.That(System.TryCoverSector(_blueOfficer, 7), Is.False);
            Assert.That(_blueCombat.CoveredSectors, Is.EquivalentTo(new[] { 1, 4 }));
            Assert.That(System.TryCoverSector(_bluePilot, 1), Is.True);
            Assert.That(System.TryCoverSector(_blueOfficer, 7), Is.True);
            Assert.That(_blueCombat.CoveredSectors, Is.EquivalentTo(new[] { 4, 7 }));
            Assert.That(System.TryDeployFlares(_bluePilot), Is.False, "No defensive charges can be used before a warning.");
        });
        await Advance(.6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryUnbuckle(_blueOfficer, _blueOfficer), Is.True);
            Assert.That(System.TryCoverSector(_blueOfficer, 4), Is.False);
            CleanWorld();
        });
    }

    [TestCase("friendly")]
    [TestCase("unknown")]
    [TestCase("other-map")]
    [TestCase("holding")]
    [TestCase("uncovered")]
    public async Task CoverageDoesNotAttackIneligibleAircraft(string reason)
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            EnterSector(_blue);
            EnterSector(_red);
            Assert.That(System.TryCoverSector(_bluePilot, 4), Is.True);
            switch (reason)
            {
                case "friendly": SEntMan.GetComponent<FighterWeaponsComponent>(_red).Faction = "GOVFOR"; break;
                case "unknown": SEntMan.GetComponent<FighterWeaponsComponent>(_red).Faction = null; break;
                case "other-map": _red.Comp.TerrainMap = Server.Transform(_blue).MapUid!.Value; break;
                case "holding": _red.Comp.Phase = FighterPhase.Holding; _red.Comp.Flying = false; break;
                case "uncovered":
                    _red.Comp.Entry += new Vector2(90, 0);
                    _red.Comp.Exit += new Vector2(90, 0);
                    _red.Comp.Position += new Vector2(90, 0);
                    break;
            }
        });
        await Advance(.8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_redCombat.Incoming, Is.False);
            Assert.That(_blueCombat.InterceptReadyAt, Is.EqualTo(TimeSpan.Zero));
            CleanWorld();
        });
    }

    [TestCase(false, 1f, FighterAirResult.Hit)]
    [TestCase(true, 0f, FighterAirResult.Hit)]
    [TestCase(true, 1f, FighterAirResult.Evaded)]
    public async Task InterceptionGivesOnePilotFlareAttemptAndHitsForceRetreat(bool flare, float chance, FighterAirResult result)
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            EnterSector(_blue);
            EnterSector(_red);
            _redCombat.FlareEvasionChance = chance;
            Assert.That(System.TryCoverSector(_bluePilot, 4), Is.True);
            Assert.That(System.TryCoverSector(_redOfficer, 0), Is.True);
        });
        await Advance(.2);
        await Server.WaitAssertion(() => Assert.That(_redCombat.Incoming, Is.False, "Moving coverage must first arm."));
        await Advance(.5);
        TimeSpan launchedCooldown = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(_redCombat.Incoming, Is.True);
            Assert.That(_redCombat.IncomingSector, Is.EqualTo(4));
            Assert.That(_redCombat.IncomingAt, Is.GreaterThan(Now));
            Assert.That(_redCombat.IncomingStartedAt, Is.LessThanOrEqualTo(Now));
            Assert.That(_redCombat.IncomingDirection.Length(), Is.EqualTo(1).Within(.001));
            Assert.That(SEntMan.GetComponent<FighterEffectsComponent>(_blue).Cues.Count(cue => cue.Kind == FighterEffectKind.Interceptor), Is.EqualTo(1));
            Assert.That(_redCombat.Result, Is.EqualTo(FighterAirResult.None));
            launchedCooldown = _blueCombat.InterceptReadyAt;
            Assert.That(System.TryDeployFlares(_redOfficer), Is.False, "The weapons officer cannot spend the pilot's response.");
            Assert.That(System.TryDeployFlares(_outsider), Is.False);
            Assert.That(SEntMan.GetComponent<FighterEffectsComponent>(_red).Cues.Any(cue => cue.Kind == FighterEffectKind.Flares), Is.False);
            if (flare)
            {
                Assert.That(System.TryDeployFlares(_redPilot), Is.True);
                Assert.That(System.TryDeployFlares(_redPilot), Is.False, "Repeated clicks cannot reroll evasion.");
                Assert.That(_redCombat.Incoming, Is.True, "The result resolves at impact, not at button press.");
                Assert.That(SEntMan.GetComponent<FighterEffectsComponent>(_red).Cues.Count(cue => cue.Kind == FighterEffectKind.Flares), Is.EqualTo(1));
            }
        });
        await Advance(1.1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_redCombat.Incoming, Is.False);
            Assert.That(_redCombat.Result, Is.EqualTo(result));
            var resultEffect = result == FighterAirResult.Hit ? FighterEffectKind.Hit : FighterEffectKind.Evaded;
            Assert.That(SEntMan.GetComponent<FighterEffectsComponent>(_red).Cues.Count(cue => cue.Kind == resultEffect), Is.EqualTo(1));
            Assert.That(System.TryDeployFlares(_redPilot), Is.False);
            Assert.That(_blueCombat.InterceptReadyAt, Is.EqualTo(launchedCooldown), "One aircraft cannot launch repeatedly during its reload.");
            Assert.That(_red.Comp.ForcedRetreat, Is.EqualTo(result == FighterAirResult.Hit));
            if (result == FighterAirResult.Hit)
            {
                Assert.That(_red.Comp.Phase, Is.EqualTo(FighterPhase.Return));
                Assert.That(_redCombat.CoveredSectors, Is.Empty);
                Assert.That(_redCombat.RecoveryUntil, Is.EqualTo(TimeSpan.Zero), "Repairs begin only after reaching the hold point.");
                Assert.That(FighterFlight.Launch(_red.Comp), Is.False);
                Assert.That(System.TryCoverSector(_redPilot, 4), Is.False);
                Assert.That(System.TryFire(_redPilot, out var status), Is.False);
                Assert.That(status, Is.EqualTo(FighterFireStatus.Retreat));
                Assert.That(System.TryLase(_redPilot, out status), Is.False);
                Assert.That(status, Is.EqualTo(FighterFireStatus.Retreat));
                _red.Comp.Speed = _red.Comp.TargetSpeed = 26;
            }
            else Assert.That(_red.Comp.Phase, Is.EqualTo(FighterPhase.Pass));
        });
        if (result == FighterAirResult.Hit)
        {
            var holding = false;
            for (var i = 0; i < 30 && !holding; i++)
            {
                await Advance(1);
                await Server.WaitAssertion(() => holding = _red.Comp.Phase == FighterPhase.Holding);
            }
            await Server.WaitAssertion(() =>
            {
                Assert.That(holding, Is.True, "A hit must bring the aircraft all the way back to its hold point.");
                Assert.That(_red.Comp.ForcedRetreat, Is.True);
                Assert.That(FighterFlight.Launch(_red.Comp), Is.False, "The pilot cannot skip the repair wait.");
            });
            await Advance(2.2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_red.Comp.ForcedRetreat, Is.False);
                Assert.That(SEntMan.GetComponent<FighterEffectsComponent>(_red).Cues.Any(cue => cue.Kind == FighterEffectKind.Repaired), Is.True);
                Assert.That(FighterFlight.Launch(_red.Comp), Is.True);
            });
        }
        await Server.WaitAssertion(CleanWorld);
    }

    [Test]
    public async Task FlareAttemptCapturesDeploymentTimingAndSpeed()
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            EnterSector(_blue);
            EnterSector(_red);
            _blueCombat.MissileFlightTime = TimeSpan.FromSeconds(5);
            _redCombat.SlowFlareEvasionMultiplier = .5f;
            _redCombat.LateFlareEvasionMultiplier = .25f;
            Assert.That(System.TryCoverSector(_bluePilot, 4), Is.True);
        });
        await Advance(.8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_redCombat.Incoming, Is.True);
            _red.Comp.Speed = _red.Comp.MaximumSpeed;
            var earlyFast = FighterAirCombat.FlareEvasion(_red.Comp, _redCombat, Now);
            _red.Comp.Speed = _red.Comp.MinimumSpeed;
            // Simulate a pilot responding with only one second left in the original five-second warning.
            _redCombat.IncomingStartedAt = Now - TimeSpan.FromSeconds(4);
            _redCombat.IncomingAt = Now + TimeSpan.FromSeconds(1);
            Assert.That(System.TryDeployFlares(_redPilot), Is.True);
            var deployedChance = _redCombat.DeployedFlareEvasionChance;
            Assert.That(deployedChance, Is.EqualTo(.09f).Within(.0001f));
            Assert.That(deployedChance, Is.LessThan(earlyFast));
            _red.Comp.Speed = _red.Comp.MaximumSpeed;
            Assert.That(System.TryDeployFlares(_redPilot), Is.False);
            Assert.That(_redCombat.DeployedFlareEvasionChance, Is.EqualTo(deployedChance),
                "speeding up after releasing flares cannot improve or reroll the spent attempt");
            CleanWorld();
        });
    }

    [Test]
    public async Task OppositeSideCanLaunchTheSameAutomaticInterceptor()
    {
        await CreateWorld();
        await Server.WaitAssertion(() =>
        {
            EnterSector(_red);
            EnterSector(_blue);
            Assert.That(System.TryCoverSector(_redOfficer, 4), Is.True);
        });
        await Advance(.8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_blueCombat.Incoming, Is.True);
            Assert.That(_redCombat.Incoming, Is.False);
            Assert.That(_redCombat.LastLaunchSector, Is.EqualTo(4));
            CleanWorld();
        });
    }
}
