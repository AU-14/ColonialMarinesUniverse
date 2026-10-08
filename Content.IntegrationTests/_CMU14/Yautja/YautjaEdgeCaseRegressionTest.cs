using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

// edge cases from the yautja bug pass, stuff butchery/rituals can produce that the happy-path tests never hit
[TestFixture]
public sealed class YautjaEdgeCaseRegressionTest
{
    [Test]
    public async Task RitualDuelIsNotCreditedWhenSomeoneElseKillsThePrey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid marine = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rituals = entMan.System<YautjaRitualSystem>();

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            entMan.EnsureComponent<YautjaComponent>(hunter);

            Assert.That(rituals.TryClaimCaptive(hunter, target, bypassControlRequirement: true), Is.True);
            Assert.That(rituals.TryBeginDuel(hunter, target), Is.True);
            entMan.System<MobStateSystem>().ChangeMobState(target, MobState.Dead, origin: marine);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var wins = entMan.TryGetComponent(hunter, out YautjaTrophyRecordComponent? record) ? record.RitualDuelWins : 0;
            Assert.Multiple(() =>
            {
                Assert.That(wins, Is.Zero, "A marine finishing the duelled prey is not the hunter's duel win.");
                Assert.That(entMan.HasComponent<YautjaRitualDuelComponent>(target), Is.False,
                    "The duel still ends when the prey dies.");
            });

            foreach (var uid in new[] { hunter, target, marine })
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KillsAreCountedTowardHonorWorth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            var killer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var first = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var second = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var examiner = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));

            Assert.That(YautjaHonorWorth.Get(killer, entMan), Is.EqualTo(1), "An untested human is worth the default honor.");

            mobState.ChangeMobState(first, MobState.Dead, origin: killer);
            mobState.ChangeMobState(second, MobState.Dead, origin: killer);
            // dead -> dead again shouldn't count twice
            mobState.ChangeMobState(second, MobState.Dead, origin: killer);
            // no origin = nobody gets the kill
            mobState.ChangeMobState(examiner, MobState.Dead);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<YautjaHonorWorthComponent>(killer).LifeKillsTotal, Is.EqualTo(2));
                Assert.That(YautjaHonorWorth.Get(killer, entMan), Is.EqualTo(2),
                    "Honor worth must reflect the mob's real kill count, as CMSS13 life_kills_total does.");
            });

            foreach (var uid in new[] { killer, first, second, examiner })
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeadYautjaCannotBeButchered()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var fallen = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.System<MobStateSystem>().ChangeMobState(fallen, MobState.Dead);

            var trophies = entMan.System<YautjaTrophySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(trophies.TryStartButcher(hunter, fallen, YautjaButcherProcedure.Skin), Is.False,
                    "A fallen hunter is not prey; harvest and the ceremonial dagger already refuse Yautja.");
                Assert.That(trophies.TryStartButcher(hunter, fallen, YautjaButcherProcedure.LeftArm), Is.False);
                Assert.That(trophies.TryOpenButcherDialog(hunter), Is.False,
                    "The butcher dialog must not offer a corpse the procedure refuses.");
            });

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(fallen);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RaiseThrallRefusesAHeadlessCorpseButRaisesAnIntactOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid headless = default;
        EntityUid intact = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            // raise thrall only exists on the bad blood bracer
            var inventory = entMan.System<InventorySystem>();
            if (inventory.TryUnequip(hunter, "gloves", out var oldBracer, silent: true, force: true))
                entMan.DeleteEntity(oldBracer.Value);
            var badBloodBracer = entMan.SpawnEntity("CMUYautjaBadBloodBracer", map.GridCoords);
            Assert.That(inventory.TryEquip(hunter, badBloodBracer, "gloves", silent: true, force: true), Is.True);
            headless =entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            intact = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
            mobState.ChangeMobState(headless, MobState.Dead);
            mobState.ChangeMobState(intact, MobState.Dead);

            Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(
                headless, new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None), out var head), Is.True);
            var sever = new BodyPartSeverAttemptEvent(headless, head, BodyPartType.Head);
            entMan.EventBus.RaiseLocalEvent(head, ref sever, broadcast: true);
            Assert.That(sever.Succeeded, Is.True, "Test setup: the head must come off.");
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.System<InventorySystem>().TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
            var action = entMan.System<SharedRMCActionsSystem>()
                .GetActionsWithEvent<YautjaRaiseThrallActionEvent>(hunter).Single();

            var refused = new YautjaRaiseThrallActionEvent { Performer = hunter, Action = action, Target = headless };
            entMan.EventBus.RaiseLocalEvent(bracer!.Value, refused);

            Assert.Multiple(() =>
            {
                Assert.That(refused.Handled, Is.False);
                Assert.That(entMan.System<MobStateSystem>().IsDead(headless), Is.True,
                    "Rejuvenate cannot regrow a head; a decapitated corpse must stay dead.");
                Assert.That(entMan.HasComponent<YautjaThrallComponent>(headless), Is.False);
            });

            var raised = new YautjaRaiseThrallActionEvent { Performer = hunter, Action = action, Target = intact };
            entMan.EventBus.RaiseLocalEvent(bracer.Value, raised);

            Assert.Multiple(() =>
            {
                Assert.That(raised.Handled, Is.True, "An intact corpse can still be raised.");
                Assert.That(entMan.System<MobStateSystem>().IsDead(intact), Is.False);
                Assert.That(entMan.HasComponent<YautjaThrallComponent>(intact), Is.True);
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[] { hunter, headless, intact })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }
}
