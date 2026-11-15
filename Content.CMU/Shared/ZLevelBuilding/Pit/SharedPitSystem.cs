// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Insurgency.Sapper;
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

        // The plate with the code of a mine is a sapper's work: the verb for it is a sapper's, and nobody else sees it.
        if (HasComp<SapperComponent>(user))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("au14-pit-build-mineplate-verb"),
                Act = () => BuildMinePlate(pit, user),
                Priority = -1,
            });
        }
    }

    // Starts building the plate over the pit (on the server).
    protected virtual void BuildMinePlate(Entity<PitComponent> pit, EntityUid user)
    {
    }

    protected virtual void OpenBuildMenu(Entity<PitComponent> pit, EntityUid user)
    {
    }
}
