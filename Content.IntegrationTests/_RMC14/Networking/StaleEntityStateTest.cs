using Content.Shared._RMC14.Cassette;
using Content.Shared._RMC14.Armor.Magnetic;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Deploy;
// CMU14 Begin: deleted combat-reference coverage.
using Content.Shared._RMC14.Weapons.Ranged.AimedShot.FocusedShooting;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Spray;
// CMU14 End
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared._RMC14.Xenonids.Fruit.Components;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Sentry.Laptop;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Sentinel;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CombatMode;
using Content.Shared.Placeable;
using Content.Shared.Projectiles;
using Content.Shared.Speech.Components;
using Content.Shared.StepTrigger.Components; // CMU14
using Content.Shared.Trigger.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RMC14.Networking;

[TestFixture]
public sealed class StaleEntityStateTest
{
    // CMU14: references retained after a target dies, a shooter leaves, or a xeno evolves.

    // CMU14 method: contact end events may arrive after a collider was deleted.

    [Test]
    public async Task ReferencesAndGameplayStateReplicateAfterSourceDeletion()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        EntityUid owner = default;
        EntityUid source = default;
        NetEntity ownerNet = default;
        NetEntity sourceNet = default;
        var components = new List<Component>();
        var boonId = new EntProtoId<HiveBoonDefinitionComponent>("RMCHiveBoonKing");

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            owner = entities.SpawnEntity(null, map.GridCoords);
            source = entities.SpawnEntity(null, map.GridCoords);
            ownerNet = entities.GetNetEntity(owner);
            sourceNet = entities.GetNetEntity(source);

            // Opening/power follow the parent surface when a laptop first enters the client's PVS.
            var table = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<PlaceableSurfaceComponent>(table);
            entities.System<SharedTransformSystem>().SetParent(owner, table);

            var collision = entities.AddComponent<RMCConstructionPreventCollideComponent>(owner);
            collision.Target = source;
            collision.Range = 3f;
            components.Add(collision);

            var vocal = entities.AddComponent<VocalComponent>(owner);
            SetField(vocal, nameof(vocal.EmoteActionEntity), (EntityUid?) source);
            SetField(vocal, nameof(vocal.WilhelmProbability), 0.25f);
            SetField(vocal, nameof(vocal.EmoteSounds), (ProtoId<EmoteSoundsPrototype>?) new ProtoId<EmoteSoundsPrototype>("FemaleSlime"));
            components.Add(vocal);

            var laptop = entities.AddComponent<SentryLaptopComponent>(owner);
            SetField(laptop, nameof(laptop.IsOpen), true);
            SetField(laptop, nameof(laptop.IsPowered), true);
            SetField(laptop, nameof(laptop.Range), 42f);
            SetField(laptop, nameof(laptop.MaxLinkedSentries), 5);
            SetField(laptop, nameof(laptop.LinkedSentries), new HashSet<EntityUid> { source });
            SetField(laptop, nameof(laptop.SentryCustomNames), new Dictionary<EntityUid, string> { [source] = "North gate" });
            SetField(laptop, nameof(laptop.Watchers), new List<EntityUid> { source });
            SetField(laptop, nameof(laptop.CurrentCamera), (EntityUid?) source);
            components.Add(laptop);

            var projectile = entities.AddComponent<ProjectileComponent>(owner);
            projectile.Shooter = source;
            projectile.Weapon = source;
            projectile.MaxFixedRange = 17f;
            components.Add(projectile);

            // CMU14 Begin: combat references may survive their target's deletion.
            var focus = entities.AddComponent<RMCFocusedShootingComponent>(owner);
            focus.CurrentTarget = source;
            focus.FocusCounter = 2;
            focus.FocusMultiplier = 0.75f;
            components.Add(focus);

            var prediction = entities.AddComponent<PredictedProjectileServerComponent>(owner);
            prediction.ClientEnt = source;
            prediction.ClientId = 42;
            components.Add(prediction);

            var splatter = entities.AddComponent<XenoAcidSplatterComponent>(owner);
            SetField(splatter, nameof(splatter.Xeno), (EntityUid?) source);
            components.Add(splatter);

            var step = new StepTriggerComponent();
            SetField(step, nameof(step.Active), false);
            SetField(step, nameof(step.StepOn), true);
            SetField(step, nameof(step.Colliding), new HashSet<EntityUid> { source });
            SetField(step, nameof(step.CurrentlySteppedOn), new HashSet<EntityUid> { source });
            entities.AddComponent(owner, step);
            components.Add(step);
            // CMU14 End

