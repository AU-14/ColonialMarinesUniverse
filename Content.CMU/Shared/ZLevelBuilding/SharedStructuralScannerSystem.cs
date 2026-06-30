// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): toggles the <see cref="StructuralScannerComponent"/> heat-map on use-in-hand.
/// The actual overlay rendering is client-side and holder-only (see the client scanner system).
/// </summary>
public sealed class SharedStructuralScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StructuralScannerComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<StructuralScannerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);

        _popup.PopupClient(
            Loc.GetString(ent.Comp.Enabled ? "au-scanner-on" : "au-scanner-off"),
            ent,
            args.User);

        args.Handled = true;
    }
}
