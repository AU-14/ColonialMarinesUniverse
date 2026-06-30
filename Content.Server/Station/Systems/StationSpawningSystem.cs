using System.Collections.Frozen;
using Content.Server.Access.Systems;
using Content.Server.AU14.Roles;
using Content.Server.AU14.Round;
using Content.Server.Humanoid;
using Content.Server.IdentityManagement;
using Content.Server.Jobs;
using Content.Server.Mind.Commands;
using Content.Server.PDA;
using Content.Server.Station.Components;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared._RMC14.Marines;
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
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;

namespace Content.Server.Station.Systems;

/// <summary>
/// Manages spawning into the game, tracking available spawn points.
/// Also provides helpers for spawning in the player's mob.
/// </summary>
[PublicAPI]
public sealed partial class StationSpawningSystem : SharedStationSpawningSystem
{
    private static readonly ProtoId<NpcFactionPrototype> GovforNpcFaction = new("GOVFOR");
    private static readonly ProtoId<NpcFactionPrototype> OpforNpcFaction = new("OPFOR");

    [Dependency] private SharedAccessSystem _accessSystem = default!;
    [Dependency] private ActorSystem _actors = default!;
    [Dependency] private IdCardSystem _cardSystem = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private PdaSystem _pdaSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;
    [Dependency] private SquadSystem _squadSystem = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    private static readonly PlatoonJobClass[] PlatoonJobClasses = Enum.GetValues<PlatoonJobClass>();
    private static readonly FrozenDictionary<PlatoonJobClass, string> PlatoonJobClassNames = PlatoonJobClasses.ToFrozenDictionary(v => v, v => v.ToString());

    // Round-robin rotation indices for squads per side
    private readonly string[] _govforSquads = { "SquadGovfor", "SquadGovforBravo", "SquadGovforCharlie" };
    private readonly string[] _opforSquads = { "SquadOpfor", "SquadOpforBravo", "SquadOpforCharlie" };
    private int _govforNextSquadIndex;
    private int _opforNextSquadIndex;

