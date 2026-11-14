using Content.Shared.CMU14.Yautja;
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

    // the user-side prevention question the gun system asks before every shot. true means refused
    private static bool Refused(IEntityManager entMan, EntityUid gun, EntityUid user)
    {
        var ev = new ShotAttemptedEvent { User = user, Used = (gun, entMan.GetComponent<GunComponent>(gun)) };
        entMan.EventBus.RaiseLocalEvent(user, ref ev);
        return ev.Cancelled;
    }
}
