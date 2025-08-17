using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.AU14.Round;
using Content.Server.Humanoid;
using Content.Server.IdentityManagement;
using Content.Server.Jobs;
using Content.Server.Mind.Commands;
using Content.Server.PDA;
using Content.Server.Station.Components;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Station;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.AU14.util;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server.Station.Systems;

/// <summary>
/// Manages spawning into the game, tracking available spawn points.
/// Also provides helpers for spawning in the player's mob.
/// </summary>
[PublicAPI]
public sealed class StationSpawningSystem : SharedStationSpawningSystem
{
    [Dependency] private readonly SharedAccessSystem _accessSystem = default!;
    [Dependency] private readonly ActorSystem _actors = default!;
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly PdaSystem _pdaSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;
    [Dependency] private readonly SquadSystem _squadSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;

    /// <summary>
    /// Attempts to spawn a player character onto the given station.
    /// </summary>
    /// <param name="station">Station to spawn onto.</param>
    /// <param name="job">The job to assign, if any.</param>
    /// <param name="profile">The character profile to use, if any.</param>
    /// <param name="stationSpawning">Resolve pattern, the station spawning component for the station.</param>
    /// <returns>The resulting player character, if any.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    /// <remarks>
    /// This only spawns the character, and does none of the mind-related setup you'd need for it to be playable.
    /// </remarks>
    public EntityUid? SpawnPlayerCharacterOnStation(EntityUid? station, ProtoId<JobPrototype>? job, HumanoidCharacterProfile? profile, StationSpawningComponent? stationSpawning = null)
    {
        if (station != null && !Resolve(station.Value, ref stationSpawning))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        var ev = new PlayerSpawningEvent(job, profile, station);

        RaiseLocalEvent(ev);
        DebugTools.Assert(ev.SpawnResult is { Valid: true } or null);

        return ev.SpawnResult;
    }

    //TODO: Figure out if everything in the player spawning region belongs somewhere else.
    #region Player spawning helpers

