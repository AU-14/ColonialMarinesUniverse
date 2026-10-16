using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Radio;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Radio;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMURtoRadioTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("govfor", "radioGovforJTAC")]
    [TestCase("opfor", "radioOpforJTAC")]
    public async Task PositionRelayBroadcastsExactGpsWithoutCreatingCasRequest(string faction, string channel)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.Grid).Offset = new Vector2i(500, 500);
            var rto = SEntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(-12.75f, 15.75f)));
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(rto).Faction = faction;
            SEntMan.AddComponent<WearingANPRCComponent>(rto).Radio = pack;
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreatePositionRequest(rto, 0, 0, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out var id), Is.True);
            Assert.That(requests.TryAbort(rto, id), Is.True);
            var existingRequests = requests.GetRequests(null).ToArray();
            _ = Server.System<PositionRadioProbeSystem>();
            var probe = SEntMan.AddComponent<PositionRadioProbeComponent>(rto);
            var transform = Server.System<SharedTransformSystem>();
            var marks = SEntMan.QueryEntities<DropshipTargetComponent>().Count();
            void Relay()
            {
                var position = transform.GetMapCoordinates(rto).Position;
                SEntMan.EventBus.RaiseLocalEvent(pack, new CMUCasRelayPositionMsg()
                    { Actor = rto, UiKey = CMUCasCallUi.Key });
                Assert.That(probe.Channels.Last(), Is.EqualTo(channel));
                Assert.That(probe.Messages.Last(), Does.Contain($"GPS X {(int) position.X}, Y {(int) position.Y},"));
            }
            Relay();
            transform.SetCoordinates(rto, map.GridCoords.Offset(new Vector2(4.8f, -7.9f)));
            Relay();
            Assert.That(probe.Messages.Count, Is.EqualTo(2));
            Assert.That(requests.GetRequests(null), Is.EqualTo(existingRequests),
                "Relaying must neither create requests nor change existing request history.");
            Assert.That(SEntMan.QueryEntities<DropshipTargetComponent>().Count(), Is.EqualTo(marks));
        });
    }

    [TestCase("broken")]
    [TestCase("off")]
    [TestCase("not-worn")]
    [TestCase("wrong-pack")]
    [TestCase("no-faction")]
    public async Task PositionRelayRequiresWorkingWornPackAndJtacFaction(string failure)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            if (failure != "no-faction")
                SEntMan.EnsureComponent<MarineComponent>(rto).Faction = "govfor";
            if (failure != "not-worn")
                SEntMan.AddComponent<WearingANPRCComponent>(rto).Radio = failure == "wrong-pack"
                    ? SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords) : pack;
            var radio = SEntMan.GetComponent<ANPRCRadioComponent>(pack);
            radio.WiresRipped = failure == "broken";
            radio.Enabled = failure != "off";
            _ = Server.System<PositionRadioProbeSystem>();
            var probe = SEntMan.AddComponent<PositionRadioProbeComponent>(rto);
            var requests = Server.System<CMUCasRequestSystem>();
            var existingRequests = requests.GetRequests(null).ToArray();
            SEntMan.EventBus.RaiseLocalEvent(pack, new CMUCasRelayPositionMsg()
                { Actor = rto, UiKey = CMUCasCallUi.Key });
            Assert.That(probe.Messages, Is.Empty);
            Assert.That(requests.GetRequests(null), Is.EqualTo(existingRequests));
        });
    }

    [TestCase(0, 0)]
    [TestCase(15, 0)]
    [TestCase(-15, 0)]
    [TestCase(0, 15)]
    [TestCase(0, -15)]
    [TestCase(15, 15)]
    [TestCase(-15, -15)]
    [TestCase(15, -15)]
    [TestCase(-15, 15)]
    public async Task PositionCallsNeedNoHostileAndStayWithinDeviation(int x, int y)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity(null, map.GridCoords);
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            SEntMan.AddComponent<WearingANPRCComponent>(rto).Radio = pack;
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            var requests = Server.System<CMUCasRequestSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var origin = transform.GetMapCoordinates(rto);
            var positions = new HashSet<Vector2>();
            for (var i = 0; i < 20; i++)
            {
                Assert.That(requests.TryCreatePositionRequest(rto, x, y, CMUCasMunition.Any,
                    CMUCasEffect.Any, "", out var id), Is.True);
                Assert.That(requests.TryGetRequest(id, out var request), Is.True);
                var mark = SEntMan.GetEntity(request.Target);
                var coordinates = transform.GetMapCoordinates(mark);
                Assert.That(coordinates.MapId, Is.EqualTo(origin.MapId));
                Assert.That((coordinates.Position - origin.Position - new Vector2(x, y)).Length(),
                    Is.LessThanOrEqualTo(CMUCasRequestSystem.PositionDeviation + 0.001f));
                positions.Add(coordinates.Position);
                Assert.That(request.RadioMark, Is.True);
                Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Pending));
                Assert.That(requests.TryAbort(rto, id), Is.True);
            }

            Assert.That(positions.Count, Is.GreaterThan(1), "position calls must actually deviate");
        });
    }

    [TestCase(16, 0)]
    [TestCase(0, -16)]
    [TestCase(int.MinValue, int.MaxValue)]
    public async Task InvalidPositionOffsetsAreRejected(int x, int y)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity(null, map.GridCoords);
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            SEntMan.AddComponent<WearingANPRCComponent>(rto).Radio = pack;
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            Assert.That(Server.System<CMUCasRequestSystem>().TryCreatePositionRequest(rto, x, y,
                CMUCasMunition.Any, CMUCasEffect.Any, "", out _), Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task XenosCanRipPackOrWearerAfterSevenSeconds(bool targetWearer, bool interrupt)
    {
        var map = await Pair.CreateTestMap();
        EntityUid pack = default;
        EntityUid xeno = default;
        await Server.WaitAssertion(() =>
        {
            xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords.Offset(new Vector2(1, 0)));
            var target = pack;
            if (targetWearer)
            {
                target = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
                SEntMan.AddComponent<WearingANPRCComponent>(target).Radio = pack;
            }
            var verbs = new GetVerbsEvent<Verb>(xeno, target, null,
                SEntMan.GetComponent<HandsComponent>(xeno), true, true, true, new());
            SEntMan.EventBus.RaiseLocalEvent(target, verbs);
            var rip = verbs.Verbs.Single(verb => verb.Text == "Rip out radio wires");
            rip.Act!();
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(xeno).DoAfters.Values.Single().Args.Delay,
                Is.EqualTo(TimeSpan.FromSeconds(7)));
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<ANPRCRadioComponent>(pack).WiresRipped, Is.False));
        if (interrupt)
        {
            await Server.WaitPost(() => Server.System<SharedTransformSystem>()
                .SetCoordinates(xeno, map.GridCoords.Offset(new Vector2(5, 0))));
        }
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            var radio = SEntMan.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.WiresRipped, Is.EqualTo(!interrupt));
            Assert.That(radio.Enabled, Is.EqualTo(interrupt));
            Assert.That(SEntMan.HasComponent<ANPRCRelayAnchorComponent>(pack), Is.False);
        });
    }

    [TestCase("RMCM5Bayonet", false, false)]
    [TestCase("RMCM5Bayonet", true, false)]
    [TestCase("RMCKitchenKnife", false, false)]
    [TestCase("RMCM5Bayonet", false, true)]
    public async Task HumansNeedHeldSharpToolToRipWires(string prototype, bool targetWearer, bool drop)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid tool = default;
        EntityUid pack = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            tool = SEntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, tool), Is.True);
            var target = pack;
            if (targetWearer)
            {
                target = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.AddComponent<WearingANPRCComponent>(target).Radio = pack;
            }
            var verbs = new GetVerbsEvent<Verb>(user, target, tool,
                SEntMan.GetComponent<HandsComponent>(user), true, true, true, new());
            SEntMan.EventBus.RaiseLocalEvent(target, verbs);
            verbs.Verbs.Single(verb => verb.Text == "Rip out radio wires").Act!();
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ANPRCRadioComponent>(pack).WiresRipped, Is.False);
            if (drop)
                Assert.That(Server.System<SharedHandsSystem>().TryDrop(user, tool), Is.True);
        });
        await Pair.RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<ANPRCRadioComponent>(pack).WiresRipped, Is.EqualTo(!drop)));
    }

    [TestCase(null)]
    [TestCase("RMCCableCoil30")]
    public async Task HumansWithoutSharpToolHaveNoSabotageVerb(string? prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            EntityUid? tool = null;
            if (prototype != null)
            {
                tool = SEntMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, tool.Value), Is.True);
            }
            var verbs = new GetVerbsEvent<Verb>(user, pack, tool,
                SEntMan.GetComponent<HandsComponent>(user), true, true, true, new());
            SEntMan.EventBus.RaiseLocalEvent(pack, verbs);
            Assert.That(verbs.Verbs.Any(verb => verb.Text == "Rip out radio wires"), Is.False);
        });
    }

    [TestCase(true, 0, 5, true)]
    [TestCase(false, 1, 5, true)]
    [TestCase(false, 0, 5, false)]
    [TestCase(true, 0, 4, false)]
    [TestCase(false, 1, 4, false)]
    [TestCase(true, 0, 8, true)]
    public async Task RepairsRequireTrainingAndFiveCable(bool rto, int engineering, int amount, bool repaired)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            if (rto)
                SEntMan.AddComponent<ANPRCRadioUserComponent>(user);
            if (engineering > 0)
                Server.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", engineering);
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            var radio = SEntMan.GetComponent<ANPRCRadioComponent>(pack);
            radio.WiresRipped = true;
            radio.Enabled = false;
            var cable = SEntMan.SpawnEntity("RMCCableCoil30", map.GridCoords);
            Server.System<SharedStackSystem>().SetCount((cable, SEntMan.GetComponent<StackComponent>(cable)), amount);
            var ev = new InteractUsingEvent(user, cable, pack, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(pack, ev);
            Assert.That(ev.Handled, Is.True);
            Assert.That(radio.WiresRipped, Is.EqualTo(!repaired));
            Assert.That(radio.Enabled, Is.False, "repair should leave the power switch off");
            if (!repaired)
                Assert.That(SEntMan.GetComponent<StackComponent>(cable).Count, Is.EqualTo(amount));
            else if (amount == 5)
                Assert.That(SEntMan.Deleted(cable) || SEntMan.GetComponent<StackComponent>(cable).Count == 0, Is.True);
            else
                Assert.That(SEntMan.GetComponent<StackComponent>(cable).Count, Is.EqualTo(amount - 5));
        });
    }

    [Test]
    public async Task BrokenPackCannotCreateCasCalls()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            SEntMan.AddComponent<WearingANPRCComponent>(user).Radio = pack;
            SEntMan.AddComponent<MarineComponent>(user).Faction = "govfor";
            SEntMan.GetComponent<ANPRCRadioComponent>(pack).WiresRipped = true;
            Assert.That(Server.System<CMUCasRequestSystem>().TryCreatePositionRequest(user, 0, 0,
                CMUCasMunition.Any, CMUCasEffect.Any, "", out _), Is.False);
            Assert.That(Server.System<CMUCasRequestSystem>().TryCreateGpsRequest(user, 1, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);
        });
    }
}

[RegisterComponent]
public sealed partial class PositionRadioProbeComponent : Component
{
    public readonly List<string> Messages = new();
    public readonly List<string> Channels = new();
}

public sealed class PositionRadioProbeSystem : EntitySystem
{
    public override void Initialize() =>
        SubscribeLocalEvent<PositionRadioProbeComponent, RadioSendAttemptEvent>(OnTransmission);

    private static void OnTransmission(Entity<PositionRadioProbeComponent> ent, ref RadioSendAttemptEvent args)
    {
        ent.Comp.Messages.Add(args.Message);
        ent.Comp.Channels.Add(args.Channel.ID);
    }
}
