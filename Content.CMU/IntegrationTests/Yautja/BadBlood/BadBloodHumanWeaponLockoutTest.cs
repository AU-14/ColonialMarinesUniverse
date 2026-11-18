using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-302: a Bad Blood's hands refuse human guns and accept Yautja ones. Honourable hunters have no such lockout
// and must stay that way.
[TestFixture]
public sealed class BadBloodHumanWeaponLockoutTest
{
    private const string Hunter = "CMUMobYautja";
    private const string MarineRifle = "RMCWeaponRifleM54C2";
    private const string Flashbang = "RMCGrenadeFlashBang";
    private const string HandFlash = "Flash";

    // CMU14 ECS-603: MP tools (D8)
    private const string StunBaton = "CMStunbaton";
    private const string Handcuffs = "Handcuffs";
    private const string Zipties = "Zipties";
    private const string FragGrenade = "CMGrenadeFrag";
    private const string DemolitionCharge = "CMUBadBloodDemolitionCharge";
    private const string Crowbar = "CMCrowbar";

    private static readonly string[] YautjaGuns =
    [
        "CMUYautjaPlasmaCaster",
        "CMUYautjaSpikeLauncher",
        "CMUBadBloodScavengedSpikeLauncher",
    ];

    [Test]
    public async Task BadBloodsCannotFireHumanGuns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            EntityUid Spawn(string prototype)
            {
                var uid = entMan.SpawnEntity(prototype, map.GridCoords);
                spawned.Add(uid);
                return uid;
            }

