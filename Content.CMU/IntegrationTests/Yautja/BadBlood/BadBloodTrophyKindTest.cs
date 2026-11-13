using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-109: the scalp, hide, spine and torso are trophies of their own kind, and every hard kind polishes
[TestFixture]
public sealed class BadBloodTrophyKindTest
{
    private const string Rag = "CMUYautjaPolishingRag";

    private static readonly (string Prototype, YautjaTrophyKind Kind)[] HardKinds =
    [
        ("CMUYautjaHumanSkullTrophy", YautjaTrophyKind.HumanSkull),
        ("CMUYautjaHumanLeftArmBoneTrophy", YautjaTrophyKind.HumanLeftArmBone),
        ("CMUYautjaHumanRightArmBoneTrophy", YautjaTrophyKind.HumanRightArmBone),
        ("CMUYautjaHumanLeftHandBoneTrophy", YautjaTrophyKind.HumanLeftHandBone),
        ("CMUYautjaHumanRightHandBoneTrophy", YautjaTrophyKind.HumanRightHandBone),
        ("CMUYautjaHumanLeftLegBoneTrophy", YautjaTrophyKind.HumanLeftLegBone),
        ("CMUYautjaHumanRightLegBoneTrophy", YautjaTrophyKind.HumanRightLegBone),
        ("CMUYautjaHumanLeftFootBoneTrophy", YautjaTrophyKind.HumanLeftFootBone),
        ("CMUYautjaHumanRightFootBoneTrophy", YautjaTrophyKind.HumanRightFootBone),
        ("CMUYautjaHumanRibcageTrophy", YautjaTrophyKind.HumanRibcage),
        ("CMUYautjaXenoSkullTrophy", YautjaTrophyKind.XenoSkull),
        ("CMUYautjaHumanSpine", YautjaTrophyKind.HumanSpine),
        ("CMUYautjaHumanTorso", YautjaTrophyKind.HumanTorso),
    ];

    [Test]
    public async Task SoftAndButcheredPiecesAreTrophiesOfTheirOwnKind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            YautjaTrophyKind? KindOf(string prototype)
            {
                var uid = entMan.SpawnEntity(prototype, map.GridCoords);
                return entMan.TryGetComponent(uid, out YautjaTrophyComponent? trophy) ? trophy.Kind : null;
            }

            Assert.Multiple(() =>
            {
                Assert.That(KindOf("CMUYautjaScalp"), Is.EqualTo(YautjaTrophyKind.HumanScalp), "The scalp is not a skull.");
                Assert.That(KindOf("CMUYautjaHumanHide"), Is.EqualTo(YautjaTrophyKind.HumanHide));
                Assert.That(KindOf("CMUYautjaHumanSpine"), Is.EqualTo(YautjaTrophyKind.HumanSpine));
                Assert.That(KindOf("CMUYautjaHumanTorso"), Is.EqualTo(YautjaTrophyKind.HumanTorso));
            });

            var spine = entMan.SpawnEntity("CMUYautjaHumanSpine", map.GridCoords);
            Assert.That(entMan.GetComponent<MetaDataComponent>(spine).EntityName, Is.EqualTo("spine"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryHardKindPolishes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var trophies = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            for (var i = 0; i < HardKinds.Length; i++)
            {
                // one polisher each, a do-after belongs to its user
                var coords = map.GridCoords.Offset(new Vector2(i, 0));
                var user = entMan.SpawnEntity("CMMobHuman", coords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);
                var rag = entMan.SpawnEntity(Rag, coords);
                var trophy = entMan.SpawnEntity(HardKinds[i].Prototype, coords);
                trophies.Add(trophy);

                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(trophy).Kind, Is.EqualTo(HardKinds[i].Kind));

                var use = new InteractUsingEvent(user, rag, trophy, coords);
                entMan.EventBus.RaiseLocalEvent(trophy, use);
                Assert.That(use.Handled, Is.True);
            }
        });

        await pair.RunTicksSync(pair.SecondsToTicks(5.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                for (var i = 0; i < HardKinds.Length; i++)
                {
                    Assert.That(entMan.GetComponent<YautjaTrophyComponent>(trophies[i]).Polished, Is.True,
                        $"{HardKinds[i].Kind} must polish.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
