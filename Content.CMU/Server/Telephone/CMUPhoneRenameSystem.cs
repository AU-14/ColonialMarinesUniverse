using Content.Server.Administration;
using Content.Shared._RMC14.Telephone;
using Content.Shared.CMU14.Telephone;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Telephone;

/// <summary>
///     A screwdriver on a desk or wall phone lets you relabel it, which is the name it shows under in the phone
///     directory. Payphones keep the name of the area they're in.
/// </summary>
public sealed class CMUPhoneRenameSystem : EntitySystem
{
    private const string ScrewingQuality = "Screwing";
    private const int MaxNameLength = 32;

    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RotaryPhoneComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<RotaryPhoneComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            HasComp<CMUPayphoneComponent>(ent) ||
            !_tool.HasQuality(args.Used, ScrewingQuality) ||
            !TryComp(args.User, out ActorComponent? actor))
        {
            return;
        }

        args.Handled = true;

        var phone = ent.Owner;
        var user = args.User;
        _quickDialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("cmu-phone-rename-title"),
            Loc.GetString("cmu-phone-rename-prompt"),
            (string name) =>
            {
                name = name.Trim();
                if (name.Length > MaxNameLength)
                    name = name[..MaxNameLength];

                if (name.Length == 0 || TerminatingOrDeleted(phone))
                    return;

                _metaData.SetEntityName(phone, name);
                _popup.PopupEntity(Loc.GetString("cmu-phone-rename-done", ("name", name)), phone, user);
            });
    }
}
