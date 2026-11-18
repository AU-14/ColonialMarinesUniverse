#pragma warning disable RA0002 // tests read component data directly
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Tackle;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 Playtest 5 (docs/GraphAudit-BadBlood-Playtest5Plan.md). COUPLING-701: a clan beast's xeno abilities, xeno
// projectiles and tackle spare Bad Bloods and the clan's other beasts, and still hit marines; an ordinary xeno is not
// filtered at all. COUPLING-704: a clan beast dragging a downed Bad Blood does not choke it, a hostile xeno still does
[TestFixture]
public sealed class BadBloodBeastFriendlyFireTest : GameTest
{
    private const string Hound = "CMUMobYautjaBadBloodHound";
    private const string Thrall = "CMUBadBloodThrallSpitter";
    private const string HostileXeno = "CMXenoRunner";

    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task ClanBeastsSpareTheClanAndHitMarines()
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
                var xenos = Server.System<XenoSystem>();

                var hound = entMan.SpawnEntity(Hound, map.GridCoords.Offset(new Vector2(0, 1)));
                var otherHound = entMan.SpawnEntity(Hound, map.GridCoords.Offset(new Vector2(1, 1)));
                var thrall = entMan.SpawnEntity(Thrall, map.GridCoords.Offset(new Vector2(2, 1)));
                var hostile = entMan.SpawnEntity(HostileXeno, map.GridCoords.Offset(new Vector2(3, 1)));
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 2));
                spawned.AddRange(new[] { hound, otherHound, thrall, hostile, marine });

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<XenoTargetFilterComponent>(hound), Is.True, "every clan beast is filtered");
                    Assert.That(entMan.HasComponent<XenoTargetFilterComponent>(thrall), Is.True);
                    Assert.That(entMan.HasComponent<XenoTargetFilterComponent>(hostile), Is.False, "an ordinary xeno is not");

                    // abilities
                    Assert.That(xenos.CanAbilityAttackTarget(hound, grunt), Is.False, "a hound's ability spares a Bad Blood");
                    Assert.That(xenos.CanAbilityAttackTarget(hound, otherHound), Is.False, "and the clan's other beasts");
                    Assert.That(xenos.CanAbilityAttackTarget(thrall, grunt), Is.False, "a thrall's ability spares a Bad Blood");
                    Assert.That(xenos.CanAbilityAttackTarget(thrall, hound), Is.False);
                    Assert.That(xenos.CanAbilityAttackTarget(hound, marine), Is.True, "a marine is still prey");
                    Assert.That(xenos.CanAbilityAttackTarget(thrall, marine), Is.True);
                    Assert.That(xenos.CanAbilityAttackTarget(hostile, grunt), Is.True, "RMC xenos are unchanged");
                });

                // projectiles: the hive check asks the same filter for the projectile's shooter
                Assert.Multiple(() =>
                {
                    Assert.That(XenoTargetFilter.IsBlocked(entMan, thrall, grunt), Is.True, "a thrall's spit spares a Bad Blood");
                    Assert.That(XenoTargetFilter.IsBlocked(entMan, thrall, marine), Is.False, "and hits a marine");
                    Assert.That(XenoTargetFilter.IsBlocked(entMan, hostile, grunt), Is.False);
                });

                // CMU14 repair H1: a real spit projectile, through the projectile system's own collision check.
                // XenoSpitProjectile deletes on friendly xenos, the path the first hook placement missed
                Assert.Multiple(() =>
                {
                    Assert.That(Collides(entMan, thrall, grunt, map, spawned), Is.False, "a thrall's spit passes a Bad Blood");
                    Assert.That(Collides(entMan, thrall, hound, map, spawned), Is.False, "and the clan's beasts");
                    Assert.That(Collides(entMan, thrall, marine, map, spawned), Is.True, "and hits a marine");
                    Assert.That(Collides(entMan, hostile, grunt, map, spawned), Is.True, "an RMC xeno's spit still hits a Bad Blood");
                });

                // tackle: refused on a Bad Blood, taken on a marine
                var onClan = new CMDisarmEvent(hound);
                entMan.EventBus.RaiseLocalEvent(grunt, ref onClan);
                var onMarine = new CMDisarmEvent(hound);
                entMan.EventBus.RaiseLocalEvent(marine, ref onMarine);
                Assert.Multiple(() =>
                {
                    Assert.That(onClan.Handled, Is.True, "the disarm is swallowed, nothing else acts on it");
                    Assert.That(entMan.HasComponent<TackledRecentlyByComponent>(grunt), Is.False, "a hound never tackles a Bad Blood");
                    Assert.That(entMan.HasComponent<TackledRecentlyByComponent>(marine), Is.True, "a marine is tackled");
                });

                // COUPLING-704: only a hostile xeno chokes a downed Bad Blood it drags
                var whitelist = entMan.GetComponent<DamageOnPulledWhileCritComponent>(grunt).PullerWhitelist;
                var whitelists = Server.System<EntityWhitelistSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(whitelists.IsWhitelistPassOrNull(whitelist, hound), Is.False, "a clan hound drags without choking");
                    Assert.That(whitelists.IsWhitelistPassOrNull(whitelist, thrall), Is.False, "so does a thrall");
                    Assert.That(whitelists.IsWhitelistPassOrNull(whitelist, hostile), Is.True, "a hostile xeno still chokes");
                    Assert.That(whitelists.IsWhitelistPassOrNull(whitelist, marine), Is.False, "a marine never did");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
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

    // fires nothing: builds the collision check a spit projectile from shooter meets on reaching target
    private static bool Collides(IEntityManager entMan, EntityUid shooter, EntityUid target, TestMapData map, List<EntityUid> spawned)
    {
        var spit = entMan.SpawnEntity("XenoSpitProjectile", map.GridCoords.Offset(new Vector2(0, 3)));
        spawned.Add(spit);
        var projectile = entMan.GetComponent<Content.Shared.Projectiles.ProjectileComponent>(spit);
        entMan.System<Content.Shared.Projectiles.SharedProjectileSystem>().SetShooter(spit, projectile, shooter);

        var ourBody = entMan.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(spit);
        var otherBody = entMan.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(target);
        var ourFixture = entMan.GetComponent<Robust.Shared.Physics.FixturesComponent>(spit).Fixtures.First().Value;
        var otherFixture = entMan.GetComponent<Robust.Shared.Physics.FixturesComponent>(target).Fixtures.First().Value;
        var ev = new Robust.Shared.Physics.Events.PreventCollideEvent(spit, target, ourBody, otherBody, ourFixture, otherFixture);
        entMan.EventBus.RaiseLocalEvent(spit, ref ev);
        return !ev.Cancelled;
    }
}
