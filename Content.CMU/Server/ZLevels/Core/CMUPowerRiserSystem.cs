using Content.Server.Power.Components;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed class CMUPowerRiserSystem : EntitySystem
{
    private const string ScrewingQuality = "Screwing";
    private const string PryingQuality = "Prying";

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly CMUZPairingSystem _zPairing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPowerRiserComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CMUPowerRiserComponent, CMUZPairedEvent>(OnPaired);
        SubscribeLocalEvent<CMUPowerRiserComponent, CMUZUnpairedEvent>(OnUnpaired);
        SubscribeLocalEvent<CMUPowerRiserComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUPowerRiserComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<CMUPowerRiserComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            return;

        if (!TryComp<CMUZPairedComponent>(ent, out var paired))
            return;

        if (_tool.HasQuality(args.Used, ScrewingQuality))
        {
            args.Handled = true;
            ent.Comp.Source = !ent.Comp.Source;

            var localizedRole = ent.Comp.Source
                ? Loc.GetString("cmu-power-riser-role-source")
                : Loc.GetString("cmu-power-riser-role-sink");

            _popup.PopupClient(Loc.GetString("cmu-power-riser-role-toggled", ("role", localizedRole)), ent, args.User);

            RefreshPair(ent);
        }
        else if (_tool.HasQuality(args.Used, PryingQuality))
        {
            args.Handled = true;

            paired.Offset = -paired.Offset;
            Dirty(ent.Owner, paired);

            var direction = Loc.GetString(paired.Offset > 0 ? "cmu-z-direction-above" : "cmu-z-direction-below");
            _popup.PopupClient(Loc.GetString("cmu-power-riser-dir-toggled", ("direction", direction)), ent, args.User);

            _zPairing.Unpair((ent.Owner, paired));
            _zPairing.TryPair((ent.Owner, paired));
        }
    }

    private void OnPowerChanged(Entity<CMUPowerRiserComponent> ent, ref PowerChangedEvent args)
    {
        if (!ent.Comp.Source)
            return;

        ent.Comp.Powered = args.Powered;
        SetTwinSupply(ent);
    }

    private void OnPaired(Entity<CMUPowerRiserComponent> ent, ref CMUZPairedEvent args)
    {
        if (!ent.Comp.Source)
            return;

        ArmAsSource(ent, args.Twin);
    }

    private void OnUnpaired(Entity<CMUPowerRiserComponent> ent, ref CMUZUnpairedEvent args)
    {
        ent.Comp.Powered = false;

        if (TryComp<PowerSupplierComponent>(ent, out var own))
            own.Enabled = false;
    }

    // A role flip rewrites the pair's supply contract: both suppliers off,
    // then whichever half is a source re-arms its twin.
    private void RefreshPair(Entity<CMUPowerRiserComponent> ent)
    {
        if (TryComp<PowerSupplierComponent>(ent, out var own))
            own.Enabled = false;

        if (!TryComp<CMUZPairedComponent>(ent, out var paired) || paired.Twin is not { } twin)
            return;

        if (TryComp<PowerSupplierComponent>(twin, out var twinSupplier))
            twinSupplier.Enabled = false;

        ArmAsSource(ent, twin);
        if (TryComp<CMUPowerRiserComponent>(twin, out var twinRiser))
            ArmAsSource((twin, twinRiser), ent);
    }

    private void ArmAsSource(Entity<CMUPowerRiserComponent> ent, EntityUid twin)
    {
        if (!ent.Comp.Source)
            return;

        ent.Comp.Powered =
            (TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && receiver.Powered)
            || (TryComp<CMUPowerRiserComponent>(twin, out var twinRiser) && twinRiser.Powered);
        SetTwinSupply(ent);
    }

    private void SetTwinSupply(Entity<CMUPowerRiserComponent> ent)
    {
        if (!TryComp<CMUZPairedComponent>(ent, out var paired) || paired.Twin is not { } twin)
            return;

        if (TryComp<PowerSupplierComponent>(twin, out var supplier))
            supplier.Enabled = ent.Comp.Powered;
    }

    private void OnExamined(Entity<CMUPowerRiserComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<CMUZPairedComponent>(ent, out var paired))
            return;

        var twinAbove = paired.Offset > 0;
        var key = paired.Twin == null
            ? "cmu-power-riser-unlinked"
            : ent.Comp.Source
                ? TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && receiver.Powered
                    ? "cmu-power-riser-source-live"
                    : "cmu-power-riser-source-dead"
                : "cmu-power-riser-sink";

        var direction = Loc.GetString(twinAbove ? "cmu-z-direction-above" : "cmu-z-direction-below");
        args.PushMarkup(Loc.GetString(key, ("direction", direction)));
    }
}
