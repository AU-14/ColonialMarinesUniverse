using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.JumpPack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.JumpPack;

[TestFixture]
[TestOf(typeof(CMUJumpPackSystem))]
public sealed class CMUJumpPackTest : GameTest
{
    private readonly List<EntityUid> _cleanup = new();
    private SharedMapSystem _maps = null!;
    private CMUZLevelsSystem _z = null!;
    private EntityUid _lower;
    private EntityUid _upper;
    private EntityUid _rider;
    private EntityUid _pack;
    private Tile _floor;

    [SetUp]
    public async Task SetupLevels()
    {
        await Server.WaitAssertion(() =>
        {
            _maps = SEntMan.System<SharedMapSystem>();
            _z = SEntMan.System<CMUZLevelsSystem>();
            _floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            _lower = _maps.CreateMap(runMapInit: true);
            _upper = _maps.CreateMap(runMapInit: true);
            _cleanup.Add(_lower);
            _cleanup.Add(_upper);
            var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(_lower);
            var upperGrid = SEntMan.EnsureComponent<MapGridComponent>(_upper);
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
            {
                _maps.SetTile(_lower, lowerGrid, new Vector2i(x, y), _floor);

                // An open shaft above the rider, with a ledge to the north.
                if (Math.Abs(x) > 1 || Math.Abs(y) > 1)
                    _maps.SetTile(_upper, upperGrid, new Vector2i(x, y), _floor);
            }

            var network = _z.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(_z.TryAddMapsIntoZNetwork(network, new() { [_lower] = 0, [_upper] = 1 }), Is.True);

            var spawn = new EntityCoordinates(_lower, new Vector2(0.5f, 0.5f));
            _rider = SEntMan.SpawnEntity("CMMobHuman", spawn);
            _pack = SEntMan.SpawnEntity("CMUJumpPackFilled", spawn);
            Assert.That(SEntMan.HasComponent<CMUZPhysicsComponent>(_rider), Is.True);
        });

        // RMCInputSystem strips the input mover from unpossessed mobs a tick after spawning; a real rider always has one.
        await Server.WaitRunTicks(1);
        await Server.WaitPost(() => SEntMan.EnsureComponent<InputMoverComponent>(_rider));
    }

