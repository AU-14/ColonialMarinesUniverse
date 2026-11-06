using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Access;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.Access;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Roles;

/// <summary>
///     Each Intelligence Section job admins can spawn comes with its own ID and the section
///     radio. The Case Officer also carries a colonist cover ID under their own name, and the
///     section's racks serve only section jobs.
/// </summary>
[TestFixture]
public sealed class CMUIntelligenceSectionTest : GameTest
{
    private const string CaseOfficer = "CMUJobIntelCaseOfficer";
    private const string SectionChannel = "CMUIntelSection";
    private const string SectionAccess = "CMUAccessIntelligenceSection";

    private static readonly string[] Jobs =
    {
        CaseOfficer,
        "CMUJobIntelParamilitaryOfficer",
        "CMUJobIntelParamilitaryMember",
    };

    [Test]
    public async Task EveryJobIsInTheDepartmentAndHasASpawnMarker()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var department = SProtoMan.Index<DepartmentPrototype>("CMUDepartmentIntelligenceSection");
            Assert.That(department.Roles.Select(job => job.Id), Is.EquivalentTo(Jobs));

            var markers = SProtoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract)
                .Select(proto => proto.TryGetComponent<SpawnPointComponent>(out var point, factory) ? point.Job?.Id : null)
                .ToHashSet();
            Assert.That(Jobs.Where(job => !markers.Contains(job)), Is.Empty, "every job needs a map spawn marker");
        });
    }

    /// <summary>The section's racks serve section jobs only, and a renamed or removed item would leave an empty pick.</summary>
    [TestCase("CMUIntelSectionClothingVendor")]
    [TestCase("CMUIntelSISParamilitaryVendor")]
    public async Task SectionRackServesTheSectionAndStocksRealItems(string rack)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SProtoMan.Index<EntityPrototype>(rack)
                .TryGetComponent<CMAutomatedVendorComponent>(out var vendor, SEntMan.ComponentFactory), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(vendor!.Jobs, Is.Not.Empty, "an open rack would arm anyone");
                Assert.That(vendor.Jobs.Select(job => job.Id), Is.SubsetOf(Jobs));
                foreach (var entry in vendor.Sections.SelectMany(section => section.Entries))
                {
                    Assert.That(SProtoMan.HasIndex<EntityPrototype>(entry.Id), Is.True, entry.Id.Id);
                }
            });
        });
    }

    [TestCaseSource(nameof(Jobs))]
    public async Task JobSpawnsWithItsIdAndTheSectionRadio(string job)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            var mob = Server.System<StationSpawningSystem>()
                .SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>(job), new HumanoidCharacterProfile(), station: null);

            Assert.That(inventory.TryGetSlotEntity(mob, "id", out var card), Is.True, "no ID card");
            Assert.That(SComp<IdCardComponent>(card!.Value).LocalizedJobTitle,
                Is.EqualTo(SProtoMan.Index<JobPrototype>(job).LocalizedName));
            Assert.That(SComp<AccessComponent>(card.Value).Tags, Does.Contain(new ProtoId<AccessLevelPrototype>(SectionAccess)));

            Assert.That(inventory.TryGetSlotEntity(mob, "ears", out var headset), Is.True, "no headset");
            Assert.That(SComp<EncryptionKeyHolderComponent>(headset!.Value).Channels,
                Does.Contain(new ProtoId<Content.Shared.Radio.RadioChannelPrototype>(SectionChannel)));

            SEntMan.DeleteEntity(mob);
        });
    }

    [Test]
    public async Task CaseOfficerCarriesACoverIdUnderTheirName()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            var profile = new HumanoidCharacterProfile().WithName("Mara Kessler");
            var mob = Server.System<StationSpawningSystem>()
                .SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>(CaseOfficer), profile, station: null);

            // Round start and late join raise PlayerSpawnCompleteEvent, which runs this.
            Server.System<CMUCoverIdCardSystem>().ApplyCoverNames(mob);

            Assert.That(inventory.TryGetSlotEntity(mob, "id", out var real), Is.True);
            Assert.That(inventory.TryGetSlotEntity(mob, "pocket1", out var cover), Is.True, "no cover ID in the pocket");
            Assert.That(SEntMan.HasComponent<CMUCoverIdCardComponent>(cover!.Value), Is.True);

            var plainColonistId = SEntMan.SpawnEntity("AU14IDCardColonyColonist", map.GridCoords);
            var coverCard = SComp<IdCardComponent>(cover.Value);
            Assert.Multiple(() =>
            {
                Assert.That(SComp<IdCardComponent>(real!.Value).FullName, Is.EqualTo("Mara Kessler"));
                Assert.That(coverCard.FullName, Is.EqualTo("Mara Kessler"));
                Assert.That(coverCard.LocalizedJobTitle,
                    Is.EqualTo(SComp<IdCardComponent>(plainColonistId).LocalizedJobTitle),
                    "the cover must read like any colonist ID");
                Assert.That(SComp<AccessComponent>(cover.Value).Tags,
                    Is.EquivalentTo(SComp<AccessComponent>(plainColonistId).Tags),
                    "the cover must not open section doors");
            });

            SEntMan.DeleteEntity(plainColonistId);
            SEntMan.DeleteEntity(mob);
        });
    }
}
