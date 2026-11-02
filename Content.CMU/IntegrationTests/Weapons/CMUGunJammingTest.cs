#pragma warning disable RA0002 // Tests reset the gun cooldown so every shot fires on demand.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.CMU14.Weapons.Jamming;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
[TestOf(typeof(CMUGunJammingSystem))]
public sealed class CMUGunJammingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = @"
- type: cmuGunJamming
  id: CMUGunJammingTest
  parent: CMUGunJammingDefault
  maxWear: 100
  baseWearPerShot: 1
  semiAutoWearMultiplier: 1
  burstWearMultiplier: 2
  fullAutoWearMultiplier: 3
  shotgunTags:
  - RMCWeaponShotgun
  shotgunSemiAutoWearMultiplier: 4
  ammoIdKeywords:
    AP: 5
  minJamChance: 0
  maxJamChance: 0
  slamDelay: 0.5
  slamWear: 10
  slamBreakChance: 0
  slamBreakChanceAtMaxWear: 0
  jamTypes:
    FailureToFeed:
      name: cmu-gun-jam-type-feed
      cycleDelay: 0.5
      cycleClearChance: 1
      cycleRoundLoss: None
      slamClearChance: 1
      slamDischargeChance: 0
    FailureToEject:
      name: cmu-gun-jam-type-eject
      cycleDelay: 0.5
      cycleClearChance: 1
      cycleRoundLoss: None
      slamClearChance: 1
      slamDischargeChance: 0
    FailureToChamber:
      name: cmu-gun-jam-type-chamber
      cycleDelay: 0.5
      cycleClearChance: 1
      cycleRoundLoss: Eject
      slamClearChance: 1
      slamDischargeChance: 1
    FailureToFire:
      name: cmu-gun-jam-type-fire
      cycleDelay: 0.5
      cycleClearChance: 1
      cycleRoundLoss: Destroy
      slamClearChance: 1
      slamDischargeChance: 0

- type: cmuGunJamming
  id: CMUGunJammingTestAlwaysJam
  parent: CMUGunJammingTest
  jamWearThreshold: 0
  minJamChance: 1
  maxJamChance: 1

- type: cmuGunJamming
  id: CMUGunJammingTestBreak
  parent: CMUGunJammingTest
  slamBreakChance: 1
  slamBreakChanceAtMaxWear: 1

- type: entity
  parent: CMCartridgePistol9mm
  id: CMUGunJammingTestCartridgeAP
  components:
  - type: CMUGunWearAmmo
    multiplier: 7

- type: entity
  parent: BaseItem
  id: CMUGunJammingTestPlainGun
  components:
  - type: Gun
    selectedMode: SemiAuto
    availableModes: [SemiAuto]
  - type: BallisticAmmoProvider
    proto: CMCartridgePistol9mm
    capacity: 10

- type: entity
  parent: CMUGunJammingTestPlainGun
  id: CMUGunJammingTestSemi
  components:
  - type: CMUGunJamming
    profile: CMUGunJammingTest

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestBurst
  components:
  - type: Gun
    selectedMode: Burst
    availableModes: [Burst]

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestAuto
  components:
  - type: Gun
    selectedMode: FullAuto
    availableModes: [FullAuto]

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestAP
  components:
  - type: BallisticAmmoProvider
    proto: CMCartridgeRifle10x24mmAP

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestWearAmmo
  components:
  - type: BallisticAmmoProvider
    proto: CMUGunJammingTestCartridgeAP

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestShotgunSemi
  components:
  - type: Tag
    tags:
    - RMCWeaponShotgun
  - type: BallisticAmmoProvider
    proto: CMShellShotgunBuckshot
    capacity: 5

- type: entity
  parent: CMUGunJammingTestShotgunSemi
  id: CMUGunJammingTestShotgunPump
  components:
  - type: PumpAction
    pumped: true

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestAlwaysJamGun
  components:
  - type: CMUGunJamming
    profile: CMUGunJammingTestAlwaysJam

- type: entity
  parent: CMUGunJammingTestSemi
  id: CMUGunJammingTestBreakGun
  components:
  - type: CMUGunJamming
    profile: CMUGunJammingTestBreak