    private static readonly HashSet<string> NoSquadRoundRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Advisor",
        "DropshipCrewChief",
        "DropshipPilot",
        "MilitaryDoctor",
        "MilitaryPolice",
        "PlatoonCommander",
    };

    private static readonly HashSet<string> AuxiliarySquadRoundRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AuxSupportSynth",
        "JuniorOfficer",
    };

    // Legacy fallback for jobs that have not been migrated to roundRole yet.
    private static readonly HashSet<string> NoSquadJobIdFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcc",
        "pilot",
        "platco",
        "policeman",
        "militarydoctor"
    };

    private static readonly HashSet<string> AuxiliarySquadJobIdFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "synth",
        "platop"
    };

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
        var originalJob = job;
        _prototypeManager.Resolve(originalJob, out JobPrototype? originalPrototype);
        var originalSide = _roundJobProfiles.GetRoundSide(originalPrototype, jobId);
        var team = GetTeamForSide(originalSide);
        if (!string.IsNullOrEmpty(jobId))
        {
            var platoon = originalSide switch
            {
                RoundJobSide.Govfor => _platoonSpawnRuleSystem.SelectedGovforPlatoon,
                RoundJobSide.Opfor => _platoonSpawnRuleSystem.SelectedOpforPlatoon,
                _ => null,
            };

            // --- JobClassOverride logic: match by suffix ---
            if (platoon != null)
            {
                if (TryGetPlatoonJobClass(originalPrototype, jobId, out var jobClass) &&
                    platoon.JobClassOverride.TryGetValue(jobClass, out var overrideJob))
                {
                    job = overrideJob;
                }
                else
                {
                    foreach (var kvp in platoon.JobClassOverride)
                    {
                        // If the jobId ends with the enum name (e.g., AU14JobGOVFORSquadRifleman ends with SquadRifleman)
                        if (jobId.EndsWith(PlatoonJobClassNames[kvp.Key], StringComparison.OrdinalIgnoreCase))
                        {
                            job = kvp.Value;
                            break;
                        }
                    }
                }
            }
        }
        // --- Platoon job override logic end ---

        _prototypeManager.Resolve(job, out var prototype);
        // Get the original job prototype for access/faction/ID
        RoleLoadout? loadout = null;
        RoleLoadoutPrototype? loadoutProto = null;
        string? loadoutKey = null;

        if (prototype?.ID is { } id)
            (loadoutKey, loadoutProto) = LoadoutSystem.GetJobLoadoutInfo(id, _prototypeManager);

        // Need to get the loadout up-front to handle names if we use an entity spawn override.
        if (loadoutProto != null && loadoutKey != null)
            loadout = profile?.GetLoadoutOrDefault(loadoutKey, _actors.GetSession(entity), profile.Species, EntityManager, _prototypeManager);

        // RMC14 UseLoadoutOfJob
        if (prototype?.UseLoadoutOfJob != null && _prototypeManager.Resolve(prototype.UseLoadoutOfJob, out var usedPrototype))
        {
            var (newKey, newProto) = LoadoutSystem.GetJobLoadoutInfo(usedPrototype.ID, _prototypeManager);
            if (newProto != null && newKey != null && profile != null)
            {
                loadout = profile.GetLoadoutOrDefault(newKey, _actors.GetSession(entity), profile.Species, EntityManager, _prototypeManager);
                loadoutProto = newProto;
            }
        }

        // Spawn a custom JobEntity (e.g. Working Joe, rAI), this skips a lot of the humanoid stuff
        // Only apply player profile when UsePlayerProfile: true (default)
        if (prototype?.JobEntity != null)
        {
            DebugTools.Assert(entity is null);
            var jobEntity = Spawn(prototype.JobEntity, coordinates);
            MakeSentientCommand.MakeSentient(jobEntity, EntityManager);

            if (profile != null && prototype is not { UsePlayerProfile: false } && TryComp(jobEntity, out HumanoidAppearanceComponent? humanoid))
            {
                _humanoidSystem.LoadProfile(jobEntity, profile.WithSpecies(humanoid.Species), humanoid);
                _metaSystem.SetEntityName(jobEntity, profile.Name);

                if (profile.FlavorText != "" && _configurationManager.GetCVar(CCVars.FlavorText))
                    AddComp<DetailExaminableComponent>(jobEntity).Content = profile.FlavorText;
            }

            // Make sure custom names get handled, what is gameticker control flow whoopy.
            if (loadout != null && loadoutProto != null)
                EquipRoleName(jobEntity, loadout, loadoutProto);

            DoJobSpecials(job, jobEntity);
            ApplyTeamFaction(jobEntity, team);

            // Use originalPrototype for access, ID, and faction
            _identity.QueueIdentityUpdate(jobEntity);
            if (originalPrototype != null && TryComp(jobEntity, out MetaDataComponent? metaDataJobEntity))
                SetPdaAndIdCardData(jobEntity, metaDataJobEntity.EntityName, originalPrototype, station);

            return jobEntity;
        }

        string speciesId = profile != null ? profile.Species : SharedHumanoidAppearanceSystem.DefaultSpecies;
        if (!_prototypeManager.TryIndex<SpeciesPrototype>(speciesId, out var species))
            throw new ArgumentException($"Invalid species prototype was used: {speciesId}");

        entity ??= Spawn(species.Prototype, coordinates);

        if (profile != null && prototype is not { UsePlayerProfile: false })
        {
            _humanoidSystem.LoadProfile(entity.Value, profile);
            _metaSystem.SetEntityName(entity.Value, profile.Name);

            if (profile.FlavorText != "" && _configurationManager.GetCVar(CCVars.FlavorText))
                AddComp<DetailExaminableComponent>(entity.Value).Content = profile.FlavorText;
        }

        if (loadout != null && loadoutProto != null)
            EquipRoleLoadout(entity.Value, loadout, loadoutProto);

        if (prototype?.StartingGear != null)
        {
            var startingGear = _prototypeManager.Index<StartingGearPrototype>(prototype.StartingGear);
            EquipStartingGear(entity.Value, startingGear, raiseEvent: false);
        }

        if (!Equals(job, originalJob) && originalPrototype?.StartingGear != null)
        {
            var origGear = _prototypeManager.Index<StartingGearPrototype>(originalPrototype.StartingGear);
            // var newGear intentionally unused
            // Remove current headset (if any)
            if (InventorySystem.TryGetSlotEntity(entity.Value, "ears", out var currentHeadset))
                Del(currentHeadset.Value);

            // Always check if the ears slot is empty after equipping new starting gear
            var hasHeadset = InventorySystem.TryGetSlotEntity(entity.Value, "ears", out var _);
            if (!hasHeadset && origGear.Equipment.TryGetValue("ears", out var headsetId))
            {
                var headset = Spawn(headsetId, Comp<TransformComponent>(entity.Value).Coordinates);
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
                        var origIdCard = Spawn(origIdCardProto, Comp<TransformComponent>(entity.Value).Coordinates);
                        if (TryComp<ItemIFFComponent>(origIdCard, out var origIff))
                            CopyComp(origIdCard, idUid.Value, origIff);
                        Del(origIdCard);
                    }
                }
                var cardId = idUid.Value;
                if (TryComp<PdaComponent>(idUid, out var pdaComponent) && pdaComponent.ContainedId != null)
                    cardId = pdaComponent.ContainedId.Value;
                if (HasComp<IdCardComponent>(cardId))
                {
                    var extendedAccess = false;
                    if (station != null && TryComp<StationJobsComponent>(station.Value, out var stationJobs))
                        extendedAccess = stationJobs.ExtendedAccess;

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

        // Set ID card and PDA: use new job for title/icon, but old job for access
        if (prototype != null && TryComp(entity.Value, out MetaDataComponent? metaDataEntity))
            SetPdaAndIdCardDataWithSplitJob(entity.Value, metaDataEntity.EntityName, prototype, originalPrototype ?? prototype, station);

        DoJobSpecials(job, entity.Value);
        _identity.QueueIdentityUpdate(entity.Value);

        string? teamCheckJobId = originalJob?.ToString();

        bool assignToSquad = team != null && ShouldAssignToSquad(originalPrototype, teamCheckJobId);
        if (assignToSquad)
        {
            string protoId;

            // Roles that should go into the intel/auxiliary squad
            if (IsAuxiliarySquadRole(originalPrototype, jobId))
                protoId = team == "govfor" ? "SquadGovforIntel" : "SquadOpforIntel";
            else
            {
                var candidates = team == "govfor" ? _govforSquads : _opforSquads;

                // New: prioritize distributing Sergeants, Automatic Riflemen, and Radio Telephone Operators
                var isSergeant = IsSquadLeaderRole(originalPrototype, jobId);
                var isAutomaticRifleman = IsRoundRole(originalPrototype, "SquadAutomaticRifleman") ||
                                          jobId?.Contains("automaticrifleman", StringComparison.OrdinalIgnoreCase) == true ||
                                          jobId?.Contains("autora", StringComparison.OrdinalIgnoreCase) == true ||
                                          jobId?.Contains("auto", StringComparison.OrdinalIgnoreCase) == true ||
                                          jobId?.Contains("afn", StringComparison.OrdinalIgnoreCase) == true ||
                                          jobId?.EndsWith("squadautomaticrifleman", StringComparison.OrdinalIgnoreCase) == true;
                var isRadioTelephone = IsRoundRole(originalPrototype, "RadioTelephoneOperator") ||
                                       jobId?.Contains("radiotelephoneoperator", StringComparison.OrdinalIgnoreCase) == true ||
                                       jobId?.Contains("radio", StringComparison.OrdinalIgnoreCase) == true ||
                                       jobId?.Contains("rto", StringComparison.OrdinalIgnoreCase) == true ||
                                       jobId?.EndsWith("radiotelephoneoperator", StringComparison.OrdinalIgnoreCase) == true;

                // Sergeants: try to place into a squad without a leader where possible (existing behavior)
                if (isSergeant)
                {
                    string? chosen = null;
                    foreach (var cand in candidates)
                    {
                        if (_squadSystem.TryEnsureSquad(cand, out var s) && !_squadSystem.TryGetSquadLeader(s, out _))
                        {
                            chosen = cand;
                            break;
                        }
                    }

                    if (chosen != null)
                    {
                        protoId = chosen;
                    }
                    else
                    {
                        // all squads already have leaders, fall back to round-robin
                        if (team == "govfor")
                        {
                            protoId = candidates[_govforNextSquadIndex % candidates.Length];
                            _govforNextSquadIndex = (_govforNextSquadIndex + 1) % candidates.Length;
                        }
                        else
                        {
                            protoId = candidates[_opforNextSquadIndex % candidates.Length];
                            _opforNextSquadIndex = (_opforNextSquadIndex + 1) % candidates.Length;
                        }
                    }
                }
                // Automatic riflemen and radio telephone operators: try to evenly distribute so each squad gets one of each
                else if (isAutomaticRifleman || isRadioTelephone)
                {
                    string? chosen = null;
                    // Prefer squads that exist and don't yet have this role
                    foreach (var cand in candidates)
                    {
                        if (_squadSystem.TryEnsureSquad(cand, out var s))
                        {
                            // If job is available as a ProtoId, check the role count in the squad.
                            if (job != null)
                            {
                                s.Comp.Roles.TryGetValue(job.Value, out var existingCount);
                                if (existingCount == 0)
                                {
                                    chosen = cand;
                                    break;
                                }
                            }
                            else
                            {
                                // If we don't have a proto id for the job for whatever reason,
                                // prefer squads that exist but currently have fewer members (heuristic)
                                if (_squadSystem.GetSquadMembersAlive(s) == 0)
                                {
                                    chosen = cand;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Squad doesn't exist yet, so it definitely has none of the role
                            chosen = cand;
                            break;
                        }
                    }

                    if (chosen != null)
                    {
                        protoId = chosen;
                    }
                    else
                    {
                        // Fallback to round-robin distribution when every squad already has the role
                        if (team == "govfor")
                        {
                            protoId = candidates[_govforNextSquadIndex % candidates.Length];
                            _govforNextSquadIndex = (_govforNextSquadIndex + 1) % candidates.Length;
                        }
                        else
                        {
                            protoId = candidates[_opforNextSquadIndex % candidates.Length];
                            _opforNextSquadIndex = (_opforNextSquadIndex + 1) % candidates.Length;
                        }
                    }
                }
                else
                {
                    // Default distribution (round-robin)
                    // Sergeants already handled above; everyone else falls through here.
                    if (team == "govfor")
                    {
                        protoId = candidates[_govforNextSquadIndex % candidates.Length];
                        _govforNextSquadIndex = (_govforNextSquadIndex + 1) % candidates.Length;
                    }
                    else
                    {
                        protoId = candidates[_opforNextSquadIndex % candidates.Length];
                        _opforNextSquadIndex = (_opforNextSquadIndex + 1) % candidates.Length;
                    }
                }
            }

            if (!_squadSystem.TryEnsureSquad(protoId, out Entity<SquadTeamComponent> ensured))
            {
                // Fallback: spawn a new entity with SquadTeamComponent
                var squadEnt = Spawn(protoId, coordinates);
                var squadComp = EnsureComp<SquadTeamComponent>(squadEnt);
                ensured = (squadEnt, squadComp);
            }

            _squadSystem.AssignSquad(entity.Value, (ensured.Owner, (SquadTeamComponent?)ensured.Comp), job);

            // If this is the sergeant, set as squad leader
            if (IsSquadLeaderRole(originalPrototype, jobId))
            {
                var memberComp = EnsureComp<SquadMemberComponent>(entity.Value);
                var leaderIcon = ensured.Comp.LeaderIcon;
                _squadSystem.PromoteSquadLeader((entity.Value, memberComp), entity.Value, leaderIcon);
            }
        }

        ApplyTeamFaction(entity.Value, team);
        return entity.Value;
    }

    private static string? GetTeamForSide(RoundJobSide side)
    {
        return side switch
        {
            RoundJobSide.Govfor => "govfor",
            RoundJobSide.Opfor => "opfor",
            _ => null,
        };
    }

    private void ApplyTeamFaction(EntityUid entity, string? team)
    {
        if (team != "govfor" && team != "opfor")
            return;

        if (TryComp<MarineComponent>(entity, out var marine))
        {
            marine.Faction = team;
            Dirty(entity, marine);
        }

        var faction = team == "govfor" ? GovforNpcFaction : OpforNpcFaction;
        _npcFaction.AddFaction((entity, default), faction);

        PlatoonPrototype? selectedPlatoon = team == "govfor"
            ? _platoonSpawnRuleSystem.SelectedGovforPlatoon
            : _platoonSpawnRuleSystem.SelectedOpforPlatoon;

        if (selectedPlatoon == null)
            return;

        foreach (var addFaction in selectedPlatoon.Factions)
            _npcFaction.AddFaction((entity, default), addFaction);

        if (selectedPlatoon.NpcFaction is { } platoonNpcFaction)
            _npcFaction.AddFaction((entity, default), platoonNpcFaction);
    }

    private static bool ShouldAssignToSquad(JobPrototype? job, string? fallbackJobId)
    {
        if (job?.RoundRole is { } roundRole)
            return !NoSquadRoundRoles.Contains(roundRole);

        if (string.IsNullOrEmpty(fallbackJobId))
            return false;

        foreach (var fragment in NoSquadJobIdFragments)
        {
            if (fallbackJobId.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool IsAuxiliarySquadRole(JobPrototype? job, string? fallbackJobId)
    {
        if (job?.RoundRole is { } roundRole)
            return AuxiliarySquadRoundRoles.Contains(roundRole);

        if (string.IsNullOrEmpty(fallbackJobId))
            return false;

        foreach (var fragment in AuxiliarySquadJobIdFragments)
        {
            if (fallbackJobId.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsSquadLeaderRole(JobPrototype? job, string? fallbackJobId)
    {
        if (IsRoundRole(job, "SectionSergeant", "SquadSergeant"))
            return true;

        return fallbackJobId?.Contains("sergeant", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsRoundRole(JobPrototype? job, string expectedRoundRole)
    {
        if (job?.RoundRole is not { } roundRole)
            return false;

        return roundRole.Equals(expectedRoundRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoundRole(JobPrototype? job, string firstRoundRole, string secondRoundRole)
    {
        if (job?.RoundRole is not { } roundRole)
            return false;

        return roundRole.Equals(firstRoundRole, StringComparison.OrdinalIgnoreCase) ||
               roundRole.Equals(secondRoundRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPlatoonJobClass(
        JobPrototype? job,
        string fallbackJobId,
        out PlatoonJobClass jobClass)
    {
        if (job?.RoundRole is { } roundRole &&
            TryMapRoundRoleToPlatoonJobClass(roundRole, out jobClass))
        {
            return true;
        }

        foreach (var value in PlatoonJobClasses)
        {
            if (!fallbackJobId.EndsWith(PlatoonJobClassNames[value], StringComparison.OrdinalIgnoreCase))
                continue;

            jobClass = value;
            return true;
        }

        jobClass = default;
        return false;
    }

    private static bool TryMapRoundRoleToPlatoonJobClass(string roundRole, out PlatoonJobClass jobClass)
    {
        switch (roundRole)
        {
            case "AuxSupportSynth":
                jobClass = PlatoonJobClass.SupportSynth;
                return true;
            case "DropshipCrewChief":
                jobClass = PlatoonJobClass.DCC;
                return true;
            case "DropshipPilot":
                jobClass = PlatoonJobClass.DSPilot;
                return true;
            case "JuniorOfficer":
                jobClass = PlatoonJobClass.PlatOp;
                return true;
            case "PlatoonCommander":
                jobClass = PlatoonJobClass.PlatCo;
                return true;
            case "PlatoonCorpsman":
                jobClass = PlatoonJobClass.PlatoonCorpsman;
                return true;
            case "RadioTelephoneOperator":
                jobClass = PlatoonJobClass.RadioTelephoneOperator;
                return true;
            case "SectionSergeant":
                jobClass = PlatoonJobClass.SectionSergeant;
                return true;
            case "SquadAutomaticRifleman":
                jobClass = PlatoonJobClass.SquadAutomaticRifleman;
                return true;
            case "SquadCombatTech":
                jobClass = PlatoonJobClass.SquadCombatTech;
                return true;
            case "SquadRifleman":
                jobClass = PlatoonJobClass.SquadRifleman;
                return true;
            case "SquadSergeant":
                jobClass = PlatoonJobClass.SquadSergeant;
                return true;
            default:
                jobClass = default;
                return false;
        }
    }

    private void DoJobSpecials(ProtoId<JobPrototype>? job, EntityUid entity)
    {
        if (!_prototypeManager.Resolve(job, out JobPrototype? prototype))
            return;

        foreach (var jobSpecial in prototype.Special)
        {
            jobSpecial.AfterEquip(entity);
        }

        _roundJobProfiles.ApplyJobProfile(entity, prototype);
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

        // Respect cards with a prototype-defined title (e.g. fixed-role/faction IDs).
        if (card.JobTitle == null)
        {
            _cardSystem.TryChangeJobTitle(cardId, jobPrototype.LocalizedName, card);

            if (_prototypeManager.TryIndex(jobPrototype.Icon, out var jobIcon))
                _cardSystem.TryChangeJobIcon(cardId, jobIcon, card);
        }

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

        // Set name and (unless fixed by prototype) job title/icon from the selected job.
        _cardSystem.TryChangeFullName(cardId, characterName, card);
        if (card.JobTitle == null)
        {
            _cardSystem.TryChangeJobTitle(cardId, titleJobPrototype.LocalizedName, card);
            if (_prototypeManager.TryIndex(titleJobPrototype.Icon, out var jobIcon))
                _cardSystem.TryChangeJobIcon(cardId, jobIcon, card);
        }

        // Normal spawns need access applied from their actual job. Split-job spawns
        // already merge access before this helper, so avoid overwriting that union.
        if (titleJobPrototype.ID == accessJobPrototype.ID)
        {
            var extendedAccess = false;
            if (station != null)
            {
                var data = Comp<StationJobsComponent>(station.Value);
                extendedAccess = data.ExtendedAccess;
            }

            _accessSystem.SetAccessToJob(cardId, accessJobPrototype, extendedAccess);
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
public sealed partial class PlayerSpawningEvent : EntityEventArgs
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
