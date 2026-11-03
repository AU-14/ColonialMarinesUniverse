// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.Examine;

namespace Content.Server.CMU14.ZLevelBuilding;

// Tells in the examine text that nothing can be dug below this support.
public sealed class NoDigBelowSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoDigBelowComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<NoDigBelowComponent> support, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("au14-no-dig-examine", ("radius", support.Comp.Radius)));
    }
}
