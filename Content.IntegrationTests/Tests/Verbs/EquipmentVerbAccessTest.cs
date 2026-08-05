using System.Numerics;
using Content.Server.Verbs;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Verbs;

[TestFixture]
[TestOf(typeof(SharedVerbSystem))]
public sealed class EquipmentVerbAccessTest
{
    private const string TestVerbText = "Equipment access test";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: EquipmentVerbAccessTestWearer
  components:
  - type: Inventory
  - type: ContainerContainer

- type: entity
  id: EquipmentVerbAccessTestItem
  components:
  - type: EquipmentVerbAccessTest
";

    [Test]
    public async Task EquipmentVerbsUseCombinedAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var interaction = server.System<SharedInteractionSystem>();
        var verbs = server.System<VerbSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.MapCoords);

            var nearWearer = entMan.SpawnEntity("EquipmentVerbAccessTestWearer", map.MapCoords);
            var nearEquipment = entMan.SpawnEntity("EquipmentVerbAccessTestItem", map.MapCoords);
            Assert.That(inventory.TryEquip(nearWearer, nearEquipment, "outerClothing", force: true));

            var farCoordinates = new MapCoordinates(
                map.MapCoords.Position + new Vector2(SharedInteractionSystem.InteractionRange + 1f, 0f),
                map.MapId);
            var farWearer = entMan.SpawnEntity("EquipmentVerbAccessTestWearer", farCoordinates);
            var farEquipment = entMan.SpawnEntity("EquipmentVerbAccessTestItem", farCoordinates);
            Assert.That(inventory.TryEquip(farWearer, farEquipment, "outerClothing", force: true));

            var looseItem = entMan.SpawnEntity("EquipmentVerbAccessTestItem", map.MapCoords);

            Assert.Multiple(() =>
            {
                Assert.That(interaction.InRangeAndAccessible(user, nearEquipment), Is.False);
                Assert.That(interaction.CanAccessEquipment(user, nearEquipment), Is.True);
                Assert.That(verbs.GetLocalVerbs(nearEquipment, user, typeof(EquipmentVerb)),
                    Has.Count.EqualTo(1),
                    "Nearby equipped items must use equipment access.");

                Assert.That(interaction.InRangeAndAccessible(user, looseItem), Is.True);
                Assert.That(interaction.CanAccessEquipment(user, looseItem), Is.False);
                Assert.That(verbs.GetLocalVerbs(looseItem, user, typeof(EquipmentVerb)),
                    Has.Count.EqualTo(1),
                    "Loose items must retain normal interaction access.");

                Assert.That(interaction.InRangeAndAccessible(user, farEquipment), Is.False);
                Assert.That(interaction.CanAccessEquipment(user, farEquipment), Is.False);
                Assert.That(verbs.GetLocalVerbs(farEquipment, user, typeof(EquipmentVerb)),
                    Is.Empty,
                    "Equipment access must not bypass range checks.");
            });
        });

        await pair.CleanReturnAsync();
    }

    private sealed class EquipmentVerbAccessTestSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<EquipmentVerbAccessTestComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
        }

        private void OnGetVerbs(
            Entity<EquipmentVerbAccessTestComponent> _,
            ref GetVerbsEvent<EquipmentVerb> args)
        {
            if (!args.CanAccess)
                return;

            args.Verbs.Add(new EquipmentVerb { Text = TestVerbText });
        }
    }
}

// Components must be directly in the namespace for source generation.
[RegisterComponent]
public sealed partial class EquipmentVerbAccessTestComponent : Component;