    [TearDown]
    public async Task CleanupLevels()
    {
        // Leaf-first deletion would tick between the pack's actions and the pack, leaving dangling networked refs.
        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(_pack);
            SEntMan.DeleteEntity(_rider);
        });

        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
        _cleanup.Clear();
    }

    [Test]
    public void DirectionCyclesClockwise()
    {
        var four = new List<Direction>();
        var eight = new List<Direction>();
        var four0 = Direction.North;
        var eight0 = Direction.North;
        for (var i = 0; i < 4; i++)
            four.Add(four0 = CMUJumpPackSystem.NextDirection(four0, false));
        for (var i = 0; i < 8; i++)
            eight.Add(eight0 = CMUJumpPackSystem.NextDirection(eight0, true));

        Assert.Multiple(() =>
        {
            Assert.That(four, Is.EqualTo(new[] { Direction.East, Direction.South, Direction.West, Direction.North }));
            Assert.That(eight, Is.EqualTo(new[]
            {
                Direction.NorthEast, Direction.East, Direction.SouthEast, Direction.South,
                Direction.SouthWest, Direction.West, Direction.NorthWest, Direction.North,
            }));
            Assert.That(CMUJumpPackSystem.NextDirection(Direction.NorthEast, false), Is.EqualTo(Direction.East));
        });
    }

    [Test]
    public async Task AltClickRotatesHeadingWithoutOpeningGasValve()
    {
        await Server.WaitAssertion(() =>
        {
            Equip();
            var tank = SComp<GasTankComponent>(_pack);
            var valveOpen = tank.ReleaseValveOpen;
            var before = Pack.BoostDirection;
            var mode = Pack.EightDirections;
            var fuel = Moles;
            var verbs = SEntMan.System<SharedVerbSystem>().GetLocalVerbs(_pack, _rider, typeof(AlternativeVerb));
            Assert.That(verbs, Is.Not.Empty);
            var primary = verbs.Min!;
            Assert.That(primary.Disabled, Is.False);
            Assert.That(primary.Act, Is.Not.Null);
            primary.Act!();

            Assert.Multiple(() =>
            {
                Assert.That(Pack.BoostDirection, Is.EqualTo(CMUJumpPackSystem.NextDirection(before, mode)));
                Assert.That(Pack.EightDirections, Is.EqualTo(mode));
                Assert.That(tank.ReleaseValveOpen, Is.EqualTo(valveOpen));
                Assert.That(Moles, Is.EqualTo(fuel));
            });
        });
    }

    [Test]
    public async Task ActionsOnlyGrantedInBackSlot()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(_rider, _pack, animate: false), Is.True);
            Assert.That(HasAction("CMUActionJumpPackBoost"), Is.False, "A pack held in hand must not grant actions.");

            Equip();
            Assert.Multiple(() =>
            {
                Assert.That(HasAction("CMUActionJumpPackBoost"), Is.True);
                Assert.That(HasAction("CMUActionJumpPackDirection"), Is.True);
                Assert.That(HasAction("CMUActionJumpPackJump"), Is.True);
                Assert.That(HasAction("CMUActionJumpPackHover"), Is.True);
            });

            Assert.That(SEntMan.System<InventorySystem>().TryUnequip(_rider, "back", force: true), Is.True);
            Assert.That(HasAction("CMUActionJumpPackBoost"), Is.False);
        });
    }

    [Test]
    public async Task BoostBurnsFuelAndSharesCooldownWithJump()
    {
        float before = 0;
        Vector2 start = default;
        float distance = default;
        await Server.WaitAssertion(() =>
        {
            Equip();
            before = Moles;
            start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(_rider);
            distance = Pack.BoostDistance;
            Perform(Pack.BoostActionEntity);

            Assert.Multiple(() =>
            {
                Assert.That(before - Moles, Is.EqualTo(Pack.FuelPerBoost).Within(1e-5f));
                Assert.That(CooldownLeft(Pack.BoostActionEntity), Is.EqualTo(Pack.BoostCooldown).Within(TimeSpan.FromMilliseconds(100)));
                Assert.That(CooldownLeft(Pack.JumpActionEntity), Is.EqualTo(Pack.BoostCooldown).Within(TimeSpan.FromMilliseconds(100)));
            });
        });

        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var position = SEntMan.System<SharedTransformSystem>().GetWorldPosition(_rider);
            Assert.That(position.Y - start.Y, Is.InRange(distance * 0.8f, distance * 1.2f),
                "A ground boost must travel the configured distance, not get braked by grounded movement.");
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
        });
    }

    [Test]
    public async Task JumpWithoutALedgeUsesFuelToLandSafely()
    {
        float afterJump = 0;
        await Server.WaitAssertion(() =>
        {
            Equip();
#pragma warning disable RA0002
            Pack.JumpDashDistance = 0;
#pragma warning restore RA0002
            Perform(Pack.JumpActionEntity);
            afterJump = Moles;
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_upper)));
        await Pair.RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
            Assert.That(Pack.DescendingUser, Is.Null);
            Assert.That(Moles, Is.LessThan(afterJump), "Landing must consume fuel in addition to the jump.");
            AssertUnhurt();
        });
    }

    [TestCase(true, true, false, false)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(true, true, true, false)]
    [TestCase(true, true, false, true)]
    public async Task OrdinaryFallsAreBrakedOnlyByAWornFueledPack(
        bool worn, bool fueled, bool removeWhileFalling, bool emptyWhileFalling)
    {
        float before = 0;
        await Server.WaitAssertion(() =>
        {
            if (worn)
                Equip();
            if (!fueled)
                SComp<GasTankComponent>(_pack).Air.Clear();
            before = Moles;
            SEntMan.System<SharedTransformSystem>().SetCoordinates(_rider,
                new EntityCoordinates(_upper, new Vector2(0.5f, 0.5f)));
            _z.SetZLocalPosition(_rider, 0.5f);
            _z.SetZVelocity(_rider, -5f);
        });

        await Pair.RunTicksSync(2);
        if (removeWhileFalling)
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Pack.DescendingUser, Is.EqualTo(_rider));
                Assert.That(SEntMan.System<InventorySystem>().TryUnequip(_rider, "back", force: true), Is.True);
                Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.False);
            });
        }
        else if (emptyWhileFalling)
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Pack.DescendingUser, Is.EqualTo(_rider));
                SComp<GasTankComponent>(_pack).Air.Clear();
            });
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.False));
        }

        await Pair.RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
            Assert.That(Pack.DescendingUser, Is.Null);
            var damage = SEntMan.System<DamageableSystem>().GetTotalDamage(_rider).Float();
            if (worn && fueled && !removeWhileFalling && !emptyWhileFalling)
            {
                Assert.That(Moles, Is.LessThan(before));
                Assert.That(damage, Is.Zero);
            }
            else
            {
                Assert.That(damage, Is.GreaterThan(0), "Unprotected falls must retain ordinary fall damage.");
            }
        });
    }

    [Test]
    public async Task JumpReachesTheLevelAbove()
    {
        await Server.WaitAssertion(() =>
        {
            Equip();
            Perform(Pack.JumpActionEntity);
            Assert.That(CooldownLeft(Pack.BoostActionEntity), Is.GreaterThan(TimeSpan.Zero));
        });

        await Pair.RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_upper));
            AssertUnhurt();
        });
    }

    [Test]
    public async Task JumpCanInterruptAutomaticDescent()
    {
        await Server.WaitAssertion(() =>
        {
            Equip();
            _z.SetZLocalPosition(_rider, 0.5f);
            _z.SetZVelocity(_rider, -1f);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Pack.DescendingUser, Is.EqualTo(_rider));
            Perform(Pack.JumpActionEntity);
            Assert.That(Pack.DescendingUser, Is.Null);
            Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.False);
            Assert.That(SComp<CMUZPhysicsComponent>(_rider).Velocity, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task HoverHoldsAboveAndLandsSafelyWhenEndedEarly()
    {
        float before = 0;
        await Server.WaitAssertion(() =>
        {
            Equip();
            Perform(Pack.BoostActionEntity);
            Assert.That(CooldownLeft(Pack.BoostActionEntity), Is.GreaterThan(TimeSpan.Zero));
        });

        // Let the boost land back inside the open shaft before lifting off.
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
            SEntMan.System<SharedTransformSystem>().SetCoordinates(_rider, new EntityCoordinates(_lower, new Vector2(0.5f, 0.5f)));
            before = Moles;
            Perform(Pack.HoverActionEntity);

            Assert.Multiple(() =>
            {
                Assert.That(Pack.HoverUser, Is.EqualTo(_rider));
                Assert.That(Action(Pack.HoverActionEntity).Toggled, Is.True);
                Assert.That(CooldownLeft(Pack.BoostActionEntity), Is.EqualTo(TimeSpan.Zero),
                    "Hovering clears the boost cooldown.");
            });
        });

        await Pair.RunSeconds(4f);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_upper));
                Assert.That(SComp<CMUZPhysicsComponent>(_rider).LocalPosition, Is.Zero.Within(0.01f));
                Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.True);
                Assert.That(before - Moles, Is.EqualTo(Pack.HoverFuelPerSecond * 4f).Within(Pack.HoverFuelPerSecond * 0.5f));
            });

            Perform(Pack.HoverActionEntity);
            Assert.That(Pack.HoverUser, Is.Null);
            Assert.That(Action(Pack.HoverActionEntity).Toggled, Is.False);
            Assert.That(CooldownLeft(Pack.HoverActionEntity), Is.GreaterThan(TimeSpan.FromSeconds(25)));
        });

        await Pair.RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
                Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.False);
                Assert.That(Pack.DescendingUser, Is.Null);
            });
            AssertUnhurt();
        });
    }

    [Test]
    public async Task BoostWhileHoveringHasNoCooldown()
    {
        await Server.WaitAssertion(() =>
        {
            Equip();
            Perform(Pack.HoverActionEntity);
            Perform(Pack.BoostActionEntity);
            Assert.That(CooldownLeft(Pack.BoostActionEntity), Is.EqualTo(TimeSpan.Zero));
            Assert.That(CooldownLeft(Pack.JumpActionEntity), Is.EqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task HoverExpiresAfterItsDuration()
    {
        await Server.WaitAssertion(() =>
        {
            Equip();
            Perform(Pack.HoverActionEntity);
            Assert.That(Pack.HoverUser, Is.EqualTo(_rider));
        });

        await Pair.RunSeconds((float) Pack.HoverDuration.TotalSeconds + 4f);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Pack.HoverUser, Is.Null);
                Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_lower));
                Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.False);
            });
            AssertUnhurt();
        });
    }

    [Test]
    public async Task HoverAndJumpRefusedUnderACeiling()
    {
        await Server.WaitAssertion(() =>
        {
            _maps.SetTile(_upper, SComp<MapGridComponent>(_upper), Vector2i.Zero, _floor);
            Equip();
            var before = Moles;
            Perform(Pack.HoverActionEntity);
            Perform(Pack.JumpActionEntity);

            Assert.Multiple(() =>
            {
                Assert.That(Pack.HoverUser, Is.Null);
                Assert.That(SEntMan.HasComponent<CMUZHoverComponent>(_rider), Is.False);
                Assert.That(Moles, Is.EqualTo(before));
            });
        });
    }

    private CMUJumpPackComponent Pack => SComp<CMUJumpPackComponent>(_pack);

    private float Moles => SComp<GasTankComponent>(_pack).Air.TotalMoles;

    private void Equip()
    {
        Assert.That(SEntMan.System<InventorySystem>().TryEquip(_rider, _pack, "back", silent: true, force: true), Is.True);
    }

    private ActionComponent Action(EntityUid? action) => SComp<ActionComponent>(action!.Value);

    private void Perform(EntityUid? action)
    {
        Assert.That(action, Is.Not.Null);
        SEntMan.System<SharedActionsSystem>().PerformAction(_rider, (action!.Value, Action(action)), predicted: false);
    }

    private TimeSpan CooldownLeft(EntityUid? action)
    {
        if (Action(action).Cooldown is not { } cooldown || cooldown.End <= SGameTiming.CurTime)
            return TimeSpan.Zero;

        return cooldown.End - SGameTiming.CurTime;
    }

    private bool HasAction(string prototype)
    {
        if (!SEntMan.TryGetComponent<ActionsComponent>(_rider, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (SEntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }

    private void AssertUnhurt()
    {
        Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(_rider).Float(), Is.Zero,
            "Jump pack movement must not cause fall damage.");
    }
}
