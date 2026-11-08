using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Consoles;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Shared.Alert;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Phase E3, playtest 4: River-Melded conversions. A console assimilation schedules a meld on an eligible
/// synthetic player; the victim is warned, then converted in place and shown an alert; only the Weyland
/// key or the Joe's death frees a thrall. Organics are never melded: an assimilation that finds no
/// synthetic gives the Joe its one respawn instead.
/// </summary>
[TestFixture]
public sealed class EWJoeMeldingTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Console = "CMUResearchDataTerminal";
    private const string SeegsonKey = "RMCSynthResetKeySeegson";
    private const string WeylandKey = "CMUSynthResetKeyWeyland";
    private const string ThrallAlert = "CMUEWJoeRiverMelded";
    private const string RespawnAlert = "CMUEWJoeRespawn";

    [Test]
    public async Task SynthPlayerIsMeldedAfterWarningsAndOnlyTheWeylandKeyCures()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var player = pair.Player!;

        EntityUid joe = default, victim = default, console = default, bystander = default;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var consoles = entities.System<EWJoeConsoleSystem>();
            var melding = entities.System<EWJoeMeldingSystem>();
            var mobState = entities.System<MobStateSystem>();

            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            comp.MeldDuration = TimeSpan.FromSeconds(3);

            bystander = entities.SpawnEntity(Human, map.GridCoords);

            // a synthetic player: a Working Joe body with its ghost role stripped and the test player's mind inside
            victim = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.RemoveComponent<GhostTakeoverAvailableComponent>(victim);
            entities.RemoveComponent<GhostRoleComponent>(victim);
            var mind = mindSys.CreateMind(player.UserId, "Victim");
            mindSys.TransferTo(mind, victim);

            Assert.That(melding.IsEligible((joe, comp), victim, null), "a synthetic player is eligible");
            Assert.That(melding.IsEligible((joe, comp), bystander, null), Is.False, "a mindless mob is not");

            // a meld is cancelled by the victim's death
            console = entities.SpawnEntity(Console, map.GridCoords);
            consoles.Assimilate((joe, comp), console);
            Assert.That(entities.TryGetComponent(victim, out RiverMeldingComponent? pending), "meld scheduled by the assimilation");
            Assert.That(pending!.Master, Is.EqualTo(joe));
            Assert.That(comp.RespawnReady, Is.False, "a synthetic was found: no respawn");

            mobState.ChangeMobState(victim, MobState.Dead);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var mobState = entities.System<MobStateSystem>();
            var melding = entities.System<EWJoeMeldingSystem>();
            var comp = entities.GetComponent<EWJoeComponent>(joe);

            Assert.That(entities.HasComponent<RiverMeldingComponent>(victim), Is.False, "death cancels the meld");
            Assert.That(entities.HasComponent<RiverMeldedComponent>(victim), Is.False);

            mobState.ChangeMobState(victim, MobState.Alive);
            Assert.That(melding.TryStartMeld((joe, comp)), "a second meld starts on the revived victim");
        });

        // 3 s meld, three warnings one per 1 s pass, conversion on the pass after the last warning
        var ticks = (int) Math.Ceiling(6.5 / timing.TickPeriod.TotalSeconds);
        await pair.RunTicksSync(ticks);

        await server.WaitAssertion(() =>
        {
            var roles = entities.System<SharedRoleSystem>();
            var mindSys = entities.System<MindSystem>();
            var alerts = entities.System<AlertsSystem>();
            var comp = entities.GetComponent<EWJoeComponent>(joe);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<RiverMeldingComponent>(victim), Is.False, "countdown finished");
                Assert.That(entities.TryGetComponent(victim, out RiverMeldedComponent? melded), "victim is a thrall");
                Assert.That(melded!.Master, Is.EqualTo(joe));
                Assert.That(melded.WarningsReceived, Is.EqualTo(EWJoeMeldingSystem.Warnings), "all warnings went out first");
                Assert.That(comp.Thralls, Is.EquivalentTo(new[] { victim }));
                Assert.That(mindSys.TryGetMind(victim, out var mindId, out _) && roles.MindHasRole<EWJoeThrallRoleComponent>(mindId), "thrall mind role");
                Assert.That(entities.GetComponent<ActiveRadioComponent>(victim).Channels, Does.Contain(comp.ThrallRadioChannel!.Value), "thrall radio");
                Assert.That(entities.GetComponent<ActiveRadioComponent>(joe).Channels, Does.Contain(comp.ThrallRadioChannel!.Value), "master radio");
                Assert.That(alerts.IsShowingAlert(victim, ThrallAlert), "playtest 4: the thrall is shown what it is now");
                Assert.That(entities.HasComponent<GrandMachineTouchedComponent>(victim), "touched by the machine");
            });

            // the Seegson key (and so the Working Joe reboot verb) does nothing to a thrall
            var seegson = entities.SpawnEntity(SeegsonKey, map.GridCoords);
            var seegsonEv = new TargetDefibrillatedEvent(bystander, victim, (seegson, entities.GetComponent<DefibrillatorComponent>(seegson)), new());
            entities.EventBus.RaiseLocalEvent(victim, ref seegsonEv);
            Assert.That(entities.HasComponent<RiverMeldedComponent>(victim), "Seegson key does not cure");

            // the Weyland key does
            var weyland = entities.SpawnEntity(WeylandKey, map.GridCoords);
            Assert.That(entities.HasComponent<CMUWeylandResetKeyComponent>(weyland));
            var weylandEv = new TargetDefibrillatedEvent(bystander, victim, (weyland, entities.GetComponent<DefibrillatorComponent>(weyland)), new());
            entities.EventBus.RaiseLocalEvent(victim, ref weylandEv);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<RiverMeldedComponent>(victim), Is.False, "Weyland key cures");
                Assert.That(comp.Thralls, Is.Empty);
                Assert.That(mindSys.TryGetMind(victim, out var mindId, out _) && roles.MindHasRole<EWJoeThrallRoleComponent>(mindId), Is.False, "role removed");
                Assert.That(alerts.IsShowingAlert(victim, ThrallAlert), Is.False, "the thrall alert goes with the cure");
                Assert.That(entities.HasComponent<GrandMachineTouchedComponent>(victim), Is.False, "and so does the machine's mark");
            });
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            // the Working Joe body had its own intrinsic radio; only the machine channel is gone
            Assert.That(entities.TryGetComponent(victim, out ActiveRadioComponent? radio) && radio.Channels.Contains(comp.ThrallRadioChannel!.Value), Is.False,
                "machine channel revoked");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganicsAreNeverMeldedAndAnEmptyRiverGivesOneRespawn()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var player = pair.Player!;

        EntityUid orphanThrall = default;

        await server.WaitAssertion(() =>
        {
            var mindSys = entities.System<MindSystem>();
            var melding = entities.System<EWJoeMeldingSystem>();
            var respawn = entities.System<EWJoeRespawnSystem>();
            var alerts = entities.System<AlertsSystem>();

            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            // N7: no search window, so a miss gives the copy at once (the debt is covered by EWJoeMeldingDebtTest)
            comp.MeldSearchWindow = TimeSpan.Zero;

            // an organic player is the only candidate
            var organic = entities.SpawnEntity(Human, map.GridCoords);
            var mind = mindSys.CreateMind(player.UserId, "Organic");
            mindSys.TransferTo(mind, organic);

            Assert.Multiple(() =>
            {
                Assert.That(melding.IsEligible((joe, comp), organic, null), Is.False, "an organic player is never eligible");
                Assert.That(melding.TryStartMeld((joe, comp)), Is.False, "nobody is melded");
                Assert.That(entities.HasComponent<RiverMeldingComponent>(organic), Is.False);
                Assert.That(comp.RespawnReady, "the empty river gives the Joe its respawn");
                Assert.That(alerts.IsShowingAlert(joe, RespawnAlert), "and shows it");
            });

            // the Joe's own panel says so
            var objectives = entities.System<EWJoeObjectiveSystem>();
            objectives.UpdateStatus((joe, comp));
            Assert.That(entities.GetComponent<EWJoeStatusComponent>(joe).RespawnReady, "the panel shows the copy");

            // it never stacks
            Assert.That(melding.TryStartMeld((joe, comp)), Is.False);
            Assert.That(respawn.Grant((joe, comp)), Is.False, "a second respawn is not granted while one is held");

            // T7: the Joe's death frees its thralls. A synthetic thrall, converted directly; the respawn is
            // spent first so the death sticks.
            var synth = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.RemoveComponent<GhostTakeoverAvailableComponent>(synth);
            entities.RemoveComponent<GhostRoleComponent>(synth);
            melding.Convert(synth, new RiverMeldingComponent { Master = joe });
            Assert.That(entities.HasComponent<RiverMeldedComponent>(synth), "synthetic thrall made");
            objectives.UpdateStatus((joe, comp));
            Assert.That(entities.GetComponent<EWJoeStatusComponent>(joe).Thralls,
                Is.EquivalentTo(new[] { entities.GetComponent<MetaDataComponent>(synth).EntityName }), "the panel lists the thrall");

            comp.RespawnReady = false;
            Assert.That(respawn.Grant((joe, comp)), Is.False, "RB-05: a copy once given is not given again this round");
            entities.System<MobStateSystem>().ChangeMobState(joe, MobState.Dead);
            Assert.That(entities.HasComponent<RiverMeldedComponent>(synth), Is.False, "freed on the Joe's death");
            Assert.That(comp.Thralls, Is.Empty);
            Assert.That(comp.ThrallsMade, Is.EqualTo(1), "the summary counts history, not the roster");

            // a thrall whose master is deleted outright (gib, admin delete) is freed by the sweep
            var joe2 = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp2 = entities.EnsureComponent<EWJoeComponent>(joe2);
            var melded2 = entities.EnsureComponent<RiverMeldedComponent>(synth);
            melded2.Master = joe2;
            comp2.Thralls.Add(synth);
            entities.DeleteEntity(joe2);
            orphanThrall = synth;
        });

        var sweepTicks = (int) Math.Ceiling(1.5 / server.ResolveDependency<IGameTiming>().TickPeriod.TotalSeconds);
        await pair.RunTicksSync(sweepTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<RiverMeldedComponent>(orphanThrall), Is.False, "orphaned thrall freed by the sweep");
        });

        await pair.CleanReturnAsync();
    }
}
