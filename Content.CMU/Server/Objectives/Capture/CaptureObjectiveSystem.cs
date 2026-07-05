using System.Linq;
using Content.Server.Popups;
using Content.Server.AU14.Objectives;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Capture;
using Content.Shared.Popups;

namespace Content.Server.AU14.Objectives.Capture;

public sealed partial class CaptureObjectiveSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AuObjectiveSystem _objectiveSystem = default!;
    [Dependency] private Round.PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;

    // Tracks time since last increment for each capture objective
    private readonly Dictionary<EntityUid, float> _timeSinceLastIncrement = new();
    // Tracks last known slash damage for each capture objective
    private readonly Dictionary<EntityUid, float> _lastSlashDamage = new();
    // Factions allowed to hoist flags
    private static readonly string[] HoistAllowedFactions = { "govfor", "opfor", "clf" };
    private ISawmill _logs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-capture");
        SubscribeLocalEvent<CaptureObjectiveComponent, FlagHoistStartedEvent>(OnFlagHoistStarted);
        SubscribeLocalEvent<CaptureObjectiveComponent, HoistFlagDoAfterEvent>(OnHoistFlagDoAfter); // Subscribe to DoAfter completion
        // Removed broken damage event subscription
    }

    private string? GetPlatoonNameForFaction(string faction)
    {
        switch (faction.ToLowerInvariant())
        {
            case "govfor":
                return _platoonSpawnRuleSystem.SelectedGovforPlatoon?.Name;
            case "opfor":
                return _platoonSpawnRuleSystem.SelectedOpforPlatoon?.Name;
            default:
                return null;
        }
    }

    private void OnFlagHoistStarted(EntityUid uid, CaptureObjectiveComponent comp, FlagHoistStartedEvent args)
    {
        // If already in progress, block further actions
        if (comp.ActionState != CaptureObjectiveComponent.FlagActionState.Idle)
        {
            _popup.PopupEntity($"The flag is already being {(comp.ActionState == CaptureObjectiveComponent.FlagActionState.Hoisting ? "hoisted" : "lowered")}!", uid, args.User, PopupType.Medium);
            return;
        }

        var userFactions = new List<string>();
        if (args.User != EntityUid.Invalid && TryComp(args.User, out Content.Shared.NPC.Components.NpcFactionMemberComponent? factionComp))
        {
            userFactions.AddRange(factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()));
        }
        var hoistingFaction = args.Faction.ToLowerInvariant();
        if (!userFactions.Contains(hoistingFaction))
            userFactions.Add(hoistingFaction);

        // Lowering: anyone can lower if the flag is raised and not being lowered
        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            comp.ActionState = CaptureObjectiveComponent.FlagActionState.Lowering;
            comp.ActionUser = args.User;
            comp.ActionTimeRemaining = comp.HoistTime;
            comp.ActionUserFaction = comp.CurrentController; // Track which faction is being lowered
            _popup.PopupEntity($"You begin lowering the flag...", uid, args.User, PopupType.Medium);
            return;
        }

        // Raising: only allowed factions can raise if the flag is lowered and not being hoisted
        string? allowed = null;
        foreach (var fac in HoistAllowedFactions)
        {
            if (!userFactions.Contains(fac))
                continue;
            allowed = fac;
            break;
        }

        if (allowed == null)
        {
            _popup.PopupEntity($"Your faction cannot raise this flag.", uid, args.User, PopupType.Medium);
            return;
        }

        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Hoisting;
        comp.ActionUser = args.User;
        comp.ActionTimeRemaining = comp.HoistTime;
        comp.ActionUserFaction = allowed; // Track which faction is being raised

        var platoonName = GetPlatoonNameForFaction(allowed);
        var displayName = !string.IsNullOrEmpty(platoonName) ? platoonName : allowed;

        _popup.PopupEntity($"You begin raising the flag for {displayName}...", uid, args.User, PopupType.Medium);
    }

    // New handler for DoAfter completion
    private void OnHoistFlagDoAfter(EntityUid uid, CaptureObjectiveComponent comp, HoistFlagDoAfterEvent args)
    {
        // Always reset action state after DoAfter completes (success or cancel)
        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Idle;
        comp.ActionUser = null;
        comp.ActionUserFaction = null;
        comp.ActionTimeRemaining = 0f;

        if (args.Cancelled)
            return;
        var popupUser = args.User != EntityUid.Invalid ? args.User : uid;
        // If the flag is currently held, this is a lowering action
        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            comp.CurrentController = string.Empty;
            _popup.PopupEntity($"You have lowered the flag.", uid, popupUser, PopupType.Medium);
        }
        else // Otherwise, this is a hoisting action
        {
            comp.CurrentController = args.Faction;
            var allowed = comp.CurrentController;
            var platoonName = GetPlatoonNameForFaction(allowed);
            var displayName = !string.IsNullOrEmpty(platoonName) ? platoonName : allowed;
            _popup.PopupEntity($"You have raised the flag for {displayName}.", uid, popupUser, PopupType.Medium);
        }
    }

    public override void Update(float frameTime)
    {
        // Get selected platoons and their flag states
        var govforPlatoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon;
        var opforPlatoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon;
        var govforFlag = govforPlatoon?.PlatoonFlag ?? "uaflag";
        var opforFlag = opforPlatoon?.PlatoonFlag ?? "uaflagworn";
        // If both have the same non-empty flag, opfor uses default
        if (!string.IsNullOrEmpty(govforFlag) && govforFlag == opforFlag)
            opforFlag = "uaflagworn";
        var query = EntityQueryEnumerator<CaptureObjectiveComponent, AuObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp, out var objComp))
        {
            // --- Begin: Slash damage tracking ---
            if (TryComp(uid, out Content.Shared.Damage.DamageableComponent? damageable))
            {
                float currentSlash = 0f;
                if (damageable.Damage.DamageDict.TryGetValue("Slash", out var slash))
                    currentSlash = slash.Float();
                _lastSlashDamage.TryGetValue(uid, out float lastSlash);
                float delta = currentSlash - lastSlash;
                if (delta > 0f)
                {
                    comp.FlagHealth -= delta;
                    if (comp.FlagHealth <= 0f)
                    {
                        comp.FlagHealth = comp.FlagInitialHealth;
                        if (!string.IsNullOrEmpty(comp.CurrentController))
                        {
                            comp.CurrentController = string.Empty;
                            _popup.PopupEntity($"The flag has been lowered due to heavy damage!", uid, PopupType.Medium);
                        }
                    }
                }
                _lastSlashDamage[uid] = currentSlash;
            }

            comp.GovforFlagState = govforFlag;
            comp.OpforFlagState = opforFlag;
            // Only process active objectives
            if (!objComp.Active)
                continue;
            if (comp.MaxHoldTimes > 0 && comp.timesincremented >= comp.MaxHoldTimes)
                continue;
            if (comp is { OnceOnly: true, timesincremented: > 0 })
                continue;
            if (string.IsNullOrEmpty(comp.CurrentController))
                continue;
            _timeSinceLastIncrement.TryAdd(uid, 0f);
            _timeSinceLastIncrement[uid] += frameTime;

            if (!(_timeSinceLastIncrement[uid] >= comp.PointIncrementTime))
                continue;

            _timeSinceLastIncrement[uid] = 0f;
            comp.timesincremented++;
            // Increment per-faction count for progress display
            var factionKey = comp.CurrentController.ToLowerInvariant();
            comp.TimesIncrementedPerFaction.TryAdd(factionKey, 0);
            comp.TimesIncrementedPerFaction[factionKey]++;
            // Award points
            _objectiveSystem.AwardPointsToFaction(comp.CurrentController, objComp);
            _logs.Debug($"[CAPTURE OBJ] Awarded points to '{comp.CurrentController}' for ({uid}) (increment {comp.timesincremented}/{comp.MaxHoldTimes})");
            // If OnceOnly, complete after first increment
            if (comp is { OnceOnly: true, timesincremented: > 0 })
            {
                _objectiveSystem.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController);
                _logs.Debug($"[CAPTURE OBJ] Completed once-only capture objective ({uid}) for '{comp.CurrentController}'");
            }
            // If reached max hold times, complete (but only if maxholdtimes > 0)
            if (comp is not { OnceOnly: false, MaxHoldTimes: > 0 }
                || comp.timesincremented < comp.MaxHoldTimes)
                continue;

            _objectiveSystem.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController);
            _logs.Debug($"[CAPTURE OBJ] Completed capture objective ({uid}) for '{comp.CurrentController}' after max hold times");
            // --- Hoist/Lower timer logic removed ---
        }
    }
}
