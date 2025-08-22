using System.Linq;
using Content.Shared.AU14.Objectives.Capture;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Content.Server.Popups;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects;

namespace Content.Server.AU14.Objectives.Capture;

public sealed class CaptureObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly Content.Server.AU14.Objectives.AuObjectiveSystem _objectiveSystem = default!;
    [Dependency] private readonly Content.Server.AU14.Round.PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;
    [Dependency] private readonly Robust.Shared.Prototypes.IPrototypeManager _prototypeManager = default!;

    // Tracks ongoing hoists to prevent multiple simultaneous hoists per structure
    private readonly HashSet<EntityUid> _hoisting = new();

    // Tracks time since last increment for each capture objective
    private readonly Dictionary<EntityUid, float> _timeSinceLastIncrement = new();

    private static readonly ISawmill Sawmill = Logger.GetSawmill("capture-obj");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptureObjectiveComponent, FlagHoistStartedEvent>(OnFlagHoistStarted);
        SubscribeLocalEvent<CaptureObjectiveComponent, FlagHoistedEvent>(OnFlagHoisted);
    }

    private void OnFlagHoistStarted(EntityUid uid, CaptureObjectiveComponent comp, FlagHoistStartedEvent args)
    {
        _popup.PopupEntity($"You begin hoisting the flag for {args.Faction}...", uid, args.User, PopupType.Medium);
    }

    private void OnFlagHoisted(EntityUid uid, CaptureObjectiveComponent comp, FlagHoistedEvent args)
    {
        var allowedFactions = new[] { "govfor", "opfor", "clf" };
        // Get the objective component for allowed factions list
        if (!_entManager.TryGetComponent(uid, out Content.Shared.AU14.Objectives.AuObjectiveComponent? objComp))
        {
            comp.CurrentController = string.Empty;
            _popup.PopupEntity($"You cannot hoist the flag.", uid, args.User, PopupType.Medium);
            return;
        }
        // Get all factions for the user (player or NPC)
        var userFactions = new List<string>();
        if (args.User != EntityUid.Invalid && _entManager.TryGetComponent(args.User, out Content.Shared.NPC.Components.NpcFactionMemberComponent? factionComp))
        {
            userFactions.AddRange(factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()));
        }
        // Always include args.Faction as a fallback (for legacy or player cases)
        var hoistingFaction = args.Faction.ToLowerInvariant();
        if (!userFactions.Contains(hoistingFaction))
            userFactions.Add(hoistingFaction);
        // Find the first allowed faction, preferring govfor > opfor > clf > others in objComp.Factions
        string? allowed = null;
        foreach (var pref in allowedFactions)
        {
            if (userFactions.Contains(pref))
            {
                allowed = pref;
                break;
            }
        }
        if (allowed == null)
        {
            // Check for any allowed faction in the objective's possiblefactions
            foreach (var possible in objComp.Factions.Select(f => f.ToLowerInvariant()))
            {
                if (userFactions.Contains(possible))
                {
                    allowed = possible;
                    break;
                }
            }
        }
        if (allowed == null)
        {
            // Not allowed: lower the flag
            comp.CurrentController = string.Empty;
            _popup.PopupEntity($"Your faction cannot hoist this flag. The flag is lowered.", uid, args.User, PopupType.Medium);
            return;
        }
        // Allowed: set controller to the preferred/allowed faction
        comp.CurrentController = allowed;
        _popup.PopupEntity($"You have hoisted the flag for {allowed}!", uid, args.User, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // Get selected platoons and their flag states
        var govforPlatoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon;
        var opforPlatoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon;
        var govforFlag = govforPlatoon?.PlatoonFlag ?? "uaflag";
        var opforFlag = opforPlatoon?.PlatoonFlag ?? "uaflagworn";
        // If both have the same non-empty flag, opfor uses default
        if (!string.IsNullOrEmpty(govforFlag) && govforFlag == opforFlag)
            opforFlag = "uaflagworn";
        var query = EntityQueryEnumerator<CaptureObjectiveComponent, Content.Shared.AU14.Objectives.AuObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp, out var objComp))
        {
            // Set the flag states for this objective
            comp.GovforFlagState = govforFlag;
            comp.OpforFlagState = opforFlag;
            // Only process active objectives
            if (!objComp.Active)
                continue;
            // If completed, skip
            if (comp.MaxHoldTimes > 0 && comp.timesincremented >= comp.MaxHoldTimes)
                continue;
            if (comp.OnceOnly && comp.timesincremented > 0)
                continue;
            // Only increment if there is a controller
            if (string.IsNullOrEmpty(comp.CurrentController))
                continue;
            // Track time
            if (!_timeSinceLastIncrement.ContainsKey(uid))
                _timeSinceLastIncrement[uid] = 0f;
            _timeSinceLastIncrement[uid] += frameTime;
            // Check if it's time to increment
            if (_timeSinceLastIncrement[uid] >= comp.PointIncrementTime)
            {
                _timeSinceLastIncrement[uid] = 0f;
                comp.timesincremented++;
                // Increment per-faction count for progress display
                var factionKey = comp.CurrentController.ToLowerInvariant();
                if (!comp.TimesIncrementedPerFaction.ContainsKey(factionKey))
                    comp.TimesIncrementedPerFaction[factionKey] = 0;
                comp.TimesIncrementedPerFaction[factionKey]++;
                // Award points
                _objectiveSystem.AwardPointsToFaction(comp.CurrentController, objComp);
                Sawmill.Info($"[CAPTURE OBJ] Awarded points to {comp.CurrentController} for {uid} (increment {comp.timesincremented}/{comp.MaxHoldTimes})");
                // If OnceOnly, complete after first increment
                if (comp.OnceOnly && comp.timesincremented > 0)
                {
                    _objectiveSystem.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController);
                    Sawmill.Info($"[CAPTURE OBJ] Completed once-only capture objective {uid} for {comp.CurrentController}");
                }
                // If reached max hold times, complete (but only if maxholdtimes > 0)
                if (!comp.OnceOnly && comp.MaxHoldTimes > 0 && comp.timesincremented >= comp.MaxHoldTimes)
                {
                    _objectiveSystem.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController);
                    Sawmill.Info($"[CAPTURE OBJ] Completed capture objective {uid} for {comp.CurrentController} after max hold times");
                }
            }
        }
    }
}
