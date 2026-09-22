using Content.Shared._RMC14.Cassette;
using Content.Shared._RMC14.Armor.Magnetic;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Deploy;
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
    [TestCase(typeof(ProjectileComponent), nameof(ProjectileComponent.Shooter))]
    [TestCase(typeof(VocalComponent), nameof(VocalComponent.EmoteActionEntity))]
    [TestCase(typeof(TimerTriggerComponent), nameof(TimerTriggerComponent.User))]
    [TestCase(typeof(CombatModeComponent), nameof(CombatModeComponent.CombatToggleActionEntity))]
    [TestCase(typeof(RMCConstructionPreventCollideComponent), nameof(RMCConstructionPreventCollideComponent.Target))]
    [TestCase(typeof(ProjectileComponent), nameof(ProjectileComponent.Weapon))]
    [TestCase(typeof(XenoIntoxicatedComponent), nameof(XenoIntoxicatedComponent.LastSource))]
    [TestCase(typeof(XenoParasiteComponent), nameof(XenoParasiteComponent.InfectedVictim))]
    [TestCase(typeof(ProduceComponent), nameof(ProduceComponent.PlantData))]
    [TestCase(typeof(SeedComponent), nameof(SeedComponent.PlantData))]
    [TestCase(typeof(XenoWeedsComponent), nameof(XenoWeedsComponent.Source))]
    [TestCase(typeof(XenoFruitComponent), nameof(XenoFruitComponent.Hive))]
    [TestCase(typeof(XenoFruitComponent), nameof(XenoFruitComponent.Planter))]
    [TestCase(typeof(SquadLeaderTrackerComponent), nameof(SquadLeaderTrackerComponent.Target))]
    [TestCase(typeof(SquadLeaderTrackerComponent), nameof(SquadLeaderTrackerComponent.BattleBuddy))]
    [TestCase(typeof(RMCReturnToInventoryComponent), nameof(RMCReturnToInventoryComponent.ReceivingItem))]
    [TestCase(typeof(DropshipDestinationComponent), nameof(DropshipDestinationComponent.Ship))]
    [TestCase(typeof(DropshipDestinationComponent), nameof(DropshipDestinationComponent.ArrivalSoundEntity))]
    public async Task OptionalReferencesSurviveSourceDeletion(Type componentType, string field)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var owner = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var source = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var component = (Component) Activator.CreateInstance(componentType)!;
            entities.AddComponent(owner, component);
            // Some components create their own reference during startup.
            SetField(component, field, (EntityUid?) null);

            object GetReference()
            {
                var state = entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero)!;
                var stateField = state.GetType().GetField(field);
                var stateProperty = state.GetType().GetProperty(field);
                Assert.That(stateField != null || stateProperty != null, Is.True, $"The state must include {field}.");
                return stateField != null ? stateField.GetValue(state) : stateProperty!.GetValue(state);
            }

            Assert.That(GetReference(), Is.Null, "An unset reference must remain null.");
            SetField(component, field, (EntityUid?) source);
            Assert.That(GetReference(), Is.EqualTo(entities.GetNetEntity(source)), "Live references must still replicate.");

            entities.DeleteEntity(source);
            Assert.That(GetReference(), Is.EqualTo(NetEntity.Invalid), "Deleted references must serialize without resolution errors.");
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(typeof(RMCDeployedEntityComponent), nameof(RMCDeployedEntityComponent.OriginalEntity))]
    [TestCase(typeof(CMUSurgeryInFlightComponent), nameof(CMUSurgeryInFlightComponent.Surgeon))]
    [TestCase(typeof(RMCReturnToInventoryComponent), nameof(RMCReturnToInventoryComponent.User))]
    [TestCase(typeof(RMCReturnToInventoryComponent), nameof(RMCReturnToInventoryComponent.Magnetizer))]
    public async Task RequiredReferencesSurviveSourceDeletion(Type componentType, string field)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var source = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var component = (Component) Activator.CreateInstance(componentType)!;
            entities.AddComponent(owner, component);
            SetField(component, field, source);
            object GetReference()
            {
                var state = entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero)!;
                return state.GetType().GetProperty(field)!.GetValue(state);
            }

            Assert.That(GetReference(), Is.EqualTo(entities.GetNetEntity(source)));
            entities.DeleteEntity(source);
            Assert.That(GetReference(), Is.EqualTo(NetEntity.Invalid));
            Assert.That(componentType.GetField(field)!.GetValue(component), Is.EqualTo(source),
                "State generation must not mutate authoritative gameplay state.");
            entities.DeleteEntity(owner);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(typeof(XenoWeedsComponent), nameof(XenoWeedsComponent.Spread))]
    [TestCase(typeof(XenoWeedsComponent), nameof(XenoWeedsComponent.LocalWeeded))]
    [TestCase(typeof(XenoWeedsComponent), nameof(XenoWeedsComponent.WeedboundStructures))]
    [TestCase(typeof(AttachableDirectionLockedComponent), nameof(AttachableDirectionLockedComponent.AttachableList))]
    public async Task DeletedListEntriesAreFilteredWithoutChangingGameplayState(Type componentType, string field)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var live = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var dead = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var component = (Component) Activator.CreateInstance(componentType)!;
            entities.AddComponent(owner, component);
            var originals = new List<EntityUid> { dead, live, dead };
            SetField(component, field, originals);
            entities.DeleteEntity(dead);
            var state = entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero)!;
            var actual = state.GetType().GetProperty(field)!.GetValue(state);
            Assert.That(actual, Is.EqualTo(new[] { entities.GetNetEntity(live) }));
            Assert.That(originals, Is.EqualTo(new[] { dead, live, dead }));
            entities.DeleteEntity(owner);
            entities.DeleteEntity(live);
        });
        await pair.CleanReturnAsync();
    }

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

    [Test]
    public async Task HiveBoonsSerializeLiveAndDeletedReferences()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hive = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var live = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var deleted = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var boons = entities.AddComponent<HiveBoonsComponent>(hive);
            var liveBoon = new EntProtoId<HiveBoonDefinitionComponent>("LiveBoon");
            var deletedBoon = new EntProtoId<HiveBoonDefinitionComponent>("DeletedBoon");
            SetField(boons, nameof(HiveBoonsComponent.Active), new Dictionary<EntProtoId<HiveBoonDefinitionComponent>, EntityUid>
            {
                [liveBoon] = live,
                [deletedBoon] = deleted,
            });
            entities.DeleteEntity(deleted);

            var state = entities.GetComponentState(entities.EventBus, boons, null, GameTick.Zero)!;
            var active = (Dictionary<EntProtoId<HiveBoonDefinitionComponent>, NetEntity>) (
                state.GetType().GetField(nameof(HiveBoonsComponent.Active))?.GetValue(state)
                ?? state.GetType().GetProperty(nameof(HiveBoonsComponent.Active))!.GetValue(state))!;
            Assert.Multiple(() =>
            {
                Assert.That(active[liveBoon], Is.EqualTo(entities.GetNetEntity(live)));
                Assert.That(active[deletedBoon], Is.EqualTo(NetEntity.Invalid));
                Assert.That(active, Has.Count.EqualTo(2));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedEntityReferencesSerializeWithoutResolutionErrors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
        });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var deleted = entities.SpawnEntity(null, MapCoordinates.Nullspace);

            var projectile = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var targeted = entities.AddComponent<TargetedProjectileComponent>(projectile);
            SetField(targeted, nameof(TargetedProjectileComponent.Target), deleted);

            var player = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var cassette = entities.AddComponent<CassettePlayerComponent>(player);
            SetField(cassette, nameof(CassettePlayerComponent.PlayPauseAction), (EntityUid?) deleted);
            SetField(cassette, nameof(CassettePlayerComponent.NextAction), (EntityUid?) deleted);
            SetField(cassette, nameof(CassettePlayerComponent.RestartAction), (EntityUid?) deleted);
            SetField(cassette, nameof(CassettePlayerComponent.AudioStream), (EntityUid?) deleted);

            entities.DeleteEntity(deleted);

            var targetedState = (TargetedProjectileComponentState) entities.GetComponentState(
                entities.EventBus,
                targeted,
                null,
                GameTick.Zero)!;
            var cassetteState = (CassettePlayerComponentState) entities.GetComponentState(
                entities.EventBus,
                cassette,
                null,
                GameTick.Zero)!;

            Assert.Multiple(() =>
            {
                Assert.That(targetedState.Target, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.PlayPauseAction, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.NextAction, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.RestartAction, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.AudioStream, Is.EqualTo(NetEntity.Invalid));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void SetField<T>(object component, string name, T value)
    {
        component.GetType().GetField(name)!.SetValue(component, value);
    }
}
