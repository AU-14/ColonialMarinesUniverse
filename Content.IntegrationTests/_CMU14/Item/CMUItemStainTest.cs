using System.Linq;
using Content.Client.CMU14.Item.Stain;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.Inventory;
using Content.Shared.StepTrigger.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Item;

[TestFixture]
[TestOf(typeof(CMUItemStainSystem))]
public sealed class CMUItemStainTest
{
    private const string StainableItem = "CMUItemStainTestItem";
    private const string UnstainableItem = "CMUItemStainTestImmuneItem";
    private const string CleanerItem = "CMUItemStainTestCleaner";
    private const string PreEffectReactionItem = "CMUItemStainTestPreEffectReactionItem";
    private const string VisualItem = "CMUItemStainTestVisualItem";
    private const string WaterPuddle = "CMUItemStainTestWaterPuddle";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: BaseItem
  id: {StainableItem}

- type: entity
  parent: BaseItem
  id: {UnstainableItem}
  components:
  - type: CMUItemStain
    canStain: false

- type: entity
  parent: BaseItem
  id: {CleanerItem}
  components:
  - type: CleansForensics
    cleanDelay: 0.1
  - type: Forensics

- type: entity
  parent: BaseItem
  id: {PreEffectReactionItem}
  components:
  - type: Solution
    id: stain
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Blood
        Quantity: 10
      - ReagentId: Oil
        Quantity: 5
  - type: Reactive
    reactions:
    - reagents: [ Blood ]
      methods: [ Touch ]
      effects:
      - !type:AdjustReagent
        reagent: Blood
        amount: -1

- type: entity
  parent: BaseItem
  id: {VisualItem}
  components:
  - type: Sprite
    sprite: Objects/Tools/crowbar.rsi
    state: icon
  - type: CMUItemStain
    color: ""#800000""
    wornStates:
      head: m10helmet_blood

- type: entity
  parent: Puddle
  id: {WaterPuddle}
  components:
  - type: SolutionContainerManager
    solutions:
      puddle:
        maxVol: 1000
        reagents:
        - ReagentId: Water
          Quantity: 10
";

    [Test]
    public async Task StainStateReplacesExaminesAndCleans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stains = entMan.System<CMUItemStainSystem>();
            var item = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            var immune = entMan.SpawnEntity(UnstainableItem, MapCoordinates.Nullspace);