            try
            {
                var hunter = Spawn(Hunter);
                var grunt = Spawn(BadBloodTestHelpers.GruntPrototype);
                var leader = Spawn(BadBloodTestHelpers.LeaderPrototype);
                var rifle = Spawn(MarineRifle);

                Assert.Multiple(() =>
                {
                    Assert.That(Refused(entMan, rifle, grunt), Is.True, "a Bad Blood grunt cannot fire a marine rifle");
                    Assert.That(Refused(entMan, rifle, leader), Is.True, "a Bad Blood Leader cannot fire a marine rifle");
                    Assert.That(Refused(entMan, rifle, hunter), Is.False, "honourable hunters are not locked out");
                });

                foreach (var prototype in YautjaGuns)
                {
                    var gun = Spawn(prototype);
                    Assert.Multiple(() =>
                    {
                        Assert.That(Refused(entMan, gun, grunt), Is.False, $"{prototype}: grunt");
                        Assert.That(Refused(entMan, gun, leader), Is.False, $"{prototype}: Leader");
                        Assert.That(Refused(entMan, gun, hunter), Is.False, $"{prototype}: hunter");
                    });
                }
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    // the lockout tells a Yautja gun from a human one by YautjaTechItem, so a Yautja gun shipped without it would
    // be refused by its own clan
    [Test]
    public async Task EveryYautjaGunIsMarkedAsYautjaTech()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();

            Assert.Multiple(() =>
            {
                foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract ||
                        !proto.ID.StartsWith("CMUYautja") && !proto.ID.StartsWith("CMUBadBlood") ||
                        !proto.TryGetComponent<GunComponent>(out _, factory))
                    {
                        continue;
                    }

                    Assert.That(proto.TryGetComponent<YautjaTechItemComponent>(out _, factory), Is.True,
                        $"{proto.ID} is a Yautja gun without YautjaTechItem; Bad Bloods could not fire it");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    // flashes and flashbangs: a Bad Blood cannot use one in hand (fire the flash, arm the flashbang) or hit with a flash
    [Test]
    public async Task BadBloodsCannotUseFlashesOrFlashbangs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blocker = entMan.System<ActionBlockerSystem>();
            var spawned = new List<EntityUid>();

            EntityUid Spawn(string prototype)
            {
                var uid = entMan.SpawnEntity(prototype, map.GridCoords);
                spawned.Add(uid);
                return uid;
            }

            try
            {
                var hunter = Spawn(Hunter);
                var grunt = Spawn(BadBloodTestHelpers.GruntPrototype);
                var flashbang = Spawn(Flashbang);
                var flash = Spawn(HandFlash);

                Assert.Multiple(() =>
                {
                    Assert.That(blocker.CanUseHeldEntity(grunt, flashbang), Is.False, "a Bad Blood cannot arm a flashbang");
                    Assert.That(blocker.CanUseHeldEntity(grunt, flash), Is.False, "a Bad Blood cannot fire a flash");
                    Assert.That(blocker.CanUseHeldEntity(hunter, flashbang), Is.True, "honourable hunters are not locked out");

                    var attack = new AttackAttemptEvent(grunt, null, (flash, entMan.GetComponent<MeleeWeaponComponent>(flash)));
                    entMan.EventBus.RaiseLocalEvent(grunt, attack);
                    Assert.That(attack.Cancelled, Is.True, "a Bad Blood cannot flash someone by hitting them");
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    // CMU14 ECS-603: batons, cuffs, zipties and human grenades are refused like flashes. The clan's own demolition
    // charge and generic human melee (a crowbar) stay allowed (D8), and honourable hunters are not locked out
    [Test]
    public async Task BadBloodsCannotUseMpTools()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blocker = entMan.System<ActionBlockerSystem>();
            var spawned = new List<EntityUid>();

            EntityUid Spawn(string prototype)
            {
                var uid = entMan.SpawnEntity(prototype, map.GridCoords);
                spawned.Add(uid);
                return uid;
            }

            // a light attack with the item, the check the melee system makes before every swing. true means refused
            bool SwingRefused(EntityUid user, EntityUid weapon)
            {
                var attack = new AttackAttemptEvent(user, null, (weapon, entMan.GetComponent<MeleeWeaponComponent>(weapon)));
                entMan.EventBus.RaiseLocalEvent(user, attack);
                return attack.Cancelled;
            }

            try
            {
                var hunter = Spawn(Hunter);
                var grunt = Spawn(BadBloodTestHelpers.GruntPrototype);
                var baton = Spawn(StunBaton);
                var cuffs = Spawn(Handcuffs);
                var zipties = Spawn(Zipties);
                var frag = Spawn(FragGrenade);
                var charge = Spawn(DemolitionCharge);
                var crowbar = Spawn(Crowbar);

                Assert.Multiple(() =>
                {
                    Assert.That(SwingRefused(grunt, baton), Is.True, "a Bad Blood cannot hit with a stun baton");
                    Assert.That(blocker.CanUseHeldEntity(grunt, baton), Is.False, "a Bad Blood cannot switch a stun baton on");
                    Assert.That(blocker.CanUseHeldEntity(grunt, cuffs), Is.False, "a Bad Blood cannot use handcuffs");
                    Assert.That(blocker.CanUseHeldEntity(grunt, zipties), Is.False, "a Bad Blood cannot use zipties");
                    Assert.That(SwingRefused(grunt, cuffs), Is.True, "a Bad Blood cannot cuff someone by hitting them");
                    Assert.That(blocker.CanUseHeldEntity(grunt, frag), Is.False, "a Bad Blood cannot arm a frag grenade");

                    Assert.That(blocker.CanUseHeldEntity(grunt, charge), Is.True, "the Engineer's demolition charge stays usable");
                    Assert.That(blocker.CanUseHeldEntity(grunt, crowbar), Is.True, "a crowbar stays usable");
                    Assert.That(SwingRefused(grunt, crowbar), Is.False, "a Bad Blood can still swing a crowbar");

                    Assert.That(SwingRefused(hunter, baton), Is.False, "honourable hunters are not locked out of batons");
                    Assert.That(blocker.CanUseHeldEntity(hunter, cuffs), Is.True, "honourable hunters are not locked out of cuffs");
                    Assert.That(blocker.CanUseHeldEntity(hunter, frag), Is.True, "honourable hunters are not locked out of grenades");
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    // the user-side prevention question the gun system asks before every shot. true means refused
    private static bool Refused(IEntityManager entMan, EntityUid gun, EntityUid user)
    {
        var ev = new ShotAttemptedEvent { User = user, Used = (gun, entMan.GetComponent<GunComponent>(gun)) };
        entMan.EventBus.RaiseLocalEvent(user, ref ev);
        return ev.Cancelled;
    }
}
