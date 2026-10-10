using Content.Server._RMC14.Telephone;
using Content.Server.Administration;
using Content.Shared._RMC14.Telephone;
using Content.Shared.CMU14.Telephone;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Telephone;

public sealed partial class CMUMobilePhoneSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private RMCTelephoneSystem _telephone = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUMobilePhoneComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<CMUMobilePhoneComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUMobilePhoneComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnEquippedHand(Entity<CMUMobilePhoneComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.OwnerName != null)
            return;

        SetOwnerName(ent, Name(args.User));
    }

    private void OnUseInHand(Entity<CMUMobilePhoneComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp(ent, out RotaryPhoneComponent? rotary))
            return;

        args.Handled = true;
        _telephone.CMUUseMobilePhone((ent, rotary), args.User);
    }

    private void OnInteractUsing(Entity<CMUMobilePhoneComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, ScrewingQuality))
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        args.Handled = true;

        var user = args.User;
        _quickDialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("cmu-mobile-phone-rename-title"),
            Loc.GetString("cmu-mobile-phone-rename-prompt"),
            (string newName) =>
            {
                if (TerminatingOrDeleted(ent))
                    return;

                newName = newName.Trim();
                if (newName.Length == 0 || newName.Length > ent.Comp.MaxNameLength)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-mobile-phone-rename-invalid", ("max", ent.Comp.MaxNameLength)), user, user, PopupType.SmallCaution);
                    return;
                }

                SetOwnerName(ent, newName);
                _popup.PopupEntity(Loc.GetString("cmu-mobile-phone-renamed", ("name", Name(ent))), user, user);
            });
    }

    public void SetOwnerName(Entity<CMUMobilePhoneComponent> ent, string ownerName)
    {
        ent.Comp.OwnerName = ownerName;
        Dirty(ent);
        _metaData.SetEntityName(ent, Loc.GetString("cmu-mobile-phone-name", ("owner", ownerName)));
    }
}
