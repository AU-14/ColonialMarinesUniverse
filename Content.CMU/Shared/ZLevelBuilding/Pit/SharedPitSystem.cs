// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Verbs;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

public abstract class SharedPitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PitComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<PitComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var pit = ent;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-pit-build-verb"),
            Act = () => OpenBuildMenu(pit, user),
            ClientExclusive = true,
        });
    }

    protected virtual void OpenBuildMenu(Entity<PitComponent> pit, EntityUid user)
    {
    }
}