            var intoxicated = entities.AddComponent<XenoIntoxicatedComponent>(owner);
            SetField(intoxicated, nameof(XenoIntoxicatedComponent.LastSource), (EntityUid?) source);
            SetField(intoxicated, nameof(XenoIntoxicatedComponent.Stacks), 25);
            SetField(intoxicated, nameof(XenoIntoxicatedComponent.NextTick), TimeSpan.FromDays(1));
            components.Add(intoxicated);

            var parasite = entities.AddComponent<XenoParasiteComponent>(owner);
            SetField(parasite, nameof(XenoParasiteComponent.InfectedVictim), (EntityUid?) source);
            SetField(parasite, nameof(XenoParasiteComponent.FallOffAt), (TimeSpan?) TimeSpan.FromDays(1));
            SetField(parasite, nameof(XenoParasiteComponent.LeapCollisionActive), true);
            components.Add(parasite);

            var produce = entities.AddComponent<ProduceComponent>(owner);
            SetField(produce, nameof(ProduceComponent.PlantData), (EntityUid?) source);
            SetField(produce, nameof(ProduceComponent.PlantProtoId), (EntProtoId?) new EntProtoId("CarrotPlants"));
            components.Add(produce);

            var seed = entities.AddComponent<SeedComponent>(owner);
            SetField(seed, nameof(seed.PlantData), (EntityUid?) source);
            SetField(seed, nameof(seed.PlantProtoId), new EntProtoId("CarrotPlants"));
            SetField(seed, nameof(seed.HealthOverride), (float?) 42f);
            components.Add(seed);

            var surgery = entities.AddComponent<CMUSurgeryInFlightComponent>(owner);
            SetField(surgery, nameof(surgery.Surgeon), source);
            SetField(surgery, nameof(surgery.SurgeonName), "Historical surgeon");
            SetField(surgery, nameof(surgery.LeafSurgeryId), "test-operation");
            SetField(surgery, nameof(surgery.LeafSurgeryDisplayName), "Test operation");
            components.Add(surgery);

            var destination = entities.AddComponent<DropshipDestinationComponent>(owner);
            SetField(destination, nameof(destination.Ship), (EntityUid?) source);
            SetField(destination, nameof(destination.ArrivalSoundEntity), (EntityUid?) source);
            SetField(destination, nameof(destination.AutoRecall), true);
            SetField(destination, nameof(destination.LightSearchRadius), 8);
            SetField(destination, nameof(destination.FactionController), "GOVFOR");
            components.Add(destination);

            var boons = entities.AddComponent<HiveBoonsComponent>(owner);
            SetField(boons, nameof(HiveBoonsComponent.RoyalResin), 7);
            SetField(boons, nameof(HiveBoonsComponent.Active), new Dictionary<EntProtoId<HiveBoonDefinitionComponent>, EntityUid>
            {
                [boonId] = source,
            });
            components.Add(boons);

