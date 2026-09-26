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
using Content.Server.Station.Systems;
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

    [TestCase("govfor", "opfor", "USCM", "UPP", "AU14DesertFatigues", "AU14FatiguesUPP")]
    [TestCase("opfor", "govfor", "UPP", "USCM", "AU14FatiguesUPP", "AU14DesertFatigues")]
    public async Task UniformMarkerUsesTheViewersFactionAndUniforms(
        string faction, string otherFaction, string platoon, string otherPlatoon, string ownUniform, string otherUniform)
    {
        var map = await Pair.CreateTestMap();
        EntityUid viewer = default;
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            viewer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var viewerMarine = SEntMan.EnsureComponent<MarineComponent>(viewer);
            viewerMarine.Faction = faction;
            SEntMan.Dirty(viewer, viewerMarine);
            var policy = SEntMan.EnsureComponent<ForceOnForceUniformComponent>(viewer);
            policy.Uniforms.UnionWith(SProtoMan.Index<ForceOnForceUniformPrototype>(platoon).Uniforms);
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var targetMarine = SEntMan.EnsureComponent<MarineComponent>(target);
            targetMarine.Faction = otherFaction;
            var targetPolicy = SEntMan.EnsureComponent<ForceOnForceUniformComponent>(target);
            targetPolicy.Uniforms.UnionWith(SProtoMan.Index<ForceOnForceUniformPrototype>(otherPlatoon).Uniforms);
            var recognition = Server.System<ForceOnForceUniformSystem>();
            var inventory = Server.System<InventorySystem>();
            Assert.That(recognition.IsUnidentified(target, viewer), Is.True, "missing uniforms are unfamiliar");
            Assert.That(recognition.IsUnidentified(viewer, viewer), Is.False, "the viewer is never marked");
            Assert.That(recognition.IsUnidentified(target, null), Is.False, "spectators have no faction uniform policy");
            var own = SEntMan.SpawnEntity(ownUniform, map.GridCoords);
            Assert.That(inventory.TryEquip(target, own, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(target, viewer), Is.False, "recognize the viewer's uniforms on other factions");
            Assert.That(inventory.TryUnequip(target, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(target, viewer), Is.True);
            var enemy = SEntMan.SpawnEntity(otherUniform, map.GridCoords);
            Assert.That(inventory.TryEquip(target, enemy, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(target, viewer), Is.True, "the target's own whitelist does not identify them to the viewer");
            targetMarine.Faction = faction.ToUpperInvariant();
            Assert.That(recognition.IsUnidentified(target, viewer), Is.False, "teammates keep identifiers even in foreign uniforms");
            Assert.That(inventory.TryUnequip(target, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(recognition.IsUnidentified(target, viewer), Is.False, "teammates without uniforms are never marked");
        });
    }

    [TestCase("govfor", "opfor", "USCM", "UPP", "CMJumpsuitXOFormal", "AU14FatiguesUPP")]
    [TestCase("opfor", "govfor", "UPP", "USCM", "AU14ServiceFatiguesUPP", "AU14CamoUSCMFatigues")]
    public async Task UniformMarkerRecognizesVendorStockOnTheClient(
        string faction, string enemyFaction, string platoon, string enemyPlatoon, string ownUniform, string enemyUniform)
    {
        var map = await Pair.CreateTestMap();
        EntityUid viewer = default;
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            platoons.SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>(faction == "govfor" ? platoon : enemyPlatoon);
            platoons.SelectedOpforPlatoon = SProtoMan.Index<PlatoonPrototype>(faction == "opfor" ? platoon : enemyPlatoon);
            viewer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var viewerMarine = SEntMan.EnsureComponent<MarineComponent>(viewer);
            viewerMarine.Faction = faction;
            SEntMan.Dirty(viewer, viewerMarine);
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(target).Faction = enemyFaction;
            SEntMan.EventBus.RaiseLocalEvent(viewer, new PlayerSpawnCompleteEvent(viewer, ServerSession!, null,
                false, true, 0, map.Grid.Owner, HumanoidCharacterProfile.DefaultWithSpecies()), true);
            var policy = SEntMan.GetComponent<ForceOnForceUniformComponent>(viewer);
            Assert.That(policy.Uniforms.Contains(ownUniform), Is.True, "include uniforms from the resolved vendors");
            Assert.That(policy.Uniforms.Contains(enemyUniform), Is.False, "enemy vendor stock stays unfamiliar");
            Server.PlayerMan.SetAttachedEntity(ServerSession!, viewer);
        });

        try
        {
            foreach (var (uniform, friendly, state, expected) in new (string, bool, MobState, bool)[]
                     {
                         (enemyUniform, false, MobState.Alive, true),
                         (ownUniform, false, MobState.Alive, false),
                         (enemyUniform, true, MobState.Alive, false),
                         (null, true, MobState.Alive, false),
                         (null, false, MobState.Alive, true),
                         (enemyUniform, false, MobState.Dead, false),
                         (null, false, MobState.Dead, false),
                         (enemyUniform, false, MobState.Alive, true),
                     })
            {
                await Server.WaitAssertion(() =>
                {
                    var inventory = Server.System<InventorySystem>();
                    if (inventory.TryGetSlotEntity(target, "jumpsuit", out _))
                        Assert.That(inventory.TryUnequip(target, "jumpsuit", silent: true, force: true), Is.True);
                    if (uniform != null)
                        Assert.That(inventory.TryEquip(target, SEntMan.SpawnEntity(uniform, map.GridCoords),
                            "jumpsuit", silent: true, force: true), Is.True);
                    var marine = SEntMan.GetComponent<MarineComponent>(target);
                    marine.Faction = friendly ? faction.ToUpperInvariant() : enemyFaction;
                    SEntMan.Dirty(target, marine);
                    Server.System<MobStateSystem>().ChangeMobState(target, state);
                });
                await Pair.RunTicksSync(5);
                await Pair.RunUntilSynced();
                await Client.WaitAssertion(() =>
                {
                    Assert.That(CEntMan.GetComponent<MarineComponent>(ToClientUid(viewer)).Faction,
                        Is.EqualTo(faction), "replicated viewer faction");
                    Assert.That(CEntMan.GetComponent<MarineComponent>(ToClientUid(target)).Faction,
                        Is.EqualTo(friendly ? faction.ToUpperInvariant() : enemyFaction), "replicated target faction");
                    Assert.That(
                    Client.System<ForceOnForceUniformSystem>().IsUnidentified(ToClientUid(target), ToClientUid(viewer)),
                    Is.EqualTo(expected), $"client recognition: uniform={uniform}, friendly={friendly}, state={state}");
                });
            }
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, null));
        }
    }

    [Test]
    public async Task UniformMarkerSettingDefaultsOffAndCanBeChangedLocally()
    {
        await Client.WaitAssertion(() =>
        {
            var configuration = Client.ResolveDependency<IConfigurationManager>();
            Assert.That(configuration.GetCVar(CCVars.ForceOnForceUnidentifiedMarkerEnabled), Is.False);
            using var tab = new CmuTab();
            var checkbox = tab.FindControl<CheckBox>("FoFUnidentifiedMarkerCheckBox");
            Assert.That(checkbox.Pressed, Is.False);
            checkbox.Pressed = true;
            tab.Control.ApplyChanges();
            Assert.That(configuration.GetCVar(CCVars.ForceOnForceUnidentifiedMarkerEnabled), Is.True);
            tab.Control.ReloadValues();
            Assert.That(checkbox.Pressed, Is.True);
            checkbox.Pressed = false;
            tab.Control.ApplyChanges();
            Assert.That(configuration.GetCVar(CCVars.ForceOnForceUnidentifiedMarkerEnabled), Is.False);
        });
    }

    [TestCase("govfor", RoundJobSide.Govfor, RoundJobSide.Opfor)]
    [TestCase("opfor", RoundJobSide.Opfor, RoundJobSide.Govfor)]
    public async Task RespawnsStayOnTheirOriginalSideUntilTheRoundResets(string faction, RoundJobSide own, RoundJobSide enemy)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var ticker = Server.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!.SetValue(ticker,
                SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
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

    [TestCase(ForceOnForceSide.Govfor, true, false)]
    [TestCase(ForceOnForceSide.Opfor, false, true)]
    [TestCase(ForceOnForceSide.Either, true, true)]
    public async Task UnifiedRoleListHonorsThePreferredSideWithoutRequiringSideFallback(
        ForceOnForceSide side, bool govfor, bool opfor)
    {
        await Server.WaitAssertion(() =>
        {
            var jobs = Server.System<StationJobsSystem>();
            var costMethod = typeof(StationJobsSystem).GetMethod("GetForceOnForceJobCost", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithForceOnForcePreferences(side, ForceOnForceFallback.StayInLobby)
                .WithForceOnForceJobPriority(SProtoMan.Index<JobPrototype>("AU14JobGOVFORSquadSergeant"), JobPriority.High, SProtoMan);
            bool Accepted(string id) => costMethod.Invoke(jobs, [profile, SProtoMan.Index<JobPrototype>(id)]) != null;
            Assert.That(Accepted("AU14JobGOVFORSquadSergeant"), Is.EqualTo(govfor));
            Assert.That(Accepted("AU14JobOPFORSquadSergeant"), Is.EqualTo(opfor));
            Assert.That(Accepted("AU14JobGOVFORSquadRifleman"), Is.False);
            Assert.That(Accepted("AU14JobOPFORSquadRifleman"), Is.False);
        });
    }

    [Test]
    public async Task UnifiedRoleListReadsAndReplacesLegacyOppositionPreferences()
    {
        await Server.WaitAssertion(() =>
        {
            var govfor = SProtoMan.Index<JobPrototype>("AU14JobGOVFORSquadSergeant");
            var opfor = SProtoMan.Index<JobPrototype>("AU14JobOPFORSquadSergeant");
            var rifleman = SProtoMan.Index<JobPrototype>("AU14JobGOVFORSquadRifleman");
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithGamemodeJobPriority("ForceOnForce", opfor.ID, JobPriority.High)
                .WithGamemodeJobPriority("ForceOnForce", rifleman.ID, JobPriority.Medium)
                .WithGamemodeJobPriority("DistressSignal", govfor.ID, JobPriority.Low);
            Assert.That(profile.GetForceOnForceJobPriority(govfor, SProtoMan), Is.EqualTo(JobPriority.High));

            var lowered = profile.WithForceOnForceJobPriority(govfor, JobPriority.Low, SProtoMan);
            Assert.That(lowered.GetForceOnForceJobPriority(govfor, SProtoMan), Is.EqualTo(JobPriority.Low));
            Assert.That(lowered.GetForceOnForceJobPriority(opfor, SProtoMan), Is.EqualTo(JobPriority.Low));
            Assert.That(lowered.GetForceOnForceJobPriority(rifleman, SProtoMan), Is.EqualTo(JobPriority.Medium));
            var cleared = new HumanoidCharacterProfile(lowered.WithForceOnForceJobPriority(govfor, JobPriority.Never, SProtoMan));
            Assert.That(cleared.GetForceOnForceJobPriority(govfor, SProtoMan), Is.EqualTo(JobPriority.Never));
            Assert.That(cleared.GetForceOnForceJobPriority(opfor, SProtoMan), Is.EqualTo(JobPriority.Never));
            Assert.That(cleared.GetJobPriorityForGamemode("DistressSignal", govfor.ID), Is.EqualTo(JobPriority.Low));

            var newHigh = profile.WithForceOnForceJobPriority(rifleman, JobPriority.High, SProtoMan);
            Assert.That(newHigh.GetForceOnForceJobPriority(govfor, SProtoMan), Is.EqualTo(JobPriority.Medium));
            Assert.That(newHigh.GetForceOnForceJobPriority(opfor, SProtoMan), Is.EqualTo(JobPriority.Medium));
            Assert.That(newHigh.GetForceOnForceJobPriority(rifleman, SProtoMan), Is.EqualTo(JobPriority.High));
            Assert.That(profile.GetForceOnForceJobPriority(govfor, SProtoMan), Is.EqualTo(JobPriority.High),
                "editing creates a new profile without mutating the saved one");
        });
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
