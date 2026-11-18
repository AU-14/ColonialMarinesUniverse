#pragma warning disable RA0002 // tests read component data directly
using Content.Shared.Ghost.Roles.Components;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.K9.Components;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 Playtest 5 (docs/GraphAudit-BadBlood-Playtest5Plan.md). ARCH-702 / ARCH-706: kennel hounds answer to the
// Beast-Master, the Hunt Leader only while none lives; taking the caste re-binds them, the Beast-Master's death makes
// them rage (Last Order) and then go to the next master. ARCH-705: the Beast-Master's armour counts for less.
// STATE-702: the kennel keeps one more hound ready per living Beast-Master. ECS-707: Blood Scent, Rally Howl and
// Mend Beast reach the bonded beasts
[TestFixture]
public sealed class BadBloodBeastMasterTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Kennel = "CMUBadBloodCampKennel";
    private const string Hound = "CMUMobYautjaBadBloodHound";
    private static readonly ProtoId<BadBloodCastePrototype> BeastMaster = "BeastMaster";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task KennelHoundsFollowTheBeastMasterThenTheLeader()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var beastMaster = party.Grunts[0];
        var session = Pair.Player!;
        var spawned = new List<EntityUid>();
        EntityUid kennel = default;
        EntityUid hound = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.ReserveQueue.Clear();
                Server.PlayerMan.SetAttachedEntity(session, null);

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));
                spawned.Add(kennel);
                var stock = entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel);
                Assert.That(stock.Max, Is.EqualTo(2), "four Bad Bloods, no Beast-Master: the floor of two");

                // no Beast-Master yet: the hound answers to the Hunt Leader
                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(kennel, ref take);
                Assert.That(take.TookRole, Is.True);
                hound = stock.LiveHounds.Single();
                Assert.That(entMan.GetComponent<BadBloodBeastComponent>(hound).Master, Is.EqualTo(party.Leader));
                Assert.That(entMan.GetComponent<K9DogComponent>(hound).Master, Is.EqualTo(party.Leader));

                // ARCH-706: a Beast-Master takes over the kennel's hound; the Leader keeps no empty handler kit
                Assert.That(Server.System<BadBloodCasteSystem>().TryTakeCaste(beastMaster, BeastMaster), Is.True);
                var pack = entMan.GetComponent<BadBloodBeastMasterComponent>(beastMaster);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BadBloodBeastComponent>(hound).Master, Is.EqualTo(beastMaster));
                    Assert.That(entMan.GetComponent<K9DogComponent>(hound).Master, Is.EqualTo(beastMaster));
                    Assert.That(entMan.HasComponent<K9HandlerComponent>(beastMaster), Is.True);
                    Assert.That(entMan.HasComponent<K9HandlerComponent>(party.Leader), Is.False,
                        "the Leader lost their last dog, and the handler kit with it");
                    Assert.That(pack.Beasts, Does.Contain(hound));
                    Assert.That(pack.BloodScentActionEntity, Is.Not.Null, "ECS-707: the pack actions come with the caste");
                    Assert.That(pack.RallyHowlActionEntity, Is.Not.Null);
                    Assert.That(pack.MendBeastActionEntity, Is.Not.Null);
                    Assert.That(pack.PackSightActionEntity, Is.Not.Null);
                });

                // ARCH-705: the caste's armour counts for less
                var armor = new CMGetArmorEvent(SlotFlags.All);
                entMan.EventBus.RaiseLocalEvent(beastMaster, ref armor);
                Assert.That(armor.ArmorModifier, Is.EqualTo(pack.ArmorMultiplier).Within(0.001));

                // STATE-702: one more hound ready for the living Beast-Master
                Server.System<BadBloodCampSystem>().UpdateKennel(
                    (kennel, stock, entMan.GetComponent<GhostRoleMobSpawnerComponent>(kennel)),
                    Server.Timing.CurTime);
                Assert.That(stock.Max, Is.EqualTo(3));
            });

            // ECS-707 Last Order: the Beast-Master dies, the pack rages, then answers to the Leader
            await BadBloodTestHelpers.Kill(Pair, beastMaster);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var beast = entMan.GetComponent<BadBloodBeastComponent>(hound);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<K9ProtectorRageComponent>(hound), Is.True, "the pack rages");
                    Assert.That(beast.RebindAt, Is.Not.Null, "and finds a new master when the rage ends");
                    Assert.That(beast.Master, Is.EqualTo(beastMaster), "not before");
                });

                // the test does not wait the full 20 s
                beast.RebindAt = Server.Timing.CurTime;
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.GetComponent<BadBloodBeastComponent>(hound).Master, Is.EqualTo(party.Leader),
                    "with the Beast-Master dead the hound answers to the Hunt Leader again");
                Assert.That(entMan.GetComponent<K9DogComponent>(hound).Master, Is.EqualTo(party.Leader));
            });
        }
        finally
        {
            await Cleanup(party, spawned.Append(hound));
        }
    }

    [Test]
    public async Task PackActionsReachTheBondedBeasts()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var beastMaster = party.Grunts[0];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var beasts = Server.System<BadBloodBeastSystem>();
                var damageable = Server.System<DamageableSystem>();
                Assert.That(Server.System<BadBloodCasteSystem>().TryTakeCaste(beastMaster, BeastMaster), Is.True);
                var pack = new Entity<BadBloodBeastMasterComponent>(beastMaster,
                    entMan.GetComponent<BadBloodBeastMasterComponent>(beastMaster));

                var hound = entMan.SpawnEntity(Hound, map.GridCoords.Offset(new Vector2(0, 1)));
                spawned.Add(hound);
                Assert.That(beasts.Bind(hound, beastMaster), Is.True);
                Assert.That(beasts.IsClanBeast(hound), Is.True);

                var scented = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 3));
                var plain = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 5));
                spawned.Add(scented);
                spawned.Add(plain);

                // Blood Scent: the bonded hound hits the scented prey x1.35
                var scent = new BadBloodBloodScentActionEvent { Performer = beastMaster, Target = scented };
                entMan.EventBus.RaiseLocalEvent(beastMaster, scent);
                Assert.That(scent.Handled, Is.True);
                Assert.That(entMan.GetComponent<BadBloodBloodScentComponent>(scented).Master, Is.EqualTo(beastMaster));

                var bite = new DamageSpecifier();
                bite.DamageDict["Slash"] = 20;
                damageable.TryChangeDamage(scented, bite, origin: hound);
                damageable.TryChangeDamage(plain, bite, origin: hound);
                var scentedTotal = damageable.GetTotalDamage(scented).Float();
                var plainTotal = damageable.GetTotalDamage(plain).Float();
                Assert.That(plainTotal, Is.GreaterThan(0f));
                Assert.That(scentedTotal, Is.EqualTo(plainTotal * pack.Comp.BloodScentMultiplier).Within(0.05f),
                    "the scented prey takes x1.35 from a bonded beast");

                // Rally Howl: the bonded hound nearby runs faster
                var howl = new BadBloodRallyHowlActionEvent { Performer = beastMaster };
                entMan.EventBus.RaiseLocalEvent(beastMaster, howl);
                Assert.That(entMan.GetComponent<BadBloodRallyHowlComponent>(hound).SpeedMultiplier,
                    Is.EqualTo(pack.Comp.RallySpeedMultiplier));

                // Mend Beast: 60 off a hurt beast, then the beast's cooldown
                var wound = new DamageSpecifier();
                wound.DamageDict["Blunt"] = 100;
                damageable.TryChangeDamage(hound, wound, ignoreResistances: true);
                var hurt = damageable.GetTotalDamage(hound).Float();
                Assert.That(beasts.TryMend(pack, hound), Is.True);
                Assert.That(damageable.GetTotalDamage(hound).Float(), Is.EqualTo(hurt - pack.Comp.MendHeal).Within(0.5f));
                Assert.That(beasts.CanMend(pack, hound, out var why), Is.False, "once a minute per beast");
                Assert.That(why, Is.EqualTo("cmu-bad-blood-beast-mend-cooldown"));
                Assert.That(beasts.CanMend(pack, scented, out _), Is.False, "only the clan's beasts");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    private async Task Cleanup(BadBloodTestParty party, IEnumerable<EntityUid> extra)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        var all = extra.ToList();
        await Server.WaitPost(() =>
        {
            foreach (var uid in all.Concat(party.All))
            {
                if (uid.IsValid() && Server.EntMan.EntityExists(uid))
                    Server.EntMan.DeleteEntity(uid);
            }
        });
    }
}
