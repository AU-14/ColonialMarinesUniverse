// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Ghost.Components;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// Hatches over a pit ladder: verbs to open and close it, and to camouflage it so it is invisible when closed.
public sealed class HatchSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HatchComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HatchLinkComponent, GetVerbsEvent<AlternativeVerb>>(OnGetLinkVerbs);
        SubscribeLocalEvent<HatchComponent, HatchCamouflageDoAfterEvent>(OnCamouflageDoAfter);
    }

    // The ladder under a hatch lets whoever is below open or close the hatch above them.
    private void OnGetLinkVerbs(Entity<HatchLinkComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<GhostComponent>(args.User))
            return;

        if (!TryComp<HatchComponent>(ent.Comp.Hatch, out var hatch))
            return;

        var target = new Entity<HatchComponent>(ent.Comp.Hatch, hatch);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(hatch.Open ? "au14-hatch-close" : "au14-hatch-open"),
            Act = () => SetOpen(target, !target.Comp.Open),
            Priority = 50,
        });
    }

    private void OnGetVerbs(Entity<HatchComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        // Ghosts are let through ladders, but they do not touch hatches.
        if (!args.CanAccess || !args.CanInteract || HasComp<GhostComponent>(args.User))
            return;

        var target = ent;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Open ? "au14-hatch-close" : "au14-hatch-open"),
            Act = () => SetOpen(target, !target.Comp.Open),
            Priority = 50,
        });

        if (HasComp<SapperComponent>(args.User))
        {
            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ent.Comp.InvisibleWhenClosed ? "au14-hatch-camouflage-off" : "au14-hatch-camouflage-on"),
                Act = () => StartCamouflageToggle(target, user),
                Priority = 40,
            });
        }
    }

    private void StartCamouflageToggle(Entity<HatchComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.CamouflageTime, new HatchCamouflageDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("au14-hatch-camouflage-working"), ent, user);
    }

    private void OnCamouflageDoAfter(Entity<HatchComponent> ent, ref HatchCamouflageDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!HasComp<SapperComponent>(args.User))
            return;

        SetInvisibleWhenClosed(ent, !ent.Comp.InvisibleWhenClosed);
    }

    public void SetInvisibleWhenClosed(Entity<HatchComponent> ent, bool invisible)
    {
        if (ent.Comp.InvisibleWhenClosed == invisible)
            return;

        ent.Comp.InvisibleWhenClosed = invisible;
        Dirty(ent);

        _appearance.SetData(ent.Owner, HatchVisuals.Invisible, invisible && !ent.Comp.Open);
    }

    public void SetOpen(Entity<HatchComponent> ent, bool open)
    {
        if (ent.Comp.Open == open)
            return;

        ent.Comp.Open = open;
        Dirty(ent);

        if (open)
        {
            var ladder = EnsureComp<CMUZLevelLadderComponent>(ent.Owner);
            ladder.Offset = -1;
            ladder.CanMoveUp = false;
            ladder.CanMoveDown = true;
            ladder.LandingLocalPosition = 0f;
            Dirty(ent.Owner, ladder);

            EnsureComp<CMUZLevelFallThroughComponent>(ent.Owner);
        }
        else
        {
            RemComp<CMUZLevelLadderComponent>(ent.Owner);
            RemComp<CMUZLevelFallThroughComponent>(ent.Owner);
        }

        _appearance.SetData(ent.Owner, HatchVisuals.Open, open);
        _appearance.SetData(ent.Owner, HatchVisuals.Invisible, ent.Comp.InvisibleWhenClosed && !open);
    }
}