    /// <summary>
    /// Spawns in a player's mob according to their job and character information at the given coordinates.
    /// Used by systems that need to handle spawning players.
    /// </summary>
    /// <param name="coordinates">Coordinates to spawn the character at.</param>
    /// <param name="job">Job to assign to the character, if any.</param>
    /// <param name="profile">Appearance profile to use for the character.</param>
    /// <param name="station">The station this player is being spawned on.</param>
    /// <param name="entity">The entity to use, if one already exists.</param>
    /// <returns>The spawned entity</returns>
    public EntityUid SpawnPlayerMob(
        EntityCoordinates coordinates,
        ProtoId<JobPrototype>? job,
        HumanoidCharacterProfile? profile,
        EntityUid? station,
        EntityUid? entity = null)
    {
        // --- Platoon job override logic start ---
        string? jobId = job?.ToString();
        var originalJob = job; // Store the original job before any override
        if (job != null)
        {
            if (!string.IsNullOrEmpty(jobId))
            {
                PlatoonPrototype? platoon = null;
                if (jobId.Contains("GOVFOR", StringComparison.OrdinalIgnoreCase))
                {
                    platoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon;
                }
                else if (jobId.Contains("Opfor", StringComparison.OrdinalIgnoreCase) || jobId.Contains("OPFOR", StringComparison.OrdinalIgnoreCase))
                {
                    platoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon;
                }

                // --- JobClassOverride logic: match by suffix ---
                if (platoon != null)
                {
                    foreach (var kvp in platoon.JobClassOverride)
                    {
                        // If the jobId ends with the enum name (e.g., AU14JobGOVFORSquadRifleman ends with SquadRifleman)
                        if (jobId.EndsWith(kvp.Key.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            job = kvp.Value;
                            break;
                        }
                    }
                }
            }
        }
        // --- Platoon job override logic end ---

        _prototypeManager.TryIndex(job ?? string.Empty, out var prototype, false);
        // Get the original job prototype for access/faction/ID
        _prototypeManager.TryIndex(originalJob ?? string.Empty, out var originalPrototype, false);
        RoleLoadout? loadout = null;

        // Need to get the loadout up-front to handle names if we use an entity spawn override.
        var jobLoadout = LoadoutSystem.GetJobPrototype(prototype?.ID);

        if (_prototypeManager.TryIndex(jobLoadout, out RoleLoadoutPrototype? roleProto))
        {
            profile?.Loadouts.TryGetValue(jobLoadout, out loadout);

            // Set to default if not present
            if (loadout == null)
            {
                loadout = new RoleLoadout(jobLoadout);
                loadout.SetDefault(profile, _actors.GetSession(entity), _prototypeManager);
            }
        }

        // RMC14 UseLoadoutOfJob
        if (prototype?.UseLoadoutOfJob != null && _prototypeManager.TryIndex(prototype.UseLoadoutOfJob, out var usedPrototype, false))
        {
            var newJobLoadout = LoadoutSystem.GetJobPrototype(usedPrototype.ID);

            if (_prototypeManager.TryIndex(newJobLoadout, out RoleLoadoutPrototype? newRoleProto))
            {
                if (profile != null && profile.Loadouts.TryGetValue(newJobLoadout, out var newLoadout))
                {
                    roleProto = newRoleProto;
                    loadout = newLoadout;
                }
            }
        }

        // If we're not spawning a humanoid, we're gonna exit early without doing all the humanoid stuff.
        if (prototype?.JobEntity != null)
        {
            DebugTools.Assert(entity is null);
            var jobEntity = Spawn(prototype.JobEntity, coordinates);
            MakeSentientCommand.MakeSentient(jobEntity, EntityManager);

            // Make sure custom names get handled, what is gameticker control flow whoopy.
            if (loadout != null)
            {
                EquipRoleName(jobEntity, loadout, roleProto!);
            }

            DoJobSpecials(job, jobEntity);
            // Use originalPrototype for access, ID, and faction
            _identity.QueueIdentityUpdate(jobEntity);
            if (originalPrototype != null && TryComp(jobEntity, out MetaDataComponent? metaDataJobEntity))
            {
                SetPdaAndIdCardData(jobEntity, metaDataJobEntity.EntityName, originalPrototype, station);
            }
            return jobEntity;
        }

        string speciesId = profile != null ? profile.Species : SharedHumanoidAppearanceSystem.DefaultSpecies;

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(speciesId, out var species))
            throw new ArgumentException($"Invalid species prototype was used: {speciesId}");

        entity ??= Spawn(species.Prototype, coordinates);

        // --- GOVFOR/OPFOR squad and team assignment ---
        string? team = null;
        bool assignToSquad = false;
        // Use the original jobId (before override) for team/faction logic
        string? teamCheckJobId = originalJob?.ToString();
        // hardcoding until I fix overwatch - EG
        if (!string.IsNullOrEmpty(teamCheckJobId))
        {
            if (teamCheckJobId.Contains("GOVFOR", StringComparison.OrdinalIgnoreCase))
            {
                team = "govfor";
                if (!teamCheckJobId.Contains("dcc", StringComparison.OrdinalIgnoreCase) &&
                    !teamCheckJobId.Contains("pilot", StringComparison.OrdinalIgnoreCase) &&
                    !teamCheckJobId.Contains("platco", StringComparison.OrdinalIgnoreCase))
                {
                    assignToSquad = true;
                }
            }
            else if (teamCheckJobId.Contains("Opfor", StringComparison.OrdinalIgnoreCase))
            {
                team = "opfor";
                if (!teamCheckJobId.Contains("dcc", StringComparison.OrdinalIgnoreCase) &&
                    !teamCheckJobId.Contains("pilot", StringComparison.OrdinalIgnoreCase) &&
                    !teamCheckJobId.Contains("platco", StringComparison.OrdinalIgnoreCase))
                {
                    assignToSquad = true;
                }
            }
        }

        // --- Ensure player has NpcFactionMemberComponent and is in correct faction ---
        // Moved faction assignment to after player is spawned
        // --- END GOVFOR/OPFOR faction ensure ---

        // Assign to squad if eligible
        if (assignToSquad && team != null)
        {
            var protoId = team == "govfor" ? "SquadGovfor" : "SquadOpfor";
            Entity<SquadTeamComponent> squad;
            if (!_squadSystem.TryEnsureSquad(protoId, out squad))
            {
                // Fallback: spawn a new entity with SquadTeamComponent
                var squadEnt = EntityManager.SpawnEntity(protoId, coordinates);
                var squadComp = EnsureComp<SquadTeamComponent>(squadEnt);
                squad = (squadEnt, squadComp);
            }
            _squadSystem.AssignSquad(entity.Value, (squad.Owner, (SquadTeamComponent?)squad.Comp), job);

            // If this is the sergeant, set as squad leader
            if (jobId != null && jobId.ToLowerInvariant().Contains("sergeant"))
            {
                var memberComp = EnsureComp<SquadMemberComponent>(entity.Value);
                var leaderIcon = squad.Comp.LeaderIcon;
                _squadSystem.PromoteSquadLeader((entity.Value, memberComp), entity.Value, leaderIcon);
            }
        }

        // --- Add opfor/govfor faction after player is spawned ---
        if (team == "govfor" || team == "opfor")
        {
            var faction = team.ToUpperInvariant(); // GOVFOR or OPFOR
            if (!HasComp<NpcFactionMemberComponent>(entity.Value))
                EnsureComp<NpcFactionMemberComponent>(entity.Value);
            _npcFaction.AddFaction((entity.Value, CompOrNull<NpcFactionMemberComponent>(entity.Value)), faction);
            // Add additional factions from platoon if present
            PlatoonPrototype? selectedPlatoon = null;
            if (team == "govfor")
                selectedPlatoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon;
            else if (team == "opfor")
                selectedPlatoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon;
            if (selectedPlatoon != null)
            {
                foreach (var addFaction in selectedPlatoon.Factions)
                {
                    _npcFaction.AddFaction((entity.Value, CompOrNull<NpcFactionMemberComponent>(entity.Value)), addFaction);
                }
            }
        }

        if (profile != null)
        {
            _humanoidSystem.LoadProfile(entity.Value, profile);
            _metaSystem.SetEntityName(entity.Value, profile.Name);

            if (profile.FlavorText != "" && _configurationManager.GetCVar(CCVars.FlavorText))
            {
                AddComp<DetailExaminableComponent>(entity.Value).Content = profile.FlavorText;
            }
        }

        if (loadout != null)
        {
            EquipRoleLoadout(entity.Value, loadout, roleProto!);
        }

        if (prototype?.StartingGear != null)
        {
            var startingGear = _prototypeManager.Index<StartingGearPrototype>(prototype.StartingGear);
            EquipStartingGear(entity.Value, startingGear, raiseEvent: false);
        }

        if (!Equals(job, originalJob) && originalPrototype?.StartingGear != null)
        {
            var origGear = _prototypeManager.Index<StartingGearPrototype>(originalPrototype.StartingGear);
            var newGear = prototype?.StartingGear != null ? _prototypeManager.Index<StartingGearPrototype>(prototype.StartingGear) : null;
            // Remove current headset (if any)
            if (InventorySystem.TryGetSlotEntity(entity.Value, "ears", out var currentHeadset))
            {
                EntityManager.DeleteEntity(currentHeadset.Value);
            }
            // Always check if the ears slot is empty after equipping new starting gear
            var hasHeadset = InventorySystem.TryGetSlotEntity(entity.Value, "ears", out var _);
            if (!hasHeadset && origGear.Equipment.TryGetValue("ears", out var headsetId))
            {
                var headset = EntityManager.SpawnEntity(headsetId, EntityManager.GetComponent<TransformComponent>(entity.Value).Coordinates);
                InventorySystem.TryEquip(entity.Value, headset, "ears");
            }

        }

        // --- Combine access from both jobs ---
        if (!Equals(job, originalJob) && originalPrototype != null && prototype != null)
        {
            if (InventorySystem.TryGetSlotEntity(entity.Value, "id", out var idUid))
            {
                // --- Clone ItemIFF from original job's ID card if present ---
                if (originalPrototype.StartingGear != null)
                {
                    var origGear = _prototypeManager.Index<StartingGearPrototype>(originalPrototype.StartingGear);
                    if (origGear.Equipment.TryGetValue("id", out var origIdCardProto))
                    {
                        var origIdCard = EntityManager.SpawnEntity(origIdCardProto, EntityManager.GetComponent<TransformComponent>(entity.Value).Coordinates);
                        if (TryComp<ItemIFFComponent>(origIdCard, out var origIff))
                        {
                            // Copy the component from the original card
                            CopyComp(origIdCard, idUid.Value, origIff);
                        }
                        EntityManager.DeleteEntity(origIdCard);
                    }
                }
                var cardId = idUid.Value;
                if (TryComp<PdaComponent>(idUid, out var pdaComponent) && pdaComponent.ContainedId != null)
                    cardId = pdaComponent.ContainedId.Value;
                if (TryComp<IdCardComponent>(cardId, out var card))
                {
                    var extendedAccess = false;
                    if (station != null)
                    {
                        var data = Comp<StationJobsComponent>(station.Value);
                        extendedAccess = data.ExtendedAccess;
                    }
                    // Merge all access tags and groups from both jobs, including extended
                    var allGroups = new HashSet<ProtoId<AccessGroupPrototype>>();
                    var allTags = new HashSet<ProtoId<AccessLevelPrototype>>();
                    void AddJobAccess(JobPrototype proto)
                    {
                        allGroups.UnionWith(proto.AccessGroups);
                        allTags.UnionWith(proto.Access);
                        if (extendedAccess)
                        {
                            allGroups.UnionWith(proto.ExtendedAccessGroups);
                            allTags.UnionWith(proto.ExtendedAccess);
                        }
                    }
                    AddJobAccess(originalPrototype);
                    AddJobAccess(prototype);
                    // Clear and set all tags/groups at once
                    _accessSystem.TrySetTags(cardId, allTags);
                    _accessSystem.TryAddGroups(cardId, allGroups);
                }
            }
        }

        var gearEquippedEv = new StartingGearEquippedEvent(entity.Value);
        RaiseLocalEvent(entity.Value, ref gearEquippedEv);

        if (prototype != null && TryComp(entity.Value, out MetaDataComponent? metaDataEntity))
        {
            // Set ID card and PDA: use new job for title/icon, but old job for access
            SetPdaAndIdCardDataWithSplitJob(entity.Value, metaDataEntity.EntityName, prototype, originalPrototype ?? prototype, station);
        }

        DoJobSpecials(job, entity.Value);
        _identity.QueueIdentityUpdate(entity.Value);



        return entity.Value;
    }

