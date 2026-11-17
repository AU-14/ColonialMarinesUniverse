using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ARCH-02: the bracer weapon is forged in place at the armory (tier II of starter -> forged -> golden).
// ARCH-04 to ARCH-06: the Engineer and Medic castes are taken up at their Lodge stations, capped by clan size, and
// only their caste may use a station.
[TestFixture]
public sealed class BadBloodProgressionTest : GameTest
{
    private const string Armory = "CMUBadBloodCampArmory";
    private const string EngineerStation = "CMUBadBloodCampEngineerStation";
    private const string MedicStation = "CMUBadBloodCampMedicStation";
    private const string Forge = "CMUBadBloodForgeUpgrade";
    private const string Bracer = "CMUYautjaBadBloodBracer";
    private const string Brick = "CMUSheetBadBloodBrick";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TheForgeUpgradesTheBuyersOwnBracerWeaponOnce()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var rules = entMan.System<BadBloodCampRuleSystem>();
                var camp = entMan.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // a fresh bracer with the scimitar picked, like a landing Bad Blood's
                if (inventory.TryUnequip(grunt, "gloves", out var old, silent: true, force: true))
                    entMan.DeleteEntity(old.Value);

                var bracer = entMan.SpawnEntity(Bracer, map.GridCoords);
                spawned.Add(bracer);
                Assert.That(inventory.TryEquip(grunt, bracer, "gloves", silent: true, force: true), Is.True);
                Choose(entMan, bracer, grunt, YautjaGearKind.Scimitar);
                Choose(entMan, bracer, grunt, YautjaGearKind.Scimitar);
                var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gear.Gear.TryGetValue(YautjaGearKind.Scimitar, out var scimitar), Is.True);

                spawned.Add(entMan.SpawnEntity("CMUBadBloodCamp", map.GridCoords));
                var armory = entMan.SpawnEntity(Armory, map.GridCoords);
                spawned.Add(armory);
                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory, entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                camp.PrepareArmoryBuyer(armoryEnt, grunt);

                // tier II and the price, all from the grunt's own Honour
                Assert.That(rules.AddSp(rule.Comp.TierTwoLifetimeSp), Is.True);
                Assert.That(rules.AddHonour(grunt, FixedPoint4.New(10)), Is.True);
                Assert.That(rules.GetTier(), Is.EqualTo(2));
                var before = rules.GetHonour(grunt);
                var price = FixedPoint4.New(Entry(entMan, armory, Forge).Entry.Points!.Value);

                Vend(entMan, armory, grunt, Forge);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent(scimitar, out BadBloodForgedComponent? forged), Is.True,
                        "the forge reworks the bracer weapon itself");
                    Assert.That(forged!.Multiplier, Is.EqualTo(1.2f).Within(0.001f));
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(before - price), "the forge costs the buyer's Honour");
                });

                // a forged swing hits a fifth harder
                var base20 = new DamageSpecifier(Server.ProtoMan.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>("Slash"), 20);
                var ev = new GetMeleeDamageEvent(scimitar, new DamageSpecifier(base20), new(), grunt);
                entMan.EventBus.RaiseLocalEvent(scimitar, ref ev);
                Assert.That(ev.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(24)));

                // nothing left to forge: refused, and nothing is charged
                Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, Forge, out var why), Is.False);
                Assert.That(why, Is.EqualTo("cmu-bad-blood-armory-forge-nothing"));
                var after = rules.GetHonour(grunt);
                Vend(entMan, armory, grunt, Forge);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(after));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    [Test]
    public async Task CastesAreTakenAtTheirStationsAndCappedByClanSize()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var castes = entMan.System<BadBloodCasteSystem>();
                var verbs = entMan.System<SharedVerbSystem>();
                var skills = entMan.System<SkillsSystem>();
                var engineerStation = entMan.SpawnEntity(EngineerStation, map.GridCoords);
                var medicStation = entMan.SpawnEntity(MedicStation, map.GridCoords);
                spawned.Add(engineerStation);
                spawned.Add(medicStation);

                var (a, b, c) = (party.Grunts[0], party.Grunts[1], party.Grunts[2]);

                // four living Bad Bloods: one of each caste
                Assert.That(castes.GetCasteCap(BadBloodCaste.Engineer), Is.EqualTo(1));

                // the station offers the caste to whoever has none
                var take = verbs.GetLocalVerbs(engineerStation, a, typeof(Verb))
                    .FirstOrDefault(v => v.Text == Loc.GetString("cmu-bad-blood-caste-verb-engineer"));
                Assert.That(take, Is.Not.Null, "the forge-bench offers the Engineer path");
                verbs.ExecuteVerb(take!, a, engineerStation);

                Assert.Multiple(() =>
                {
                    Assert.That(castes.IsCaste(a, BadBloodCaste.Engineer), Is.True);
                    Assert.That(skills.GetSkill(a, "RMCSkillEngineer"), Is.GreaterThanOrEqualTo(1),
                        "an Engineer can plant the demolition charge");
                    Assert.That(castes.TryTakeCaste(b, BadBloodCaste.Engineer), Is.False, "one Engineer per four Bad Bloods");
                    Assert.That(castes.TryTakeCaste(a, BadBloodCaste.Medic), Is.False, "one caste per life");
                    Assert.That(castes.TryTakeCaste(b, BadBloodCaste.Medic), Is.True);
                    Assert.That(castes.IsCaste(b, BadBloodCaste.Medic), Is.True);
                });

                // a station opens only for its caste
                Assert.Multiple(() =>
                {
                    Assert.That(OpenRefused(entMan, engineerStation, a), Is.False, "the Engineer opens the forge-bench");
                    Assert.That(OpenRefused(entMan, engineerStation, c), Is.True, "a Bad Blood without the caste cannot");
                    Assert.That(OpenRefused(entMan, medicStation, a), Is.True, "nor can another caste");
                    Assert.That(OpenRefused(entMan, medicStation, b), Is.False, "the Medic opens the medicine cache");
                });

                // an Engineer builds where nobody else may: there is no Lodge here at all
                var brick = entMan.SpawnEntity(Brick, map.GridCoords);
                spawned.Add(brick);
                Assert.Multiple(() =>
                {
                    Assert.That(BuildRefused(entMan, c, brick, map), Is.True, "out of range for an ordinary Bad Blood");
                    Assert.That(BuildRefused(entMan, a, brick, map), Is.False, "an Engineer builds anywhere");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    // Bad Bloods used to read each other as "unknown" (Yautja see Yautja by their identity name). Now each carries a
    // clan code and the clan sees "Grunt (BB-12)"; a marine still sees the masked identity
    [Test]
    public async Task BadBloodsKnowEachOtherByRoleAndClanCode()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var codes = entMan.System<BadBloodCodeSystem>();
                var (a, b) = (party.Grunts[0], party.Grunts[1]);
                var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                spawned.Add(marine);

                var numbers = party.All.Select(uid => entMan.GetComponent<BadBloodCodeComponent>(uid).Number).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(numbers, Is.All.InRange(BadBloodCodeSystem.First, BadBloodCodeSystem.Last));
                    Assert.That(numbers, Is.Unique, "every Bad Blood has its own code");

                    var gruntName = Loc.GetString("cmu-bad-blood-code-name",
                        ("role", Loc.GetString("cmu-bad-blood-code-role-grunt")),
                        ("number", entMan.GetComponent<BadBloodCodeComponent>(b).Number));
                    Assert.That(codes.GetClanName(b), Is.EqualTo(gruntName));
                    Assert.That(Identity.Name(b, entMan, a).Name, Is.EqualTo(gruntName), "a Bad Blood sees its clan by code");
                    Assert.That(Identity.Name(party.Leader, entMan, a).Name, Does.StartWith(Loc.GetString("cmu-bad-blood-code-role-leader")),
                        "and the Leader as the Leader");
                    Assert.That(Identity.Name(b, entMan, marine).Name, Is.Not.EqualTo(gruntName), "a marine does not learn the code");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Cleanup(spawned, party);
        }
    }

    private static bool OpenRefused(IEntityManager entMan, EntityUid station, EntityUid user)
    {
        var ev = new ActivatableUIOpenAttemptEvent(user, true);
        entMan.EventBus.RaiseLocalEvent(station, ev);
        return ev.Cancelled;
    }

    private static bool BuildRefused(IEntityManager entMan, EntityUid user, EntityUid brick, TestMapData map)
    {
        var ev = new CMUConstructionBuildAttemptEvent(user, brick, default, map.GridCoords);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return ev.Cancelled;
    }

    private static void Choose(IEntityManager entMan, EntityUid bracer, EntityUid actor, YautjaGearKind kind)
    {
        entMan.EventBus.RaiseLocalEvent(bracer, (object) new YautjaBadBloodWeaponChoiceMsg(kind)
        {
            UiKey = YautjaBadBloodWeaponChoiceUI.Key,
            Actor = actor,
        });
    }

    private static (CMVendorSection Section, CMVendorEntry Entry) Entry(IEntityManager entMan, EntityUid vendor, string id)
    {
        foreach (var section in entMan.GetComponent<CMAutomatedVendorComponent>(vendor).Sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Id.Id == id)
                    return (section, entry);
            }
        }

        throw new InvalidOperationException($"{id} is not sold here");
    }

    private static void Vend(IEntityManager entMan, EntityUid vendor, EntityUid user, string id)
    {
        var sections = entMan.GetComponent<CMAutomatedVendorComponent>(vendor).Sections;
        var (section, entry) = Entry(entMan, vendor, id);
        entMan.EventBus.RaiseLocalEvent(vendor, new CMVendorVendBuiMsg(sections.IndexOf(section), section.Entries.IndexOf(entry), new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }

    private async Task Cleanup(List<EntityUid> spawned, BadBloodTestParty party)
    {
        await Server.WaitPost(() =>
        {
            foreach (var uid in spawned.Concat(party.All))
            {
                if (Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
