using Content.Shared._RMC14.Cassette;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Sentinel;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Projectiles;
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
    [TestCase(typeof(ProjectileComponent), nameof(ProjectileComponent.Weapon))]
    [TestCase(typeof(XenoIntoxicatedComponent), nameof(XenoIntoxicatedComponent.LastSource))]
    [TestCase(typeof(XenoParasiteComponent), nameof(XenoParasiteComponent.InfectedVictim))]
    [TestCase(typeof(ProduceComponent), nameof(ProduceComponent.PlantData))]
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
                var projectile = entities.GetComponent<ProjectileComponent>(clientOwner);
                var intoxicated = entities.GetComponent<XenoIntoxicatedComponent>(clientOwner);
                var parasite = entities.GetComponent<XenoParasiteComponent>(clientOwner);
                var produce = entities.GetComponent<ProduceComponent>(clientOwner);
                var boons = entities.GetComponent<HiveBoonsComponent>(clientOwner);
                Assert.Multiple(() =>
                {
                    Assert.That(projectile.Shooter, Is.EqualTo(expected));
                    Assert.That(projectile.Weapon, Is.EqualTo(expected));
                    Assert.That(projectile.MaxFixedRange, Is.EqualTo(17f));
                    Assert.That(intoxicated.LastSource, Is.EqualTo(expected));
                    Assert.That(intoxicated.Stacks, Is.EqualTo(25));
                    Assert.That(parasite.InfectedVictim, Is.EqualTo(expected));
                    Assert.That(parasite.LeapCollisionActive, Is.True);
                    Assert.That(produce.PlantData, Is.EqualTo(expected));
                    Assert.That(produce.PlantProtoId, Is.EqualTo((EntProtoId?) new EntProtoId("CarrotPlants")));
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
