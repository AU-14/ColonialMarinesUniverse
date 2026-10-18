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
            Text = Loc.GetString(ent.Comp.Underbarrel ? "cmu-sling-switch-rail" : "cmu-sling-switch-underbarrel"),
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

        ent.Comp.Underbarrel = !ent.Comp.Underbarrel;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Underbarrel ? "cmu-sling-set-underbarrel" : "cmu-sling-set-rail"),
            ent, user);
    }

    private void OnExamined(Entity<CMUSlingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Underbarrel ? "cmu-sling-examine-underbarrel" : "cmu-sling-examine-rail"));
    }
}
