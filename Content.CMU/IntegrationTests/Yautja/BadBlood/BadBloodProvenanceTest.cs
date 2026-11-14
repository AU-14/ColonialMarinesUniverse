using Content.Server.CMU14.Yautja;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-103: trophies and meat remember the body they came from
[TestFixture]
public sealed class BadBloodProvenanceTest
{
    private const string Cauldron = "CMUYautjaStructureYautjaMachinesVat";

    [Test]
    public async Task SeveredLimbsScalpsHarvestsAndSplitHidesKeepTheirBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var trophies = entMan.System<YautjaTrophySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);

            // a limb off a living body is not from a kill
            var living = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));
            var livingLimb = entMan.GetComponent<BadBloodTrophyOriginComponent>(SeverArm(entMan, living));

            var corpse = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            GiveMind(server, corpse);
            mobState.ChangeMobState(corpse, MobState.Dead);
            var deadLimb = entMan.GetComponent<BadBloodTrophyOriginComponent>(SeverArm(entMan, corpse));

            var scalp = trophies.SpawnRuntimeScalp(corpse, hunter);
            Assert.That(trophies.TryHarvestTrophy(hunter, corpse, YautjaTrophyKind.HumanSkull, out var skull), Is.True);

            var hide = entMan.SpawnEntity("CMUYautjaHumanHide", map.GridCoords);
            entMan.System<BadBloodProvenanceSystem>().StampTrophy(hide, corpse);
            var stacks = entMan.System<SharedStackSystem>();
            stacks.SetCount(hide, 2);
            var split = stacks.Split(hide, 1, map.GridCoords);
            Assert.That(split, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(livingLimb.Body, Is.EqualTo(living));
                Assert.That(livingLimb.WasKill, Is.False);
                Assert.That(livingLimb.WasPlayer, Is.False, "A body that never had a mind is not a player body.");

                Assert.That(deadLimb.Body, Is.EqualTo(corpse));
                Assert.That(deadLimb.WasKill, Is.True);
                Assert.That(deadLimb.WasPlayer, Is.True);
                Assert.That(deadLimb.IsYautja || deadLimb.IsSynth || deadLimb.IsXeno || deadLimb.IsLarva, Is.False);

                Assert.That(BodyOf(entMan, scalp), Is.EqualTo(corpse));
                Assert.That(BodyOf(entMan, skull), Is.EqualTo(corpse));
                Assert.That(BodyOf(entMan, split!.Value), Is.EqualTo(corpse), "A hide split off a stack keeps its body.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ButcheryAndTheCauldronStampWhatTheyMake()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid victim = default;
        EntityUid xeno = default;
        EntityUid limbVictim = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var trophies = entMan.System<YautjaTrophySystem>();

            var butcher = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            mobState.ChangeMobState(victim, MobState.Dead);
            Assert.That(trophies.TryStartButcher(butcher, victim, YautjaButcherProcedure.Skin), Is.True);

            var xenoButcher = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 4)));
            xeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 4)));
            mobState.ChangeMobState(xeno, MobState.Dead);
            Assert.That(trophies.TryStartButcher(xenoButcher, xeno, YautjaButcherProcedure.Skin), Is.True);

            var boilerCoords = map.GridCoords.Offset(new Vector2(0, 8));
            var boiler = entMan.SpawnEntity("CMUMobYautja", boilerCoords);
            var cauldron = entMan.SpawnEntity(Cauldron, map.GridCoords.Offset(new Vector2(1, 8)));
            limbVictim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 10)));
            mobState.ChangeMobState(limbVictim, MobState.Dead);
            var limb = SeverArm(entMan, limbVictim);
            entMan.EnsureComponent<YautjaFlayedComponent>(limb);

            var use = new InteractUsingEvent(boiler, limb, cauldron, entMan.GetComponent<TransformComponent>(cauldron).Coordinates);
            entMan.EventBus.RaiseLocalEvent(cauldron, use);
            Assert.That(use.Handled, Is.True);
        });

        // the whole skin pipeline: 7 + 6.5 + 7 + 9 seconds. the cauldron boils for 15 of them
        await pair.RunTicksSync(pair.SecondsToTicks(7f + 6.5f + 7f + 9f + 2f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var kinds = new Dictionary<EntityUid, List<YautjaTrophyKind>>();
            var xenoFlags = true;
            var trophies = entMan.EntityQueryEnumerator<BadBloodTrophyOriginComponent, YautjaTrophyComponent>();
            while (trophies.MoveNext(out _, out var origin, out var trophy))
            {
                kinds.GetOrNew(origin.Body).Add(trophy.Kind);
                if (origin.Body == xeno)
                    xenoFlags &= origin.IsXeno && origin.WasKill;
            }

            var meat = new Dictionary<EntityUid, int>();
            var cuts = entMan.EntityQueryEnumerator<BadBloodMeatOriginComponent>();
            while (cuts.MoveNext(out _, out var origin))
                meat[origin.Body] = meat.GetValueOrDefault(origin.Body) + 1;

            Assert.Multiple(() =>
            {
                Assert.That(kinds.GetValueOrDefault(victim), Does.Contain(YautjaTrophyKind.HumanHide));
                Assert.That(kinds.GetValueOrDefault(victim)?.Any(kind => kind is YautjaTrophyKind.HumanSpine or YautjaTrophyKind.HumanTorso),
                    Is.True, "The spine or the torso is stamped.");
                Assert.That(meat.GetValueOrDefault(victim), Is.GreaterThanOrEqualTo(2), "Both butcher cuts are stamped.");

                Assert.That(kinds.GetValueOrDefault(xeno), Does.Contain(YautjaTrophyKind.XenoSkull));
                Assert.That(kinds.GetValueOrDefault(xeno), Does.Contain(YautjaTrophyKind.XenoPelt));
                Assert.That(xenoFlags, Is.True, "A butchered xeno's trophies are stamped as a xeno kill.");
                Assert.That(meat.GetValueOrDefault(xeno), Is.GreaterThanOrEqualTo(2));

                Assert.That(kinds.GetValueOrDefault(limbVictim), Does.Contain(YautjaTrophyKind.HumanLeftArmBone),
                    "The bone the cauldron makes keeps the limb's body.");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid BodyOf(IEntityManager entMan, EntityUid item)
    {
        return entMan.TryGetComponent(item, out BadBloodTrophyOriginComponent? origin) ? origin.Body : EntityUid.Invalid;
    }

    // a mind with no player behind it. call on the server thread
    private static void GiveMind(RobustIntegrationTest.ServerIntegrationInstance server, EntityUid body)
    {
        var minds = server.System<SharedMindSystem>();
        minds.TransferTo(minds.CreateMind(null, "prey"), body);
    }

    private static EntityUid SeverArm(IEntityManager entMan, EntityUid body)
    {
        Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(body,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);

        var sever = new BodyPartSeverAttemptEvent(body, arm, BodyPartType.Arm, Surgical: true);
        entMan.EventBus.RaiseLocalEvent(arm, ref sever);
        Assert.That(sever.Succeeded, Is.True);
        Assert.That(sever.DetachedBody, Is.Not.Null);
        return sever.DetachedBody!.Value;
    }
}
