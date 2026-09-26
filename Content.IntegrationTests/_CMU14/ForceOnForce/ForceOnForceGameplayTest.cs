#pragma warning disable RA0002 // Arrange faction ownership explicitly for authorization regressions.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ForceOnForce;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Roles;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using ServerDropshipSystem = Content.Server._RMC14.Dropship.DropshipSystem;

namespace Content.IntegrationTests._CMU14.ForceOnForce;

[TestFixture]
public sealed class ForceOnForceGameplayTest : GameTest
{
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
                SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
            // Surface resolution receives map-relative coordinates, so mark the map itself.
            var planetMap = SEntMan.GetComponent<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value;
            SEntMan.EnsureComponent<RMCPlanetComponent>(planetMap);
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.GridCoords.EntityId);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["RMCFloorVehicleInteriorDarkSterile"].TileId);
            for (var x = -20; x <= 20; x++)
            for (var y = -20; y <= 20; y++) maps.SetTile(grid.Owner, grid, new Vector2i(x, y), floor);
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
            Assert.That(SEntMan.EntityQuery<FighterStrikeVisualComponent>().Any(), Is.False, "effects wait for the siren warning");
        });
        await Pair.RunSeconds(9);
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var strikes = SEntMan.EntityQuery<FighterStrikeVisualComponent>().ToArray();
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
    [TestCase("opfor", "govfor")]
    [TestCase(null, null)]
    public async Task RandomHijackCandidatesExcludeTheHumanAttackersOwnCarrier(string attacker, string enemy)
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
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
                SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
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
                SProtoMan.Index<GamePresetPrototype>("DistressSignal"));
            Assert.That(CanHijack(), Is.False);
        });
    }

    [TestCase("govfor", "opfor")]
    [TestCase("opfor", "govfor")]
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

    [Test]
    public async Task UniformRecognitionUpdatesWhenStrippedOrWearingEnemyClothing()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var policy = SEntMan.EnsureComponent<ForceOnForceUniformComponent>(user);
            policy.Uniforms.UnionWith(SProtoMan.Index<ForceOnForceUniformPrototype>("USCM").Uniforms);
            var recognition = Server.System<ForceOnForceUniformSystem>();
            var inventory = Server.System<InventorySystem>();
            Assert.That(recognition.IsUnidentified(user), Is.True);
            var own = SEntMan.SpawnEntity("AU14DesertFatigues", map.GridCoords);
            Assert.That(inventory.TryEquip(user, own, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(user), Is.False);
            Assert.That(inventory.TryUnequip(user, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(user), Is.True);
            var enemy = SEntMan.SpawnEntity("AU14FatiguesUPP", map.GridCoords);
            Assert.That(inventory.TryEquip(user, enemy, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(user), Is.True);
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

    [TestCase(ForceOnForceFallback.StayInLobby, false, false)]
    [TestCase(ForceOnForceFallback.OtherSide, true, false)]
    [TestCase(ForceOnForceFallback.OtherRole, false, true)]
    [TestCase(ForceOnForceFallback.Both, true, true)]
    public async Task FallbackOnlyPermitsTheSelectedSideAndRoleChanges(ForceOnForceFallback fallback, bool side, bool role)
    {
        await Server.WaitAssertion(() =>
        {
            var jobs = Server.System<StationJobsSystem>();
            var costMethod = typeof(StationJobsSystem).GetMethod("GetForceOnForceJobCost", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithForceOnForcePreferences(ForceOnForceSide.Govfor, fallback)
                .WithGamemodeJobPriority("ForceOnForce", "AU14JobGOVFORSquadSergeant", JobPriority.High);
            bool Accepted(string id) => costMethod.Invoke(jobs, [profile, SProtoMan.Index<JobPrototype>(id)]) != null;
            Assert.That(Accepted("AU14JobGOVFORSquadSergeant"), Is.True);
            Assert.That(Accepted("AU14JobOPFORSquadSergeant"), Is.EqualTo(side));
            Assert.That(Accepted("AU14JobGOVFORSquadRifleman"), Is.EqualTo(role));
            Assert.That(Accepted("AU14JobOPFORSquadRifleman"), Is.EqualTo(side && role));
        });
    }
}
