using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-111: the leader-bracer scuttle counts down in the open, cannot be carried off, and gibs no one
[TestFixture]
public sealed class BadBloodScuttleTest
{
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";
    private const string HunterBracer = "CMUYautjaBracer";

    [Test]
    public async Task WornBracerCountsDownStaysOnAndBlowsWithoutGibbingAnyone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid wearer = default;
        EntityUid bystander = default;
        EntityUid bracer = default;
        var fuse = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            wearer = SpawnWearer(entMan, map.GridCoords, out bracer);
            bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            // the wearer lock would refuse the unequip on its own, so take it out of the picture
            entMan.GetComponent<YautjaBracerComponent>(bracer).Locked = false;
            Assert.That(inventory.CanUnequip(wearer, "gloves", out _), Is.True);

            var scuttles = entMan.System<BadBloodScuttleSystem>();
            Assert.That(scuttles.TryStartScuttle(bracer), Is.True);
            Assert.That(scuttles.TryStartScuttle(bracer), Is.False, "a second call must not restart the fuse");

            var scuttle = entMan.GetComponent<BadBloodScuttleComponent>(bracer);
            fuse = scuttle.Fuse;
            Assert.Multiple(() =>
            {
                Assert.That(fuse, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(scuttle.MaxIntensity, Is.EqualTo(12f));
                Assert.That(scuttle.TotalIntensity, Is.EqualTo(120f));
                Assert.That(scuttle.IntensitySlope, Is.EqualTo(6f));
                Assert.That(scuttle.DetonateAt, Is.EqualTo(timing.CurTime + fuse));
                Assert.That(inventory.TryUnequip(wearer, "gloves", silent: true), Is.False,
                    "the bracer must stay on while it counts down");
            });
        });

        await pair.RunSeconds(4);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scuttle = entMan.GetComponent<BadBloodScuttleComponent>(bracer);
            Assert.Multiple(() =>
            {
                Assert.That(scuttle.LastWarnedSeconds, Is.InRange(5, 7), "the countdown must be announced each second");
                Assert.That(entMan.HasComponent<PointLightComponent>(bracer), Is.True, "the bracer must blink");
                Assert.That(entMan.System<InventorySystem>().TryUnequip(wearer, "gloves", silent: true), Is.False);
            });
        });

        await pair.RunSeconds((float) fuse.TotalSeconds);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(bracer), Is.True, "the bracer must be gone after the blast");
                Assert.That(entMan.Deleted(wearer), Is.False, "the wearer must not be gibbed");
                Assert.That(entMan.Deleted(bystander), Is.False, "a bystander must not be gibbed");
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(wearer);
            server.EntMan.DeleteEntity(bystander);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DroppedBracerCannotBePickedUpWhileItCountsDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid badBlood = default;
        EntityUid worn = default;
        EntityUid dropped = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            badBlood = SpawnWearer(entMan, map.GridCoords, out worn);
            dropped = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);

            Assert.That(hands.CanPickupAnyHand(badBlood, dropped), Is.True);
            Assert.That(entMan.System<BadBloodScuttleSystem>().TryStartScuttle(dropped, badBlood), Is.True);
            Assert.That(hands.CanPickupAnyHand(badBlood, dropped), Is.False,
                "a scuttling bracer must not be picked up");
            Assert.That(hands.TryPickupAnyHand(badBlood, dropped), Is.False);
        });

        await pair.RunSeconds(12);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(dropped), Is.True);
                Assert.That(entMan.Deleted(badBlood), Is.False, "a Bad Blood standing on the bracer must not be gibbed");
                Assert.That(entMan.Deleted(worn), Is.False, "the scuttle must not sweep up nearby Yautja gear");
            });
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(badBlood));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArmedSelfDestructIsLeftAloneAndHonourableBracersNeverScuttleOnTheirOwn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scuttles = entMan.System<BadBloodScuttleSystem>();
            var hunterBracer = entMan.SpawnEntity(HunterBracer, map.GridCoords);
            var badBloodBracer = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);
            var notABracer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<BadBloodScuttleComponent>(hunterBracer), Is.False);
                Assert.That(entMan.HasComponent<BadBloodScuttleComponent>(badBloodBracer), Is.False,
                    "only an explicit call starts a scuttle");
                Assert.That(scuttles.TryStartScuttle(notABracer), Is.False);
            });

            // the honourable blast is already running and destroys the bracer itself
            var armed = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
            armed.SelfDestructArmed = true;
            Assert.That(scuttles.TryStartScuttle(hunterBracer), Is.False);
            Assert.That(entMan.HasComponent<BadBloodScuttleComponent>(hunterBracer), Is.False);
            armed.SelfDestructArmed = false;

            entMan.DeleteEntity(hunterBracer);
            entMan.DeleteEntity(badBloodBracer);
            entMan.DeleteEntity(notABracer);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnWearer(IEntityManager entMan, EntityCoordinates coords, out EntityUid bracer)
    {
        var inventory = entMan.System<InventorySystem>();
        var user = entMan.SpawnEntity("CMUMobYautja", coords);
        entMan.EnsureComponent<YautjaBadBloodComponent>(user);

        if (inventory.TryUnequip(user, "gloves", out var oldBracer, silent: true, force: true))
            entMan.DeleteEntity(oldBracer.Value);

        bracer = entMan.SpawnEntity(BadBloodBracer, coords);
        Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
        return user;
    }
}
