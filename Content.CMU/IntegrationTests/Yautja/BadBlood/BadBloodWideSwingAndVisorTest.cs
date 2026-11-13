using Content.Shared._RMC14.NightVision;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-401 and ECS-402: Bad Bloods swing Yautja weapons wide (never the chain gauntlet) and their mask visor keeps
// field of view on. Honourable hunters keep neither change.
[TestFixture]
public sealed class BadBloodWideSwingAndVisorTest
{
    private const string Hunter = "CMUMobYautja";
    private const string ChainGauntlet = "CMUYautjaChainGauntlet";
    private const string MarineKnife = "RMCKitchenKnife";

    private static readonly string[] WideWeapons =
    [
        "CMUYautjaWristBlades",
        "CMUYautjaScimitar",
        "CMUYautjaWarGlaive",
        "CMUYautjaWarAxe",
        "CMUYautjaChainwhip",
    ];

    [Test]
    public async Task BadBloodsSwingWideExceptTheChainGauntlet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var melee = server.System<SharedMeleeWeaponSystem>();
            var spawned = new List<EntityUid>();

            EntityUid Spawn(string prototype)
            {
                var uid = entMan.SpawnEntity(prototype, map.GridCoords);
                spawned.Add(uid);
                return uid;
            }

            bool Allowed(EntityUid user, EntityUid weapon)
            {
                return melee.CMUWideAttackAllowed(user, weapon, entMan.GetComponent<MeleeWeaponComponent>(weapon));
            }

            try
            {
                var hunter = Spawn(Hunter);
                var grunt = Spawn(BadBloodTestHelpers.GruntPrototype);
                var leader = Spawn(BadBloodTestHelpers.LeaderPrototype);

                foreach (var prototype in WideWeapons)
                {
                    var weapon = Spawn(prototype);
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.GetComponent<MeleeWeaponComponent>(weapon).WideAttackAllowed, Is.False,
                            $"{prototype}: the prototype itself still forbids it");
                        Assert.That(Allowed(grunt, weapon), Is.True, $"{prototype}: grunt");
                        Assert.That(Allowed(leader, weapon), Is.True, $"{prototype}: Leader");
                        Assert.That(Allowed(hunter, weapon), Is.False, $"{prototype}: honourable hunter");
                    });
                }

                var gauntlet = Spawn(ChainGauntlet);
                Assert.Multiple(() =>
                {
                    Assert.That(Allowed(grunt, gauntlet), Is.False, "the chain gauntlet never swings wide");
                    Assert.That(Allowed(leader, gauntlet), Is.False);
                    Assert.That(Allowed(hunter, gauntlet), Is.False);
                });

                // the override is about Yautja weapons only: a human weapon keeps whatever its prototype says
                var knife = Spawn(MarineKnife);
                var knifeAllows = entMan.GetComponent<MeleeWeaponComponent>(knife).WideAttackAllowed;
                Assert.That(Allowed(grunt, knife), Is.EqualTo(knifeAllows));
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

    [Test]
    public async Task BadBloodVisorKeepsFieldOfView()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();

            var mask = new YautjaMaskComponent();
            var honourable = protoMan.Index<EntityPrototype>(mask.VisorGlassesPrototype);
            var badBlood = protoMan.Index<EntityPrototype>(mask.BadBloodVisorGlassesPrototype);

            Assert.That(honourable.TryGetComponent<NightVisionItemComponent>(out var honourableVision, factory));
            Assert.That(badBlood.TryGetComponent<NightVisionItemComponent>(out var badBloodVision, factory));

            Assert.Multiple(() =>
            {
                Assert.That(badBlood.ID, Is.Not.EqualTo(honourable.ID));
                Assert.That(badBloodVision!.Overlay, Is.True, "mobs are still drawn through walls");
                Assert.That(badBloodVision.ExperimentalMesonFov, Is.False, "walls hide the map behind them");
                Assert.That(badBloodVision.DefaultState, Is.EqualTo(honourableVision!.DefaultState));
                Assert.That(honourableVision.ExperimentalMesonFov, Is.True, "honourable hunters keep wall vision");
                Assert.That(badBlood.TryGetComponent<YautjaMaskVisorGlassesComponent>(out _, factory), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