    private void DoJobSpecials(ProtoId<JobPrototype>? job, EntityUid entity)
    {
        if (!_prototypeManager.TryIndex(job ?? string.Empty, out JobPrototype? prototype, false))
            return;

        foreach (var jobSpecial in prototype.Special)
        {
            jobSpecial.AfterEquip(entity);
        }
    }

    /// <summary>
    /// Sets the ID card and PDA name, job, and access data.
    /// </summary>
    /// <param name="entity">Entity to load out.</param>
    /// <param name="characterName">Character name to use for the ID.</param>
    /// <param name="jobPrototype">Job prototype to use for the PDA and ID.</param>
    /// <param name="station">The station this player is being spawned on.</param>
    public void SetPdaAndIdCardData(EntityUid entity, string characterName, JobPrototype jobPrototype, EntityUid? station)
    {
        if (!InventorySystem.TryGetSlotEntity(entity, "id", out var idUid))
            return;

        var cardId = idUid.Value;
        if (TryComp<PdaComponent>(idUid, out var pdaComponent) && pdaComponent.ContainedId != null)
            cardId = pdaComponent.ContainedId.Value;

        if (!TryComp<IdCardComponent>(cardId, out var card))
            return;

        _cardSystem.TryChangeFullName(cardId, characterName, card);
        _cardSystem.TryChangeJobTitle(cardId, jobPrototype.LocalizedName, card);

        if (_prototypeManager.TryIndex(jobPrototype.Icon, out var jobIcon))
            _cardSystem.TryChangeJobIcon(cardId, jobIcon, card);

        var extendedAccess = false;
        if (station != null)
        {
            var data = Comp<StationJobsComponent>(station.Value);
            extendedAccess = data.ExtendedAccess;
        }

        _accessSystem.SetAccessToJob(cardId, jobPrototype, extendedAccess);

        if (pdaComponent != null)
            _pdaSystem.SetOwner(idUid.Value, pdaComponent, entity, characterName);
    }

