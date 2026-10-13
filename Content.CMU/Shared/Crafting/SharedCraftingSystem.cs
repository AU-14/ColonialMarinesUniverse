// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Content.Shared.Verbs;

namespace Content.Shared.CMU14.Crafting;

// Adds the "Craft" button to the right-click menu of the player's own character.
public abstract class SharedCraftingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoAfterComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<DoAfterComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target || !args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("au14-craft-verb"),
            Priority = 5,
            ClientExclusive = true,
            Act = () => OpenMenu(user),
        });
    }

    protected virtual void OpenMenu(EntityUid user)
    {
    }
}
