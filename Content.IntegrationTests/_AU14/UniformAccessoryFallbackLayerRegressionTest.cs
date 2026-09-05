#pragma warning disable RA0002 // Integration regression intentionally inspects restricted sprite state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests._AU14;

[TestFixture]
public sealed class UniformAccessoryFallbackLayerRegressionTest : GameTest
{
    [Test]
    public async Task UppFatiguesDefaultAccessoriesUseDistinctPlainClientLayerKeys()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        NetEntity wearerNet = default;
        NetEntity uniformNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var wearer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var uniform = SEntMan.SpawnEntity("AU14FatiguesUPP", map.GridCoords);
                var holder = SEntMan.GetComponent<UniformAccessoryHolderComponent>(uniform);

                Assert.That(holder.StartingAccessories?.Select(id => id.Id), Is.EqualTo(new[]
                {
                    "AU14PatchUPP",
                    "AU14PatchUPPNavalInfantry",
                }));
                Assert.That(Server.System<InventorySystem>()
                    .TryEquip(wearer, uniform, "jumpsuit", silent: true, force: true), Is.True);

                Server.PlayerMan.SetAttachedEntity(session, wearer);
                wearerNet = SEntMan.GetNetEntity(wearer);
                uniformNet = SEntMan.GetNetEntity(uniform);
            });

            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var wearer = CEntMan.GetEntity(wearerNet);
                var uniform = CEntMan.GetEntity(uniformNet);
                Assert.That(CEntMan.HasComponent<UniformAccessoryHolderComponent>(uniform), Is.True);

                var sprite = CEntMan.GetComponent<SpriteComponent>(wearer);
                var visuals = new GetEquipmentVisualsEvent(wearer, "jumpsuit");
                CEntMan.EventBus.RaiseLocalEvent(uniform, visuals);
                var accessoryKeys = visuals.Layers
                    .Select(layer => layer.Item1)
                    .Where(key => key.StartsWith("uniform-accessory-", StringComparison.Ordinal))
                    .ToArray();
                var sprites = Client.System<SpriteSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(accessoryKeys, Has.Length.EqualTo(2));
                    Assert.That(accessoryKeys.Distinct().ToArray(), Has.Length.EqualTo(2));
                    Assert.That(accessoryKeys.All(key => !key.StartsWith("enum.", StringComparison.Ordinal)),
                        Is.True);
                    Assert.That(accessoryKeys.All(key =>
                        sprites.LayerMapTryGet((wearer, sprite), key, out _, false)), Is.True,
                        "both fallback accessory layers must be installed on the connected wearer");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }
}

#pragma warning restore RA0002