            foreach (var component in components)
                entities.Dirty(owner, component);
        });

        async Task AssertClientState(bool deleted)
        {
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientOwner = entities.GetEntity(ownerNet);
                var expected = deleted ? EntityUid.Invalid : entities.GetEntity(sourceNet);
                var collision = entities.GetComponent<RMCConstructionPreventCollideComponent>(clientOwner);
                var vocal = entities.GetComponent<VocalComponent>(clientOwner);
                var laptop = entities.GetComponent<SentryLaptopComponent>(clientOwner);
                var projectile = entities.GetComponent<ProjectileComponent>(clientOwner);
                var intoxicated = entities.GetComponent<XenoIntoxicatedComponent>(clientOwner);
                var parasite = entities.GetComponent<XenoParasiteComponent>(clientOwner);
                var produce = entities.GetComponent<ProduceComponent>(clientOwner);
                var seed = entities.GetComponent<SeedComponent>(clientOwner);
                var surgery = entities.GetComponent<CMUSurgeryInFlightComponent>(clientOwner);
                var destination = entities.GetComponent<DropshipDestinationComponent>(clientOwner);
                var boons = entities.GetComponent<HiveBoonsComponent>(clientOwner);
                Assert.Multiple(() =>
                {
                    Assert.That(collision.Target, Is.EqualTo(expected));
                    Assert.That(collision.Range, Is.EqualTo(3f));
                    Assert.That(vocal.EmoteActionEntity, Is.EqualTo(expected));
                    Assert.That(vocal.ScreamId.Id, Is.EqualTo("Scream"));
                    Assert.That(vocal.WilhelmProbability, Is.EqualTo(0.25f));
                    Assert.That(vocal.EmoteAction?.Id, Is.EqualTo("ActionScream"));
                    Assert.That(vocal.EmoteSounds?.Id, Is.EqualTo("FemaleSlime"));
                    Assert.That(laptop.IsOpen, Is.True);
                    Assert.That(laptop.IsPowered, Is.True);
                    Assert.That(laptop.Range, Is.EqualTo(42f));
                    Assert.That(laptop.MaxLinkedSentries, Is.EqualTo(5));
                    Assert.That(laptop.CurrentCamera, Is.EqualTo(expected));
                    if (deleted)
                    {
                        Assert.That(laptop.LinkedSentries, Is.Empty);
                        Assert.That(laptop.SentryCustomNames, Is.Empty);
                        Assert.That(laptop.Watchers, Is.Empty);
                    }
                    else
                    {
                        Assert.That(laptop.LinkedSentries, Is.EquivalentTo(new[] { expected }));
                        Assert.That(laptop.SentryCustomNames[expected], Is.EqualTo("North gate"));
                        Assert.That(laptop.Watchers, Is.EqualTo(new[] { expected }));
                    }
                    Assert.That(projectile.Shooter, Is.EqualTo(expected));
                    Assert.That(projectile.Weapon, Is.EqualTo(expected));
                    Assert.That(projectile.MaxFixedRange, Is.EqualTo(17f));
                    // CMU14 Begin
                    var focus = entities.GetComponent<RMCFocusedShootingComponent>(clientOwner);
                    var prediction = entities.GetComponent<PredictedProjectileServerComponent>(clientOwner);
                    var splatter = entities.GetComponent<XenoAcidSplatterComponent>(clientOwner);
                    Assert.That(focus.CurrentTarget, Is.EqualTo(expected));
                    Assert.That(focus.FocusCounter, Is.EqualTo(2));
                    Assert.That(focus.FocusMultiplier, Is.EqualTo(0.75f));
                    Assert.That(prediction.ClientEnt, Is.EqualTo(expected));
                    Assert.That(prediction.ClientId, Is.EqualTo(42));
                    Assert.That(splatter.Xeno, Is.EqualTo(expected));
                    var step = entities.GetComponent<StepTriggerComponent>(clientOwner);
                    Assert.That(step.StepOn, Is.True);
                    Assert.That(step.Active, Is.False);
                    var expectedContacts = deleted ? Array.Empty<EntityUid>() : new[] { expected };
                    Assert.That(step.Colliding, Is.EquivalentTo(expectedContacts));
                    Assert.That(step.CurrentlySteppedOn, Is.EquivalentTo(expectedContacts));
                    // CMU14 End
                    Assert.That(intoxicated.LastSource, Is.EqualTo(expected));
                    Assert.That(intoxicated.Stacks, Is.EqualTo(25));
                    Assert.That(parasite.InfectedVictim, Is.EqualTo(expected));
                    Assert.That(parasite.LeapCollisionActive, Is.True);
                    Assert.That(produce.PlantData, Is.EqualTo(expected));
                    Assert.That(produce.PlantProtoId, Is.EqualTo((EntProtoId?) new EntProtoId("CarrotPlants")));
                    Assert.That(seed.PlantData, Is.EqualTo(expected));
                    Assert.That(seed.PlantProtoId, Is.EqualTo(new EntProtoId("CarrotPlants")));
                    Assert.That(seed.HealthOverride, Is.EqualTo(42f));
                    Assert.That(surgery.Surgeon, Is.EqualTo(expected));
                    Assert.That(surgery.SurgeonName, Is.EqualTo("Historical surgeon"));
                    Assert.That(surgery.LeafSurgeryId, Is.EqualTo("test-operation"));
                    Assert.That(surgery.LeafSurgeryDisplayName, Is.EqualTo("Test operation"));
                    Assert.That(destination.Ship, Is.EqualTo(expected));
                    Assert.That(destination.ArrivalSoundEntity, Is.EqualTo(expected));
                    Assert.That(destination.AutoRecall, Is.True);
                    Assert.That(destination.LightSearchRadius, Is.EqualTo(8));
                    Assert.That(destination.FactionController, Is.EqualTo("GOVFOR"));
                    Assert.That(boons.Active[boonId], Is.EqualTo(expected));
                    Assert.That(boons.RoyalResin, Is.EqualTo(7));
                });
            });
        }

        await AssertClientState(false);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.DeleteEntity(source);
            foreach (var component in components)
                entities.Dirty(owner, component);
        });
        await AssertClientState(true);

        await pair.CleanReturnAsync();
    }

    private static void SetField<T>(object component, string name, T value)
    {
        component.GetType().GetField(name)!.SetValue(component, value);
    }
}
