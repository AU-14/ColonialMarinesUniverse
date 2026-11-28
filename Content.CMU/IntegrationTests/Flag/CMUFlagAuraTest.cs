using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Flag;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Roles;
using HardpointIntegrityComponent = Content.Shared._RMC14.Vehicle.HardpointIntegrityComponent;
using HardpointSystem = Content.Shared._RMC14.Vehicle.HardpointSystem;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Deploy;
using Content.Shared.CMU14.Flag;
using Content.Shared.CMU14.Sentry;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.CMU14.util;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.CMU14.Flag;

[TestFixture]
[TestOf(typeof(CMUFlagAuraSystem))]
[TestOf(typeof(CMUPlantedFlagSystem))]
public sealed class CMUFlagAuraTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private PlatoonPrototype? _originalGovfor;
    private PlatoonPrototype? _originalOpfor;

    // A round with selected platoons overrides the bearer's side, which most tests here rally.
    [SetUp]
    public async Task ClearPlatoons()
    {
        await Server.WaitPost(() =>
        {
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            _originalGovfor = platoons.SelectedGovforPlatoon;
            _originalOpfor = platoons.SelectedOpforPlatoon;
            platoons.SelectedGovforPlatoon = null;
            platoons.SelectedOpforPlatoon = null;
        });
    }

    [TearDown]
    public async Task RestorePlatoons()
    {
        await Server.WaitPost(() =>
        {
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            platoons.SelectedGovforPlatoon = _originalGovfor;
            platoons.SelectedOpforPlatoon = _originalOpfor;
        });
    }

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUFlagAuraTestVehicle
          components:
          - type: Vehicle
          - type: HardpointSlots
            vehicleFamily: Tank
            slots:
            - id: support
              hardpointType: Support
              slotType: SupportAttachment
              required: false
        """;

    private const string GovFor = "GOVFOR";
    private const string OpFor = "OPFOR";

    [Test]
    public async Task MountedFlagRalliesTheDriversSideLikeWarbanner()
    {
        var map = await Pair.CreateTestMap();
        EntityUid near = default;
        EntityUid far = default;
        EntityUid outOfRange = default;
        EntityUid enemy = default;
        EntityUid flag = default;
        EntityUid vehicle = default;

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            vehicle = SEntMan.SpawnEntity("CMUFlagAuraTestVehicle", Tile(map, new Vector2(0, 0)));
            var driver = SpawnMarine(map, new Vector2(0, 0), GovFor);
            near = SpawnMarine(map, new Vector2(2, 0), GovFor);
            // Beyond a regular warbanner's 4.515 range, inside the tank flag's 10.
            far = SpawnMarine(map, new Vector2(9, 0), GovFor);
            outOfRange = SpawnMarine(map, new Vector2(12, 0), GovFor);
            enemy = SpawnMarine(map, new Vector2(1, 0), OpFor);

            flag = SEntMan.SpawnEntity("VehicleTankMountedFlag", Tile(map, new Vector2(0, 0)));
            var slots = SEntMan.GetComponent<ItemSlotsComponent>(vehicle);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert((vehicle, slots), "support", flag, null, excludeUserAudio: true), Is.True);
            Assert.That(Server.System<VehicleSystem>().TrySetOperator((vehicle, SEntMan.GetComponent<VehicleComponent>(vehicle)), driver), Is.True);
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<MoveOrderComponent>(near), Is.True, "A mounted flag goes up as soon as a driver sits down.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True);
                Assert.That(SEntMan.HasComponent<MoveOrderComponent>(far), Is.True, "The tank flag reaches 10 tiles.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(outOfRange), Is.False, "The tank flag reaches no further than 10 tiles.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(enemy), Is.False, "A mounted flag rallies its driver's side.");
            });

            // The driver climbs out. With no platoons to say who owns the flag, it keeps rallying the last driver's side.
            Assert.That(Server.System<VehicleSystem>().TryRemoveOperator((vehicle, SEntMan.GetComponent<VehicleComponent>(vehicle))), Is.True);
        });

        // Let the orders from while the driver was seated run out.
        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True, "The mounted flag stays up with nobody driving.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(enemy), Is.False, "It still only rallies the last driver's side.");
            });

            // Removed from the vehicle, the hardpoint no longer rallies anyone.
            Assert.That(Server.System<SharedContainerSystem>().TryRemoveFromContainer(flag, force: true), Is.True);
        });

        // Let the warbanner's 5 second orders expire.
        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.False, "An unmounted tank flag has no aura."));
    }

    [Test]
    public async Task CarriedFlagRalliesLikeExtendedFlagAndPlantedLikeWarbanner()
    {
        var map = await Pair.CreateTestMap();
        EntityUid bearer = default;
        EntityUid near = default;
        EntityUid far = default;
        EntityUid enemy = default;
        EntityUid flag = default;

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            bearer = SpawnMarine(map, new Vector2(0, 0), GovFor);
            near = SpawnMarine(map, new Vector2(2, 0), GovFor);
            // Inside the extended flag's 8.466 range, outside the warbanner's 4.515 range.
            far = SpawnMarine(map, new Vector2(7, 0), GovFor);
            enemy = SpawnMarine(map, new Vector2(1, 0), OpFor);

            flag = SEntMan.SpawnEntity("AU14FlagCarriableUA", Tile(map, new Vector2(0, 0)));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(far), Is.True, "A carried flag reaches as far as an extended JIMA flag.");
                Assert.That(SEntMan.HasComponent<HoldOrderComponent>(far), Is.True);
                Assert.That(SEntMan.HasComponent<MoveOrderComponent>(near), Is.False, "A carried flag does not grant the move order.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(enemy), Is.False, "The flag only rallies its bearer's side.");
            });

            var doAfter = new DoAfterArgs(SEntMan, bearer, TimeSpan.FromSeconds(6), new PlantableFlagPlantDoAfterEvent(), flag, flag, flag)
            {
                BreakOnMove = true,
                NeedHand = true,
            };
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter), Is.True);
        });

        await Pair.RunSeconds(7);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<TransformComponent>(flag).Anchored, Is.True));

        // Let orders from the carried flag expire.
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<MoveOrderComponent>(near), Is.True, "A planted flag grants the warbanner's move order.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True);
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(far), Is.False, "A planted flag only reaches as far as a warbanner.");
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(enemy), Is.False, "A planted flag keeps rallying the side that planted it.");
            });
        });
    }

    [Test]
    public async Task JimaFlagDeploysForTheDeployersSide()
    {
        var map = await Pair.CreateTestMap();
        EntityUid engineer = default;
        EntityUid ally = default;
        EntityUid enemy = default;
        EntityUid flag = default;

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            engineer = SpawnMarine(map, new Vector2(0, 0), GovFor);
            ally = SpawnMarine(map, new Vector2(3, 0), GovFor);
            enemy = SpawnMarine(map, new Vector2(1, 0), OpFor);

            flag = SEntMan.SpawnEntity("CMUPlantedFlag", Tile(map, new Vector2(0, 0)));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(engineer, flag), Is.True);

            var doAfter = new DoAfterArgs(SEntMan, engineer, TimeSpan.FromSeconds(1), new CMUEquipmentDeployDoAfterEvent(), flag)
            {
                BreakOnMove = true,
            };
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter), Is.True);
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<CMUDeployableEquipmentComponent>(flag).DeployedState, Is.EqualTo(CMUDeployedState.Deployed));
                Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.Off));
                Assert.That(SEntMan.GetComponent<TransformComponent>(flag).Anchored, Is.True);
                Assert.That(Server.System<GunIFFSystem>().IsInFaction(flag, GovFor), Is.True, "The flag takes its deployer's side.");
            });

            Assert.That(Server.System<SharedInteractionSystem>().InteractionActivate(engineer, flag), Is.True);
            Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.On));
            Assert.That(SEntMan.HasComponent<DropshipTargetComponent>(flag), Is.True, "An active flag can be targeted by dropships.");
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(ally), Is.True);
                Assert.That(SEntMan.HasComponent<HoldOrderComponent>(ally), Is.True);
                Assert.That(SEntMan.HasComponent<MoveOrderComponent>(ally), Is.False);
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(enemy), Is.False);
            });

            var doAfter = new DoAfterArgs(SEntMan, engineer, TimeSpan.FromSeconds(1), new CMUEquipmentUnDeployDoAfterEvent(), flag)
            {
                BreakOnMove = true,
            };
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter), Is.True);
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<CMUDeployableEquipmentComponent>(flag).DeployedState, Is.EqualTo(CMUDeployedState.Undeployed));
                Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.Item));
                Assert.That(SEntMan.GetComponent<TransformComponent>(flag).Anchored, Is.False);
                Assert.That(SEntMan.HasComponent<DropshipTargetComponent>(flag), Is.False, "A packed-up flag is no longer a dropship target.");
            });
        });
    }

    [Test]
    public async Task FlagBuffsDoNotFlickerBetweenPulses()
    {
        var map = await Pair.CreateTestMap();
        EntityUid bearer = default;
        EntityUid near = default;

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            bearer = SpawnMarine(map, new Vector2(0, 0), GovFor);
            near = SpawnMarine(map, new Vector2(2, 0), GovFor);

            // The carried flag's orders last exactly as long as its pulse interval.
            var flag = SEntMan.SpawnEntity("AU14FlagCarriableUA", Tile(map, new Vector2(0, 0)));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);
        });

        await Pair.RunSeconds(1);

        // Several pulses, checked on every tick.
        for (var tick = 0; tick < 200; tick++)
        {
            await Server.WaitRunTicks(1);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True,
                    $"A marine near a flag lost its focus order on tick {tick}.");
                Assert.That(SEntMan.HasComponent<HoldOrderComponent>(near), Is.True,
                    $"A marine near a flag lost its hold order on tick {tick}.");
            });
        }
    }

    [Test]
    public async Task TankFlagStaysRaisedUntilTheTankIsBadlyDamaged()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tank = default;
        EntityUid flag = default;
        EntityUid driver = default;
        EntityUid near = default;
        EntityUid far = default;
        EntityUid enemy = default;

        await WithPlatoons("USCM", "UPP", async () =>
        {
            await Server.WaitAssertion(() =>
            {
                PaveStrip(map);
                tank = SEntMan.SpawnEntity("VehicleTank", Tile(map, new Vector2(0, 0)));
                driver = SpawnMarine(map, new Vector2(0, 0), GovFor);
                near = SpawnMarine(map, new Vector2(3, 0), GovFor);
                far = SpawnMarine(map, new Vector2(9, 0), GovFor);
                enemy = SpawnMarine(map, new Vector2(2, 1), OpFor);

                flag = SEntMan.SpawnEntity("VehicleTankMountedFlag", Tile(map, new Vector2(0, 0)));
                Assert.That(Server.System<ItemSlotsSystem>().TryInsert(tank, "support", flag, null), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True,
                    "The tank flag flies for the UA side before anyone has driven.");
                Assert.That(Server.System<VehicleSystem>().TrySetOperator((tank, SEntMan.GetComponent<VehicleComponent>(tank)), driver), Is.True);
            });

            await Pair.RunSeconds(1);
            await Server.WaitAssertion(() =>
            {
                // The driver climbs out; the flag stays up.
                Assert.That(Server.System<VehicleSystem>().TryRemoveOperator((tank, SEntMan.GetComponent<VehicleComponent>(tank))), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<MoveOrderComponent>(near), Is.True,
                        "The tank flag keeps rallying the UA side after its driver leaves.");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True);
                    Assert.That(SEntMan.HasComponent<MoveOrderComponent>(far), Is.True, "The tank flag reaches 10 tiles.");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(enemy), Is.False,
                        "The UA tank flag never rallies the UPP side.");
                });

                DamageHullTo(tank, 0.4f);
            });

            await Pair.RunSeconds(7);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True, "The flag stays up at 40% integrity.");
                DamageHullTo(tank, 0.3f);
            });

            // Let the 5 second orders run out.
            await Pair.RunSeconds(7);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.False, "The flag comes down at 35% integrity.");

                var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(tank);
                hull.Integrity = hull.MaxIntegrity;
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(near), Is.True,
                    "A repaired tank raises its flag again, driver or not."));
        });
    }

    private void DamageHullTo(EntityUid tank, float share)
    {
        var hull = SEntMan.GetComponent<HardpointIntegrityComponent>(tank);
        var hardpoints = Server.System<HardpointSystem>();
        for (var i = 0; i < 20 && hull.Integrity > hull.MaxIntegrity * share + 1f; i++)
        {
            hardpoints.DamageVehicleHull(tank, hull.Integrity - hull.MaxIntegrity * share);
        }

        Assert.That(hull.Integrity / hull.MaxIntegrity, Is.LessThanOrEqualTo(share + 0.01f));
    }

    [TestCase("AU14FlagCarriableUA")]
    [TestCase("AU14FlagCarriableUPP")]
    [TestCase("AU14FlagCarriableCLF")]
    [TestCase("RuMCFlagCarriableBEAR")]
    [TestCase("RMCFlagCarriableUN")]
    public async Task CarriableFlagsCanBeDestroyed(string flagId)
    {
        var map = await Pair.CreateTestMap();
        EntityUid flag = default;

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            flag = SEntMan.SpawnEntity(flagId, Tile(map, new Vector2(0, 0)));
            Assert.That(SEntMan.HasComponent<CMUSentryBreakableComponent>(flag), Is.False, "Carriable flags are never wrecked.");
            var fixtures = SEntMan.GetComponent<FixturesComponent>(flag);
            Assert.That(fixtures.Fixtures.ContainsKey("fix1"), Is.True, "Carriable flags keep their item fixture.");
            Assert.That(fixtures.Fixtures.ContainsKey("deploy"), Is.True, "Carriable flags can be shot.");

            var damage = new DamageSpecifier { DamageDict = new() { ["Blunt"] = 500 } };
            Server.System<DamageableSystem>().TryChangeDamage(flag, damage, ignoreResistances: true);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.Deleted(flag), Is.True, "Enough damage destroys a carriable flag."));
    }

    [TestCase("CMUFlagCarriableDecorativeUA", "AU14FlagCarriableUA")]
    [TestCase("CMUFlagCarriableDecorativeUPP", "AU14FlagCarriableUPP")]
    [TestCase("CMUFlagCarriableDecorativeCLF", "AU14FlagCarriableCLF")]
    [TestCase("CMUFlagCarriableDecorativeBEAR", "RuMCFlagCarriableBEAR")]
    [TestCase("CMUFlagCarriableDecorativeUN", "RMCFlagCarriableUN")]
    public async Task DecorativeFlagsNeverRally(string decorativeId, string realId)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            var decorative = SEntMan.SpawnEntity(decorativeId, Tile(map, new Vector2(0, 0)));
            var real = SEntMan.SpawnEntity(realId, Tile(map, new Vector2(2, 0)));
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUFlagAuraComponent>(decorative), Is.False, "Decorative flags give no buffs.");
                Assert.That(SEntMan.HasComponent<CMUFlagAuraComponent>(real), Is.True, "The real flag still rallies.");
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(decorative).EntityName,
                    Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(real).EntityName));
                Assert.That(SEntMan.GetComponent<FixturesComponent>(decorative).Fixtures.ContainsKey("deploy"), Is.True,
                    "Decorative flags can still be shot down.");
            });
        });
    }

    [Test]
    public async Task RoyalMarinesGetTheirOwnPlantedFlag()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var banner = SProtoMan.Index("CMUPlantedFlagRoyalMarines");
            Assert.That(banner.Parents, Does.Contain("CMUPlantedFlagExtendedRange"));
            Assert.That(banner.TryGetComponent(out CMUFlagAllegianceComponent? allegiance, factory), Is.True);
            Assert.That(allegiance!.Allegiances, Is.EquivalentTo(new[] { "TWE" }));
        });
    }

    [TestCase("AU14FlagCarriableUA", "USCM", "UPP", GovFor, true, false)]
    [TestCase("AU14FlagCarriableUPP", "USCM", "UPP", GovFor, false, true)]
    [TestCase("AU14FlagCarriableUA", "USCM", "LACN", OpFor, false, true)]
    [TestCase("RuMCFlagCarriableBEAR", "USCM", "BEAR", GovFor, false, true)]
    [TestCase("AU14FlagCarriableUPP", "USCM", "RMC", GovFor, false, false)]
    [TestCase("RMCFlagCarriableUN", "USCM", "UPP", OpFor, false, false)]
    public async Task CarriedFlagRalliesOnlyItsOwnSide(
        string flagId,
        string govfor,
        string opfor,
        string bearerSide,
        bool rallyGovfor,
        bool rallyOpfor)
    {
        var map = await Pair.CreateTestMap();
        EntityUid govforAlly = default;
        EntityUid opforAlly = default;

        await WithPlatoons(govfor, opfor, async () =>
        {
            await Server.WaitAssertion(() =>
            {
                PaveStrip(map);
                var bearer = SpawnMarine(map, new Vector2(0, 0), bearerSide);
                govforAlly = SpawnMarine(map, new Vector2(2, 0), GovFor);
                opforAlly = SpawnMarine(map, new Vector2(3, 0), OpFor);

                var flag = SEntMan.SpawnEntity(flagId, Tile(map, new Vector2(0, 0)));
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(govforAlly), Is.EqualTo(rallyGovfor), "GOVFOR");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(opforAlly), Is.EqualTo(rallyOpfor), "OPFOR");
                });
            });
        });
    }

    [Test]
    public async Task ClfFlagRalliesInsurgentsWithoutAClfIdCard()
    {
        var map = await Pair.CreateTestMap();
        EntityUid commando = default;
        EntityUid govfor = default;

        await WithPlatoons("USCM", "UPP", async () =>
        {
            await Server.WaitAssertion(() =>
            {
                PaveStrip(map);
                var bearer = SpawnInsurgent(map, new Vector2(0, 0));
                // Like a CLF commando reinforcement: INSFOR membership but a plain colonist ID.
                commando = SpawnInsurgent(map, new Vector2(2, 0));
                govfor = SpawnMarine(map, new Vector2(1, 0), GovFor);

                var flag = SEntMan.SpawnEntity("AU14FlagCarriableCLF", Tile(map, new Vector2(0, 0)));
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(commando), Is.True, "The CLF flag rallies INSFOR.");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(govfor), Is.False, "The CLF flag only rallies INSFOR.");
                });
            });
        });
    }

    [TestCase("RMCFlagCarriableUN", "AU14JobUNISCRifleman")]
    [TestCase("RMCFlagCarriableUNMC", "AU14JobUNISCRifleman")]
    [TestCase("RMCFlagCarriableUN", "AU14JobRedCrossDoctor")]
    public async Task UnFlagsOnlyRallyUnReinforcements(string flagId, string job)
    {
        var map = await Pair.CreateTestMap();
        EntityUid peacekeeper = default;
        EntityUid govfor = default;
        EntityUid opfor = default;

        await WithPlatoons("USCM", "UPP", async () =>
        {
            await Server.WaitAssertion(() =>
            {
                PaveStrip(map);
                var bearer = SpawnMarine(map, new Vector2(0, 0), GovFor);
                govfor = SpawnMarine(map, new Vector2(1, 0), GovFor);
                opfor = SpawnMarine(map, new Vector2(2, 0), OpFor);
                // A UNISC or Red Cross reinforcement, whichever side's IFF it carries.
                peacekeeper = SpawnMarine(map, new Vector2(3, 0), OpFor);
                SEntMan.EnsureComponent<OriginalRoleComponent>(peacekeeper).Job = job;

                var flag = SEntMan.SpawnEntity(flagId, Tile(map, new Vector2(0, 0)));
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(peacekeeper), Is.True, "UN flags rally UN reinforcements.");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(govfor), Is.False, "UN flags don't rally GOVFOR.");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(opfor), Is.False, "UN flags don't rally OPFOR.");
                });
            });
        });
    }

    // The UPP side's UPP flag rallies UPP reinforcements, but not UA ones fighting alongside it.
    [TestCase("AU14FlagCarriableUPP", OpFor, "CMUJobUPPMilitsiyaLead", OpFor, true)]
    [TestCase("AU14FlagCarriableUPP", OpFor, "CMUJobUPPMilitsiyaLead", "FactionUPPSOF", true)]
    [TestCase("AU14FlagCarriableUPP", OpFor, "AU14FORECONRifleman", OpFor, false)]
    [TestCase("RuMCFlagCarriableBEAR", OpFor, "AU14USArmyTrooper", OpFor, false)]
    [TestCase("AU14FlagCarriableUPP", OpFor, "AU14UPPGROMRifleman", "FactionUPPSOF", true)]
    [TestCase("AU14FlagCarriableUPP", OpFor, "CMUJobParamarineTeamLeader", OpFor, false)]
    // The UA side's UA flag rallies UA reinforcements, but not UPP ones fighting alongside it.
    [TestCase("AU14FlagCarriableUA", GovFor, "AU14FORECONRifleman", GovFor, true)]
    [TestCase("AU14FlagCarriableUA", GovFor, "AU14USArmyTrooper", "FactionUASOF", true)]
    [TestCase("AU14FlagCarriableUA", GovFor, "CMUJobUPPMilitsiyaLead", GovFor, false)]
    [TestCase("AU14FlagCarriableUA", GovFor, "AU14JobUNISCRifleman", GovFor, false)]
    [TestCase("AU14FlagCarriableUA", GovFor, "AU14UPPGROMRifleman", GovFor, false)]
    [TestCase("AU14FlagCarriableUA", GovFor, "CMUJobParamarineTeamLeader", GovFor, false)]
    // A UA reinforcement fighting for the UPP platoon isn't rallied by the other side's UA flag.
    [TestCase("AU14FlagCarriableUA", GovFor, "AU14FORECONRifleman", OpFor, false)]
    public async Task NationFlagsOnlyRallyTheirOwnReinforcements(
        string flagId,
        string bearerSide,
        string job,
        string reinforcementIff,
        bool rallied)
    {
        var map = await Pair.CreateTestMap();
        EntityUid reinforcement = default;
        EntityUid platoon = default;

        await WithPlatoons("USCM", "UPP", async () =>
        {
            await Server.WaitAssertion(() =>
            {
                PaveStrip(map);
                var bearer = SpawnMarine(map, new Vector2(0, 0), bearerSide);
                platoon = SpawnMarine(map, new Vector2(1, 0), bearerSide);
                reinforcement = SpawnMarine(map, new Vector2(2, 0), reinforcementIff);
                SEntMan.EnsureComponent<OriginalRoleComponent>(reinforcement).Job = job;

                var flag = SEntMan.SpawnEntity(flagId, Tile(map, new Vector2(0, 0)));
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(platoon), Is.True, "The flag still rallies its platoon.");
                    Assert.That(SEntMan.HasComponent<FocusOrderComponent>(reinforcement), Is.EqualTo(rallied), job);
                });
            });
        });
    }

    [Test]
    public async Task ReinforcementListsNameRealThirdParties()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var list in SProtoMan.EnumeratePrototypes<CMUFlagReinforcementsPrototype>())
                {
                    foreach (var party in list.ThirdParties)
                    {
                        Assert.That(SProtoMan.TryIndex(party, out var partyProto), Is.True, $"{list.ID}: {party}");
                        Assert.That(SProtoMan.HasIndex(partyProto!.PartySpawn), Is.True, $"{list.ID}: {party} spawn");
                    }
                }
            });
        });
    }

    [TestCase("CMUPlantedFlag", "CMUFlagReinforcementsUA")]
    [TestCase("CMUPlantedFlagWarbanner", "CMUFlagReinforcementsUA")]
    [TestCase("CMUPlantedFlagUPP", "CMUFlagReinforcementsUPP")]
    [TestCase("CMUPlantedFlagRoyalMarines", "CMUFlagReinforcementsRMC")]
    [TestCase("AU14FlagCarriableUA", "CMUFlagReinforcementsUA")]
    [TestCase("AU14FlagCarriableUPP", "CMUFlagReinforcementsUPP")]
    [TestCase("RuMCFlagCarriableBEAR", "CMUFlagReinforcementsUPP")]
    [TestCase("CMUPlantedFlagCLF", null)]
    [TestCase("CMUPlantedFlagWeYu", "CMUFlagReinforcementsWY")]
    public async Task FlagsUseTheirNationsReinforcements(string flagId, string? reinforcements)
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            Assert.That(SProtoMan.Index(flagId).TryGetComponent(out CMUFlagAllegianceComponent? allegiance, factory), Is.True);
            Assert.That(allegiance!.Reinforcements?.Id, Is.EqualTo(reinforcements));
        });
    }

    [Test]
    public async Task UnPlantedFlagOnlyRalliesUnReinforcements()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            Assert.That(SProtoMan.Index("CMUPlantedFlagUN").TryGetComponent(out CMUFlagAllegianceComponent? allegiance, factory), Is.True);
            Assert.That(allegiance!.Reinforcements?.Id, Is.EqualTo("CMUFlagReinforcementsUN"));
            Assert.That(allegiance.Allegiances, Is.Empty);
        });
    }

    // The Weyland-Yutani flag rallies corporate reinforcements and colony staff, but not other nations' reinforcements.
    [TestCase("AU14JobCivilianCorporateLiaison", GovFor, true)]
    [TestCase("AU14JobWYGuard", GovFor, true)]
    [TestCase("CMUJobWeYuGoon", "FactionCMB", true)]
    [TestCase("AU14JobWYPMCPartyLeader", GovFor, true)]
    [TestCase("AU14FORECONRifleman", GovFor, false)]
    [TestCase("CMUJobUPPMilitsiyaLead", GovFor, false)]
    // A corporate reinforcement fighting for the other platoon isn't rallied.
    [TestCase("AU14JobCivilianCorporateLiaison", OpFor, false)]
    public async Task WeYuFlagRalliesCorporateReinforcements(string job, string reinforcementIff, bool rallied)
    {
        var map = await Pair.CreateTestMap();
        EntityUid engineer = default;
        EntityUid reinforcement = default;
        EntityUid flag = default;

        await WithPlatoons("WEYU", "UPP", async () =>
        {
            await Server.WaitAssertion(() =>
            {
                PaveStrip(map);
                engineer = SpawnMarine(map, new Vector2(0, 0), GovFor);
                reinforcement = SpawnMarine(map, new Vector2(2, 0), reinforcementIff);
                SEntMan.EnsureComponent<OriginalRoleComponent>(reinforcement).Job = job;

                flag = SEntMan.SpawnEntity("CMUPlantedFlagWeYu", Tile(map, new Vector2(0, 0)));
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(engineer, flag), Is.True);
                var doAfter = new DoAfterArgs(SEntMan, engineer, TimeSpan.FromSeconds(1), new CMUEquipmentDeployDoAfterEvent(), flag)
                {
                    BreakOnMove = true,
                };
                Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedInteractionSystem>().InteractionActivate(engineer, flag), Is.True);
                Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.On));
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.HasComponent<FocusOrderComponent>(reinforcement), Is.EqualTo(rallied), job));
        });
    }

    [Test]
    public async Task WreckedPlantedFlagLowersAndStaysDownUntilRepaired()
    {
        var map = await Pair.CreateTestMap();
        EntityUid engineer = default;
        EntityUid ally = default;
        EntityUid flag = default;

        await Server.WaitAssertion(() =>
        {
            PaveStrip(map);
            engineer = SpawnMarine(map, new Vector2(0, 0), GovFor);
            ally = SpawnMarine(map, new Vector2(2, 0), GovFor);

            flag = SEntMan.SpawnEntity("CMUPlantedFlag", Tile(map, new Vector2(0, 0)));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(engineer, flag), Is.True);
            var doAfter = new DoAfterArgs(SEntMan, engineer, TimeSpan.FromSeconds(1), new CMUEquipmentDeployDoAfterEvent(), flag)
            {
                BreakOnMove = true,
            };
            Assert.That(Server.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter), Is.True);
        });

        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedInteractionSystem>().InteractionActivate(engineer, flag), Is.True);
            Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.On));

            var damage = new DamageSpecifier { DamageDict = new() { ["Blunt"] = 500 } };
            Server.System<DamageableSystem>().TryChangeDamage(flag, damage, ignoreResistances: true);
        });

        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.Deleted(flag), Is.False, "Damage wrecks a planted flag instead of destroying it.");
                Assert.That(SEntMan.HasComponent<CMUSentryBrokenComponent>(flag), Is.True);
                Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.Off), "A wrecked flag comes down.");
                Assert.That(SEntMan.HasComponent<DropshipTargetComponent>(flag), Is.False, "A wrecked flag stops marking itself for dropships.");
            });

            Server.System<SharedInteractionSystem>().InteractionActivate(engineer, flag);
            Assert.That(SEntMan.GetComponent<CMUPlantedFlagComponent>(flag).Mode, Is.EqualTo(CMUFlagMode.Off), "A wrecked flag can't be raised.");
        });

        // Let the last orders run out.
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<FocusOrderComponent>(ally), Is.False, "A wrecked flag rallies nobody."));
    }

    [Test]
    public async Task PlantedFlagRepairStepsAreFlavouredForFlags()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            Assert.That(SProtoMan.Index("CMUPlantedFlag").TryGetComponent(out CMUSentryBreakableComponent? breakable, factory), Is.True);
            var prefix = breakable!.LocPrefix;
            Assert.That(prefix, Is.EqualTo("cmu-planted-flag"));

            var loc = IoCManager.Resolve<ILocalizationManager>();
            string[] steps = ["cut", "weld-frame", "rewire", "screw", "tune", "weld-casing"];
            var keys = new List<string>
            {
                "broken", "broken-examine", "repair-deploy-first", "repair-wrong-tool", "repair-need-cable",
                "repair-finished", "repair-finished-others",
            };
            foreach (var step in steps)
            {
                keys.Add($"repair-step-{step}");
                keys.Add($"repair-start-{step}");
                if (step != "weld-casing")
                    keys.Add($"repair-done-{step}");
            }

            Assert.Multiple(() =>
            {
                foreach (var key in keys)
                {
                    Assert.That(loc.HasString($"{prefix}-{key}"), Is.True, $"{prefix}-{key}");
                }
            });
        });
    }

    private async Task WithPlatoons(string govfor, string opfor, Func<Task> test)
    {
        var platoons = Server.System<PlatoonSpawnRuleSystem>();
        PlatoonPrototype? originalGovfor = null;
        PlatoonPrototype? originalOpfor = null;
        await Server.WaitPost(() =>
        {
            originalGovfor = platoons.SelectedGovforPlatoon;
            originalOpfor = platoons.SelectedOpforPlatoon;
            platoons.SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>(govfor);
            platoons.SelectedOpforPlatoon = SProtoMan.Index<PlatoonPrototype>(opfor);
        });

        try
        {
            await test();
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                platoons.SelectedGovforPlatoon = originalGovfor;
                platoons.SelectedOpforPlatoon = originalOpfor;
            });
        }
    }

    private EntityUid SpawnInsurgent(TestMapData map, Vector2 tile)
    {
        var insurgent = SEntMan.SpawnEntity("CMMobHuman", Tile(map, tile));
        SEntMan.EnsureComponent<MarineComponent>(insurgent);
        SEntMan.EnsureComponent<CLFMemberComponent>(insurgent);
        return insurgent;
    }

    private void PaveStrip(TestMapData map)
    {
        var maps = Server.System<SharedMapSystem>();
        for (var x = -2; x <= 14; x++)
        for (var y = -2; y <= 2; y++)
            maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
    }

    private static EntityCoordinates Tile(TestMapData map, Vector2 tile)
        => map.GridCoords.Offset(tile + new Vector2(0.5f, 0.5f));

    private EntityUid SpawnMarine(TestMapData map, Vector2 tile, string faction)
    {
        var marine = SEntMan.SpawnEntity("CMMobHuman", Tile(map, tile));
        SEntMan.EnsureComponent<MarineComponent>(marine);
        Server.System<GunIFFSystem>().SetUserFaction(marine, faction);
        return marine;
    }
}
