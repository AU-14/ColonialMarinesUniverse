using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-108: Bad Bloods cure the soft kinds with the cleanser gel, one piece at a time.
// STATE-104: preparing a trophy pays a hunter honour, and pays a Bad Blood nothing.
[TestFixture]
public sealed class BadBloodCuringTest
{
    private const string Gel = "CMUYautjaCleanserGelVial";
    private const string Rag = "CMUYautjaPolishingRag";
    private const string Hide = "CMUYautjaHumanHide";
    private const string Bone = "CMUYautjaHumanLeftArmBoneTrophy";

    [Test]
    public async Task ASingleHideCuresAndAStackIsRefused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid single = default;
        EntityUid stack = default;
        EntityUid hunterHide = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stacks = entMan.System<SharedStackSystem>();

            var curer = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            single = entMan.SpawnEntity(Hide, map.GridCoords);
            Assert.That(Use(entMan, curer, entMan.SpawnEntity(Gel, map.GridCoords), single), Is.True);

            var stackCoords = map.GridCoords.Offset(new Vector2(2, 0));
            var stackCurer = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, stackCoords);
            stack = entMan.SpawnEntity(Hide, stackCoords);
            stacks.SetCount(stack, 2);
            Assert.That(Use(entMan, stackCurer, entMan.SpawnEntity(Gel, stackCoords), stack), Is.True,
                "A refused stack is still answered, so the gel does not go on to dissolve it.");

            // an honourable hunter does not cure. the gel is left to do what it always did
            var hunterCoords = map.GridCoords.Offset(new Vector2(4, 0));
            var hunter = entMan.SpawnEntity("CMUMobYautja", hunterCoords);
            hunterHide = entMan.SpawnEntity(Hide, hunterCoords);
            Assert.That(Use(entMan, hunter, entMan.SpawnEntity(Gel, hunterCoords), hunterHide), Is.False);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(4f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(single).Polished, Is.True,
                    "Curing sets the same flag polishing does.");
                Assert.That(entMan.GetComponent<MetaDataComponent>(single).EntityName, Does.StartWith("cured "));
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(stack).Polished, Is.False,
                    "A stack of 2 must be split before it can be cured.");
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(hunterHide).Polished, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparingATrophyPaysAHunterAndNotABadBlood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid badBlood = default;
        EntityUid hunter = default;
        EntityUid badBloodBone = default;
        EntityUid hunterBone = default;
        EntityUid badBloodHide = default;
        EntityUid curer = default;
        var badBloodScore = 0;
        var curerScore = 0;
        var hunterScore = 0;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            badBloodBone = entMan.SpawnEntity(Bone, map.GridCoords);
            Assert.That(Use(entMan, badBlood, entMan.SpawnEntity(Rag, map.GridCoords), badBloodBone), Is.True);

            var curerCoords = map.GridCoords.Offset(new Vector2(-2, 0));
            curer = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, curerCoords);
            badBloodHide = entMan.SpawnEntity(Hide, curerCoords);
            Assert.That(Use(entMan, curer, entMan.SpawnEntity(Gel, curerCoords), badBloodHide), Is.True);

            var hunterCoords = map.GridCoords.Offset(new Vector2(3, 0));
            hunter = entMan.SpawnEntity("CMUMobYautja", hunterCoords);
            hunterBone = entMan.SpawnEntity(Bone, hunterCoords);
            Assert.That(Use(entMan, hunter, entMan.SpawnEntity(Rag, hunterCoords), hunterBone), Is.True);

            badBloodScore = Score(entMan, badBlood);
            curerScore = Score(entMan, curer);
            hunterScore = Score(entMan, hunter);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(5.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(badBloodBone).Polished, Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(badBloodHide).Polished, Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(hunterBone).Polished, Is.True);
                Assert.That(Score(entMan, badBlood), Is.EqualTo(badBloodScore),
                    "A Bad Blood polishing must not earn honour score.");
                Assert.That(Score(entMan, curer), Is.EqualTo(curerScore),
                    "A Bad Blood curing must not earn honour score.");
                Assert.That(Score(entMan, hunter), Is.EqualTo(hunterScore + 1),
                    "An honourable hunter still earns a point for polishing.");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static int Score(IEntityManager entMan, EntityUid uid)
    {
        return entMan.TryGetComponent(uid, out YautjaTrophyRecordComponent? record) ? record.Score : 0;
    }

    private static bool Use(IEntityManager entMan, EntityUid user, EntityUid used, EntityUid target)
    {
        var use = new InteractUsingEvent(user, used, target, entMan.GetComponent<TransformComponent>(target).Coordinates);
        entMan.EventBus.RaiseLocalEvent(target, use);
        return use.Handled;
    }
}
