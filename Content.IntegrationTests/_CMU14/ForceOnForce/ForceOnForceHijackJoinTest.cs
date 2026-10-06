#pragma warning disable RA0002 // Arrange round ownership and flight phases for transport authorization.

using System.Collections;
using System.Reflection;
using Content.Client.CMU14.ForceOnForce;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ForceOnForce;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Localization;
using Robust.Shared.Timing;
using ServerJoinEui = Content.Server.CMU14.ForceOnForce.ForceOnForceHijackJoinEui;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceHijackJoinTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };
    private EntityUid _ship;
    private EntityUid _destination;
    private EntityUid _hijacker;
    private EntityUid _user;
    private EntityCoordinates _ground;
    private EntityCoordinates _attackerCarrier;
    private EntityCoordinates _defenderCarrier;
    private EntityCoordinates _boarding;
    private ForceOnForceHijackJoinSystem System => Server.System<ForceOnForceHijackJoinSystem>();
    private ServerJoinEui[] Offers => ((IDictionary) typeof(ForceOnForceHijackJoinSystem)
        .GetField("_offers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(System)!).Keys
        .Cast<ServerJoinEui>().ToArray();

    private async Task CreateWorld(string attacker, bool attacking)
    {
        var ground = await Pair.CreateTestMap();
        var friendly = await Pair.CreateTestMap();
        var enemy = await Pair.CreateTestMap();
        var ship = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(Server.System<GameTicker>(),
                SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
            _ground = ground.GridCoords;
            _attackerCarrier = friendly.GridCoords;
            _defenderCarrier = enemy.GridCoords;
            _boarding = ship.GridCoords;
            _ship = ship.Grid.Owner;
            SEntMan.EnsureComponent<RMCPlanetComponent>(ground.Grid.Owner);
            SEntMan.EnsureComponent<ShipFactionComponent>(friendly.Grid.Owner).Faction = attacker;
            SEntMan.EnsureComponent<ShipFactionComponent>(enemy.Grid.Owner).Faction = ForceOnForceSystem.Opponent(attacker);
            var dropship = SEntMan.EnsureComponent<DropshipComponent>(_ship);
            dropship.HijackerFaction = attacker;
            dropship.VictimFaction = ForceOnForceSystem.Opponent(attacker);
            var flight = SEntMan.EnsureComponent<FTLComponent>(_ship);
            flight.State = FTLState.Starting;
            flight.StateTime = StartEndTime.FromCurTime(Server.ResolveDependency<IGameTiming>(), 300);
            flight.TargetCoordinates = enemy.GridCoords;
            _destination = SEntMan.SpawnEntity(null, enemy.GridCoords);
            SEntMan.EnsureComponent<DropshipHijackDestinationComponent>(_destination);
            var spawn = SEntMan.SpawnEntity(null, enemy.GridCoords);
            SEntMan.EnsureComponent<SpawnPointComponent>(spawn).SpawnType = SpawnPointType.LateJoin;
            _hijacker = SEntMan.SpawnEntity("CMMobHuman", ship.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(_hijacker).Faction = attacker;
            _user = SEntMan.SpawnEntity("CMMobHuman", ground.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(_user).Faction = attacking ? attacker : ForceOnForceSystem.Opponent(attacker);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, _user);
        });
    }

    private void Offer()
    {
        var ev = new ForceOnForceHijackStartedEvent(_hijacker, _destination);
        SEntMan.EventBus.RaiseLocalEvent(_ship, ref ev);
    }

    [TestCase("govfor", true)]
    [TestCase("govfor", false)]
    [TestCase("opfor", true)]
    [TestCase("opfor", false)]
    public async Task BothSidesReceiveAChoiceAndOnlyJoinMovesThem(string attacker, bool attacking)
    {
        await CreateWorld(attacker, attacking);
        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.ResolveDependency<ILocalizationManager>().TryGetString(
                    "rmc-announcement-dropship-hijack-human", out var announcement), Is.True,
                    "the hijack alert must render text instead of its localization key");
                Assert.That(announcement, Does.Contain("hijacked a dropship"));
                var transform = Server.System<SharedTransformSystem>();
                Assert.That(System.IsEligible(_user, _ship, attacker), Is.True, "ground personnel are offered transport");
                transform.SetCoordinates(_user, _attackerCarrier);
                Assert.That(System.IsEligible(_user, _ship, attacker), Is.True, "both factions can join from the attackers' carrier");
                transform.SetCoordinates(_user, _defenderCarrier);
                Assert.That(System.IsEligible(_user, _ship, attacker), Is.False, "personnel already at the carrier fight stay there");
                transform.SetCoordinates(_user, _boarding);
                Assert.That(System.IsEligible(_user, _ship, attacker), Is.False, "do not prompt existing hijack passengers");
                transform.SetCoordinates(_user, _ground);
                Offer();
                Assert.That(Offers, Has.Length.EqualTo(1));
                Assert.That(((ForceOnForceHijackJoinState) Offers.Single().GetNewState()).Attacking, Is.EqualTo(attacking));
            });
            await Pair.RunTicksSync(5);
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var manager = Client.ResolveDependency<Content.Client.Eui.EuiManager>();
                var open = (IDictionary) typeof(Content.Client.Eui.EuiManager)
                    .GetField("_openUis", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
                Assert.That(open.Values.Cast<object>().Any(entry => entry.GetType().GetField("Eui")!
                    .GetValue(entry) is Content.Client.CMU14.ForceOnForce.ForceOnForceHijackJoinEui), Is.True,
                    "the real popup must reach the client");
            });
            await Server.WaitAssertion(() =>
            {
                Offers.Single().HandleMessage(new ForceOnForceHijackJoinMessage(false));
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(_ground.EntityId));
                Assert.That(Offers, Is.Empty);
                Offer();
                var ui = Offers.Single();
                ui.HandleMessage(new ForceOnForceHijackJoinMessage(true));
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(attacking ? _ship : _defenderCarrier.EntityId));
                Assert.That(Offers, Is.Empty);
                // A replay has no authority to move the player again.
                Server.System<SharedTransformSystem>().SetCoordinates(_user, _ground);
                System.Respond(ui, true);
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(_ground.EntityId));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, null));
        }
    }

    [Test]
    public async Task DeadChangedAndExpiredOffersCannotTeleport()
    {
        await CreateWorld("govfor", true);
        try
        {
            await Server.WaitAssertion(() =>
            {
                Offer();
                Server.System<MobStateSystem>().ChangeMobState(_user, MobState.Dead);
                Offers.Single().HandleMessage(new ForceOnForceHijackJoinMessage(true));
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(_ground.EntityId));
                Assert.That(System.IsEligible(_user, _ship, "govfor"), Is.False);
                Server.System<MobStateSystem>().ChangeMobState(_user, MobState.Alive);
                Offer();
                SEntMan.GetComponent<MarineComponent>(_user).Faction = "opfor";
                Offers.Single().HandleMessage(new ForceOnForceHijackJoinMessage(true));
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(_ground.EntityId));
                SEntMan.GetComponent<MarineComponent>(_user).Faction = "govfor";
                Offer();
                var previousBody = Offers.Single();
                Server.PlayerMan.SetAttachedEntity(ServerSession!, _hijacker);
                previousBody.HandleMessage(new ForceOnForceHijackJoinMessage(true));
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(_ground.EntityId));
                Server.PlayerMan.SetAttachedEntity(ServerSession!, _user);
                Offer();
                var expired = Offers.Single();
                SEntMan.RemoveComponent<FTLComponent>(_ship);
                System.Update(0);
                Assert.That(expired.IsShutDown, Is.True);
                Assert.That(Offers, Is.Empty);
                System.Respond(expired, true);
                Assert.That(Server.Transform(_user).GridUid, Is.EqualTo(_ground.EntityId));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, null));
        }
    }
}
