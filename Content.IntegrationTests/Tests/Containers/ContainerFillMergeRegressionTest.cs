#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Storage;
using Content.Shared.Containers;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Containers;

[TestFixture]
[TestOf(typeof(ContainerFillSystem))]
public sealed class ContainerFillMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: ContainerFillMergeHost
          components:
          - type: Storage
            maxItemSize: Huge
            grid:
            - 0,0,0,0
          - type: ContainerFillMergeProbe

        - type: entity
          id: ContainerFillMergeSmall
          components:
          - type: Item
            size: Tiny

        - type: entity
          id: ContainerFillMergeLarge
          components:
          - type: Item
            size: Large
        """;

    [Test]
    public async Task InvalidSpawnContinuesAndStorageEventPrecedesSortedInsertion()
    {
        var map = await Pair.CreateTestMap();
        var expectedInvalidSpawnErrors = 0;

        bool JudgeExpectedInvalidSpawn(string sawmill, LogEvent message)
        {
            if (sawmill != "system.container_fill" ||
                message.Level != LogEventLevel.Error ||
                !message.RenderMessage()
                    .Contains("Error spawning ContainerFillMergeMissing", StringComparison.Ordinal))
            {
                return false;
            }

            expectedInvalidSpawnErrors++;
            return true;
        }

        Pair.ServerLogHandler.JudgeLog += JudgeExpectedInvalidSpawn;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var invalidHost = SEntMan.SpawnEntity("ContainerFillMergeHost", map.GridCoords);
                var invalidStorage = SEntMan.GetComponent<StorageComponent>(invalidHost);
                var invalidFill = SEntMan.AddComponent<EntityTableContainerFillComponent>(invalidHost);
                invalidFill.Containers[StorageComponent.ContainerId] = new AllSelector
                {
                    Children =
                    {
                        new EntSelector { Id = "ContainerFillMergeMissing" },
                        new EntSelector { Id = "ContainerFillMergeSmall" },
                    },
                };

                Assert.DoesNotThrow(() => InvokeFill(invalidHost, invalidFill),
                    "one invalid/throwing table entry must not suppress later starting contents");
                Assert.That(invalidStorage.Container.ContainedEntities.Select(PrototypeId),
                    Is.EqualTo(new[] { "ContainerFillMergeSmall" }));
                SEntMan.DeleteEntity(invalidHost);

                var host = SEntMan.SpawnEntity("ContainerFillMergeHost", map.GridCoords);
                var storage = SEntMan.GetComponent<StorageComponent>(host);
                var probe = SEntMan.GetComponent<ContainerFillMergeProbeComponent>(host);
                var initialGrid = storage.Grid.ToArray();
                var fill = SEntMan.AddComponent<EntityTableContainerFillComponent>(host);
                fill.Sort = true;
                fill.Containers[StorageComponent.ContainerId] = new AllSelector
                {
                    Children =
                    {
                        new EntSelector { Id = "ContainerFillMergeSmall" },
                        new EntSelector { Id = "ContainerFillMergeLarge" },
                    },
                };
                InvokeFill(host, fill);

                var contained = storage.Container.ContainedEntities;
                Assert.Multiple(() =>
                {
                    Assert.That(contained, Has.Count.EqualTo(2));
                    Assert.That(contained.Select(PrototypeId),
                        Is.EquivalentTo(new[] { "ContainerFillMergeSmall", "ContainerFillMergeLarge" }));
                    Assert.That(probe.ItemPrototypes,
                        Is.EqualTo(new[] { "ContainerFillMergeLarge", "ContainerFillMergeSmall" }),
                        "Sort must order larger items before the fill event and insertion");
                    Assert.That(probe.WasContainedAtEvent, Is.EqualTo(new[] { false, false }),
                        "CMStorageItemFillEvent must run before either item enters the container");
                    Assert.That(storage.Grid, Is.Not.EqualTo(initialGrid),
                        "RMC storage fill must expand the undersized grid before insertion");
                    Assert.That(storage.StoredItems.Keys, Is.EquivalentTo(contained),
                        "the expanded grid must be usable by the real storage insertion lifecycle");
                });

                SEntMan.DeleteEntity(host);
            });
        }
        finally
        {
            Pair.ServerLogHandler.JudgeLog -= JudgeExpectedInvalidSpawn;
        }

        Assert.That(expectedInvalidSpawnErrors, Is.EqualTo(1),
            "the recoverable invalid entry must still emit exactly one authored-content error");
    }

    [Test]
    public async Task ContextContainersMakesEmptyConditionEffectiveAcrossRepeatedFills()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var host = SEntMan.SpawnEntity("ContainerFillMergeHost", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(host);
            var probe = SEntMan.GetComponent<ContainerFillMergeProbeComponent>(host);
            var fill = SEntMan.AddComponent<EntityTableContainerFillComponent>(host);
            fill.ContextContainers = true;
            fill.Containers[StorageComponent.ContainerId] = new AllSelector
            {
                ConditionsForChildren = { new EmptyContainerCondition() },
                Children = { new EntSelector { Id = "ContainerFillMergeSmall" } },
            };

            InvokeFill(host, fill);
            InvokeFill(host, fill);

            Assert.Multiple(() =>
            {
                Assert.That(storage.Container.ContainedEntities, Has.Count.EqualTo(1));
                Assert.That(probe.ItemPrototypes, Is.EqualTo(new[] { "ContainerFillMergeSmall" }));
                Assert.That(probe.WasContainedAtEvent, Is.EqualTo(new[] { false }));
            });

            SEntMan.DeleteEntity(host);
        });
    }

    private void InvokeFill(EntityUid uid, EntityTableContainerFillComponent component)
    {
        Entity<EntityTableContainerFillComponent> entity = (uid, component);
        var args = new MapInitEvent();
        typeof(ContainerFillSystem)
            .GetMethod("OnTableMapInit", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<ContainerFillSystem>(), new object[] { entity, args });
    }

    private string PrototypeId(EntityUid uid)
    {
        return SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID;
    }
}

[RegisterComponent]
public sealed partial class ContainerFillMergeProbeComponent : Component
{
    public readonly List<string> ItemPrototypes = new();
    public readonly List<bool> WasContainedAtEvent = new();
}

public sealed class ContainerFillMergeProbeSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContainerFillMergeProbeComponent, CMStorageItemFillEvent>(OnFill);
    }

    private void OnFill(Entity<ContainerFillMergeProbeComponent> ent, ref CMStorageItemFillEvent args)
    {
        ent.Comp.ItemPrototypes.Add(MetaData(args.Item).EntityPrototype!.ID);
        ent.Comp.WasContainedAtEvent.Add(_containers.TryGetContainingContainer(args.Item.Owner, out _));
    }
}

#pragma warning restore RA0002
