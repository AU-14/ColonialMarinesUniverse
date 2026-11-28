using System.Linq;
using Content.Server.CMU14.Threats.GrandMachine;
using Content.Server.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.WorkingJoes;

/// <summary>
/// A hijacked Joe's Offer converts a dead synthetic: it is revived, made a threat unit and given the hijacked role
/// (a connected player) or opened as a ghost role (no player). A reset key in someone else's hands cures it, taking
/// back exactly what the hijack added; a cured Joe can be converted again once it dies. Early Warning Joes,
/// River-Melded thralls and subverted synthetics are never converted.
/// </summary>
[TestFixture]
public sealed class HijackedJoeConversionTest
{
    private const string HijackedJoe = "CMUMobHijackedJoe";
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string SeegsonKey = "RMCSynthResetKeySeegson";

    [Test]
    public async Task OfferConvertsAResetKeyCuresAndRefusedTargetsStayAlone()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var player = pair.Player!;

        EntityUid user = default, target = default, bystander = default;
        HashSet<string> recorded = new();

        await server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var mobState = entities.System<MobStateSystem>();
            var hijack = entities.System<CMUHijackedJoeSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var radio = entities.System<GrandMachineRadioSystem>();

            user = entities.SpawnEntity(HijackedJoe, map.GridCoords);
            bystander = entities.SpawnEntity(Human, map.GridCoords);

            // a colony Working Joe played by the test player
            target = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.RemoveComponent<GhostTakeoverAvailableComponent>(target);
            entities.RemoveComponent<GhostRoleComponent>(target);
            var mind = minds.CreateMind(player.UserId, "Colony Joe");
            minds.TransferTo(mind, target);

            Assert.That(hijack.CanConvert(user, target), Is.False, "a living synthetic is not offered");
            mobState.ChangeMobState(target, MobState.Dead);
            Assert.That(hijack.CanConvert(user, target), "a dead colony Joe can be offered");
            Assert.That(hijack.CanConvert(bystander, target), Is.False, "only a Joe able to convert can offer");

            Assert.That(hijack.Convert(user, target), "the conversion goes through");

            var comp = entities.GetComponent<CMUHijackedJoeComponent>(target);
            recorded = new HashSet<string>(comp.AddedComponents);
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(target), Is.False, "the converted Joe is revived");
                Assert.That(entities.HasComponent<CMUJoeThreatUnitComponent>(target), "and counted as a threat unit");
                Assert.That(entities.HasComponent<CMUJoeConversionComponent>(target), "and can convert in turn");
                Assert.That(roles.MindHasRole<CMUHijackedJoeRoleComponent>(mind.Owner), "the player gets the hijacked role");
                Assert.That(radio.HasChannel(target), "and the Grand Machine radio");
                Assert.That(comp.RadioGranted, "the radio grant is recorded");
                Assert.That(recorded, Is.Not.Empty, "the added kit is recorded");
                Assert.That(comp.AddedActions, Is.Not.Empty, "and so are the granted actions");
            });

            foreach (var name in recorded)
            {
                var type = entities.ComponentFactory.GetRegistration(name).Type;
                Assert.That(entities.HasComponent(target, type), $"recorded component {name} is on the body");
            }

            // nobody plays this one: it becomes a ghost role open to anyone
            var mindless = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            mobState.ChangeMobState(mindless, MobState.Dead);
            Assert.That(hijack.Convert(user, mindless), "a mindless synthetic is converted too");
            var ghostRole = entities.GetComponent<GhostRoleComponent>(mindless);
            Assert.Multiple(() =>
            {
                Assert.That(ghostRole.JobProto?.Id, Is.EqualTo(CMUHijackedJoeSystem.HijackedJoeJob.Id));
                Assert.That(ghostRole.Taken, Is.False);
                Assert.That(ghostRole.Requirements, Is.Not.Null.And.Empty, "no playtime or trait gate");
                Assert.That(ghostRole.MindRoles.Select(r => r.Id), Does.Contain("CMUMindRoleHijackedJoe"));
                Assert.That(entities.HasComponent<GhostTakeoverAvailableComponent>(mindless));
                Assert.That(entities.HasComponent<CMUJoeThreatUnitComponent>(mindless));
            });
        });

        // past the short window in which a reset key does not undo a fresh conversion
        await pair.RunTicksSync(pair.SecondsToTicks(3));

        await server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var roles = entities.System<SharedRoleSystem>();
            var key = entities.SpawnEntity(SeegsonKey, map.GridCoords);
            var defib = entities.GetComponent<DefibrillatorComponent>(key);

            // a hijacked Joe cannot reset itself
            var self = new TargetDefibrillatedEvent(target, target, (key, defib), new());
            entities.EventBus.RaiseLocalEvent(target, ref self);
            Assert.That(entities.HasComponent<CMUJoeThreatUnitComponent>(target), "its own reset key does not cure it");

            var other = new TargetDefibrillatedEvent(bystander, target, (key, defib), new());
            entities.EventBus.RaiseLocalEvent(target, ref other);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<CMUJoeThreatUnitComponent>(target), Is.False, "the cure removes the threat unit");
                Assert.That(entities.HasComponent<CMUJoeConversionComponent>(target), Is.False, "and the conversion");
                Assert.That(minds.TryGetMind(target, out var mindId, out _)
                    && roles.MindHasRole<CMUHijackedJoeRoleComponent>(mindId), Is.False, "and the role");
            });

            foreach (var name in recorded)
            {
                var type = entities.ComponentFactory.GetRegistration(name).Type;
                Assert.That(entities.HasComponent(target, type), Is.False, $"recorded component {name} is taken back");
            }
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var mobState = entities.System<MobStateSystem>();
            var hijack = entities.System<CMUHijackedJoeSystem>();
            var radio = entities.System<GrandMachineRadioSystem>();

            Assert.That(entities.HasComponent<CMUHijackedJoeComponent>(target), Is.False, "the hijack itself goes");
            Assert.That(radio.HasChannel(target), Is.False, "and so does the machine channel");

            // a cured Joe that dies again can be converted again
            mobState.ChangeMobState(target, MobState.Dead);
            Assert.That(hijack.Convert(user, target), "a cured Joe is converted again after it dies");

            // never: an Early Warning Joe, a River-Melded thrall, a subverted synthetic
            var earlyWarning = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(earlyWarning);
            mobState.ChangeMobState(earlyWarning, MobState.Dead);

            var thrall = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<RiverMeldedComponent>(thrall);
            mobState.ChangeMobState(thrall, MobState.Dead);

            var subverted = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<SubvertedSynthComponent>(subverted);
            mobState.ChangeMobState(subverted, MobState.Dead);

            Assert.Multiple(() =>
            {
                Assert.That(hijack.CanConvert(user, earlyWarning), Is.False, "an Early Warning Joe is refused");
                Assert.That(hijack.CanConvert(user, thrall), Is.False, "a River-Melded thrall is refused");
                Assert.That(hijack.CanConvert(user, subverted), Is.False, "a subverted synthetic is refused");
                Assert.That(hijack.Convert(user, earlyWarning), Is.False);
                Assert.That(entities.HasComponent<CMUHijackedJoeComponent>(earlyWarning), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