    /// <summary>
    /// Sets the ID card and PDA name, job, and access data, allowing for different job prototypes for title/icon and access.
    /// </summary>
    /// <param name="entity">Entity to load out.</param>
    /// <param name="characterName">Character name to use for the ID.</param>
    /// <param name="titleJobPrototype">Job prototype to use for the PDA and ID title/icon.</param>
    /// <param name="accessJobPrototype">Job prototype to use for access/faction.</param>
    /// <param name="station">The station this player is being spawned on.</param>
    public void SetPdaAndIdCardDataWithSplitJob(EntityUid entity, string characterName, JobPrototype titleJobPrototype, JobPrototype accessJobPrototype, EntityUid? station)
    {
        if (!InventorySystem.TryGetSlotEntity(entity, "id", out var idUid))
            return;

        var cardId = idUid.Value;
        if (TryComp<PdaComponent>(idUid, out var pdaComponent) && pdaComponent.ContainedId != null)
            cardId = pdaComponent.ContainedId.Value;

        if (!TryComp<IdCardComponent>(cardId, out var card))
            return;

        // Set name, job title, and icon from the new job
        _cardSystem.TryChangeFullName(cardId, characterName, card);
        _cardSystem.TryChangeJobTitle(cardId, titleJobPrototype.LocalizedName, card);
        if (_prototypeManager.TryIndex(titleJobPrototype.Icon, out var jobIcon))
            _cardSystem.TryChangeJobIcon(cardId, jobIcon, card);

        // Set access from the old job
        if (station != null)
        {
            var data = Comp<StationJobsComponent>(station.Value);
        }
        if (pdaComponent != null)
            _pdaSystem.SetOwner(idUid.Value, pdaComponent, entity, characterName);
    }


