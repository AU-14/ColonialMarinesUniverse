using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Radio;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMURtoRadioTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

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
            Assert.That(Server.System<CMUCasRequestSystem>().TryCreateRadioMarkRequest(user, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);
        });
    }
}
