#pragma warning disable RA0002 // Set the requested view and control a single gun shot.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Maps;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

public sealed class CMUZShotOpeningTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static PoolSettings ConnectedSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: BaseItem
          id: CMUZOpeningTestPistol
          components:
          - type: Gun
            selectedMode: SemiAuto
            availableModes: [SemiAuto]
          - type: BallisticAmmoProvider
            proto: CMCartridgePistol9mm
            capacity: 1
        """;

    private Entity<CMUZLevelsNetworkComponent> _network;
    private EntityUid _lower;
    private EntityUid _upper;
    private MapGridComponent _upperGrid = default!;

    [SetUp]
    public async Task CreateLevels()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            _network = levels.CreateZNetwork();
            _lower = maps.CreateMap(runMapInit: true);
            _upper = maps.CreateMap(runMapInit: true);
            var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(_lower);
            _upperGrid = SEntMan.EnsureComponent<MapGridComponent>(_upper);
            var floor = GetTile("Plating");
            for (var x = -2; x <= 20; x++)
            for (var y = -14; y <= 16; y++)
                maps.SetTile(_lower, lowerGrid, new Vector2i(x, y), floor);

            maps.SetTile(_upper, _upperGrid, new Vector2i(30, 30), floor);
            Assert.That(levels.TryAddMapsIntoZNetwork(_network, new() { [_lower] = 0, [_upper] = 1 }), Is.True);
        });
    }

    [TearDown]
    public async Task DeleteLevels()
    {
        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(_upper);
            SEntMan.DeleteEntity(_lower);
            SEntMan.DeleteEntity(_network);
        });
    }

    [TestCase(-1, "Space")]
    [TestCase(1, "AU14LatticeTile6")]
    public async Task OpeningRangeUsesFiringLineAndOnlyLimitsShooter(int offset, string aperture)
    {
        await Server.WaitAssertion(() =>
        {
            FillRoof();
            SetUpper(new Vector2i(4, 0), aperture);
            SetUpper(new Vector2i(1, -1), "Space"); // A nearby opening beside the shot does not qualify.
            var (shooter, gun) = CreateShooter(offset, new Vector2(0.99f, 0.5f));
            var target = new Vector2(15.5f, 0.5f);

            AssertShot(shooter, target, false);
            Assert.That(Fire(shooter, gun, target), Is.Null, "A blocked shot must not consume the only cartridge.");

            Server.System<SharedTransformSystem>().SetCoordinates(shooter,
                new EntityCoordinates(offset < 0 ? _upper : _lower, new Vector2(1f, 0.5f)));
            SetAim(shooter, offset);
            var spawn = AssertShot(shooter, target, true);
            Assert.That(spawn.X, Is.GreaterThan(4f).And.LessThan(5f), "Exactly three tiles to the opening edge is allowed.");

            var projectiles = Fire(shooter, gun, target);
            Assert.That(projectiles, Has.Count.EqualTo(1));
            Assert.That(Server.System<SharedTransformSystem>().GetMapCoordinates(projectiles![0]), Is.EqualTo(spawn));
        });
    }

    [Test]
    public async Task DiagonalShotMeasuresEntryEdgeAndKeepsItsDirection()
    {
        await Server.WaitAssertion(() =>
        {
            FillRoof();
            SetUpper(new Vector2i(3, 2), "Space");
            var (shooter, _) = CreateShooter(-1, new Vector2(0.85f, -0.05f));
            AssertShot(shooter, new Vector2(10.85f, 9.95f), false);

            Server.System<SharedTransformSystem>().SetCoordinates(shooter,
                new EntityCoordinates(_upper, new Vector2(0.95f, 0.05f)));
            var spawn = AssertShot(shooter, new Vector2(10.95f, 10.05f), true);
            Assert.That(spawn.X, Is.GreaterThan(3f).And.LessThan(4f));
            Assert.That(spawn.X - spawn.Y, Is.EqualTo(0.9f).Within(0.0001f),
                "Moving the bullet to a tile center would bend this shot off its firing line.");
        });
    }

    [TestCase(-1)]
    [TestCase(1)]
    public async Task OpenAirShotIgnoresRoofsBesideAndBehindShooter(int offset)
    {
        await Server.WaitAssertion(() =>
        {
            SetUpper(new Vector2i(1, 0), "CMURoofDestroyable");
            SetUpper(new Vector2i(0, 1), "CMURoofDestroyable");
            var (shooter, _) = CreateShooter(offset, new Vector2(0.5f));
            var spawn = AssertShot(shooter, new Vector2(0.5f, -12.5f), true);
            Assert.That(spawn.Position, Is.EqualTo(new Vector2(0.5f)),
                "A southbound shot through open air must not search for the edge of a roof to the east or north.");
        });
    }

    [Test]
    public async Task DestroyingRoofCreatesShootableOpening()
    {
        await Server.WaitAssertion(() =>
        {
            FillRoof();
            var (shooter, _) = CreateShooter(-1, new Vector2(0.5f));
            var target = new Vector2(12.5f, 0.5f);
            AssertShot(shooter, target, false);

            var tile = Server.System<SharedMapSystem>().GetTileRef(_upper, _upperGrid, new Vector2i(2, 0));
            Server.System<CMUTileDestructionSystem>().DestroyTile(tile);
            var spawn = AssertShot(shooter, target, true);
            Assert.That(spawn.X, Is.GreaterThan(2f).And.LessThan(3f));
        });
    }

    [Test]
    public async Task OverlappingRotatedGridCannotHideSolidRoof()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var grid = maps.CreateGridEntity(SEntMan.GetComponent<MapComponent>(_upper).MapId);
            transform.SetLocalRotation(grid, Angle.FromDegrees(90));
            transform.SetLocalPosition(grid, new Vector2(0.25f, 0f));
            for (var x = -1; x <= 12; x++)
            {
                SetUpper(new Vector2i(x, 0), "AU14LatticeTile6");
                maps.SetTile(grid, new Vector2i(0, -x - 1), GetTile("CMURoofDestroyable"));
            }

            var (shooter, _) = CreateShooter(1, new Vector2(0.24f, 0.5f));
            var target = new Vector2(10.5f, 0.5f);
            AssertShot(shooter, target, false);
            maps.SetTile(grid, new Vector2i(0, -4), Tile.Empty);
            AssertShot(shooter, target, false);
            transform.SetCoordinates(shooter, new EntityCoordinates(_lower, new Vector2(0.25f, 0.5f)));
            SetAim(shooter, 1);
            var spawn = AssertShot(shooter, target, true);
            Assert.That(spawn.X, Is.GreaterThan(3.25f).And.LessThan(4.25f),
                "An opening between the main grid's boundaries must still be usable within three tiles.");
        });
    }

    [Test]
    public async Task SourceStairsRespectOpeningRangeForLobbedShots()
    {
        await Server.WaitAssertion(() =>
        {
            FillRoof();
            var (shooter, _) = CreateShooter(1, new Vector2(0.5f));
            var stair = SEntMan.SpawnAttachedTo(null, new EntityCoordinates(_lower, new Vector2(9.5f, 0.5f)));
            var stairs = SEntMan.AddComponent<CMUZLevelStairsComponent>(stair);
            stairs.Offset = 1;
            stairs.Direction = Direction.East;
            var transform = Server.System<SharedTransformSystem>();
            var shooting = Server.System<CMUZLevelShootingSystem>();
            var from = transform.GetMapCoordinates(shooter);
            var to = new MapCoordinates(new Vector2(15.5f, 0.5f), from.MapId);
            Assert.That(shooting.TryAdjustShotMapCoordinates(shooter, from, to, out _, out _, maximumRange: 20f), Is.False);

            transform.SetCoordinates(stair, new EntityCoordinates(_lower, new Vector2(2.5f, 0.5f)));
            Assert.That(shooting.TryAdjustShotMapCoordinates(shooter, from, to, out var spawn, out var target,
                maximumRange: 20f), Is.True);
            Assert.That(spawn.X, Is.GreaterThan(3f).And.LessThan(4f));
            Assert.That(target.Position, Is.EqualTo(to.Position), "The range restriction belongs to the opening, not the target.");
        });
    }

    [Test]
    [PairConfig(nameof(ConnectedSettings))]
    public async Task ReplicatedRoofOpeningGivesClientAndServerTheSameShot()
    {
        EntityUid shooter = default;
        NetEntity shooterNet = default;
        NetEntity lowerNet = default;
        var originalAttached = ServerSession!.AttachedEntity;
        var target = new Vector2(8.5f, 0.5f);
        try
        {
            await Server.WaitAssertion(() =>
            {
                FillRoof();
                (shooter, _) = CreateShooter(-1, new Vector2(0.5f));
                Server.System<CMUZLevelShootingSystem>().SetShootDown(shooter, true);
                Server.PlayerMan.SetAttachedEntity(ServerSession, shooter);
                shooterNet = SEntMan.GetNetEntity(shooter);
                lowerNet = SEntMan.GetNetEntity(_lower);
            });

            foreach (var open in new[] { false, true })
            {
                MapCoordinates serverSpawn = default;
                await Server.WaitAssertion(() =>
                {
                    if (open)
                        SetUpper(new Vector2i(2, 0), "Space");
                    serverSpawn = AssertShot(shooter, target, open);
                });
                await Pair.RunUntilSynced();
                await Client.WaitAssertion(() =>
                {
                    var clientShooter = CEntMan.GetEntity(shooterNet);
                    var transform = Client.System<SharedTransformSystem>();
                    var from = CEntMan.GetComponent<TransformComponent>(clientShooter).Coordinates;
                    var to = transform.ToCoordinates(new MapCoordinates(target, transform.ToMapCoordinates(from).MapId));
                    Assert.That(Client.System<CMUZLevelShootingSystem>().TryAdjustShotCoordinates(clientShooter,
                        from, to, out var spawn, out _), Is.EqualTo(open));
                    if (!open)
                        return;

                    var coordinates = transform.ToMapCoordinates(spawn);
                    Assert.That(coordinates.MapId, Is.EqualTo(CEntMan.GetComponent<MapComponent>(CEntMan.GetEntity(lowerNet)).MapId));
                    Assert.That(Vector2.Distance(coordinates.Position, serverSpawn.Position), Is.LessThan(0.0001f));
                });
            }
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        }
    }

    private Tile GetTile(string id) => new(Server.ResolveDependency<ITileDefinitionManager>()[id].TileId);

    private void SetUpper(Vector2i position, string id)
    {
        Server.System<SharedMapSystem>().SetTile(_upper, _upperGrid, position, GetTile(id));
    }

    private void FillRoof()
    {
        for (var x = -2; x <= 20; x++)
        for (var y = -14; y <= 16; y++)
            SetUpper(new Vector2i(x, y), "CMURoofDestroyable");
    }

    private (EntityUid Shooter, EntityUid Gun) CreateShooter(int offset, Vector2 position)
    {
        var coordinates = new EntityCoordinates(offset < 0 ? _upper : _lower, position);
        var shooter = SEntMan.SpawnAttachedTo("MobHuman", coordinates);
        var gun = SEntMan.SpawnAttachedTo("CMUZOpeningTestPistol", coordinates);
        Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(shooter, gun), Is.True);
        SetAim(shooter, offset);
        return (shooter, gun);
    }

    private void SetAim(EntityUid shooter, int offset)
    {
        Server.System<CMUZLevelShootingSystem>().SetAimDown(shooter, offset < 0);
        SEntMan.EnsureComponent<CMUZLevelViewerComponent>(shooter).LookUp = offset > 0;
    }

    private MapCoordinates AssertShot(EntityUid shooter, Vector2 target, bool allowed)
    {
        var transform = Server.System<SharedTransformSystem>();
        var shooting = Server.System<CMUZLevelShootingSystem>();
        var from = SEntMan.GetComponent<TransformComponent>(shooter).Coordinates;
        var fromMap = transform.ToMapCoordinates(from);
        var to = new MapCoordinates(target, fromMap.MapId);
        Assert.That(shooting.TryAdjustShotCoordinates(shooter, from, transform.ToCoordinates(to),
            out var entitySpawn, out var entityTarget), Is.EqualTo(allowed));
        Assert.That(shooting.TryAdjustShotMapCoordinates(shooter, fromMap, to,
            out var mapSpawn, out var mapTarget), Is.EqualTo(allowed));
        if (allowed)
        {
            var expectedMap = SEntMan.GetComponent<TransformComponent>(shooter).MapUid == _upper ? _lower : _upper;
            Assert.That(mapSpawn.MapId, Is.EqualTo(SEntMan.GetComponent<MapComponent>(expectedMap).MapId));
            Assert.That(transform.ToMapCoordinates(entitySpawn), Is.EqualTo(mapSpawn));
            Assert.That(transform.ToMapCoordinates(entityTarget), Is.EqualTo(mapTarget));
            Assert.That(Vector2.Distance(mapTarget.Position, target), Is.LessThan(0.0001f));
        }

        return mapSpawn;
    }

    private List<EntityUid>? Fire(EntityUid shooter, EntityUid gun, Vector2 target)
    {
        var gunComp = SEntMan.GetComponent<GunComponent>(gun);
        gunComp.NextFire = SGameTiming.CurTime;
        gunComp.ShootCoordinates = new EntityCoordinates(SEntMan.GetComponent<TransformComponent>(shooter).MapUid!.Value, target);
        return Server.System<SharedGunSystem>().AttemptShoot(shooter, (gun, gunComp));
    }
}
