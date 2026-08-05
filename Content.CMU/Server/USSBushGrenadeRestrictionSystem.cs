using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14;

public sealed partial class USSBushGrenadeRestrictionSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> GrenadeTag = "Grenade";
    private static readonly ProtoId<TagPrototype> HandGrenadeTag = "HandGrenade";
    private const string USSBushGridName = "USSBush";
    private const string USSBushDisplayName = "USS George W. Bush";

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnUseComponent, AttemptTriggerEvent>(OnBeforeUseTimer);
        SubscribeLocalEvent<DropshipHijackStartEvent>(OnDropshipHijackStart);
    }

    private void OnBeforeUseTimer(Entity<TriggerOnUseComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.User is not { } user || !IsGrenade(ent.Owner) || !IsOnLockedUSSBush(user))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("rmc-grenade-blocked-before-hijack"), user, user, PopupType.SmallCaution);
    }

    private void OnDropshipHijackStart(ref DropshipHijackStartEvent ev)
    {
        var query = EntityQueryEnumerator<MetaDataComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var meta, out _))
        {
            if (IsUSSBush(meta.EntityName))
                EnsureComp<USSBushGrenadesUnlockedComponent>(uid);
        }
    }

    private bool IsGrenade(EntityUid uid)
    {
        return _tag.HasTag(uid, GrenadeTag) || _tag.HasTag(uid, HandGrenadeTag);
    }

    private bool IsOnLockedUSSBush(EntityUid user)
    {
        var xform = Transform(user);
        return xform.GridUid is { } grid &&
               IsUSSBush(grid) &&
               !HasComp<USSBushGrenadesUnlockedComponent>(grid);
    }

    private bool IsUSSBush(EntityUid uid)
    {
        return TryComp(uid, out MetaDataComponent? meta) && IsUSSBush(meta.EntityName);
    }

    private static bool IsUSSBush(string name)
    {
        return string.Equals(name, USSBushGridName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, USSBushDisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
