using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Insurgency;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Insurgency;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Insurgency;

/// <summary>
///     FLOW-12 (d): every INSFOR faction's points vendor offers the ATGM kit and missile at the CLF rack's
///     prices, while free caches stay untouched and a faction's own ATGM entries are not duplicated.
/// </summary>
[TestFixture]
public sealed class InsurgencyAntiArmorAccessTest
{
    private const int KitPoints = 35;
    private const int MissilePoints = 15;

    private static readonly EntProtoId AtgmCase = "CMUATGMCase";
    private static readonly EntProtoId AtgmMissile = "CMUATGMMissile";
    private static readonly EntProtoId ClfRack = "AU14CLFObjectiveWeaponsVendor";

    [Test]
    public async Task CustomFactionPointsVendorGetsAtgm()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var apply = entMan.System<InsurgencyFactionApplySystem>();
            var rackAmount = GetRackEntry(server.ResolveDependency<IPrototypeManager>(), entMan.ComponentFactory, AtgmMissile)
                .Amount;

            var definition = new FactionDefinition { Metadata = { Title = "ATGM access test cell" } };
            definition.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
            {
                Name = "Cell arsenal",
                BaseModel = ClfRack,
                UsesIntelPoints = true,
                Sections =
                {
                    new CMVendorSection
                    {
                        Name = "Sidearms",
                        Entries = { new CMVendorEntry { Id = "RMCWeaponPistolHoldout", Amount = 4, Points = 1 } },
                    },
                },
            });
            // Free cache: UsesIntelPoints defaults to true, but nothing in it costs points.
            definition.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
            {
                Name = "Cell medical cache",
                BaseModel = ClfRack,
                Sections =
                {
                    new CMVendorSection
                    {
                        Name = "First Aid",
                        Entries = { new CMVendorEntry { Id = "CMFirstAidKitFilled", Amount = 2, Points = 0 } },
                    },
                },
            });

            apply.ApplyFaction(definition);
            var active = apply.GetActiveFaction();
            Assert.That(active, Is.Not.Null);

            var points = active!.CellKit.VendorDefinitions[0];
            var cache = active.CellKit.VendorDefinitions[1];

            Assert.Multiple(() =>
            {
                AssertEntry(points.Sections, AtgmCase, KitPoints, 1, "definition");
                AssertEntry(points.Sections, AtgmMissile, MissilePoints, rackAmount, "definition");
                Assert.That(Entries(cache.Sections).Select(e => e.Id), Is.EquivalentTo(new EntProtoId[] { "CMFirstAidKitFilled" }),
                    "The free cache must not be modified.");
                Assert.That(definition.CellKit.VendorDefinitions[0].Sections, Has.Count.EqualTo(1),
                    "The caller's definition must not be modified; only the runtime copy gains the ATGM.");
            });

            // The deployed vendors are what players actually use.
            var pointsVendor = entMan.SpawnEntity(ClfRack, map.GridCoords);
            var cacheVendor = entMan.SpawnEntity(ClfRack, map.GridCoords);
            apply.ConfigureFactionVendor(pointsVendor, points, 0);
            apply.ConfigureFactionVendor(cacheVendor, cache, 1);

            var placedPoints = entMan.GetComponent<CMAutomatedVendorComponent>(pointsVendor);
            var placedCache = entMan.GetComponent<CMAutomatedVendorComponent>(cacheVendor);

            Assert.Multiple(() =>
            {
                Assert.That(placedPoints.UseObjectivePoints, Is.True);
                AssertEntry(placedPoints.Sections, AtgmCase, KitPoints, 1, "placed vendor");
                AssertEntry(placedPoints.Sections, AtgmMissile, MissilePoints, rackAmount, "placed vendor");
                Assert.That(Entries(placedCache.Sections).Any(e => e.Id == AtgmCase || e.Id == AtgmMissile), Is.False,
                    "The placed free cache must not offer the ATGM.");
            });

            entMan.DeleteEntity(pointsVendor);
            entMan.DeleteEntity(cacheVendor);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactionAtgmEntriesAreNotDuplicated()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var apply = server.EntMan.System<InsurgencyFactionApplySystem>();

            var definition = new FactionDefinition { Metadata = { Title = "ATGM pricing test cell" } };
            definition.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
            {
                Name = "Cell arsenal",
                BaseModel = ClfRack,
                Sections =
                {
                    new CMVendorSection
                    {
                        Name = "Heavy",
                        Entries = { new CMVendorEntry { Id = AtgmCase, Amount = 2, Points = 20 } },
                    },
                },
            });

            apply.ApplyFaction(definition);
            var sections = apply.GetActiveFaction()!.CellKit.VendorDefinitions[0].Sections;

            Assert.Multiple(() =>
            {
                Assert.That(Entries(sections).Count(e => e.Id == AtgmCase), Is.EqualTo(1),
                    "A faction that already stocks the kit keeps its single entry.");
                AssertEntry(sections, AtgmCase, 20, 2, "author-priced kit");
                Assert.That(Entries(sections).Count(e => e.Id == AtgmMissile), Is.EqualTo(1),
                    "The missing missile is still added.");
            });

            // The built-in vanilla CLF uses the real rack prototype and free caches: nothing is injected.
            apply.ApplyFaction(InsurgencyBuiltinFactions.VanillaClf());
            var vanilla = apply.GetActiveFaction()!;
            foreach (var vendor in vanilla.CellKit.VendorDefinitions)
            {
                Assert.That(Entries(vendor.Sections).Any(e => e.Id == AtgmCase || e.Id == AtgmMissile), Is.False,
                    $"Vanilla CLF vendor '{vendor.Name}' must keep its own sections.");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<CMVendorEntry> Entries(IEnumerable<CMVendorSection> sections)
    {
        return sections.SelectMany(s => s.Entries);
    }

    private static void AssertEntry(
        IEnumerable<CMVendorSection> sections,
        EntProtoId id,
        int points,
        int? amount,
        string context)
    {
        var entry = Entries(sections).FirstOrDefault(e => e.Id == id);
        Assert.That(entry, Is.Not.Null, $"{context} does not offer {id}");
        Assert.That(entry?.Points, Is.EqualTo(points), $"{context} {id} points");
        Assert.That(entry?.Amount, Is.EqualTo(amount), $"{context} {id} amount");
    }

    private static CMVendorEntry GetRackEntry(IPrototypeManager prototypes, IComponentFactory factory, EntProtoId id)
    {
        var rack = prototypes.Index<EntityPrototype>(ClfRack);
        Assert.That(rack.TryComp<CMAutomatedVendorComponent>(out var vendor, factory), Is.True);
        var entry = Entries(vendor!.Sections).FirstOrDefault(e => e.Id == id);
        Assert.That(entry, Is.Not.Null, $"{ClfRack} does not stock {id}");
        return entry!;
    }
}
