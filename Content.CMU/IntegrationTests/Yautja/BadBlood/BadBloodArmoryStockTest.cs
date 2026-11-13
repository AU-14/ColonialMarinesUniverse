#pragma warning disable RA0002 // tests read component data directly
using System.Linq;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-202, STATE-203, STATE-204: what the Lodge armory stocks, and what the Totem and the kennel look like,
// read from the prototypes
[TestFixture]
public sealed class BadBloodArmoryStockTest
{
    private const string Armory = "CMUBadBloodCampArmory";
    private const string Totem = "CMUBadBloodTotem";
    private const string Kennel = "CMUBadBloodCampKennel";
    private const string SpikeLauncher = "CMUYautjaSpikeLauncher";
    private const string ScavengedSpikeLauncher = "CMUBadBloodScavengedSpikeLauncher";

    // tier -> the lowest and highest price a ware of that tier may carry
    private static readonly (int Min, int Max)[] PriceBands = { (2, 5), (4, 7), (8, 11) };

    private static readonly string[] AddedWares =
    {
        "CMUYautjaBadBloodStabilizerGel",
        "CMUYautjaCleanserGelVial",
        "CMUYautjaHarpoon",
        "CMUYautjaCeremonialDagger",
        "CMUYautjaBracerShieldAttachment",
        "CMUYautjaChainwhip",
        "CMUYautjaWarAxe",
        "CMUYautjaDualWarScythe",
        ScavengedSpikeLauncher,
    };

    [Test]
    public async Task EveryWareHasATierAPriceAndIsAllowed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ProtoMan;
            var factory = server.EntMan.ComponentFactory;

            var armoryProto = protos.Index<EntityPrototype>(Armory);
            Assert.That(armoryProto.TryComp<BadBloodCampArmoryComponent>(out var armory, factory), Is.True);
            Assert.That(armoryProto.TryComp<CMAutomatedVendorComponent>(out var vendor, factory), Is.True);
            Assert.That(vendor!.Sections, Has.Count.EqualTo(PriceBands.Length), "one section a tier");

            var sold = new HashSet<string>();
            for (var section = 0; section < vendor.Sections.Count; section++)
            {
                var tier = section + 1;
                var (min, max) = PriceBands[section];

                foreach (var entry in vendor.Sections[section].Entries)
                {
                    var id = entry.Id.Id;
                    Assert.That(sold.Add(id), Is.True, $"{id} is listed twice");
                    Assert.That(protos.TryIndex<EntityPrototype>(id, out var ware), Is.True, $"{id} does not exist");

                    Assert.Multiple(() =>
                    {
                        Assert.That(armory!.Tiers.TryGetValue(entry.Id, out var listed), Is.True, $"{id} has no tier");
                        Assert.That(listed, Is.EqualTo(tier), $"{id} is sold in the tier {tier} section");
                        Assert.That(entry.Points, Is.Not.Null, $"{id} has no price");
                        Assert.That(entry.Points, Is.InRange(min, max), $"{id}: a tier {tier} ware costs {min} to {max} SP");

                        Assert.That(ware!.TryComp<YautjaCasterComponent>(out _, factory), Is.False,
                            $"{id} is a plasma caster");
                        Assert.That(ware.TryComp<YautjaRelayBeaconComponent>(out _, factory), Is.False,
                            $"{id} teleports shipside");
                        Assert.That(Gibs(protos, factory, ware), Is.False, $"{id} gibs the prey");
                    });
                }
            }

            Assert.That(armory!.Tiers.Keys.Select(key => key.Id), Is.SubsetOf(sold), "a tier is listed for a ware that is not sold");

            foreach (var added in AddedWares)
            {
                Assert.That(sold, Does.Contain(added), $"{added} is stocked");
            }

            // the scavenged launcher is the hunters' launcher cut down, and the hunters' own is untouched
            Assert.That(protos.Index<EntityPrototype>(SpikeLauncher).TryComp<BasicEntityAmmoProviderComponent>(out var honourable, factory), Is.True);
            Assert.That(protos.Index<EntityPrototype>(ScavengedSpikeLauncher).TryComp<BasicEntityAmmoProviderComponent>(out var scavenged, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(honourable!.Capacity, Is.EqualTo(12));
                Assert.That(honourable.Proto.Id, Is.EqualTo("CMUYautjaSpikeProjectile"));
                Assert.That(scavenged!.Capacity, Is.LessThan(honourable.Capacity));
                Assert.That(scavenged.Proto.Id, Is.EqualTo("CMUBadBloodScavengedSpike"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TotemHasOneSpriteLayerAndTheKennelIsACage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var totem = SpriteOf(server.ProtoMan, Totem);
            Assert.That(totem.Get<SequenceDataNode>("layers"), Has.Count.EqualTo(1), "the Totem draws the armor prop and nothing else");

            var kennel = SpriteOf(server.ProtoMan, Kennel);
            Assert.That(kennel.Get<ValueDataNode>("sprite").Value, Is.EqualTo("Structures/Storage/Crates/livestock.rsi"),
                "the kennel is drawn as a cage, not a feeding trough");
        });

        await pair.CleanReturnAsync();
    }

    // the ware itself, or what it fires
    private static bool Gibs(IPrototypeManager protos, IComponentFactory factory, EntityPrototype ware)
    {
        if (ware.TryComp<YautjaGibHumansOnHitComponent>(out _, factory))
            return true;

        return ware.TryComp<BasicEntityAmmoProviderComponent>(out var ammo, factory) &&
               protos.TryIndex<EntityPrototype>(ammo.Proto.Id, out var fired) &&
               fired.TryComp<YautjaGibHumansOnHitComponent>(out _, factory);
    }

    private static MappingDataNode SpriteOf(IPrototypeManager protos, string id)
    {
        Assert.That(protos.TryGetMapping<EntityPrototype>(id, out var mapping), Is.True, $"{id} has no prototype data");

        foreach (var node in mapping!.Get<SequenceDataNode>("components"))
        {
            if (node is MappingDataNode component &&
                component.TryGet<ValueDataNode>("type", out var type) &&
                type.Value == "Sprite")
            {
                return component;
            }
        }

        Assert.Fail($"{id} has no Sprite");
        return null!;
    }
}
