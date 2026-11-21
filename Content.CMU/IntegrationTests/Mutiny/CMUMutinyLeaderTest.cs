using System.Collections;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Marines.Mutiny;
using Content.Server.CMU14.Mutiny;
using Content.Server.EUI;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Mutiny;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Mutiny;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using ClientPromptEui = Content.Client.CMU14.Mutiny.CMUMutinyPromptEui;

namespace Content.IntegrationTests.CMU14.Mutiny;

[TestFixture]
public sealed class CMUMutinyLeaderTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private MutinySystem Mutiny => Server.System<MutinySystem>();

    private EntityCoordinates _coords;

    private async Task CreateMap()
    {
        var map = await Pair.CreateTestMap();
        _coords = map.GridCoords;
    }

    private EntityUid Marine(string faction)
    {
        var uid = SEntMan.SpawnAtPosition("CMMobHuman", _coords);
        SEntMan.EnsureComponent<MarineComponent>(uid).Faction = faction;
        return uid;
    }

    // the player is the one who gets the windows
    private EntityUid PlayerMarine(string faction)
    {
        var uid = Marine(faction);
        var minds = Server.System<SharedMindSystem>();
        var mind = minds.TryGetMind(ServerSession!, out var mindId, out _)
            ? mindId
            : minds.CreateMind(ServerSession!.UserId).Owner;

        minds.TransferTo(mind, uid);
        Assert.That(SEntMan.HasComponent<ActorComponent>(uid), "the test player must be in the target");
        return uid;
    }

    private void Recruit(EntityUid leader, EntityUid target)
    {
        var ev = new MutineerRecruitActionEvent { Target = target };
        ev.Performer = leader;
        SEntMan.EventBus.RaiseLocalEvent(leader, ev);
    }

    private CMUMutinyPromptEui Prompt(EntityUid recipient)
    {
        Assert.That(Mutiny.TryGetPrompt(recipient, out var prompt), Is.True, "expected an open mutiny prompt");
        return prompt!;
    }

    private CMUMutinySide? Side(EntityUid uid)
    {
        return SEntMan.TryGetComponent(uid, out CMUMutinyParticipantComponent? participant) ? participant.Side : null;
    }

    private SpriteSpecifier? Icon(EntityUid uid)
    {
        return Server.System<SharedMarineSystem>().GetMarineIcon(uid).Icon;
    }

    // mutiny is an admin-made antag: the antag verb or the mutiny command, never a job, preset or spawn
    [Test]
    public async Task OnlyAdminsCanMakeAMutiny()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            string[] banned =
            [
                factory.GetComponentName<MutineerLeaderComponent>(),
                factory.GetComponentName<MutineerComponent>(),
                factory.GetComponentName<CMUMutinyParticipantComponent>(),
            ];

            var offenders = new List<string>();
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (banned.Any(proto.Components.ContainsKey))
                    offenders.Add(proto.ID);
            }

            foreach (var job in SProtoMan.EnumeratePrototypes<JobPrototype>())
            {
                var registries = new List<ComponentRegistry> { job.RoundComponents };
                registries.AddRange(job.RoundSideComponents.Values);
                registries.AddRange(job.RoundForceComponents.Values);
                if (registries.Any(registry => banned.Any(registry.ContainsKey)))
                    offenders.Add(job.ID);
            }

            Assert.That(offenders, Is.Empty, "mutiny roles must only come from admins");
        });
    }

    [Test]
    public async Task LeaderGetsBothAbilitiesAndTheLeaderBadge()
    {
        await CreateMap();
        await Server.WaitAssertion(() =>
        {
            var leader = Marine("govfor");
            SEntMan.AddComponent<MutineerLeaderComponent>(leader);

            var leaderComp = SEntMan.GetComponent<MutineerLeaderComponent>(leader);
            var cmu = SEntMan.GetComponent<CMUMutinyLeaderComponent>(leader);
            Assert.Multiple(() =>
            {
                Assert.That(leaderComp.RecruitActionEntity, Is.Not.Null);
                Assert.That(cmu.BeginActionEntity, Is.Not.Null, "begin mutiny is the second leader ability");
                Assert.That(SEntMan.HasComponent<InstantActionComponent>(cmu.BeginActionEntity!.Value));
                Assert.That(SEntMan.HasComponent<MutineerComponent>(leader));
                Assert.That(Side(leader), Is.EqualTo(CMUMutinySide.Leader));
                Assert.That(Icon(leader), Is.EqualTo(SEntMan.GetComponent<CMUMutinyParticipantComponent>(leader).Icons[CMUMutinySide.Leader]),
                    "the leader shows the leader badge, never the plain mutineer one");
            });
        });
    }

    [Test]
    public async Task RecruitSwearsInQuietlyAndBeginRevealsEveryone()
    {
        await CreateMap();
        EntityUid leader = default, target = default, police = default, enemy = default, bystander = default;
        await Server.WaitAssertion(() =>
        {
            leader = Marine("govfor");
            SEntMan.AddComponent<MutineerLeaderComponent>(leader);
            target = PlayerMarine("govfor");
            police = Marine("govfor");
            SEntMan.AddComponent<CMUMutinyLoyalistComponent>(police);
            enemy = Marine("opfor");
            bystander = Marine("govfor");

            // the RMC check needed MutinyEligible, which no CMU job carries, so this never opened anything
            Assert.That(SEntMan.HasComponent<MutinyEligibleComponent>(target), Is.False);

            Recruit(leader, enemy);
            Recruit(leader, police);
            Assert.That(Mutiny.TryGetPrompt(enemy, out _), Is.False, "the other side can't be recruited");
            Assert.That(Mutiny.TryGetPrompt(police, out _), Is.False, "MPs and command can't be recruited");

            Recruit(leader, target);
            var prompt = Prompt(target);
            Assert.That(prompt.PromptState.Kind, Is.EqualTo(CMUMutinyPromptKind.Invite));
            Assert.That(prompt.PromptState.Begun, Is.False);
        });

        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var manager = Client.ResolveDependency<Content.Client.Eui.EuiManager>();
            var open = (IDictionary) typeof(Content.Client.Eui.EuiManager)
                .GetField("_openUis", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            Assert.That(open.Values.Cast<object>().Any(entry => entry.GetType().GetField("Eui")!.GetValue(entry) is ClientPromptEui),
                "the invite window must reach the client");
        });

        await Server.WaitAssertion(() =>
        {
            Prompt(target).HandleMessage(new CMUMutinyPromptChoiceMessage(CMUMutinyChoice.Accept));
            Assert.Multiple(() =>
            {
                Assert.That(Mutiny.TryGetPrompt(target, out _), Is.False);
                Assert.That(SEntMan.GetComponent<CMUMutinySwornComponent>(target).Leader, Is.EqualTo(leader));
                Assert.That(SEntMan.HasComponent<MutineerComponent>(target), Is.False, "sworn recruits stay hidden");
                Assert.That(Side(target), Is.Null, "nothing networked gives a sworn recruit away");
            });

            Recruit(leader, target);
            Assert.That(Mutiny.TryGetPrompt(target, out _), Is.False, "can't swear in the same marine twice");

            Mutiny.BeginMutiny(leader);
            var cmu = SEntMan.GetComponent<CMUMutinyLeaderComponent>(leader);
            Assert.Multiple(() =>
            {
                Assert.That(cmu.Begun, Is.True);
                Assert.That(cmu.BeginActionEntity, Is.Null, "begin mutiny is spent");
                Assert.That(SEntMan.HasComponent<CMUMutinySwornComponent>(target), Is.False);
                Assert.That(SEntMan.HasComponent<MutineerComponent>(target), Is.True, "sworn recruits are revealed");
                Assert.That(Side(target), Is.EqualTo(CMUMutinySide.Mutineer));
                Assert.That(Icon(target), Is.EqualTo(SEntMan.GetComponent<CMUMutinyParticipantComponent>(target).Icons[CMUMutinySide.Mutineer]));
                Assert.That(Side(police), Is.EqualTo(CMUMutinySide.Loyalist), "MPs and command side with command on their own");
                Assert.That(Side(enemy), Is.Null, "the other side isn't part of this mutiny");
                Assert.That(Side(bystander), Is.Null, "no player, nobody to ask");
            });

            // a second begin from the same leader does nothing
            Mutiny.BeginMutiny(leader);
            Assert.That(Side(police), Is.EqualTo(CMUMutinySide.Loyalist));
        });

        await Pair.RunTicksSync(5);
    }

    [Test]
    public async Task EveryoneElsePicksASide()
    {
        await CreateMap();
        EntityUid leader = default, player = default;
        await Server.WaitAssertion(() =>
        {
            leader = Marine("govfor");
            SEntMan.AddComponent<MutineerLeaderComponent>(leader);
            player = PlayerMarine("govfor");

            Mutiny.BeginMutiny(leader);
            var prompt = Prompt(player);
            Assert.That(prompt.PromptState.Kind, Is.EqualTo(CMUMutinyPromptKind.ChooseSide));
            Assert.That(prompt.PromptState.Deadline, Is.Not.Null, "the side choice answers itself");
        });

        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            Prompt(player).HandleMessage(new CMUMutinyPromptChoiceMessage(CMUMutinyChoice.Loyalist));
            Assert.That(Side(player), Is.EqualTo(CMUMutinySide.Loyalist));

            // after it went loud the leader can still win people over, and they join on the spot
            Recruit(leader, player);
            Assert.That(Mutiny.TryGetPrompt(player, out _), Is.False, "loyalists can't be recruited");

            SEntMan.RemoveComponent<CMUMutinyParticipantComponent>(player);
            Recruit(leader, player);
            var invite = Prompt(player);
            Assert.That(invite.PromptState.Begun, Is.True);
            invite.HandleMessage(new CMUMutinyPromptChoiceMessage(CMUMutinyChoice.Accept));
            Assert.That(SEntMan.HasComponent<MutineerComponent>(player), Is.True);
            Assert.That(Side(player), Is.EqualTo(CMUMutinySide.Mutineer));
        });
    }

    [Test]
    public async Task UnansweredOrForbiddenChoicesFallBack()
    {
        await CreateMap();
        await Server.WaitAssertion(() =>
        {
            var player = PlayerMarine("govfor");
            var euis = Server.ResolveDependency<EuiManager>();

            CMUMutinyChoice? got = null;
            var state = new CMUMutinyPromptEuiState(CMUMutinyPromptKind.ChooseSide, "leader", null, true,
                new List<CMUMutinyRosterEntry>(), new List<CMUMutinyChoice> { CMUMutinyChoice.Mutineer }, null, TimeSpan.Zero);
            var synth = new CMUMutinyPromptEui(player, state,
                [CMUMutinyChoice.Mutineer, CMUMutinyChoice.Loyalist, CMUMutinyChoice.NonCombatant],
                CMUMutinyChoice.NonCombatant, (_, choice) => got = choice);
            euis.OpenEui(synth, ServerSession!);
            synth.HandleMessage(new CMUMutinyPromptChoiceMessage(CMUMutinyChoice.Mutineer));
            Assert.That(got, Is.EqualTo(CMUMutinyChoice.NonCombatant), "a disabled card can't be forced through");

            var calls = 0;
            got = null;
            var timed = new CMUMutinyPromptEui(player, state, [CMUMutinyChoice.Loyalist], CMUMutinyChoice.NonCombatant,
                (_, choice) => { got = choice; calls++; });
            euis.OpenEui(timed, ServerSession!);
            timed.Expire();
            timed.Expire();
            Assert.That(got, Is.EqualTo(CMUMutinyChoice.NonCombatant));
            Assert.That(calls, Is.EqualTo(1), "a prompt resolves exactly once");
        });
    }

    [Test]
    public async Task DeclineCooldownAndLeaderLossCleanUp()
    {
        await CreateMap();
        await Server.WaitAssertion(() =>
        {
            var leader = Marine("govfor");
            SEntMan.AddComponent<MutineerLeaderComponent>(leader);
            var player = PlayerMarine("govfor");
            var sworn = Marine("govfor");
            SEntMan.EnsureComponent<CMUMutinySwornComponent>(sworn).Leader = leader;

            Recruit(leader, player);
            Prompt(player).HandleMessage(new CMUMutinyPromptChoiceMessage(CMUMutinyChoice.Decline));
            Assert.That(SEntMan.HasComponent<CMUMutinySwornComponent>(player), Is.False);
            Recruit(leader, player);
            Assert.That(Mutiny.TryGetPrompt(player, out _), Is.False, "a refusal holds for a while");

            var begin = SEntMan.GetComponent<CMUMutinyLeaderComponent>(leader).BeginActionEntity;
            SEntMan.RemoveComponent<MutineerLeaderComponent>(leader);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUMutinyLeaderComponent>(leader), Is.False);
                Assert.That(SEntMan.Deleted(begin) || SEntMan.IsQueuedForDeletion(begin!.Value), Is.True, "begin mutiny goes with the leader role");
                Assert.That(Side(leader), Is.EqualTo(CMUMutinySide.Mutineer), "an ex-leader is still a mutineer");
            });
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<CMUMutinySwornComponent>();
            Assert.That(query.MoveNext(out _, out _), Is.False, "nobody stays sworn to a leader who's gone");

            var loyal = Marine("govfor");
            SEntMan.EnsureComponent<CMUMutinyParticipantComponent>(loyal).Side = CMUMutinySide.Loyalist;
            Mutiny.EndMutinyCMU();
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<CMUMutinyParticipantComponent>();
            Assert.That(query.MoveNext(out _, out _), Is.False, "ending the mutiny clears every side");
        });
    }
}
