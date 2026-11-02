using System.Linq;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Yautja;

/// <summary>
/// cmss13 lets a pred wear the cape on the armour so the back slot stays free, and the cape keeps its colour there.
/// </summary>
[TestFixture]
public sealed class YautjaCapeMountTest
{
    private const string MountLayer = "cmu-yautja-cape-mount";
    private static readonly Color CapeColor = new((byte) 0x2a, (byte) 0x5c, (byte) 0x8a);

    [Test]
    public async Task CapeFastensToArmourAndKeepsItsColour()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid armor = default;
        EntityUid cape = default;
        NetEntity hunterNet = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            armor = entMan.SpawnEntity("CMUYautjaClanArmor", map.GridCoords);
            Assert.That(inventory.TryEquip(hunter, armor, "outerClothing", silent: true, force: true), Is.True);

            cape = entMan.SpawnEntity("CMUYautjaCapeQuarter", map.GridCoords);
            var capeComp = entMan.GetComponent<YautjaCapeComponent>(cape);
            capeComp.Color = CapeColor;
            entMan.Dirty(cape, capeComp);
            Assert.That(hands.TryPickupAnyHand(hunter, cape), Is.True);

            var interact = new InteractUsingEvent(hunter, cape, armor, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(armor, interact);
            Assert.That(interact.Handled, Is.True);

            var container = entMan.System<SharedContainerSystem>()
                .GetContainer(armor, entMan.GetComponent<YautjaCapeMountComponent>(armor).ContainerId);
            Assert.That(container.ContainedEntities, Is.EqualTo(new[] { cape }));
            Assert.That(hands.IsHolding(hunter, cape), Is.False);
            Assert.That(inventory.TryGetSlotEntity(hunter, "back", out _), Is.False, "the back slot stays free");

            // a second cape bounces off
            var spare = entMan.SpawnEntity("CMUYautjaCapeHalf", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(hunter, spare), Is.True);
            entMan.EventBus.RaiseLocalEvent(armor, new InteractUsingEvent(hunter, spare, armor, map.GridCoords));
            Assert.That(container.ContainedEntities, Is.EqualTo(new[] { cape }));
            entMan.DeleteEntity(spare);

            hunterNet = entMan.GetNetEntity(hunter);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            Assert.That(entMan.TryGetEntity(hunterNet, out var clientHunter), Is.True);
            var sprite = entMan.GetComponent<SpriteComponent>(clientHunter.Value);
            var spriteSystem = entMan.System<SpriteSystem>();
            Assert.That(spriteSystem.LayerMapTryGet((clientHunter.Value, sprite), MountLayer, out var index, false), Is.True,
                "the fastened cape draws on the armour");
            Assert.That(spriteSystem.TryGetLayer((clientHunter.Value, sprite), index, out var layer, false), Is.True);
            Assert.That(layer!.Visible, Is.True);
            Assert.That(layer.Color, Is.EqualTo(CapeColor));
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var unfasten = entMan.System<SharedVerbSystem>()
                .GetLocalVerbs(armor, hunter, typeof(AlternativeVerb))
                .Single(verb => verb.Text == Loc.GetString("cmu-yautja-cape-mount-remove"));
            unfasten.Act!.Invoke();

            Assert.That(entMan.System<SharedHandsSystem>().IsHolding(hunter, cape), Is.True);
            Assert.That(entMan.GetComponent<YautjaCapeComponent>(cape).Color, Is.EqualTo(CapeColor));
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            Assert.That(entMan.TryGetEntity(hunterNet, out var clientHunter), Is.True);
            var sprite = entMan.GetComponent<SpriteComponent>(clientHunter.Value);
            var spriteSystem = entMan.System<SpriteSystem>();
            var drawn = spriteSystem.LayerMapTryGet((clientHunter.Value, sprite), MountLayer, out var index, false) &&
                        spriteSystem.TryGetLayer((clientHunter.Value, sprite), index, out var layer, false) &&
                        layer.Visible;
            Assert.That(drawn, Is.False, "the cape layer goes away with the cape");
        });

        await server.WaitPost(() =>
        {
            foreach (var uid in new[] { cape, armor, hunter })
            {
                if (uid != default && !server.EntMan.Deleted(uid))
                    server.EntMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WornCapeLayerTakesTheClothColour()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        NetEntity hunterNet = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var cape = entMan.SpawnEntity("CMUYautjaCapeFull", map.GridCoords);
            var capeComp = entMan.GetComponent<YautjaCapeComponent>(cape);
            capeComp.Color = CapeColor;
            entMan.Dirty(cape, capeComp);
            Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, cape, "back", silent: true, force: true), Is.True);

            var bone = entMan.SpawnEntity("CMUYautjaCapeBoneFull", map.GridCoords);
            Assert.That(entMan.GetComponent<YautjaCapeComponent>(bone).Recolorable, Is.False);
            entMan.DeleteEntity(bone);

            hunterNet = entMan.GetNetEntity(hunter);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            Assert.That(entMan.TryGetEntity(hunterNet, out var clientHunter), Is.True);
            var sprite = entMan.GetComponent<SpriteComponent>(clientHunter.Value);

            // the worn sheet is greyscale, so without the tint the cape showed up grey on the mob
            var capeLayers = sprite.AllLayers
                .OfType<SpriteComponent.Layer>()
                .Where(layer => layer.Visible && layer.State.Name == "equipped-BACKPACK" &&
                                layer.ActualRsi?.Path.ToString().Contains("cape_full") == true)
                .ToList();
            Assert.That(capeLayers, Is.Not.Empty);
            Assert.That(capeLayers.Select(layer => layer.Color), Is.All.EqualTo(CapeColor));
        });

        await server.WaitPost(() =>
        {
            if (!server.EntMan.Deleted(hunter))
                server.EntMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }
}
