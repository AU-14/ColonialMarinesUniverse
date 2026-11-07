#pragma warning disable RA0002 // Arrange faction ownership explicitly for authorization regressions.

using System.Reflection;
using Content.Client.Options.UI.Tabs;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ForceOnForce;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Presets;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Roles;
using Content.Shared._RMC14.Rules;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14.util;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface.Controls;
using ServerDropshipSystem = Content.Server._RMC14.Dropship.DropshipSystem;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceGameplayTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<GamePresetPrototype> ForceOnForcePrototype = "ForceOnForce";
    private static readonly Robust.Shared.Prototypes.ProtoId<GamePresetPrototype> DistressSignalPrototype = "DistressSignal";




    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task BombardmentRequiresOwnCommandConsoleHonorsCooldownAndCannotDamageTheGroup()
    {
        var map = await Pair.CreateTestMap();
        var occupants = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>(ForceOnForcePrototype));
            // Surface resolution receives map-relative coordinates, so mark the map itself.
            var planetMap = SEntMan.GetComponent<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value;
            SEntMan.EnsureComponent<RMCPlanetComponent>(planetMap);
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.GridCoords.EntityId);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["RMCFloorVehicleInteriorDarkSterile"].TileId);
            for (var x = -20; x <= 20; x++)
            for (var y = -20; y <= 20; y++) maps.SetTile(map.GridCoords.EntityId, grid, new Vector2i(x, y), floor);
            foreach (var (faction, position) in new[] { ("opfor", Vector2.Zero), ("govfor", new Vector2(9, 0)), ("neutral", new Vector2(-7, 0)) })
            {
                var mob = SEntMan.SpawnEntity(null, map.GridCoords.Offset(position));
                SEntMan.EnsureComponent<MarineComponent>(mob).Faction = faction;
                SEntMan.EnsureComponent<MobStateComponent>(mob);
                SEntMan.EnsureComponent<DamageableComponent>(mob);
                occupants.Add(mob);
            }
            var console = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(30, 0)));
            var comms = SEntMan.EnsureComponent<MarineCommunicationsComputerComponent>(console);
            comms.Faction = "govfor";
            var user = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(30, 0)));
            SEntMan.EnsureComponent<MarineComponent>(user).Faction = "govfor";
            var bombardment = Server.System<ForceOnForceBombardmentSystem>();
            int Active() => ((System.Collections.ICollection) typeof(ForceOnForceBombardmentSystem)
                .GetField("_barrages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bombardment)!).Count;
            void Request(int variant = 0) => SEntMan.EventBus.RaiseLocalEvent(console,
                new ForceOnForceBombardmentMessage(variant) { Actor = user, UiKey = MarineCommunicationsComputerUI.Key });
            Request();
            Assert.That(Active(), Is.Zero, "ordinary marines cannot order a bombardment");
            var rank = SProtoMan.EnumeratePrototypes<RankPrototype>().First(r => r.Paygrade?.StartsWith("O") == true);
            Server.System<SharedRankSystem>().SetRank(user, rank);
            comms.Faction = "opfor";
            Request();
            Assert.That(Active(), Is.Zero, "enemy command consoles cannot be used");
            comms.Faction = "govfor";
            Request(4);
            Assert.That(Active(), Is.Zero, "invalid variant messages are rejected");
            Request();
            Request();
            Assert.That(Active(), Is.EqualTo(1), "the cooldown is shared by the faction and enforced on the server");
            Assert.That(SEntMan.QueryEntities<FighterStrikeVisualComponent>().Any(), Is.False, "effects wait for the siren warning");
        });
        await Pair.RunSeconds(9);
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var strikes = SEntMan.QueryEntities<FighterStrikeVisualComponent>().ToArray();
            Assert.That(strikes, Is.Not.Empty);
            foreach (var strike in strikes)
            foreach (var occupant in occupants)
                Assert.That(Vector2.Distance(transform.GetWorldPosition(strike.Owner), transform.GetWorldPosition(occupant)),
                    Is.GreaterThanOrEqualTo(6), "every impact must clear every nearby unit");
            foreach (var occupant in occupants)
                Assert.That(SEntMan.GetComponent<DamageableComponent>(occupant).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [TestCase("govfor", "opfor")]
    [TestCase(null, null)]
    public async Task RandomHijackCandidatesExcludeTheHumanAttackersOwnCarrier(string? attacker, string? enemy)
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>(ForceOnForcePrototype));
            var maps = Server.System<SharedMapSystem>();
            var expected = new List<EntityUid>();
            foreach (var faction in new[] { "govfor", "opfor", "unowned" })
            {
                var map = maps.CreateMap();
                if (faction != "unowned") SEntMan.EnsureComponent<ShipFactionComponent>(map).Faction = faction;
                for (var i = 0; i < 3; i++)
                {
                    var marker = SEntMan.SpawnEntity(null, new EntityCoordinates(map, i, 0));
                    SEntMan.EnsureComponent<DropshipHijackDestinationComponent>(marker);
                    if (faction != "unowned" && (enemy == null || faction == enemy)) expected.Add(marker);
                }
            }
            var user = SEntMan.Spawn();
            if (attacker != null) SEntMan.EnsureComponent<MarineComponent>(user).Faction = attacker;
            SEntMan.EnsureComponent<DropshipHijackerComponent>(user);
            var candidates = (List<EntityUid>) typeof(SharedDropshipSystem)
                .GetMethod("GetHijackDestinations", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Server.System<ServerDropshipSystem>(), [user])!;
            Assert.That(candidates, Is.EquivalentTo(expected),
                "The random draw must contain only valid carrier markers and humans must only attack the opposing carrier.");
        });
    }

    [Test]
    public async Task ActingLeadersAndOfficersCanHijackOnlyTheEnemyConsoleInFoF()
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>(ForceOnForcePrototype));
            var factions = Server.System<ForceOnForceSystem>();
            var dropships = Server.System<ServerDropshipSystem>();
            var eligibility = typeof(ServerDropshipSystem).GetMethod("IsForceOnForceHijacker", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var user = SEntMan.Spawn();
            SEntMan.EnsureComponent<MarineComponent>(user).Faction = "govfor";
            var console = SEntMan.Spawn();
            var whitelist = SEntMan.EnsureComponent<WhitelistedShuttleComponent>(console);
            whitelist.Faction = "opfor";
            bool CanHijack() => (bool) eligibility.Invoke(dropships, [console, user])!;

            Assert.That(CanHijack(), Is.False, "an ordinary marine cannot initiate a hijack");
            SEntMan.EnsureComponent<SquadLeaderComponent>(user);
            Assert.That(CanHijack(), Is.True, "an acting squad leader has the same hijack authority as an SL");
            Assert.That(factions.CanCommand(user), Is.False, "squad authority alone cannot order bombardment");
            whitelist.Faction = "govfor";
            Assert.That(CanHijack(), Is.False, "a leader cannot hijack their own console");
            whitelist.Faction = "opfor";
            SEntMan.RemoveComponent<SquadLeaderComponent>(user);
            var officer = SProtoMan.EnumeratePrototypes<RankPrototype>().First(r => r.Paygrade?.StartsWith("O") == true);
            Server.System<SharedRankSystem>().SetRank(user, officer);
            Assert.That(CanHijack(), Is.True);
            Assert.That(factions.CanCommand(user), Is.True);
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>(DistressSignalPrototype));
            Assert.That(CanHijack(), Is.False);
        });
    }

    [Test]
    public async Task NoHijackInitiationIsAcceptedInForceOnForce()
    {
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipHijackInitialDelayMinutes, 0);
            var maps = Server.System<SharedMapSystem>();
            var ship = maps.CreateMap();
            var computer = SEntMan.SpawnEntity(null, new EntityCoordinates(ship, 0, 0));
            var user = SEntMan.SpawnEntity(null, new EntityCoordinates(ship, 1, 0));
            SEntMan.EnsureComponent<DropshipHijackerComponent>(user);
            var dropships = Server.System<ServerDropshipSystem>();
            var popup = typeof(SharedDropshipSystem)
                .GetMethod("TryDropshipHijackPopup", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var ticker = Server.System<GameTicker>();
            var currentPreset = typeof(GameTicker)
                .GetProperty(nameof(GameTicker.CurrentPreset))!;
            var originalPreset = (GamePresetPrototype?) currentPreset.GetValue(ticker);
            void SetPreset(Robust.Shared.Prototypes.ProtoId<GamePresetPrototype> id) => currentPreset
                .SetValue(ticker, SProtoMan.Index<GamePresetPrototype>(id));
            bool Accepted() => (bool) popup.Invoke(dropships,
            [
                computer,
                Activator.CreateInstance(typeof(Entity<DropshipHijackerComponent?>), user, null)!,
                false
            ])!;

            SetPreset(DistressSignalPrototype);
            Assert.That(Accepted(), Is.True, "a hijacker can still initiate outside Force on Force");

            SetPreset(ForceOnForcePrototype);
            Assert.That(Accepted(), Is.False, "Force on Force accepts no hijack initiation, whatever the hijacker type");

            // Leave the pooled ticker as found: CurrentPreset drives other systems' behavior.
            currentPreset.SetValue(ticker, originalPreset);
        });
    }

    [TestCase("govfor", "opfor")]
    public async Task OrdinaryNavigationRejectsEnemyCarrierAndControlledLandingZone(string own, string enemy)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var carrier = maps.CreateMap();
            SEntMan.EnsureComponent<ShipFactionComponent>(carrier).Faction = enemy;
            var destination = SEntMan.SpawnEntity(null, new EntityCoordinates(carrier, 0, 0));
            var landing = SEntMan.EnsureComponent<DropshipDestinationComponent>(destination);
            var console = SEntMan.Spawn();
            var nav = SEntMan.EnsureComponent<DropshipNavigationComputerComponent>(console);
            SEntMan.EnsureComponent<WhitelistedShuttleComponent>(console).Faction = own;
            var dropships = Server.System<ServerDropshipSystem>();
            var canLand = typeof(ServerDropshipSystem).GetMethod("CanLandAt", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool Allowed() => (bool) canLand.Invoke(dropships, [console, destination])!;
            Assert.That(Allowed(), Is.False, "an unlabelled marker on an enemy carrier still rejects ordinary landings");
            Assert.That(dropships.FlyTo((console, nav), destination, null), Is.False);
            SEntMan.GetComponent<ShipFactionComponent>(carrier).Faction = own;
            Assert.That(Allowed(), Is.True);
            landing.FactionController = enemy;
            Assert.That(Allowed(), Is.False, "the destination controller is authoritative too");
            landing.FactionController = own;
            Assert.That(Allowed(), Is.True);
        });
    }

    [TestCase("govfor", RoundJobSide.Govfor, RoundJobSide.Opfor)]
    public async Task RespawnsStayOnTheirOriginalSideUntilTheRoundResets(string faction, RoundJobSide own, RoundJobSide enemy)
    {
        var map = await Pair.CreateTestMap();
        var prefMan = Server.ResolveDependency<IServerPreferencesManager>();
        var originalProfile = prefMan.GetPreferences(ServerSession!.UserId).Characters[0];
        await Server.WaitPost(() => prefMan.SetProfile(ServerSession!.UserId, 0,
            ((HumanoidCharacterProfile) originalProfile).WithSex(Sex.Male).WithAge(25)
                .WithHeight("5'10\"").WithWeight(160)).Wait());
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>(ForceOnForcePrototype));
            var respawn = Server.System<ForceOnForceRespawnSystem>();
            var player = ServerSession!.UserId;
            var body = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var marine = SEntMan.EnsureComponent<MarineComponent>(body);
            marine.Faction = faction;
            void Spawned() => SEntMan.EventBus.RaiseLocalEvent(body, new PlayerSpawnCompleteEvent(body, ServerSession,
                null, false, true, 0, map.Grid.Owner, HumanoidCharacterProfile.DefaultWithSpecies()), true);
            Assert.That(respawn.CanJoinSide(player, own), Is.True);
            Assert.That(respawn.CanJoinSide(player, enemy), Is.True);
            Spawned();
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(player);
            minds.TransferTo(mind, body);
            Server.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
            minds.TransferTo(mind, SEntMan.Spawn());
            Assert.That(respawn.HasDied(player), Is.True);
            Assert.That(respawn.CanJoinSide(player, own), Is.True);
            Assert.That(respawn.CanJoinSide(player, enemy), Is.False, "death and ghosting do not reset the account's side");
            var banned = new HashSet<ProtoId<JobPrototype>>();
            var ev = new GetDisallowedJobsEvent(ServerSession, banned);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            Assert.That(banned.Contains(own == RoundJobSide.Govfor ? "AU14JobGOVFORSquadRifleman" : "AU14JobOPFORSquadRifleman"), Is.False);
            Assert.That(banned.Contains(enemy == RoundJobSide.Govfor ? "AU14JobGOVFORSquadRifleman" : "AU14JobOPFORSquadRifleman"), Is.True);
            marine.Faction = faction == "govfor" ? "opfor" : "govfor";
            Spawned();
            Assert.That(respawn.CanJoinSide(player, enemy), Is.False, "another spawn event must not replace the first side");
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(respawn.HasLockedSide(player), Is.False);
            Assert.That(respawn.CanJoinSide(player, enemy), Is.True, "a new round allows a fresh choice");
        });

        await Server.WaitPost(() => prefMan.SetProfile(ServerSession!.UserId, 0, originalProfile).Wait());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GibbingStartsTheRespawnWaitWithoutRestartingAnExistingWait(bool alreadyDead)
    {
        var map = await Pair.CreateTestMap();
        var player = ServerSession!.UserId;
        EntityUid body = default;
        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(player);
            minds.TransferTo(mind, body);
            Assert.That(Server.System<MobStateSystem>().IsAlive(body), Is.True);
            if (alreadyDead)
                Server.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
        });
        if (alreadyDead)
            await Pair.RunSeconds(60);
        await Server.WaitAssertion(() =>
        {
            var respawn = Server.System<ForceOnForceRespawnSystem>();
            var expected = alreadyDead ? respawn.Remaining(player) : ForceOnForceRespawnSystem.RespawnDelay;
            Assert.That(expected, Is.GreaterThan(TimeSpan.Zero));
            if (alreadyDead)
                Assert.That(expected, Is.LessThan(ForceOnForceRespawnSystem.RespawnDelay));
            Server.System<Content.Shared.Gibbing.GibbingSystem>().Gib(body);
            Assert.That(respawn.HasDied(player), Is.True, "Dropship gibbing must count as death even without a dead mob-state transition.");
            Assert.That(respawn.Remaining(player), Is.EqualTo(expected));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(body), Is.True);
            Assert.That(Server.System<ForceOnForceRespawnSystem>().HasDied(player), Is.True,
                "Deleting the gibbed body must retain the account's death record.");
        });
    }

    [Test]
    public async Task DeathWaitSurvivesGhostingAndRevivalStartsAFreshWait()
    {
        var player = ServerSession!.UserId;
        var respawn = Server.System<ForceOnForceRespawnSystem>();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.Spawn();
            SEntMan.EnsureComponent<MobStateComponent>(body);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(player);
            minds.TransferTo(mind, body);
            var states = Server.System<MobStateSystem>();
            states.ChangeMobState(body, MobState.Dead);
            Assert.That(respawn.Remaining(player), Is.EqualTo(TimeSpan.FromMinutes(5)));
            states.ChangeMobState(body, MobState.Alive);
            Assert.That(respawn.HasDied(player), Is.False);
            states.ChangeMobState(body, MobState.Dead);
            Assert.That(respawn.Remaining(player), Is.EqualTo(TimeSpan.FromMinutes(5)));
            minds.TransferTo(mind, SEntMan.Spawn());
            Assert.That(respawn.Remaining(player), Is.EqualTo(TimeSpan.FromMinutes(5)), "leaving the body cannot clear the account's wait");
        });
        await Pair.RunSeconds(299);
        await Server.WaitAssertion(() => Assert.That(respawn.Remaining(player), Is.GreaterThan(TimeSpan.Zero)));
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() => Assert.That(respawn.Remaining(player), Is.EqualTo(TimeSpan.Zero)));
    }

}
