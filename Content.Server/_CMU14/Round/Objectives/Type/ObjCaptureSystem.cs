using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.Popups;
using Content.Shared._CMU14.Round.Objectives;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared.Damage;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;

namespace Content.Server._CMU14.Round.Objectives.Type;

public sealed partial class ObjCaptureSystem : ObjectiveSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;

    private readonly Dictionary<EntityUid, float> _timeSinceLastIncrement = new();
    private readonly Dictionary<EntityUid, float> _lastSlashDamage = new();
    private static readonly string[] HoistAllowedFactions = ["govfor", "opfor", "clf"];

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-capture");
        SubscribeLocalEvent<CaptureObjectiveComponent, CaptureHoistFlagStartedEvent>(OnFlagHoistStarted);
        SubscribeLocalEvent<CaptureObjectiveComponent, CaptureHoistFlagDoAfterEvent>(OnHoistFlagDoAfter);
        SubscribeLocalEvent<CaptureObjectiveComponent, ObjectiveResetEvent>(OnReset);
    }

    public override void Shutdown()
    {
        _timeSinceLastIncrement.Clear();
        _lastSlashDamage.Clear();
        base.Shutdown();
    }

    private void OnReset(EntityUid uid, CaptureObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.CurrentController = string.Empty;
        comp.TimesIncremented = 0;
        comp.TimesIncrementedPerFaction.Clear();
        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Idle;
        comp.ActionUser = null;
        comp.ActionUserFaction = null;
        Dirty(uid, comp);
    }

    private string? GetPlatoonNameForFaction(string faction)
    {
        return faction.ToLowerInvariant() switch
        {
            "govfor" => _platoonSpawnRuleSystem.SelectedGovforPlatoon?.Name,
            "opfor" => _platoonSpawnRuleSystem.SelectedOpforPlatoon?.Name,
            _ => null
        };
    }

    private void OnFlagHoistStarted(EntityUid uid, CaptureObjectiveComponent comp, CaptureHoistFlagStartedEvent args)
    {
        if (comp.ActionState != CaptureObjectiveComponent.FlagActionState.Idle)
        {
            _popup.PopupEntity($"The flag is already being {(comp.ActionState == CaptureObjectiveComponent.FlagActionState.Hoisting ? "hoisted" : "lowered")}!", uid, args.User, PopupType.Medium);
            return;
        }

        var userFactions = new List<string>();
        if (args.User != EntityUid.Invalid && TryComp(args.User, out NpcFactionMemberComponent? factionComp))
            userFactions.AddRange(factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()));

        var hoistingFaction = args.Faction.ToLowerInvariant();
        if (!userFactions.Contains(hoistingFaction))
            userFactions.Add(hoistingFaction);

        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            comp.ActionState = CaptureObjectiveComponent.FlagActionState.Lowering;
            comp.ActionUser = args.User;
            comp.ActionUserFaction = comp.CurrentController;
            _popup.PopupEntity($"You begin lowering the flag...", uid, args.User, PopupType.Medium);
            return;
        }

        string? allowed = null;
        foreach (var fac in HoistAllowedFactions)
        {
            if (userFactions.Contains(fac))
            {
                allowed = fac;
                break;
            }
        }

        if (allowed == null)
        {
            _popup.PopupEntity($"Your faction cannot raise this flag.", uid, args.User, PopupType.Medium);
            return;
        }

        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Hoisting;
        comp.ActionUser = args.User;
        comp.ActionUserFaction = allowed;

        var platoonName = GetPlatoonNameForFaction(allowed);
        var displayName = !string.IsNullOrEmpty(platoonName) ? platoonName : allowed;
        _popup.PopupEntity($"You begin raising the flag for {displayName}...", uid, args.User, PopupType.Medium);
    }

    private void OnHoistFlagDoAfter(EntityUid uid, CaptureObjectiveComponent comp, CaptureHoistFlagDoAfterEvent args)
    {
        comp.ActionState = CaptureObjectiveComponent.FlagActionState.Idle;
        comp.ActionUser = null;
        comp.ActionUserFaction = null;

        if (args.Cancelled)
            return;

        var popupUser = args.User != EntityUid.Invalid ? args.User : uid;

        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            comp.CurrentController = string.Empty;
            _popup.PopupEntity($"You have lowered the flag.", uid, popupUser, PopupType.Medium);
        }
        else
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
        var govforPlatoon = _platoonSpawnRuleSystem.SelectedGovforPlatoon;
        var opforPlatoon = _platoonSpawnRuleSystem.SelectedOpforPlatoon;
        var govforFlag = govforPlatoon?.PlatoonFlag ?? "uaflag";
        var opforFlag = opforPlatoon?.PlatoonFlag ?? "uaflagworn";
        if (!string.IsNullOrEmpty(govforFlag) && govforFlag == opforFlag)
            opforFlag = "uaflagworn";

        var query = EntityQueryEnumerator<CaptureObjectiveComponent, CMUObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp, out var objComp))
        {
            if (TryComp(uid, out DamageableComponent? damageable))
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

            if (!objComp.Active)
                continue;
            if (comp.MaxHoldTimes > 0 && comp.TimesIncremented >= comp.MaxHoldTimes)
                continue;
            if (comp is { OnceOnly: true, TimesIncremented: > 0 })
                continue;
            if (string.IsNullOrEmpty(comp.CurrentController))
                continue;

            _timeSinceLastIncrement.TryAdd(uid, 0f);
            _timeSinceLastIncrement[uid] += frameTime;
            if (!(_timeSinceLastIncrement[uid] >= comp.PointIncrementTime))
                continue;

            _timeSinceLastIncrement[uid] = 0f;
            comp.TimesIncremented++;

            var factionKey = comp.CurrentController.ToLowerInvariant();
            comp.TimesIncrementedPerFaction.TryAdd(factionKey, 0);
            comp.TimesIncrementedPerFaction[factionKey]++;

            ObjCtrl.AwardPointsToFaction(comp.CurrentController, objComp);

            if (comp is { OnceOnly: true, TimesIncremented: > 0 })
            {
                ObjCtrl.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController, sawmill: _logs);
                continue;
            }

            if (comp.MaxHoldTimes > 0 && comp.TimesIncremented >= comp.MaxHoldTimes)
                ObjCtrl.CompleteObjectiveForFaction(uid, objComp, comp.CurrentController, sawmill: _logs);
        }
    }
}