    #endregion Player spawning helpers
}

/// <summary>
/// Ordered broadcast event fired on any spawner eligible to attempt to spawn a player.
/// This event's success is measured by if SpawnResult is not null.
/// You should not make this event's success rely on random chance.
/// This event is designed to use ordered handling. You probably want SpawnPointSystem to be the last handler.
/// </summary>
[PublicAPI]
public sealed class PlayerSpawningEvent : EntityEventArgs
{
    /// <summary>
    /// The entity spawned, if any. You should set this if you succeed at spawning the character, and leave it alone if it's not null.
    /// </summary>
    public EntityUid? SpawnResult;
    /// <summary>
    /// The job to use, if any.
    /// </summary>
    public readonly ProtoId<JobPrototype>? Job;
    /// <summary>
    /// The profile to use, if any.
    /// </summary>
    public readonly HumanoidCharacterProfile? HumanoidCharacterProfile;
    /// <summary>
    /// The target station, if any.
    /// </summary>
    public readonly EntityUid? Station;

    public PlayerSpawningEvent(ProtoId<JobPrototype>? job, HumanoidCharacterProfile? humanoidCharacterProfile, EntityUid? station)
    {
        Job = job;
        HumanoidCharacterProfile = humanoidCharacterProfile;
        Station = station;
    }
}
