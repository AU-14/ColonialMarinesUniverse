using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared.CMU14.Attachable;

public sealed partial class CMUSlingSystem : EntitySystem
{
    [Dependency] private AttachableHolderSystem _attachments = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSlingComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUSlingComponent, ExaminedEvent>(OnExamined);
    }

    private void OnGetVerbs(Entity<CMUSlingComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString($"cmu-sling-switch-{MountName(NextMount(ent.Comp.Mount))}"),
            Act = () => ToggleMount(ent, user),
        });
    }

    public void ToggleMount(Entity<CMUSlingComponent> ent, EntityUid user)
    {
        if (_attachments.TryGetHolder(ent, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-sling-detach-first"), ent, user);
            return;
        }

        ent.Comp.Mount = NextMount(ent.Comp.Mount);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString($"cmu-sling-set-{MountName(ent.Comp.Mount)}"),
            ent, user);
    }

    private void OnExamined(Entity<CMUSlingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"cmu-sling-examine-{MountName(ent.Comp.Mount)}"));
    }

    private static CMUSlingMount NextMount(CMUSlingMount mount) => mount switch
    {
        CMUSlingMount.Sling => CMUSlingMount.Rail,
        CMUSlingMount.Rail => CMUSlingMount.Underbarrel,
        CMUSlingMount.Underbarrel => CMUSlingMount.Sling,
        _ => throw new ArgumentOutOfRangeException(nameof(mount), mount, null),
    };

    private static string MountName(CMUSlingMount mount) => mount switch
    {
        CMUSlingMount.Sling => "sling",
        CMUSlingMount.Rail => "rail",
        CMUSlingMount.Underbarrel => "underbarrel",
        _ => throw new ArgumentOutOfRangeException(nameof(mount), mount, null),
    };
}
