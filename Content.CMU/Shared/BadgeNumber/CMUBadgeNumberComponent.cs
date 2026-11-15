using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.BadgeNumber;

/// <summary>A law enforcement officer's badge number, carried on their ID card.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUBadgeNumberComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Number;
}

public sealed class CMUBadgeNumberSystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBadgeNumberComponent, ExaminedEvent>(OnExamined);
        // After the ID card has named the officer, so records, radio notices and logs that sign with their name and
        // job carry their badge number too.
        SubscribeLocalEvent<TryGetIdentityShortInfoEvent>(OnTryGetIdentityShortInfo, after: new[] { typeof(SharedIdCardSystem) });
    }

    /// <summary>The badge number on the ID card someone carries, if any.</summary>
    public bool TryGetBadge(EntityUid holder, out int number)
    {
        number = 0;
        if (!_idCard.TryFindIdCard(holder, out var card) || !TryComp<CMUBadgeNumberComponent>(card, out var badge))
            return false;

        number = badge.Number;
        return true;
    }

    public static string Format(int number)
    {
        return number.ToString("D4");
    }

    private void OnTryGetIdentityShortInfo(TryGetIdentityShortInfoEvent ev)
    {
        if (ev.Title is not { } title || ev.RequestForAccessLogging || !TryGetBadge(ev.Target, out var number))
            return;

        ev.Title = Loc.GetString("cmu-badge-number-title", ("title", title), ("number", Format(number)));
    }

    private void OnExamined(Entity<CMUBadgeNumberComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-badge-number-examine", ("number", Format(ent.Comp.Number))));
    }
}