";

    private const float Tolerance = 0.001f;

    [Test]
    public async Task DefaultProfileRanksWearSources()
    {
        await Server.WaitAssertion(() =>
        {
            var profile = SProtoMan.Index(CMUGunJammingSystem.DefaultProfile);
            Assert.Multiple(() =>
            {
                Assert.That(profile.Enabled && profile.AutoApply, Is.True);
                Assert.That(profile.FullAutoWearMultiplier, Is.GreaterThan(profile.BurstWearMultiplier));
                Assert.That(profile.BurstWearMultiplier, Is.GreaterThan(profile.SemiAutoWearMultiplier));
                Assert.That(profile.ShotgunSemiAutoWearMultiplier, Is.GreaterThan(1f));
                Assert.That(profile.AmmoIdKeywords["AP"], Is.GreaterThan(1f));
                Assert.That(profile.SlamWear, Is.GreaterThan(0f));
                Assert.That(profile.SlamBreakChance, Is.GreaterThan(0f));
                foreach (var jam in Enum.GetValues<CMUGunJamType>().Where(j => j != CMUGunJamType.None))
                {
                    Assert.That(profile.JamTypes.ContainsKey(jam), Is.True, $"{jam} has no settings");
                }
            });
        });
    }

    [Test]
    public async Task GunsGetDefaultProfileOnSpawn()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            foreach (var proto in new[] { "CMUGunJammingTestPlainGun", "RMCWeaponRifleM54C" })
            {
                var gun = SEntMan.SpawnAtPosition(proto, map.GridCoords);
                Assert.That(SEntMan.TryGetComponent(gun, out CMUGunJammingComponent? comp), Is.True, proto);
                Assert.That(comp!.Profile, Is.EqualTo(CMUGunJammingSystem.DefaultProfile), proto);
            }

            var configured = SEntMan.SpawnAtPosition("CMUGunJammingTestBreakGun", map.GridCoords);
            Assert.That(SEntMan.GetComponent<CMUGunJammingComponent>(configured).Profile.Id,
                Is.EqualTo("CMUGunJammingTestBreak"), "Per-gun profiles must not be replaced by the default.");
        });
    }

    [TestCase("CMUGunJammingTestSemi", 1f)]
    [TestCase("CMUGunJammingTestBurst", 2f)]
    [TestCase("CMUGunJammingTestAuto", 3f)]
    [TestCase("CMUGunJammingTestAP", 5f)]
    [TestCase("CMUGunJammingTestWearAmmo", 7f)]
    [TestCase("CMUGunJammingTestShotgunPump", 1f)]
    [TestCase("CMUGunJammingTestShotgunSemi", 4f)]
    public async Task FiringAddsWear(string gunProto, float expectedWear)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var (user, gun) = SpawnArmed(map, gunProto);
            Assert.That(Fire(map, user, gun), Is.Not.Null.And.Not.Empty, "The shot must actually fire.");
            Assert.That(SEntMan.GetComponent<CMUGunJammingComponent>(gun).Wear,
                Is.EqualTo(expectedWear).Within(Tolerance));
        });
    }

    [Test]
    public async Task JammedGunWontFireUntilCycled()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid gun = default;
        var ammo = 0;
        await Server.WaitAssertion(() =>
        {
            (user, gun) = SpawnArmed(map, "CMUGunJammingTestAlwaysJamGun");
            Assert.That(Fire(map, user, gun), Is.Not.Null.And.Not.Empty);
            Assert.That(Jamming.IsJammed(gun), Is.True);

            ammo = AmmoCount(gun);
            Assert.That(Fire(map, user, gun), Is.Null, "A jammed gun must not fire.");
            Assert.That(AmmoCount(gun), Is.EqualTo(ammo));

            Jamming.SetJam(gun, CMUGunJamType.FailureToFeed);
            var ev = new UniqueActionEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(gun, ev);
            Assert.That(ev.Handled, Is.True, "Working the action must start clearing the jam.");
        });

        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Jamming.IsJammed(gun), Is.False);
            Assert.That(AmmoCount(gun), Is.EqualTo(ammo), "Clearing a failure to feed keeps the round.");
            Assert.That(Fire(map, user, gun), Is.Not.Null.And.Not.Empty, "A cleared gun fires again.");
        });
    }

    [TestCase(CMUGunJamType.FailureToFeed, 0, 0)]
    [TestCase(CMUGunJamType.FailureToEject, 0, 0)]
    [TestCase(CMUGunJamType.FailureToChamber, 1, 1)]
    [TestCase(CMUGunJamType.FailureToFire, 1, 0)]
    public async Task CyclingClearsEachJamType(CMUGunJamType jam, int roundsLost, int roundsDropped)
    {
        var map = await Pair.CreateTestMap();
        EntityUid gun = default;
        var ammo = 0;
        await Server.WaitAssertion(() =>
        {
            (var user, gun) = SpawnArmed(map, "CMUGunJammingTestSemi");
            ammo = AmmoCount(gun);
            Jamming.SetJam(gun, jam);
            Assert.That(Jamming.TryStartCycle((gun, SEntMan.GetComponent<CMUGunJammingComponent>(gun)), user),
                Is.True);
        });

        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Jamming.IsJammed(gun), Is.False);
            Assert.That(AmmoCount(gun), Is.EqualTo(ammo - roundsLost));
            Assert.That(CountLooseCartridges(map.MapUid), Is.EqualTo(roundsDropped));
        });
    }

    [Test]
    public async Task SlammingStockClearsJamAtWearCost()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid gun = default;
        var ammo = 0;
        await Server.WaitAssertion(() =>
        {
            (user, gun) = SpawnArmed(map, "CMUGunJammingTestSemi");
            ammo = AmmoCount(gun);
            Jamming.SetWear(gun, 20);
            Jamming.SetJam(gun, CMUGunJamType.FailureToFeed);

            var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(gun, user, typeof(Verb));
            Assert.That(verbs.Any(v => v.Text == LocString("cmu-gun-jam-verb-cycle")), Is.True);
            var slam = verbs.Single(v => v.Text == LocString("cmu-gun-jam-verb-slam"));
            slam.Act!();
        });

        await RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUGunJammingComponent>(gun);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Jam, Is.EqualTo(CMUGunJamType.None));
                Assert.That(comp.Broken, Is.False);
                Assert.That(comp.Wear, Is.EqualTo(30f).Within(Tolerance));
                Assert.That(AmmoCount(gun), Is.EqualTo(ammo));
            });

            var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(gun, user, typeof(Verb));
            Assert.That(verbs.Any(v => v.Text == LocString("cmu-gun-jam-verb-slam")), Is.False,
                "Unjammed guns don't offer the slam.");
        });
    }

    [Test]
    public async Task SlammingStockCanDischargeStuckRound()
    {
        var map = await Pair.CreateTestMap();
        EntityUid gun = default;
        var ammo = 0;
        await Server.WaitAssertion(() =>
        {
            (var user, gun) = SpawnArmed(map, "CMUGunJammingTestSemi");
            ammo = AmmoCount(gun);
            Jamming.SetJam(gun, CMUGunJamType.FailureToChamber);
            Assert.That(Jamming.TryStartSlam((gun, SEntMan.GetComponent<CMUGunJammingComponent>(gun)), user),
                Is.True);
        });

        await RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUGunJammingComponent>(gun);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Jam, Is.EqualTo(CMUGunJamType.None));
                Assert.That(comp.Broken, Is.False);
                Assert.That(comp.Wear, Is.EqualTo(10f).Within(Tolerance));
                Assert.That(AmmoCount(gun), Is.EqualTo(ammo - 1), "The discharge spends the stuck round.");
                Assert.That(CountLooseCartridges(map.MapUid), Is.Zero);
            });
        });
    }

    [Test]
    public async Task SlammingStockCanDestroyInternals()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid gun = default;
        await Server.WaitAssertion(() =>
        {
            (user, gun) = SpawnArmed(map, "CMUGunJammingTestBreakGun");
            Jamming.SetJam(gun, CMUGunJamType.FailureToFeed);
            Assert.That(Jamming.TryStartSlam((gun, SEntMan.GetComponent<CMUGunJammingComponent>(gun)), user),
                Is.True);
        });

        await RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUGunJammingComponent>(gun);
            Assert.That(comp.Broken, Is.True);
            Assert.That(comp.Jam, Is.EqualTo(CMUGunJamType.None));

            var ammo = AmmoCount(gun);
            Assert.That(Fire(map, user, gun), Is.Null, "A gun with destroyed internals never fires.");
            Assert.That(AmmoCount(gun), Is.EqualTo(ammo));

            var ev = new UniqueActionEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(gun, ev);
            Assert.That(ev.Handled, Is.True, "The broken gun swallows the unique action.");

            var oil = SEntMan.SpawnAtPosition("RMCHelmetGarbGunOil", map.GridCoords);
            Assert.That(Jamming.TryStartMaintenance((gun, comp),
                (oil, SEntMan.GetComponent<CMUGunMaintenanceToolComponent>(oil)), user), Is.False,
                "Maintenance can't fix destroyed internals.");
        });
    }

    [Test]
    public async Task MaintenanceRemovesWear()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid gun = default;
        EntityUid rag = default;
        CMUGunMaintenanceToolComponent ragTool = default!;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            gun = SEntMan.SpawnAtPosition("CMUGunJammingTestSemi", map.GridCoords);
            rag = SEntMan.SpawnAtPosition("RagItem", map.GridCoords);
            ragTool = SEntMan.GetComponent<CMUGunMaintenanceToolComponent>(rag);
            Assert.That(ragTool.MinWear, Is.GreaterThan(0f), "Rags can't fully restore a gun.");
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, rag), Is.True);

            Jamming.SetWear(gun, 80);
            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(user, rag, gun, map.GridCoords),
                Is.True);
        });

        await RunSeconds((float) ragTool.Delay.TotalSeconds + 1);

        var floor = 0f;
        await Server.WaitAssertion(() =>
        {
            floor = ragTool.MinWear * 100;
            Assert.That(Wear(gun), Is.EqualTo(MathF.Max(floor, 80 - ragTool.WearRemoved * 100)).Within(Tolerance));

            Jamming.SetWear(gun, floor + 1);
            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(user, rag, gun, map.GridCoords),
                Is.True);
        });

        await RunSeconds((float) ragTool.Delay.TotalSeconds + 1);

        EntityUid oil = default;
        CMUGunMaintenanceToolComponent oilTool = default!;
        var uses = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Wear(gun), Is.EqualTo(floor).Within(Tolerance), "Rags stop at their wear floor.");
            Assert.That(Jamming.TryStartMaintenance((gun, SEntMan.GetComponent<CMUGunJammingComponent>(gun)),
                (rag, ragTool), user), Is.False, "Nothing left for the rag to clean.");

            SEntMan.DeleteEntity(rag);
            oil = SEntMan.SpawnAtPosition("RMCHelmetGarbGunOil", map.GridCoords);
            oilTool = SEntMan.GetComponent<CMUGunMaintenanceToolComponent>(oil);
            Assert.That(oilTool.Uses, Is.Not.Null.And.GreaterThan(1));
            uses = oilTool.Uses!.Value;
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, oil), Is.True);

            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(user, oil, gun, map.GridCoords),
                Is.True);
        });

        await RunSeconds((float) oilTool.Delay.TotalSeconds + 1);

        await Server.WaitAssertion(() =>
        {
            var expected = MathF.Max(oilTool.MinWear * 100, floor - oilTool.WearRemoved * 100);
            Assert.That(Wear(gun), Is.EqualTo(expected).Within(Tolerance), "Gun oil gets past the rag's floor.");
            Assert.That(oilTool.Uses, Is.EqualTo(uses - 1));
        });
    }

    [Test]
    public async Task TowelsAndRagsAreMaintenanceTools()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            foreach (var id in new[] { "TowelColorWhite", "RagItem", "RMCHelmetGarbGunOil" })
            {
                Assert.That(SProtoMan.Index<EntityPrototype>(id).HasComp<CMUGunMaintenanceToolComponent>(factory),
                    Is.True, id);
            }
        });
    }

    private CMUGunJammingSystem Jamming => Server.System<CMUGunJammingSystem>();

    private (EntityUid User, EntityUid Gun) SpawnArmed(TestMapData map, string gunProto)
    {
        var user = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
        var gun = SEntMan.SpawnAtPosition(gunProto, map.GridCoords);
        Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
        return (user, gun);
    }

    private List<EntityUid>? Fire(TestMapData map, EntityUid user, EntityUid gun)
    {
        var gunComp = SEntMan.GetComponent<GunComponent>(gun);
        gunComp.NextFire = SGameTiming.CurTime;
        return Server.System<SharedGunSystem>()
            .AttemptShoot((gun, gunComp), user, map.GridCoords.Offset(new Vector2(4, 0)));
    }

    private int AmmoCount(EntityUid gun)
    {
        return SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun).Count;
    }

    private float Wear(EntityUid gun)
    {
        return SEntMan.GetComponent<CMUGunJammingComponent>(gun).Wear;
    }

    private int CountLooseCartridges(EntityUid mapUid)
    {
        var containers = Server.System<SharedContainerSystem>();
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<CartridgeAmmoComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == mapUid && !containers.IsEntityInContainer(uid))
                count++;
        }

        return count;
    }

    private string LocString(string id)
    {
        return Server.ResolveDependency<ILocalizationManager>().GetString(id);
    }
}
