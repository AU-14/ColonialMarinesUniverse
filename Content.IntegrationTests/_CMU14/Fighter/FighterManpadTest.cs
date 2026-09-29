using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Fighter;
using Content.Shared.Actions;
using Content.Shared._RMC14.Marines;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Fighter;

[TestFixture]
public sealed class FighterManpadTest : GameTest
{
    private Entity<FighterAircraftComponent> _aircraft;
    private FighterAirCombatComponent _combat = null!;
    private EntityUid _launcher;
    private FighterManpadComponent _manpad = null!;
    private EntityUid _terrain;
    private EntityUid _pilot;
    private EntityUid _officer;
    private EntityUid _operator;
    private readonly List<EntityUid> _operators = new();
    private Vector2 _origin;
    private FighterSystem System => SEntMan.System<FighterSystem>();
    private TimeSpan Now => Server.ResolveDependency<IGameTiming>().CurTime;

    private async Task CreateWorld(bool aim = true)
    {
        PreFinalizeHook += () => TestContext.Out.WriteLine(TestContext.CurrentContext.Result.Message);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _terrain = Server.Transform(map.GridCoords.EntityId).MapUid!.Value;
            _origin = Server.Transform(map.GridCoords.EntityId).WorldPosition;
            // The operator stands outside the small test grid. Use breathable battlefield air
            // so the longer scenarios exercise aiming rather than vacuum exposure.
            var atmosphere = new GasMixture(2500) { Temperature = 293.15f };
            atmosphere.SetMoles(Gas.Oxygen, 21.824879f);
            atmosphere.SetMoles(Gas.Nitrogen, 82.10312f);
            SEntMan.System<AtmosphereSystem>().SetMapAtmosphere(_terrain, false, atmosphere);
            _aircraft = System.CreateAircraft(_terrain, _origin);
            _aircraft.Comp.Battlefield = new Box2(_origin - new Vector2(120), _origin + new Vector2(120));
            _aircraft.Comp.AirspaceRadius = 250;
            _aircraft.Comp.Entry = _origin + new Vector2(0, 100);
            _aircraft.Comp.Exit = _origin - new Vector2(0, 100);
            _combat = SEntMan.GetComponent<FighterAirCombatComponent>(_aircraft);
            // Keep outcome checks deterministic; timing and speed have separate regression coverage.
            _combat.SlowFlareEvasionMultiplier = 1;
            _combat.LateFlareEvasionMultiplier = 1;
            SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Faction = "govfor";
            _pilot = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(_aircraft, Vector2.Zero));
            _officer = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(_aircraft, Vector2.Zero));
            SEntMan.EnsureComponent<MarineComponent>(_pilot).Faction = "govfor";
            SEntMan.EnsureComponent<MarineComponent>(_officer).Faction = "govfor";
            Assert.That(System.Board(_pilot, _aircraft), Is.True);
            Assert.That(System.Board(_officer, _aircraft, true), Is.True);
            _launcher = SEntMan.SpawnEntity("CMUFighterManpad", new EntityCoordinates(_terrain, _origin + new Vector2(25, 25)));
            _manpad = SEntMan.GetComponent<FighterManpadComponent>(_launcher);
            Assert.That(_manpad.ReloadTime, Is.EqualTo(TimeSpan.FromSeconds(15)));
            Assert.That(_manpad.MissileFlightTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(_manpad.AcquisitionTime, Is.EqualTo(TimeSpan.FromSeconds(1.2)));
            _manpad.AcquisitionTime = TimeSpan.Zero;
            _manpad.MissileFlightTime = TimeSpan.FromSeconds(1);
            _operator = SpawnOperator(new EntityCoordinates(_terrain, _origin + new Vector2(25, 25)));
            LoadLauncher(_launcher, _operator);
            if (aim) WieldAndAim(_operator, _launcher);
        });
    }

    private EntityUid SpawnOperator(EntityCoordinates coordinates)
    {
        var user = SEntMan.SpawnEntity("MobHuman", coordinates);
        SEntMan.EnsureComponent<MarineComponent>(user).Faction = "clf";
        _operators.Add(user);
        return user;
    }

    private void WieldAndAim(EntityUid user, EntityUid launcher)
    {
        LoadLauncher(launcher, user);
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, launcher), Is.True);
        Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield(launcher, user), Is.True);
        ToggleAim(user, launcher);
        Assert.That(SEntMan.GetComponent<FighterManpadComponent>(launcher).AimingUser, Is.EqualTo(user));
    }

    private void LoadLauncher(EntityUid launcher, EntityUid user)
    {
        if (SEntMan.System<FighterManpadSystem>().HasAmmo(launcher))
            return;
        var round = SEntMan.SpawnEntity("CMURocket70mm", Server.Transform(user).Coordinates);
        var provider = SEntMan.GetComponent<BallisticAmmoProviderComponent>(launcher);
        Assert.That(SEntMan.System<SharedGunSystem>().TryBallisticInsert((launcher, provider), round, user), Is.True);
    }

    private void ToggleAim(EntityUid user, EntityUid launcher)
    {
        var manpad = SEntMan.GetComponent<FighterManpadComponent>(launcher);
        var actions = SEntMan.System<SharedActionsSystem>();
        Assert.That(actions.GetAction(manpad.AimAction), Is.Not.Null, "Picking up the launcher grants its aiming action.");
        actions.PerformAction(user, actions.GetAction(manpad.AimAction)!.Value);
    }

    private void MoveOperator(Vector2 offset) => SEntMan.System<SharedTransformSystem>()
        .SetCoordinates(_operator, new EntityCoordinates(_terrain, _origin + offset));

    private void EnterSector()
    {
        _aircraft.Comp.Position = _origin;
        _aircraft.Comp.Progress = .5f;
        _aircraft.Comp.Phase = FighterPhase.Pass;
        _aircraft.Comp.Flying = true;
        _aircraft.Comp.Speed = _aircraft.Comp.TargetSpeed = 8;
    }

    private async Task Advance(double seconds)
    {
        var ticks = 0;
        await Server.WaitPost(() => ticks = (int) Math.Ceiling(seconds * Server.ResolveDependency<IGameTiming>().TickRate));
        await Pair.RunTicksSync(ticks);
    }

    private void CleanWorld()
    {
        SEntMan.DeleteEntity(Server.Transform(_aircraft).MapUid!.Value);
        if (SEntMan.EntityExists(_launcher)) SEntMan.DeleteEntity(_launcher);
        foreach (var user in _operators)
            if (SEntMan.EntityExists(user)) SEntMan.DeleteEntity(user);
        _operators.Clear();
        foreach (var visual in SEntMan.EntityQuery<FighterManpadVisualComponent>().ToArray())
            SEntMan.DeleteEntity(visual.Owner);
    }

    [TestCase("govfor", false)]
    [TestCase("opfor", true)]
    [TestCase("clf", true)]
    [TestCase("AUColonist", false)]
    public async Task OperatorIffOverridesCapturedLauncherAndProtectsFriendlyAndNeutralAircraft(string side, bool hostile)
    {
        await CreateWorld(aim: false);
        await Server.WaitAssertion(() =>
        {
            _manpad.Faction = "opfor";
            SEntMan.GetComponent<MarineComponent>(_operator).Faction = side;
            WieldAndAim(_operator, _launcher);
            Assert.That(_manpad.Faction, Is.EqualTo(side.ToLowerInvariant()));
            EnterSector();
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.EqualTo(hostile));
            Assert.That(_manpad.ReadyAt > TimeSpan.Zero, Is.EqualTo(hostile));
            CleanWorld();
        });
    }

    [Test]
    public async Task UnidentifiedOperatorCannotArmAConfiguredLauncher()
    {
        await CreateWorld(aim: false);
        await Server.WaitAssertion(() =>
        {
            SEntMan.GetComponent<MarineComponent>(_operator).Faction = null;
            _manpad.Faction = "opfor";
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(_operator, _launcher), Is.True);
            Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield(_launcher, _operator), Is.True);
            ToggleAim(_operator, _launcher);
            Assert.That(_manpad.AimingUser, Is.Null);
            EnterSector();
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False);
            CleanWorld();
        });
    }

    [Test]
    public async Task AllianceChangeCancelsAcquisitionBeforeLaunch()
    {
        await CreateWorld();
        await Server.WaitPost(() => { _manpad.AcquisitionTime = TimeSpan.FromSeconds(1); EnterSector(); });
        await Advance(.2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_manpad.Target, Is.EqualTo(_aircraft.Owner));
            var factions = SEntMan.System<NpcFactionSystem>();
            factions.RealMakeFriendly("CLF", "GOVFOR");
            factions.RealMakeFriendly("GOVFOR", "CLF");
        });
        await Advance(1.2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_manpad.Target, Is.Null);
            Assert.That(_combat.Incoming, Is.False);
            Assert.That(_manpad.ReadyAt, Is.EqualTo(TimeSpan.Zero));
            var factions = SEntMan.System<NpcFactionSystem>();
            factions.RealMakeHostile("CLF", "GOVFOR");
            factions.RealMakeNeutral("GOVFOR", "CLF");
            CleanWorld();
        });
    }

    [Test]
    public async Task GroundHeldAndWieldedLaunchersRequireAnExplicitAimAction()
    {
        await CreateWorld(aim: false);
        await Server.WaitPost(EnterSector);
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False, "A launcher on the ground must never fire.");
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(_operator, _launcher), Is.True);
            ToggleAim(_operator, _launcher);
            Assert.That(_manpad.AimingUser, Is.Null, "A held launcher cannot be aimed without wielding.");
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False);
            Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield(_launcher, _operator), Is.True);
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False, "Wielding alone is not permission to launch.");
            Assert.That(_manpad.WindupVisual, Is.Null);
            ToggleAim(_operator, _launcher);
            Assert.That(SEntMan.GetComponent<ActionComponent>(_manpad.AimAction!.Value).Toggled, Is.True);
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True, "A wielded and explicitly aimed launcher covers its sector.");
            CleanWorld();
        });
    }

    [Test]
    public async Task AcquisitionBuildsUpBeforeLaunchAndPreservesTheFullFlareWindow()
    {
        await CreateWorld();
        EntityUid windup = default;
        await Server.WaitPost(() =>
        {
            _manpad.AcquisitionTime = TimeSpan.FromSeconds(1.2);
            _manpad.MissileFlightTime = TimeSpan.FromSeconds(5);
            EnterSector();
        });
        await Advance(.3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False, "The physical launch must follow acquisition.");
            Assert.That(_manpad.Target, Is.EqualTo(_aircraft.Owner));
            Assert.That(_manpad.ReadyAt, Is.EqualTo(TimeSpan.Zero), "Acquisition does not spend the reload.");
            Assert.That(_manpad.WindupVisual, Is.Not.Null);
            windup = _manpad.WindupVisual!.Value;
            var visual = SEntMan.GetComponent<FighterManpadVisualComponent>(windup);
            Assert.That(visual.Launched, Is.False);
            Assert.That(visual.WindupSeconds, Is.EqualTo(1.2f));
        });
        await Advance(1.2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True);
            Assert.That(_combat.IncomingAt - _combat.IncomingStartedAt, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(_manpad.Target, Is.Null);
            Assert.That(_manpad.WindupVisual, Is.Null);
            Assert.That(_manpad.WindupAudio, Is.Null);
            Assert.That(_manpad.TrackingAudio, Is.Null);
            var visual = SEntMan.GetComponent<FighterManpadVisualComponent>(windup);
            Assert.That(visual.Launched, Is.True);
            Assert.That(visual.StartedAt, Is.EqualTo(_combat.IncomingStartedAt));
            Assert.That(visual.ExpiresAt - visual.StartedAt, Is.EqualTo(TimeSpan.FromSeconds(8)));
            CleanWorld();
        });
    }

    [TestCase("stored")]
    [TestCase("lowered")]
    [TestCase("unwielded")]
    [TestCase("dropped")]
    [TestCase("switched-hands")]
    [TestCase("incapacitated")]
    [TestCase("operator-stored")]
    [TestCase("destroyed")]
    [TestCase("target-sector")]
    [TestCase("moved")]
    [TestCase("turned")]
    [TestCase("friendly")]
    public async Task InterruptedAcquisitionCannotFireOrLeaveItsEffectsBehind(string reason)
    {
        await CreateWorld();
        EntityUid windup = default;
        EntityUid? box = null;
        await Server.WaitPost(() => { _manpad.AcquisitionTime = TimeSpan.FromSeconds(1.2); EnterSector(); });
        await Advance(.3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_manpad.WindupVisual, Is.Not.Null);
            windup = _manpad.WindupVisual!.Value;
            switch (reason)
            {
                case "stored":
                case "operator-stored":
                    box = SEntMan.SpawnEntity(null, new EntityCoordinates(_terrain, _origin));
                    var containers = SEntMan.System<SharedContainerSystem>();
                    var container = containers.EnsureContainer<Container>(box.Value, "manpad-windup-test");
                    Assert.That(reason == "stored"
                        ? SEntMan.System<SharedHandsSystem>().TryDropIntoContainer(_operator, _launcher, container)
                        : containers.Insert(_operator, container), Is.True);
                    break;
                case "lowered": ToggleAim(_operator, _launcher); break;
                case "unwielded": Assert.That(SEntMan.System<SharedWieldableSystem>().TryUnwield(_launcher, _operator), Is.True); break;
                case "dropped": Assert.That(SEntMan.System<SharedHandsSystem>().TryDrop(_operator, _launcher), Is.True); break;
                case "switched-hands": SEntMan.System<SharedHandsSystem>().SwapHands(_operator); break;
                case "incapacitated": SEntMan.System<MobStateSystem>().ChangeMobState(_operator, MobState.Critical); break;
                case "destroyed": SEntMan.DeleteEntity(_launcher); break;
                case "target-sector":
                    _aircraft.Comp.Entry += new Vector2(90, 0);
                    _aircraft.Comp.Exit += new Vector2(90, 0);
                    _aircraft.Comp.Position += new Vector2(90, 0);
                    break;
                case "moved":
                    MoveOperator(new Vector2(26, 25));
                    break;
                case "turned": SEntMan.System<SharedTransformSystem>().SetWorldRotation(_operator, Angle.FromDegrees(90)); break;
                case "friendly": SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Faction = "clf"; break;
            }
        });
        await Advance(.3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(windup), Is.True);
            Assert.That(_combat.Incoming, Is.False);
            Assert.That(_manpad.ReadyAt, Is.EqualTo(TimeSpan.Zero));
            if (reason is "moved" or "turned") Assert.That(_manpad.WindupVisual, Is.Not.EqualTo(windup), "Movement requires fresh acquisition.");
            else
            {
                Assert.That(_manpad.Target, Is.Null);
                Assert.That(_manpad.WindupAudio, Is.Null);
                Assert.That(_manpad.TrackingAudio, Is.Null);
            }
        });
        if (reason is "lowered" or "unwielded" or "dropped" or "switched-hands" or "incapacitated" or "operator-stored")
        {
            await Advance(1.3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_combat.Incoming, Is.False, "An interrupted lock cannot fire at the old deadline.");
                Assert.That(_manpad.AimingUser, Is.Null);
                Assert.That(SEntMan.GetComponent<ActionComponent>(_manpad.AimAction!.Value).Toggled, Is.False);
                Assert.That(_manpad.ReadyAt, Is.EqualTo(TimeSpan.Zero));
            });
        }
        await Server.WaitPost(() =>
        {
            if (box is { } storage) SEntMan.DeleteEntity(storage);
            CleanWorld();
        });
    }

    [Test]
    public async Task ReaimingStartsANewLockInsteadOfResumingTheCancelledShot()
    {
        await CreateWorld();
        EntityUid originalVisual = default;
        await Server.WaitPost(() => { _manpad.AcquisitionTime = TimeSpan.FromSeconds(1.2); EnterSector(); });
        await Advance(.95);
        await Server.WaitAssertion(() =>
        {
            originalVisual = _manpad.WindupVisual!.Value;
            ToggleAim(_operator, _launcher);
            Assert.That(_manpad.Target, Is.Null, "Lowering must cancel acquisition in the action handler.");
            ToggleAim(_operator, _launcher);
        });
        await Advance(.4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False, "The first lock's launch deadline must not survive re-aiming.");
            Assert.That(SEntMan.Deleted(originalVisual), Is.True);
            Assert.That(_manpad.WindupVisual, Is.Not.Null.And.Not.EqualTo(originalVisual));
            Assert.That(_manpad.ReadyAt, Is.EqualTo(TimeSpan.Zero));
        });
        await Advance(1.3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True,
                $"The new uninterrupted lock can launch. Aim={_manpad.AimingUser}, target={_manpad.Target}, launch={_manpad.LaunchAt}, now={Now}.");
            CleanWorld();
        });
    }

    [Test]
    public async Task HandingOverTheLauncherClearsAimButPreservesItsReloadAndLaunchedMissile()
    {
        await CreateWorld();
        TimeSpan readyAt = default;
        TimeSpan impactAt = default;
        await Server.WaitPost(() => { _combat.FlareEvasionChance = 1; EnterSector(); });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True);
            readyAt = _manpad.ReadyAt;
            impactAt = _combat.IncomingAt;
            Assert.That(System.TryDeployFlares(_pilot), Is.True);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryDrop(_operator, _launcher), Is.True);
        });
        // Let the old wielder's virtual off-hand item finish its queued deletion before re-wielding.
        await Advance(.15);
        await Server.WaitAssertion(() =>
        {
            _operator = SpawnOperator(new EntityCoordinates(_terrain, _origin + new Vector2(25, 25)));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(_operator, _launcher), Is.True);
            Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield(_launcher, _operator), Is.True);
            Assert.That(_manpad.AimingUser, Is.Null, "The new operator must explicitly aim again.");
            Assert.That(SEntMan.GetComponent<ActionComponent>(_manpad.AimAction!.Value).Toggled, Is.False);
            LoadLauncher(_launcher, _operator);
            ToggleAim(_operator, _launcher);
            Assert.That(_manpad.AimingUser, Is.EqualTo(_operator));
            Assert.That(_combat.IncomingAt, Is.EqualTo(impactAt), "Lowering does not recall a launched missile.");
        });
        await Advance(1.2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Result, Is.EqualTo(FighterAirResult.Evaded));
            Assert.That(_combat.Incoming, Is.False);
            Assert.That(_manpad.ReadyAt, Is.EqualTo(readyAt), "A new operator cannot bypass the reload.");
            _manpad.ReadyAt = Now + TimeSpan.FromSeconds(.2);
        });
        await Advance(.4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True,
                $"The new operator may fire after reloading. Aim={_manpad.AimingUser}, target={_manpad.Target}, ready={_manpad.ReadyAt}, now={Now}.");
            CleanWorld();
        });
    }

    [Test]
    public async Task FactionVariantsCanBeDestroyedToClearTheirSector()
    {
        await CreateWorld();
        EntityUid government = default;
        EntityUid opposition = default;
        await Server.WaitAssertion(() =>
        {
            government = SEntMan.SpawnEntity("CMUFighterManpadGovfor", new EntityCoordinates(_terrain, _origin));
            opposition = SEntMan.SpawnEntity("CMUFighterManpadOpfor", new EntityCoordinates(_terrain, _origin));
            Assert.That(_manpad.Faction, Is.EqualTo("clf"));
            Assert.That(SEntMan.GetComponent<FighterManpadComponent>(government).Faction, Is.EqualTo("govfor"));
            Assert.That(SEntMan.GetComponent<FighterManpadComponent>(opposition).Faction, Is.EqualTo("opfor"));
            foreach (var launcher in new[] { _launcher, government, opposition })
                Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(launcher,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 75 } },
                    ignoreResistances: true, ignoreGlobalModifiers: true), Is.Not.Null);
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            foreach (var launcher in new[] { _launcher, government, opposition })
                Assert.That(SEntMan.Deleted(launcher), Is.True, "A destroyed launcher must stop covering its square.");
            EnterSector();
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False);
            CleanWorld();
        });
    }

    [TestCase(Direction.South)]
    [TestCase(Direction.North)]
    [TestCase(Direction.East)]
    [TestCase(Direction.West)]
    public async Task OperatorPositionSelectsTheSectorAndEffectsStartAtTheShoulder(Direction facing)
    {
        await CreateWorld();
        await Server.WaitPost(() =>
        {
            EnterSector();
            SEntMan.System<SharedTransformSystem>().SetWorldRotation(_operator, facing.ToAngle());
            MoveOperator(new Vector2(81, 0));
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False, "A launcher in the neighboring square must not cover this one.");
            MoveOperator(new Vector2(25, 25));
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True);
            Assert.That(_combat.IncomingFromGround, Is.True);
            Assert.That(_combat.IncomingSector, Is.EqualTo(4));
            Assert.That(_combat.CoveredSectors, Is.Empty, "No coverage selection is required.");
            Assert.That(_combat.IncomingDirection.Length(), Is.EqualTo(1).Within(.001));
            Assert.That(_combat.IncomingAt - _combat.IncomingStartedAt, Is.EqualTo(TimeSpan.FromSeconds(1)));
            var visuals = SEntMan.EntityQuery<FighterManpadVisualComponent>().ToArray();
            Assert.That(visuals, Has.Length.EqualTo(1));
            Assert.That(visuals[0].Launched, Is.True);
            Assert.That(visuals[0].Direction, Is.EqualTo(-_combat.IncomingDirection));
            Assert.That(visuals[0].ExpiresAt - visuals[0].StartedAt, Is.EqualTo(TimeSpan.FromSeconds(8)));
            Assert.That(SEntMan.HasComponent<TimedDespawnComponent>(visuals[0].Owner), Is.True);
            var transforms = SEntMan.System<SharedTransformSystem>();
            var muzzle = transforms.GetWorldPosition(visuals[0].Owner) - transforms.GetWorldPosition(_operator);
            Assert.That(muzzle.Length(), Is.InRange(.2f, .6f), "Ignition belongs on the shoulder tube, not the operator's feet.");
            Assert.That(muzzle.Y, Is.GreaterThan(.1f), "The north/south tube ends are drawn above the hands, even when facing south.");
            Assert.That(visuals[0].TubeDirection, Is.EqualTo(facing.ToVec()), "Backblast must still follow the operator's facing.");
            Assert.That(_manpad.ReadyAt - _combat.IncomingStartedAt, Is.EqualTo(TimeSpan.FromSeconds(15)));
            CleanWorld();
        });
    }

    [Test]
    public async Task FriendlyUnidentifiedStoredAndIneligibleTargetsNeverLaunch()
    {
        await CreateWorld();
        EntityUid box = default;
        Container container = null!;
        await Server.WaitPost(() =>
        {
            box = SEntMan.SpawnEntity(null, new EntityCoordinates(_terrain, _origin));
            container = SEntMan.System<SharedContainerSystem>().EnsureContainer<Container>(box, "manpad-test");
        });
        foreach (var reason in new[] { "friendly", "unidentified", "no-iff", "other-map", "holding", "retreat", "uncrewed", "outside", "stored" })
        {
            await Server.WaitAssertion(() =>
            {
                EnterSector();
                _aircraft.Comp.TerrainMap = _terrain;
                _aircraft.Comp.ForcedRetreat = false;
                SEntMan.GetComponent<FighterSeatComponent>(_aircraft.Comp.FrontSeat!.Value).Occupant = _pilot;
                SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Faction = "govfor";
                _manpad.Faction = "clf";
                SEntMan.GetComponent<MarineComponent>(_operator).Faction = "clf";
                if (_manpad.AimingUser == null) ToggleAim(_operator, _launcher);
                MoveOperator(Vector2.Zero);
                switch (reason)
                {
                    case "friendly": SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Faction = "CLF"; break;
                    case "unidentified": SEntMan.GetComponent<FighterWeaponsComponent>(_aircraft).Faction = null; break;
                    case "no-iff": _manpad.Faction = null; break;
                    case "other-map": _aircraft.Comp.TerrainMap = Server.Transform(_aircraft).MapUid!.Value; break;
                    case "holding": _aircraft.Comp.Phase = FighterPhase.Holding; _aircraft.Comp.Flying = false; break;
                    case "retreat": _aircraft.Comp.ForcedRetreat = true; break;
                    case "uncrewed": SEntMan.GetComponent<FighterSeatComponent>(_aircraft.Comp.FrontSeat!.Value).Occupant = null; break;
                    case "outside": MoveOperator(new Vector2(150, 0)); break;
                    case "stored": Assert.That(SEntMan.System<SharedHandsSystem>().TryDropIntoContainer(_operator, _launcher, container), Is.True); break;
                }
            });
            await Advance(.25);
            await Server.WaitAssertion(() =>
            {
                Assert.That(_combat.Incoming, Is.False, reason);
                Assert.That(_manpad.ReadyAt, Is.EqualTo(TimeSpan.Zero), reason);
                Assert.That(SEntMan.EntityQuery<FighterManpadVisualComponent>(), Is.Empty, reason);
            });
        }
        await Server.WaitPost(() => { SEntMan.DeleteEntity(box); CleanWorld(); });
    }

    [TestCase(false, 1f, FighterAirResult.Hit)]
    [TestCase(true, 0f, FighterAirResult.Hit)]
    [TestCase(true, 1f, FighterAirResult.Evaded)]
    public async Task GroundMissileUsesExistingFlaresAndOneHitRetreat(bool flare, float chance, FighterAirResult expected)
    {
        await CreateWorld();
        await Server.WaitPost(() => { EnterSector(); _combat.FlareEvasionChance = chance; });
        await Advance(.25);
        TimeSpan readyAt = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True);
            readyAt = _manpad.ReadyAt;
            Assert.That(System.TryDeployFlares(_officer), Is.False);
            if (flare)
            {
                Assert.That(System.TryDeployFlares(_pilot), Is.True);
                Assert.That(System.TryDeployFlares(_pilot), Is.False);
            }
            // Picking up or destroying a launcher cannot recall a missile already in flight.
            SEntMan.DeleteEntity(_launcher);
        });
        await Advance(1.1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.False);
            Assert.That(_combat.Result, Is.EqualTo(expected));
            Assert.That(_aircraft.Comp.ForcedRetreat, Is.EqualTo(expected == FighterAirResult.Hit));
            Assert.That(SEntMan.GetComponent<FighterEffectsComponent>(_aircraft).Cues.Any(cue =>
                cue.Kind == (expected == FighterAirResult.Hit ? FighterEffectKind.Hit : FighterEffectKind.Evaded)), Is.True);
            Assert.That(readyAt, Is.GreaterThan(Now));
            CleanWorld();
        });
    }

    [Test]
    public async Task DuplicateLaunchersCannotStackWarningsAndReloadDoesNotResetOnMovement()
    {
        await CreateWorld();
        EntityUid second = default;
        EntityUid secondOperator = default;
        TimeSpan impactAt = default;
        TimeSpan readyAt = default;
        await Server.WaitPost(() =>
        {
            EnterSector();
            _combat.FlareEvasionChance = 1;
            second = SEntMan.SpawnEntity("CMUFighterManpad", new EntityCoordinates(_terrain, _origin));
            SEntMan.GetComponent<FighterManpadComponent>(second).MissileFlightTime = TimeSpan.FromSeconds(1);
            SEntMan.GetComponent<FighterManpadComponent>(second).AcquisitionTime = TimeSpan.Zero;
            secondOperator = SpawnOperator(new EntityCoordinates(_terrain, _origin));
            WieldAndAim(secondOperator, second);
        });
        await Advance(.25);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True);
            var other = SEntMan.GetComponent<FighterManpadComponent>(second);
            Assert.That(new[] { _manpad.ReadyAt, other.ReadyAt }.Count(time => time > Now), Is.EqualTo(1));
            impactAt = _combat.IncomingAt;
            Assert.That(System.TryDeployFlares(_pilot), Is.True);
            // Keep only the launcher which fired, regardless of query order.
            if (_manpad.ReadyAt > Now) SEntMan.DeleteEntity(second);
            else
            {
                SEntMan.DeleteEntity(_launcher);
                _launcher = second;
                _manpad = other;
                _operator = secondOperator;
            }
            readyAt = _manpad.ReadyAt;
            MoveOperator(new Vector2(10));
        });
        await Advance(.25);
        await Server.WaitAssertion(() => Assert.That(_combat.IncomingAt, Is.EqualTo(impactAt), "The same warning cannot be overwritten."));
        await Advance(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Result, Is.EqualTo(FighterAirResult.Evaded));
            Assert.That(_combat.Incoming, Is.False);
            Assert.That(_manpad.ReadyAt, Is.EqualTo(readyAt));
            // Shorten the remaining reload wait for a short gameplay test.
            _manpad.ReadyAt = Now + TimeSpan.FromSeconds(.2);
            LoadLauncher(_launcher, _operator);
            ToggleAim(_operator, _launcher);
        });
        await Advance(.4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_combat.Incoming, Is.True, "A reloaded and re-aimed launcher can fire after the cooldown.");
            Assert.That(_combat.FlaresUsed, Is.False, "The new missile gives one fresh flare attempt.");
            Assert.That(System.TryDeployFlares(_pilot), Is.True);
        });
        await Advance(8.2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<FighterManpadVisualComponent>(), Is.Empty, "Finished exhaust effects must despawn.");
            CleanWorld();
        });
    }
}