            try
            {
                Assert.That(stains.TryStain(item, CMUItemStainKind.Blood, Color.Red), Is.True);
                var component = entMan.GetComponent<CMUItemStainComponent>(item);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Kind, Is.EqualTo(CMUItemStainKind.Blood));
                    Assert.That(component.Color, Is.EqualTo(Color.Red));
                    Assert.That(stains.TryStain(item, CMUItemStainKind.Blood, Color.Red), Is.False);
                    Assert.That(stains.TryStain(immune, CMUItemStainKind.Blood, Color.Red), Is.False);
                });

                var examine = new ExaminedEvent(new FormattedMessage(), item, item, true, false);
                entMan.EventBus.RaiseLocalEvent(item, examine);
                Assert.That(examine.GetTotalMessage().ToMarkup(), Does.Contain("blood-stained"));

                var eligibility = new CMUCleaningEligibilityEvent(false, 0f);
                entMan.EventBus.RaiseLocalEvent(item, ref eligibility);
                Assert.Multiple(() =>
                {
                    Assert.That(eligibility.CanClean, Is.True);
                    Assert.That(eligibility.DistanceThreshold, Is.EqualTo(1.5f));
                });

                var oil = Color.FromHex("#030303");
                Assert.That(stains.TryStain(item, CMUItemStainKind.Oil, oil), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Kind, Is.EqualTo(CMUItemStainKind.Oil));
                    Assert.That(component.Color, Is.EqualTo(oil));
                    Assert.That(stains.TryClean(item), Is.True);
                    Assert.That(component.Color, Is.Null);
                    Assert.That(stains.TryClean(item), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(item);
                entMan.DeleteEntity(immune);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReagentTouchUsesDominantStainAndCleanerPrecedence()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var inventory = entMan.System<InventorySystem>();
            var reactive = entMan.System<ReactiveSystem>();
            var item = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            var wearer = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var shoes = entMan.SpawnEntity("CMBootsBlack", MapCoordinates.Nullspace);

            try
            {
                var stainMix = new Solution("Blood", FixedPoint2.New(5));
                stainMix.AddSolution(new Solution("Oil", FixedPoint2.New(10)), prototypes);
                reactive.DoEntityReaction(item, stainMix, ReactionMethod.Touch);

                var component = entMan.GetComponent<CMUItemStainComponent>(item);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Kind, Is.EqualTo(CMUItemStainKind.Oil));
                    Assert.That(component.Color, Is.EqualTo(Color.FromHex("#030303")));
                });

                var cleaningMix = new Solution("Blood", FixedPoint2.New(10));
                cleaningMix.AddSolution(new Solution("Water", FixedPoint2.New(1)), prototypes);
                reactive.DoEntityReaction(item, cleaningMix, ReactionMethod.Touch);
                Assert.That(component.Color, Is.Null);

                Assert.That(inventory.TryEquip(wearer, shoes, CMUItemStainSystem.ShoesSlot, silent: true, force: true), Is.True);
                reactive.DoEntityReaction(wearer, new Solution("Blood", FixedPoint2.New(1)), ReactionMethod.Touch);
                var shoeStain = entMan.GetComponent<CMUItemStainComponent>(shoes);
                Assert.That(shoeStain.Kind, Is.EqualTo(CMUItemStainKind.Blood));

                reactive.DoEntityReaction(wearer, new Solution("Water", FixedPoint2.New(1)), ReactionMethod.Touch);
                Assert.That(shoeStain.Color, Is.Null);
            }
            finally
            {
                entMan.DeleteEntity(item);
                entMan.DeleteEntity(wearer);
                entMan.DeleteEntity(shoes);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TouchStainSelectionRunsBeforeReactiveEntityEffects()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var reactive = entMan.System<ReactiveSystem>();
            var item = entMan.SpawnEntity(PreEffectReactionItem, MapCoordinates.Nullspace);

            try
            {
                var solution = entMan.GetComponent<SolutionComponent>(item).Solution;
                reactive.ReactionEntity(
                    item,
                    ReactionMethod.Touch,
                    new ReagentQuantity("Blood", FixedPoint2.New(10)),
                    solution);

                var stain = entMan.GetComponent<CMUItemStainComponent>(item);
                Assert.Multiple(() =>
                {
                    Assert.That(stain.Kind, Is.EqualTo(CMUItemStainKind.Blood));
                    Assert.That(stain.Color, Is.EqualTo(Color.FromHex("#800000")));
                    Assert.That(solution.GetReagentQuantity(new ReagentId("Blood", null)), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(solution.GetReagentQuantity(new ReagentId("Oil", null)), Is.EqualTo(FixedPoint2.New(5)));
                    Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
                });
            }
            finally
            {
                entMan.DeleteEntity(item);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReagentAndClothingPrototypeMetadataIsConfigured()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var reagents = server.EntMan.System<RMCReagentSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(reagents.Index("Blood").ItemStain, Is.EqualTo(CMUItemStainKind.Blood));
                Assert.That(reagents.Index("InsectBlood").ItemStain, Is.EqualTo(CMUItemStainKind.Blood));
                Assert.That(reagents.Index("RMCSynthBlood").ItemStain, Is.EqualTo(CMUItemStainKind.Blood));
                Assert.That(reagents.Index("Oil").ItemStain, Is.EqualTo(CMUItemStainKind.Oil));
                Assert.That(reagents.Index("Oil").ItemStainColor, Is.EqualTo(Color.FromHex("#030303")));
                Assert.That(reagents.Index("Water").CleansItemStains, Is.True);
                Assert.That(reagents.Index("SpaceCleaner").CleansItemStains, Is.True);
                Assert.That(reagents.Index("SoapReagent").CleansItemStains, Is.True);
            });

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            AssertWornState(prototypes, factory, "RMCArmorVest", "outerClothing", "vest_blood");
            AssertWornState(prototypes, factory, "RMCLabcoat", "outerClothing", "coat_blood");
            AssertWornState(prototypes, factory, "RMCBoonie", "head", "booniehat_blood");
            AssertWornState(prototypes, factory, "CMHeadCapSurgBlue", "head", "surgcap_blood");
            AssertWornState(prototypes, factory, "CMMaskGas", "mask", "gasmask_blood");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LocalizedBruteDamageStainsToolsWithBloodOrOil()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var part = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var bloodTool = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            var oilTool = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            var burnTool = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            var healingTool = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);

            try
            {
                RaisePartDamage(entMan, body, part, bloodTool, "Blunt", 40);
                var bloodStain = entMan.GetComponent<CMUItemStainComponent>(bloodTool);
                Assert.Multiple(() =>
                {
                    Assert.That(bloodStain.Kind, Is.EqualTo(CMUItemStainKind.Blood));
                    Assert.That(bloodStain.Color, Is.EqualTo(Color.FromHex("#800000")));
                });

                entMan.EnsureComponent<CMURoboticLimbComponent>(part);
                RaisePartDamage(entMan, body, part, oilTool, "Blunt", 40);
                var oilStain = entMan.GetComponent<CMUItemStainComponent>(oilTool);
                Assert.Multiple(() =>
                {
                    Assert.That(oilStain.Kind, Is.EqualTo(CMUItemStainKind.Oil));
                    Assert.That(oilStain.Color, Is.EqualTo(Color.FromHex("#030303")));
                });

                RaisePartDamage(entMan, body, part, burnTool, "Heat", 100);
                RaisePartDamage(entMan, body, part, healingTool, "Blunt", -100);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUItemStainComponent>(burnTool), Is.False);
                    Assert.That(entMan.HasComponent<CMUItemStainComponent>(healingTool), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(body);
                entMan.DeleteEntity(part);
                entMan.DeleteEntity(bloodTool);
                entMan.DeleteEntity(oilTool);
                entMan.DeleteEntity(burnTool);
                entMan.DeleteEntity(healingTool);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PuddlesStainAndCleanEquippedShoesButIgnoreBareFeet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var wearer = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var barefoot = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var shoes = entMan.SpawnEntity("CMBootsBlack", MapCoordinates.Nullspace);
            var bloodPuddle = entMan.SpawnEntity("PuddleBlood", MapCoordinates.Nullspace);
            var waterPuddle = entMan.SpawnEntity(WaterPuddle, MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(wearer, shoes, CMUItemStainSystem.ShoesSlot, silent: true, force: true), Is.True);

                var bloodStep = new StepTriggeredOffEvent(bloodPuddle, wearer);
                entMan.EventBus.RaiseLocalEvent(bloodPuddle, ref bloodStep);
                var stain = entMan.GetComponent<CMUItemStainComponent>(shoes);
                Assert.Multiple(() =>
                {
                    Assert.That(stain.Kind, Is.EqualTo(CMUItemStainKind.Blood));
                    Assert.That(stain.Color, Is.EqualTo(Color.FromHex("#800000")));
                });

                var cleanStep = new StepTriggeredOffEvent(waterPuddle, wearer);
                entMan.EventBus.RaiseLocalEvent(waterPuddle, ref cleanStep);
                Assert.That(stain.Color, Is.Null);

                var bareStep = new StepTriggeredOffEvent(bloodPuddle, barefoot);
                entMan.EventBus.RaiseLocalEvent(bloodPuddle, ref bareStep);
                Assert.That(entMan.HasComponent<CMUItemStainComponent>(barefoot), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(wearer);
                entMan.DeleteEntity(barefoot);
                entMan.DeleteEntity(shoes);
                entMan.DeleteEntity(bloodPuddle);
                entMan.DeleteEntity(waterPuddle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClientHelpersPreserveSourceLayerAndSupplyDirectionalWornVisuals()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var serverEntMan = server.EntMan;
        var clientEntMan = client.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid serverItem = default;

        await server.WaitPost(() =>
        {
            serverItem = serverEntMan.SpawnEntity(VisualItem, map.GridCoords);
        });
        await pair.RunTicksSync(5);

        var clientItem = clientEntMan.GetEntity(serverEntMan.GetNetEntity(serverItem));

        await client.WaitAssertion(() =>
        {
            var sprite = clientEntMan.GetComponent<SpriteComponent>(clientItem);
            var sourceLayer = sprite.AllLayers.OfType<SpriteComponent.Layer>().First();
            var visuals = clientEntMan.GetComponent<CMUItemStainVisualsComponent>(clientItem);
            Assert.Multiple(() =>
            {
                Assert.That(visuals.LayerKeys, Has.Count.EqualTo(2));
                Assert.That(sprite.AllLayers.Count(), Is.EqualTo(3));
                Assert.That(sourceLayer.Shader, Is.Null);
                Assert.That(sourceLayer.CopyToShaderParameters, Is.Null);
            });

            var equipment = new GetEquipmentVisualsEvent(clientItem, CMUItemStainSystem.HeadSlot);
            clientEntMan.EventBus.RaiseLocalEvent(clientItem, equipment);
            var wornLayer = equipment.Layers.Single(layer =>
                layer.Item1 == "cmu-item-stain-equipped-head").Item2;
            Assert.Multiple(() =>
            {
                Assert.That(wornLayer.RsiPath, Is.EqualTo("CMU14/Effects/item_stains.rsi"));
                Assert.That(wornLayer.State, Is.EqualTo("m10helmet_blood"));
                Assert.That(wornLayer.Color, Is.EqualTo(Color.FromHex("#800000")));
            });
        });

        await server.WaitPost(() =>
        {
            Assert.That(serverEntMan.System<CMUItemStainSystem>().TryClean(serverItem), Is.True);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var sprite = clientEntMan.GetComponent<SpriteComponent>(clientItem);
            var sourceLayer = sprite.AllLayers.OfType<SpriteComponent.Layer>().First();
            var visuals = clientEntMan.GetComponent<CMUItemStainVisualsComponent>(clientItem);
            Assert.Multiple(() =>
            {
                Assert.That(clientEntMan.GetComponent<CMUItemStainComponent>(clientItem).Color, Is.Null);
                Assert.That(visuals.LayerKeys, Is.Empty);
                Assert.That(sprite.AllLayers.Count(), Is.EqualTo(1));
                Assert.That(sourceLayer.Shader, Is.Null);
            });
        });

        await server.WaitPost(() => serverEntMan.DeleteEntity(serverItem));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForensicCleanerUsesOneCanonicalDoAfterForEvidenceAndStains()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid forensicUser = default;
        EntityUid forensicItem = default;
        EntityUid forensicCleaner = default;
        EntityUid stainUser = default;
        EntityUid stainItem = default;
        EntityUid stainCleaner = default;
        EntityUid bothUser = default;
        EntityUid bothItem = default;
        EntityUid bothCleaner = default;
        EntityUid neitherUser = default;
        EntityUid neitherItem = default;
        EntityUid neitherCleaner = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stains = entMan.System<CMUItemStainSystem>();
            var forensics = entMan.System<ForensicsSystem>();

            forensicUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            forensicItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            forensicCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);
            var forensicEvidence = entMan.EnsureComponent<ForensicsComponent>(forensicItem);
            forensicEvidence.Fingerprints.Add("forensic-only");
            forensicEvidence.CleanDistance = 2.75f;

            stainUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            stainItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            stainCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);
            Assert.That(stains.TryStain(stainItem, CMUItemStainKind.Blood, Color.Red), Is.True);

            bothUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            bothItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            bothCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);
            var bothEvidence = entMan.EnsureComponent<ForensicsComponent>(bothItem);
            bothEvidence.Fibers.Add("both");
            bothEvidence.CleanDistance = 3.25f;
            Assert.That(stains.TryStain(bothItem, CMUItemStainKind.Oil, Color.Black), Is.True);

            neitherUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            neitherItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            neitherCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);

            Assert.Multiple(() =>
            {
                Assert.That(StartCleaning(forensicCleaner, forensicUser, forensicItem), Is.True);
                Assert.That(StartCleaning(stainCleaner, stainUser, stainItem), Is.True);
                Assert.That(StartCleaning(bothCleaner, bothUser, bothItem), Is.True);
                Assert.That(StartCleaning(neitherCleaner, neitherUser, neitherItem), Is.False);
            });

            AssertCleaningDoAfter(forensicUser, forensicCleaner, forensicItem, 2.75f);
            AssertCleaningDoAfter(stainUser, stainCleaner, stainItem, 1.5f);
            AssertCleaningDoAfter(bothUser, bothCleaner, bothItem, 3.25f);

            var neitherDoAfters = entMan.GetComponent<DoAfterComponent>(neitherUser);
            Assert.That(neitherDoAfters.DoAfters.Values.Count(IsActive), Is.Zero);

            bool StartCleaning(EntityUid cleaner, EntityUid user, EntityUid target)
            {
                var cleanerComponent = entMan.GetComponent<CleansForensicsComponent>(cleaner);
                return forensics.TryStartCleaning((cleaner, cleanerComponent), user, target);
            }

            void AssertCleaningDoAfter(
                EntityUid user,
                EntityUid cleaner,
                EntityUid target,
                float expectedDistance)
            {
                var component = entMan.GetComponent<DoAfterComponent>(user);
                var active = component.DoAfters.Values.Where(IsActive).ToArray();
                Assert.That(active, Has.Length.EqualTo(1));
                Assert.Multiple(() =>
                {
                    Assert.That(active[0].Args.Event, Is.TypeOf<CleanForensicsDoAfterEvent>());
                    Assert.That(active[0].Args.EventTarget, Is.EqualTo(cleaner));
                    Assert.That(active[0].Args.Target, Is.EqualTo(target));
                    Assert.That(active[0].Args.Used, Is.EqualTo(cleaner));
                    Assert.That(active[0].Args.DistanceThreshold, Is.EqualTo(expectedDistance).Within(0.001f));
                });
            }

            static bool IsActive(DoAfter doAfter) => !doAfter.Cancelled && !doAfter.Completed;
        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stains = entMan.System<CMUItemStainSystem>();
            var forensicEvidence = entMan.GetComponent<ForensicsComponent>(forensicItem);
            var bothEvidence = entMan.GetComponent<ForensicsComponent>(bothItem);

            Assert.Multiple(() =>
            {
                Assert.That(forensicEvidence.Fingerprints, Is.Empty);
                Assert.That(entMan.GetComponent<CMUItemStainComponent>(stainItem).Color, Is.Null);
                Assert.That(bothEvidence.Fibers, Is.Empty);
                Assert.That(entMan.GetComponent<CMUItemStainComponent>(bothItem).Color, Is.Null);
                Assert.That(stains.TryClean(stainItem), Is.False,
                    "The stain must have been cleaned by the single canonical completion.");
                Assert.That(stains.TryClean(bothItem), Is.False,
                    "The shared completion must not leave a second stain-cleaning pass.");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[]
                     {
                         forensicUser, forensicItem, forensicCleaner,
                         stainUser, stainItem, stainCleaner,
                         bothUser, bothItem, bothCleaner,
                         neitherUser, neitherItem, neitherCleaner,
                     })
            {
                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertWornState(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string prototypeId,
        string slot,
        string expectedState)
    {
        var prototype = prototypes.Index<EntityPrototype>(prototypeId);
        Assert.That(prototype.TryComp<CMUItemStainComponent>(out var component, factory), Is.True, prototypeId);
        Assert.That(component!.WornStates[slot], Is.EqualTo(expectedState), prototypeId);
    }

    private static void RaisePartDamage(
        IEntityManager entMan,
        EntityUid body,
        EntityUid part,
        EntityUid tool,
        string damageType,
        FixedPoint2 amount)
    {
        var damage = new DamageSpecifier
        {
            DamageDict = { [damageType] = amount },
        };
        var damaged = new BodyPartDamagedEvent(
            body,
            part,
            BodyPartType.Arm,
            damage,
            amount,
            Array.Empty<EntityUid>(),
            tool,
            default,
            default);
        entMan.EventBus.RaiseLocalEvent(part, ref damaged, broadcast: true);
    }
}
