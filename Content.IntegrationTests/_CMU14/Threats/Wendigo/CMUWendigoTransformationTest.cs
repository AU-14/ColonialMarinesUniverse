using Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Round.Antags.Cannibal;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Fax.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoTransformationTest
{
    private const string Human = "CMMobHuman";
    private const string HumanMeat = "FoodMeatHuman";
    private const string OtherFood = "FoodBreadPlain";
    private const string NonHuman = "CMMobArachnid";

    [Test]
    public async Task ProcedureGatesOrderTimersAndAborts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protos = server.ProtoMan;
            var satiation = entMan.System<SatiationSystem>();

            EntityUid SpawnStarvingHuman()
            {
                var human = entMan.SpawnEntity(Human, map.GridCoords);
                satiation.SetValue((human, entMan.GetComponent<SatiationComponent>(human)), SatiationSystem.Hunger, 40f);
                return human;
            }

            var feeder = entMan.SpawnEntity(Human, map.GridCoords);

            EntityUid Bite(EntityUid eater, string food)
            {
                var item = entMan.SpawnEntity(food, map.GridCoords);
                var ev = new IngestingEvent(item, new Solution(), false);
                entMan.EventBus.RaiseLocalEvent(eater, ref ev);
                return item;
            }

            void Feed(EntityUid eater, string food)
            {
                var item = Bite(eater, food);
                // The finished event names the feeder; feed through a third party so the eater must come from the bite.
                var finished = new FullyEatenEvent(feeder);
                entMan.EventBus.RaiseLocalEvent(item, ref finished);
            }

            void Inject(EntityUid target, string reagent, float amount)
            {
                var proto = protos.Index<ReagentPrototype>(reagent);
                var ev = new ReactionEntityEvent(ReactionMethod.Injection,
                    new ReagentQuantity(reagent, amount), proto, null);
                entMan.EventBus.RaiseLocalEvent(target, ref ev);
            }

            void Expire(EntityUid subject)
            {
                entMan.GetComponent<CMUWendigoSubjectComponent>(subject).StageEndsAt = TimeSpan.Zero;
            }

            CMUWendigoSubjectStage? Stage(EntityUid uid)
            {
                return entMan.TryGetComponent(uid, out CMUWendigoSubjectComponent? comp) ? comp.Stage : null;
            }

            // A fed but not starving human never starts the procedure.
            var fed = entMan.SpawnEntity(Human, map.GridCoords);
            satiation.SetValue((fed, entMan.GetComponent<SatiationComponent>(fed)), SatiationSystem.Hunger, 200f);
            Feed(fed, HumanMeat);
            Assert.That(Stage(fed), Is.Null, "Only starving subjects can begin.");

            // Only humans can be subjects.
            var alien = entMan.SpawnEntity(NonHuman, map.GridCoords);
            if (entMan.TryGetComponent(alien, out SatiationComponent? alienSatiation))
                satiation.SetValue((alien, alienSatiation), SatiationSystem.Hunger, 40f);
            Feed(alien, HumanMeat);
            Assert.That(Stage(alien), Is.Null, "Non-human species must not become subjects.");

            // Biting human meat without finishing it does nothing; the meal counts when the piece is finished.
            var subject = SpawnStarvingHuman();
            Bite(subject, HumanMeat);
            Assert.That(Stage(subject), Is.Null, "An unfinished piece of meat must not begin the procedure.");

            // Starving + a finished piece of human meat begins the procedure.
            Feed(subject, HumanMeat);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Fed1));
            Assert.That(entMan.HasComponent<CMUWendigoSubjectComponent>(feeder), Is.False,
                "The feeder must never become the subject.");

            // Inputs before the timer finishes are rejected.
            Feed(subject, HumanMeat);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Fed1), "Early meat must not advance.");

            Expire(subject);
            Feed(subject, HumanMeat);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Fed2));

            // Stabilized Mutagen before the second timer finishes, and under 15u, does nothing.
            Inject(subject, CMUWendigoTransformationSystem.StabilizedMutagen, 15);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Fed2), "Early mutagen must not advance.");
            Expire(subject);
            Inject(subject, CMUWendigoTransformationSystem.StabilizedMutagen, 14);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Fed2), "Under 15u must not advance.");
            Inject(subject, CMUWendigoTransformationSystem.MH32, 15);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Fed2), "MH-32 out of order must not advance.");
            Inject(subject, CMUWendigoTransformationSystem.StabilizedMutagen, 15);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Mutagen));

            // From the mutagen stage on, other food no longer aborts.
            Feed(subject, OtherFood);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Mutagen), "Food after mutagen must not abort.");

            // The "ready" examine text is only for the final dose.
            var system = entMan.System<CMUWendigoTransformationSystem>();
            var subjectComp = entMan.GetComponent<CMUWendigoSubjectComponent>(subject);
            Expire(subject);
            Assert.That(system.IsReadyForFinalDose(subjectComp), Is.False, "An expired mutagen stage is not the final dose.");

            Bite(subject, HumanMeat);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Mutagen), "An unfinished final meal must not advance.");
            Feed(subject, HumanMeat);
            Assert.That(Stage(subject), Is.EqualTo(CMUWendigoSubjectStage.Gestation));
            Assert.That(system.IsReadyForFinalDose(subjectComp), Is.False, "Gestation still running.");
            Expire(subject);
            Assert.That(system.IsReadyForFinalDose(subjectComp), Is.True);

            // Other food before the mutagen stage aborts.
            var aborted = SpawnStarvingHuman();
            Feed(aborted, HumanMeat);
            Expire(aborted);
            Feed(aborted, HumanMeat);
            Assert.That(Stage(aborted), Is.EqualTo(CMUWendigoSubjectStage.Fed2));
            Feed(aborted, OtherFood);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var query = entMan.EntityQueryEnumerator<CMUWendigoSubjectComponent>();
            var stages = new List<CMUWendigoSubjectStage>();
            while (query.MoveNext(out _, out var comp))
            {
                stages.Add(comp.Stage);
            }

            Assert.That(stages, Is.EquivalentTo(new[] { CMUWendigoSubjectStage.Gestation }),
                "The aborted subject must lose its procedure state; the gestating one keeps it.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [TestCase(true, true)]
    [TestCase(false, false)]
    public async Task FinalDoseConvertsSubject(bool cannibal, bool tamed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid subject = default;
        EntityUid scientist = default;
        EntityUid mindId = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mindSys = entMan.System<SharedMindSystem>();

            subject = entMan.SpawnEntity(Human, map.GridCoords);
            scientist = entMan.SpawnEntity(Human, map.GridCoords);
            if (cannibal)
                entMan.AddComponent<CannibalComponent>(subject);

            mindId = mindSys.CreateMind(null, "Test Subject");
            mindSys.TransferTo(mindId, subject);

            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Gestation;
            comp.StageEndsAt = TimeSpan.Zero;
            comp.LastInjector = scientist;
            comp.LastInjectorAt = server.Timing.CurTime;

            var reagent = tamed ? CMUWendigoTransformationSystem.MH33 : CMUWendigoTransformationSystem.MH32;
            var proto = server.ProtoMan.Index<ReagentPrototype>(reagent);
            var ev = new ReactionEntityEvent(ReactionMethod.Injection, new ReagentQuantity(reagent, 15), proto, null);
            entMan.EventBus.RaiseLocalEvent(subject, ref ev);

            Assert.That(comp.Stage, Is.EqualTo(CMUWendigoSubjectStage.Mutating));
            Assert.That(comp.DoAfter, Is.Not.Null, "The final mutation must run as a do-after.");
        });

        // One minute of mutation plus margin.
        await server.WaitRunTicks(server.Timing.TickRate * 62);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Deleted(subject) || entMan.IsQueuedForDeletion(subject), Is.True,
                "The old body must be removed so it never counts as a colonist.");

            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(mind.OwnedEntity, Is.Not.Null);
            var wendigo = mind.OwnedEntity!.Value;

            var expected = cannibal ? "CMUWendigoLab" : "CMUWendigoLesser";
            Assert.That(entMan.GetComponent<MetaDataComponent>(wendigo).EntityPrototype?.ID, Is.EqualTo(expected));
            Assert.That(entMan.HasComponent<CMUWendigoLabMadeComponent>(wendigo), Is.True);
            Assert.That(entMan.HasComponent<GhostRoleComponent>(wendigo), Is.False,
                "A converted body must never be offered to ghosts.");

            if (tamed)
            {
                var bond = entMan.GetComponent<CMUWendigoTamedComponent>(wendigo);
                Assert.That(bond.Master, Is.EqualTo(scientist));
                Assert.That(bond.MasterName, Is.EqualTo(entMan.GetComponent<MetaDataComponent>(scientist).EntityName));
            }
            else
            {
                Assert.That(entMan.HasComponent<CMUWendigoTamedComponent>(wendigo), Is.False);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissedWindowRelapsesAndAddsInstability()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid subject = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            subject = entMan.SpawnEntity(Human, map.GridCoords);
            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Gestation;
            comp.StageEndsAt = TimeSpan.Zero;
            // Ready cue already played and the window already over.
            comp.ReadyCuePlayed = true;
            comp.WindowEndsAt = TimeSpan.Zero;
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var comp = server.EntMan.GetComponent<CMUWendigoSubjectComponent>(subject);
            Assert.That(comp.Stage, Is.EqualTo(CMUWendigoSubjectStage.Mutagen), "A missed window relapses one stage.");
            Assert.That(comp.Instability, Is.EqualTo(1));
            Assert.That(comp.WindowEndsAt, Is.GreaterThan(server.Timing.CurTime),
                "The relapsed stage is ready again with a fresh window.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MistakesBuildInstabilityAndTheMaximumKills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid subject = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUWendigoTransformationSystem>();
            subject = entMan.SpawnEntity(Human, map.GridCoords);
            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Gestation;
            comp.StageEndsAt = TimeSpan.Zero;

            void Inject(string reagent, float amount)
            {
                var proto = server.ProtoMan.Index<ReagentPrototype>(reagent);
                var ev = new ReactionEntityEvent(ReactionMethod.Injection, new ReagentQuantity(reagent, amount), proto, null);
                entMan.EventBus.RaiseLocalEvent(subject, ref ev);
            }

            // An under-dose and an out-of-order dose are each one mistake.
            Inject(CMUWendigoTransformationSystem.MH32, 10);
            Inject(CMUWendigoTransformationSystem.StabilizedMutagen, 15);
            Assert.That(comp.Instability, Is.EqualTo(2));
            Assert.That(comp.Stage, Is.EqualTo(CMUWendigoSubjectStage.Gestation));

            var reading = system.BuildScannerReading(comp);
            Assert.That(reading, Does.Contain("2/" + CMUWendigoTransformationSystem.MaxInstability),
                "The health analyzer shows instability.");
            Assert.That(reading, Does.Contain("%"), "The health analyzer shows progress.");

            comp.Instability = CMUWendigoTransformationSystem.MaxInstability - 1;
            Inject(CMUWendigoTransformationSystem.MH32, 10);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.System<MobStateSystem>().IsDead(subject), Is.True, "Maximum instability kills the subject.");
            Assert.That(entMan.HasComponent<CMUWendigoSubjectComponent>(subject), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnstableSubjectBreaksTheMh33Bond()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid mindId = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mindSys = entMan.System<SharedMindSystem>();
            var subject = entMan.SpawnEntity(Human, map.GridCoords);
            var scientist = entMan.SpawnEntity(Human, map.GridCoords);
            mindId = mindSys.CreateMind(null, "Unstable Subject");
            mindSys.TransferTo(mindId, subject);

            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Gestation;
            comp.StageEndsAt = TimeSpan.Zero;
            comp.Instability = CMUWendigoTransformationSystem.UnstableThreshold;
            comp.LastInjector = scientist;
            comp.LastInjectorAt = server.Timing.CurTime;

            var proto = server.ProtoMan.Index<ReagentPrototype>(CMUWendigoTransformationSystem.MH33);
            var ev = new ReactionEntityEvent(ReactionMethod.Injection,
                new ReagentQuantity(CMUWendigoTransformationSystem.MH33, 15), proto, null);
            entMan.EventBus.RaiseLocalEvent(subject, ref ev);
        });

        await server.WaitRunTicks(server.Timing.TickRate * 62);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var wendigo = entMan.GetComponent<MindComponent>(mindId).OwnedEntity;
            Assert.That(wendigo, Is.Not.Null);
            Assert.That(entMan.HasComponent<CMUWendigoLabMadeComponent>(wendigo!.Value), Is.True);
            Assert.That(entMan.HasComponent<CMUWendigoTamedComponent>(wendigo.Value), Is.False,
                "An unstable subject must come out feral even with MH-33.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BriefingIsFaxedToTheFaxNearestTheCorporateTerminal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            entMan.SpawnEntity("CMUResearchDataTerminalCorporate", map.GridCoords);
            var near = entMan.SpawnEntity("CMFaxWY", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var far = entMan.SpawnEntity("CMFaxWY", map.GridCoords.Offset(new System.Numerics.Vector2(6, 0)));

            Assert.That(entMan.System<CMUWendigoResearchUnlockSystem>().SendLabBriefing(), Is.True);

            var nearQueue = entMan.GetComponent<FaxMachineComponent>(near).PrintingQueue;
            Assert.That(nearQueue, Has.Count.EqualTo(1), "The lab's own fax gets the briefing.");
            Assert.That(nearQueue.Peek().PrototypeId.Id, Is.EqualTo(CMUWendigoResearchUnlockSystem.LabBriefingPaper));
            Assert.That(entMan.GetComponent<FaxMachineComponent>(far).PrintingQueue, Is.Empty,
                "Other fax machines do not.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CriticalSubjectStillConverts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid mindId = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mindSys = entMan.System<SharedMindSystem>();
            var subject = entMan.SpawnEntity(Human, map.GridCoords);
            mindId = mindSys.CreateMind(null, "Critical Subject");
            mindSys.TransferTo(mindId, subject);

            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Gestation;
            comp.StageEndsAt = TimeSpan.Zero;

            var reagent = CMUWendigoTransformationSystem.MH32;
            var proto = server.ProtoMan.Index<ReagentPrototype>(reagent);
            var ev = new ReactionEntityEvent(ReactionMethod.Injection, new ReagentQuantity(reagent, 15), proto, null);
            entMan.EventBus.RaiseLocalEvent(subject, ref ev);

            // Only death cancels the change; being beaten into crit must not.
            entMan.System<MobStateSystem>().ChangeMobState(subject, MobState.Critical);
        });

        await server.WaitRunTicks(server.Timing.TickRate * 62);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(mind.OwnedEntity, Is.Not.Null);
            Assert.That(entMan.HasComponent<CMUWendigoLabMadeComponent>(mind.OwnedEntity!.Value), Is.True,
                "A critical subject must still finish turning.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeathCancelsMutation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var subject = entMan.SpawnEntity(Human, map.GridCoords);
            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Gestation;
            comp.StageEndsAt = TimeSpan.Zero;

            var reagent = CMUWendigoTransformationSystem.MH32;
            var proto = server.ProtoMan.Index<ReagentPrototype>(reagent);
            var ev = new ReactionEntityEvent(ReactionMethod.Injection, new ReagentQuantity(reagent, 15), proto, null);
            entMan.EventBus.RaiseLocalEvent(subject, ref ev);
            Assert.That(comp.Stage, Is.EqualTo(CMUWendigoSubjectStage.Mutating));

            entMan.System<MobStateSystem>().ChangeMobState(subject, MobState.Dead);
        });

        await server.WaitRunTicks(server.Timing.TickRate * 62);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Count<CMUWendigoSubjectComponent>(), Is.Zero, "Death must cancel the procedure.");
            Assert.That(entMan.Count<CMUWendigoLabMadeComponent>(), Is.Zero, "A dead subject must never convert.");
        });

        await pair.CleanReturnAsync();
    }
}
