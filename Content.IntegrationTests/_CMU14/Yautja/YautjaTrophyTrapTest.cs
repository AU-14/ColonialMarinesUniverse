using System.Linq;
using Content.Server._CMU14.Yautja;
using Content.Server.Verbs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Speech;
using Content.Shared.Storage;
using Content.Shared.StepTrigger.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaTrophyTrapTest
{
    [Test]
    public async Task YautjaCanHarvestHumanAndXenoTrophiesOnceFromDeadPrey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var trophySystem = entMan.System<YautjaTrophySystem>();
            var unrevivable = entMan.System<RMCUnrevivableSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var belt = entMan.SpawnEntity("CMUYautjaTrophyBelt", MapCoordinates.Nullspace);
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var xeno = entMan.SpawnEntity("CMXenoRunner", MapCoordinates.Nullspace);
            var rag = entMan.SpawnEntity("CMUYautjaPolishingRag", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, belt, "belt", silent: true, force: true), Is.True);
                Assert.That(entMan.HasComponent<XenoComponent>(xeno), Is.True);
                Assert.That(entMan.HasComponent<YautjaTrophySourceComponent>(human), Is.True);
                Assert.That(entMan.HasComponent<YautjaTrophySourceComponent>(xeno), Is.True);

                Assert.That(trophySystem.TryHarvestTrophy(hunter, human, YautjaTrophyKind.HumanSkull, out _), Is.False);

                mobState.ChangeMobState(human, MobState.Dead);
                mobState.ChangeMobState(xeno, MobState.Dead);
                Assert.That(unrevivable.IsUnrevivable(human), Is.False);
                Assert.That(unrevivable.IsUnrevivable(xeno), Is.False);

                Assert.That(trophySystem.TryHarvestTrophy(hunter, human, YautjaTrophyKind.HumanSkull, out var humanSkull), Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(humanSkull).Kind, Is.EqualTo(YautjaTrophyKind.HumanSkull));
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(humanSkull).Source, Is.EqualTo(human));
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(humanSkull).Hunter, Is.EqualTo(hunter));
                Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(human).TakenTrophies, Does.Contain(YautjaTrophyKind.HumanSkull));
                Assert.That(entMan.GetComponent<StorageComponent>(belt).Container.Contains(humanSkull), Is.True);
                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);
                Assert.That(record.HumanSkulls, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(2));
                Assert.That(record.RankName, Is.EqualTo("cmu-yautja-rank-hunter"));
                Assert.That(unrevivable.IsUnrevivable(human), Is.True);
                Assert.That(trophySystem.TryHarvestTrophy(hunter, human, YautjaTrophyKind.HumanSkull, out _), Is.False);

                Assert.That(trophySystem.TryHarvestTrophy(hunter, human, YautjaTrophyKind.HumanLeftArmBone, out var armBone), Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(armBone).Kind, Is.EqualTo(YautjaTrophyKind.HumanLeftArmBone));
                Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(human).TakenTrophies, Does.Contain(YautjaTrophyKind.HumanLeftArmBone));
                Assert.That(record.HumanBones, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(3));

                var polish = new InteractUsingEvent(hunter, rag, humanSkull, entMan.GetComponent<TransformComponent>(humanSkull).Coordinates);
                entMan.EventBus.RaiseLocalEvent(humanSkull, polish);
                Assert.That(polish.Handled, Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(humanSkull).Polished, Is.True);
                Assert.That(record.PolishedTrophies, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(4));
                Assert.That(entMan.GetComponent<MetaDataComponent>(humanSkull).EntityName, Does.StartWith("polished "));

                Assert.That(trophySystem.TryHarvestTrophy(hunter, xeno, YautjaTrophyKind.HumanSkull, out _), Is.False);
                Assert.That(trophySystem.TryHarvestTrophy(hunter, xeno, YautjaTrophyKind.XenoSkull, out var xenoSkull), Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(xenoSkull).Kind, Is.EqualTo(YautjaTrophyKind.XenoSkull));
                Assert.That(entMan.GetComponent<MetaDataComponent>(xenoSkull).EntityName, Is.EqualTo("Runner skull trophy"));
                Assert.That(unrevivable.IsUnrevivable(xeno), Is.True);
                Assert.That(entMan.GetComponent<UnrevivableComponent>(xeno).Analyzable, Is.False);
                Assert.That(trophySystem.TryHarvestTrophy(hunter, xeno, YautjaTrophyKind.XenoPelt, out var xenoPelt), Is.True);
                Assert.That(entMan.GetComponent<YautjaTrophyComponent>(xenoPelt).Kind, Is.EqualTo(YautjaTrophyKind.XenoPelt));
                Assert.That(entMan.GetComponent<MetaDataComponent>(xenoPelt).EntityName, Is.EqualTo("Runner pelt trophy"));
                Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(xeno).TakenTrophies, Does.Contain(YautjaTrophyKind.XenoSkull));
                Assert.That(entMan.GetComponent<YautjaTrophySourceComponent>(xeno).TakenTrophies, Does.Contain(YautjaTrophyKind.XenoPelt));
                Assert.That(entMan.GetComponent<StorageComponent>(belt).Container.Contains(xenoSkull), Is.True);
                Assert.That(entMan.GetComponent<StorageComponent>(belt).Container.Contains(xenoPelt), Is.True);
                Assert.That(record.XenoSkulls, Is.EqualTo(1));
                Assert.That(record.XenoPelts, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(11));
                Assert.That(record.RankName, Is.EqualTo("cmu-yautja-rank-blooded"));
            }
            finally
            {
                foreach (var uid in new[] { hunter, belt, human, xeno, rag })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SkullAndRibcageTrophiesMakeTargetsUnrevivable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var trophySystem = entMan.System<YautjaTrophySystem>();
            var unrevivable = entMan.System<RMCUnrevivableSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var skullHuman = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ribcageHuman = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var xeno = entMan.SpawnEntity("CMXenoRunner", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                mobState.ChangeMobState(skullHuman, MobState.Dead);
                mobState.ChangeMobState(ribcageHuman, MobState.Dead);
                mobState.ChangeMobState(xeno, MobState.Dead);

                Assert.That(unrevivable.IsUnrevivable(skullHuman), Is.False);
                Assert.That(unrevivable.IsUnrevivable(ribcageHuman), Is.False);
                Assert.That(unrevivable.IsUnrevivable(xeno), Is.False);

                Assert.That(trophySystem.TryHarvestTrophy(hunter, skullHuman, YautjaTrophyKind.HumanSkull, out _), Is.True);
                Assert.That(trophySystem.TryHarvestTrophy(hunter, ribcageHuman, YautjaTrophyKind.HumanRibcage, out _), Is.True);
                Assert.That(trophySystem.TryHarvestTrophy(hunter, xeno, YautjaTrophyKind.XenoSkull, out _), Is.True);

                Assert.That(unrevivable.IsUnrevivable(skullHuman), Is.True);
                Assert.That(unrevivable.IsUnrevivable(ribcageHuman), Is.True);
                Assert.That(unrevivable.IsUnrevivable(xeno), Is.True);
                Assert.That(entMan.GetComponent<UnrevivableComponent>(skullHuman).Analyzable, Is.False);
                Assert.That(entMan.GetComponent<UnrevivableComponent>(ribcageHuman).Analyzable, Is.False);
                Assert.That(entMan.GetComponent<UnrevivableComponent>(xeno).Analyzable, Is.False);
            }
            finally
            {
                foreach (var uid in new[] { hunter, skullHuman, ribcageHuman, xeno })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerFullySeparatesXenoFromHiveAndRestoresOnRelease()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hiveSystem = entMan.System<SharedXenoHiveSystem>();
            var mobState = entMan.System<MobStateSystem>();
            var thralls = entMan.System<YautjaThrallSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var hive = entMan.SpawnEntity("CMXenoHive", MapCoordinates.Nullspace);
            var xeno = entMan.SpawnEntity("CMXenoRunner", MapCoordinates.Nullspace);
            var hivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", MapCoordinates.Nullspace);

            try
            {
                hiveSystem.SetHive(xeno, hive);
                Assert.That(hiveSystem.GetHive(xeno)?.Owner, Is.EqualTo(hive));
                Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(xeno), Is.False);
                Assert.That(entMan.GetComponent<XenoRegenComponent>(xeno).HealOffWeeds, Is.False);
                var originalName = entMan.GetComponent<MetaDataComponent>(xeno).EntityName;

                var originalSpeech = entMan.GetComponent<SpeechComponent>(xeno);
                Assert.That(originalSpeech.SpeechVerb.ToString(), Is.EqualTo("Xeno"));
                Assert.That(originalSpeech.SpeechSounds?.ToString(), Is.EqualTo("Xenonid"));

                mobState.ChangeMobState(xeno, MobState.Critical);
                Assert.That(mobState.IsCritical(xeno), Is.True);

                var hivebreakerComp = entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker);
                thralls.HivebreakXeno(hunter, xeno, hivebreaker, hivebreakerComp);

                var thrall = entMan.GetComponent<YautjaThrallComponent>(xeno);
                Assert.That(thrall.Master, Is.EqualTo(hunter));
                Assert.That(thrall.Hivebroken, Is.True);
                Assert.That(thrall.Blooded, Is.True);
                Assert.That(thrall.TechAuthorized, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(xeno), Is.True);
                Assert.That(mobState.IsCritical(xeno), Is.False);
                Assert.That(hiveSystem.GetHive(xeno), Is.Null);
                Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(xeno), Is.True);
                Assert.That(entMan.GetComponent<XenoRegenComponent>(xeno).HealOffWeeds, Is.True);
                Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(xeno), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(xeno).EntityName, Does.StartWith("hivebroken "));

                var hivebrokenSpeech = entMan.GetComponent<SpeechComponent>(xeno);
                Assert.That(hivebrokenSpeech.SpeechVerb.ToString(), Is.EqualTo("Default"));
                Assert.That(hivebrokenSpeech.SpeechSounds?.ToString(), Is.EqualTo("Bass"));

                var npcFactions = entMan.GetComponent<NpcFactionMemberComponent>(xeno).Factions.Select(faction => faction.ToString()).ToArray();
                Assert.That(npcFactions, Does.Contain("CMUYautja"));
                Assert.That(npcFactions, Does.Not.Contain("RMCXeno"));

                var iffFactions = entMan.GetComponent<UserIFFComponent>(xeno).Factions.Select(faction => faction.ToString()).ToArray();
                Assert.That(iffFactions, Does.Contain("FactionYautja"));
                Assert.That(iffFactions, Does.Not.Contain("FactionXeno"));

                entMan.RemoveComponent<YautjaThrallComponent>(xeno);

                Assert.That(hiveSystem.GetHive(xeno)?.Owner, Is.EqualTo(hive));
                Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(xeno), Is.False);
                Assert.That(entMan.GetComponent<XenoRegenComponent>(xeno).HealOffWeeds, Is.False);
                Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(xeno), Is.False);
                Assert.That(entMan.GetComponent<MetaDataComponent>(xeno).EntityName, Is.EqualTo(originalName));

                var restoredSpeech = entMan.GetComponent<SpeechComponent>(xeno);
                Assert.That(restoredSpeech.SpeechVerb.ToString(), Is.EqualTo("Xeno"));
                Assert.That(restoredSpeech.SpeechSounds?.ToString(), Is.EqualTo("Xenonid"));

                npcFactions = entMan.GetComponent<NpcFactionMemberComponent>(xeno).Factions.Select(faction => faction.ToString()).ToArray();
                Assert.That(npcFactions, Does.Contain("RMCXeno"));
                Assert.That(npcFactions, Does.Not.Contain("CMUYautja"));

                iffFactions = entMan.GetComponent<UserIFFComponent>(xeno).Factions.Select(faction => faction.ToString()).ToArray();
                Assert.That(iffFactions, Does.Contain("FactionXeno"));
                Assert.That(iffFactions, Does.Not.Contain("FactionYautja"));
            }
            finally
            {
                foreach (var uid in new[] { hunter, hive, xeno, hivebreaker })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaCanPickupAndUseHivebreaker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid xeno = default;
        EntityUid hivebreaker = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();

                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                xeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));
                hivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);

                Assert.That(entMan.HasComponent<YautjaComponent>(human), Is.False);
                Assert.That(hands.TryPickupAnyHand(human, hivebreaker), Is.True);

                mobState.ChangeMobState(xeno, MobState.Critical);
                Assert.That(mobState.IsCritical(xeno), Is.True);

                entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker).DoAfter = TimeSpan.Zero;
                var xenoCoords = entMan.GetComponent<TransformComponent>(xeno).Coordinates;
                var ev = new AfterInteractEvent(human, hivebreaker, xeno, xenoCoords, true);
                entMan.EventBus.RaiseLocalEvent(hivebreaker, ev);

                Assert.That(ev.Handled, Is.True);
            });

            await server.WaitRunTicks(3);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.That(entMan.TryGetComponent(xeno, out YautjaThrallComponent? thrall), Is.True);
                Assert.That(thrall!.Master, Is.EqualTo(human));
                Assert.That(thrall.Hivebroken, Is.True);
                Assert.That(thrall.Blooded, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(xeno), Is.True);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { human, xeno, hivebreaker })
                {
                    if (uid.IsValid() && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TrophyVerbsAppearOnDeadHumanAndXenoBodies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var verbs = entMan.System<VerbSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));
            var xeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new System.Numerics.Vector2(2f, 0f)));

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<YautjaTrophySourceComponent>(human), Is.True);
                Assert.That(entMan.HasComponent<YautjaTrophySourceComponent>(xeno), Is.True);

                mobState.ChangeMobState(human, MobState.Dead);
                mobState.ChangeMobState(xeno, MobState.Dead);

                var humanVerbs = verbs.GetLocalVerbs(human, hunter, typeof(AlternativeVerb));
                Assert.That(humanVerbs, Has.Some.Matches<Verb>(verb => verb.Text == "Take skull trophy"));
                Assert.That(humanVerbs, Has.Some.Matches<Verb>(verb => verb.Text == "Take left arm bone"));
                Assert.That(humanVerbs, Has.Some.Matches<Verb>(verb => verb.Text == "Take ribcage"));

                var xenoVerbs = verbs.GetLocalVerbs(xeno, hunter, typeof(AlternativeVerb));
                Assert.That(xenoVerbs, Has.Some.Matches<Verb>(verb => verb.Text == "Take xeno skull trophy"));
                Assert.That(xenoVerbs, Has.Some.Matches<Verb>(verb => verb.Text == "Take xeno pelt trophy"));
                Assert.That(xenoVerbs.Count(verb => verb.Text == "Take xeno skull trophy"), Is.EqualTo(1));
                Assert.That(xenoVerbs.Count(verb => verb.Text == "Take xeno pelt trophy"), Is.EqualTo(1));
            }
            finally
            {
                foreach (var uid in new[] { hunter, human, xeno })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RitualCaptiveCanBeDuelledForTrophyProgression()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var pulling = entMan.System<PullingSystem>();
            var verbs = entMan.System<VerbSystem>();
            var ritual = entMan.System<YautjaRitualSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var captiveVerbs = verbs.GetLocalVerbs(prey, hunter, typeof(AlternativeVerb));
                Assert.That(captiveVerbs.Count(verb => verb.Text == "Claim ritual captive"), Is.EqualTo(1));
                Assert.That(ritual.TryClaimCaptive(hunter, prey), Is.False);

                Assert.That(pulling.TryStartPull(hunter, prey), Is.True);
                Assert.That(ritual.TryClaimCaptive(hunter, prey), Is.True);

                var ritualComp = entMan.GetComponent<YautjaRitualDuelComponent>(prey);
                Assert.That(ritualComp.Hunter, Is.EqualTo(hunter));
                Assert.That(ritualComp.State, Is.EqualTo(YautjaRitualState.Captive));

                Assert.That(ritual.TryBeginDuel(hunter, prey), Is.True);
                Assert.That(ritualComp.State, Is.EqualTo(YautjaRitualState.DuelActive));
                Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.Null);

                mobState.ChangeMobState(prey, MobState.Dead);

                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);
                Assert.That(record.RitualDuelWins, Is.EqualTo(1));
                Assert.That(record.Score, Is.EqualTo(5));
                Assert.That(record.RankName, Is.EqualTo("cmu-yautja-rank-blooded"));
            }
            finally
            {
                foreach (var uid in new[] { hunter, prey })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCanPullLivingXenosButMarinesStillCannot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pulling = entMan.System<PullingSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));
            var runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new System.Numerics.Vector2(2f, 0f)));
            var queen = entMan.SpawnEntity("CMXenoQueen", map.GridCoords.Offset(new System.Numerics.Vector2(3f, 0f)));

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<ParalyzeOnPullAttemptImmuneComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<InfectOnPullAttemptImmuneComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<XenoComponent>(runner), Is.True);
                Assert.That(entMan.HasComponent<XenoComponent>(queen), Is.True);

                Assert.That(pulling.TryStartPull(marine, runner), Is.False);

                Assert.That(pulling.TryStartPull(hunter, runner), Is.True);
                Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.EqualTo(runner));

                var runnerPullable = entMan.GetComponent<PullableComponent>(runner);
                Assert.That(pulling.TryStopPull(runner, runnerPullable, hunter), Is.True);

                Assert.That(pulling.TryStartPull(hunter, queen), Is.True);
                Assert.That(entMan.GetComponent<PullerComponent>(hunter).Pulling, Is.EqualTo(queen));
            }
            finally
            {
                foreach (var uid in new[] { hunter, marine, runner, queen })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCanRecoverArmedHuntingTrap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(hands.TryPickupAnyHand(hunter, trap), Is.True);

                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                Assert.That(trapSystem.TryArmTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapComp.Armed, Is.True);
                Assert.That(entMan.GetComponent<StepTriggerComponent>(trap).Active, Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, trap), Is.False);

                Assert.That(trapSystem.TryRecoverTrap((trap, trapComp), hunter), Is.True);
                Assert.That(trapComp.Armed, Is.False);
                Assert.That(entMan.GetComponent<StepTriggerComponent>(trap).Active, Is.False);
                Assert.That(hands.IsHolding(hunter, trap), Is.True);
            }
            finally
            {
                foreach (var uid in new[] { hunter, trap })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingTrapArmsForYautjaAndTriggersOnLivingPrey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid trap = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var trapSystem = entMan.System<YautjaTrapSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));
            trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(entMan.HasComponent<StepTriggerComponent>(trap), Is.True);
                Assert.That(entMan.GetComponent<StepTriggerComponent>(trap).Active, Is.False);
                Assert.That(hands.TryPickupAnyHand(hunter, trap), Is.True);

                Assert.That(trapSystem.TryArmTrap((trap, entMan.GetComponent<YautjaTrapComponent>(trap)), hunter), Is.True);
                Assert.That(hands.IsHolding(hunter, trap), Is.False);
                Assert.That(entMan.GetComponent<YautjaTrapComponent>(trap).Armed, Is.True);
                Assert.That(entMan.GetComponent<YautjaTrapComponent>(trap).TrapOwner, Is.EqualTo(hunter));
                Assert.That(entMan.GetComponent<StepTriggerComponent>(trap).Active, Is.True);

                Assert.That(trapSystem.TryTriggerTrap((trap, entMan.GetComponent<YautjaTrapComponent>(trap)), hunter), Is.False);

                var damageable = entMan.GetComponent<DamageableComponent>(prey);
                var before = damageable.TotalDamage;

                Assert.That(trapSystem.TryTriggerTrap((trap, entMan.GetComponent<YautjaTrapComponent>(trap)), prey), Is.True);
                Assert.That(damageable.TotalDamage, Is.GreaterThan(before));
                Assert.That(entMan.GetComponent<YautjaTrapComponent>(trap).Armed, Is.False);
                Assert.That(entMan.GetComponent<YautjaRitualDuelComponent>(prey).Hunter, Is.EqualTo(hunter));
                Assert.That(entMan.GetComponent<YautjaRitualDuelComponent>(prey).State, Is.EqualTo(YautjaRitualState.Captive));
            }
            finally
            {
                foreach (var uid in new[] { hunter, prey })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            if (trap.IsValid())
                Assert.That(server.EntMan.Deleted(trap), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructCanBeArmedCancelledAndDetonated()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid armor = default;
        EntityUid mask = default;
        EntityUid smartDisc = default;
        EntityUid healingGun = default;
        EntityUid action = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            armor = entMan.SpawnEntity("CMUYautjaClanArmor", map.GridCoords);
            mask = entMan.SpawnEntity("CMUYautjaMask", map.GridCoords);
            smartDisc = entMan.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords);
            healingGun = entMan.SpawnEntity("CMUYautjaHealingGun", map.GridCoords);
            action = entMan.SpawnEntity("CMUActionYautjaSelfDestruct", map.GridCoords);

            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(hunter, armor, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, smartDisc), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, healingGun), Is.True);

            var actionComp = entMan.GetComponent<ActionComponent>(action);
            Assert.That(actionComp.CheckCanInteract, Is.False);
            Assert.That(actionComp.CheckConsciousness, Is.False);

            mobState.ChangeMobState(hunter, MobState.Critical);

            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
            Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter), Is.True);
            Assert.That(bracerComp.SelfDestructArmed, Is.True);
            Assert.That(selfDestruct.TryCancelSelfDestruct((bracer, bracerComp), hunter), Is.True);
            Assert.That(bracerComp.SelfDestructArmed, Is.False);

            Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter, TimeSpan.Zero), Is.True);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Deleted(hunter), Is.True);
            Assert.That(entMan.Deleted(bracer), Is.True);
            Assert.That(entMan.Deleted(armor), Is.True);
            Assert.That(entMan.Deleted(mask), Is.True);
            Assert.That(entMan.Deleted(smartDisc), Is.True);
            Assert.That(entMan.Deleted(healingGun), Is.True);

            if (action.IsValid() && !entMan.Deleted(action))
                entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }
}
