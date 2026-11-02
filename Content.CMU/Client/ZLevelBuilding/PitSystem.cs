// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.ZLevelBuilding;

public sealed class PitSystem : SharedPitSystem
{
    private SimpleRadialMenu? _menu;

    private static readonly (PitBuildOption Option, string Loc, SpriteSpecifier Icon)[] Options =
    {
        (PitBuildOption.Expand, "au14-pit-build-expand", new SpriteSpecifier.Rsi(new ResPath("_RMC14/Structures/Xenos/xeno_tunnel.rsi"), "hole")),
        (PitBuildOption.Rope, "au14-pit-build-rope", new SpriteSpecifier.Rsi(new ResPath("CMU14/Structures/rope_item.rsi"), "rope")),
        (PitBuildOption.Hatch, "au14-pit-build-hatch", new SpriteSpecifier.Rsi(new ResPath("CMU14/Structures/hatch.rsi"), "closed")),
        (PitBuildOption.Ladder, "au14-pit-build-ladder", new SpriteSpecifier.Rsi(new ResPath("CMU14/Structures/pit_ladder.rsi"), "ladder")),
        (PitBuildOption.Steps, "au14-pit-build-steps", new SpriteSpecifier.Rsi(new ResPath("CMU14/Structures/stairs.rsi"), "ramptop")),
    };

    public override void Shutdown()
    {
        base.Shutdown();
        _menu?.Dispose();
        _menu = null;
    }

    protected override void OpenBuildMenu(Entity<PitComponent> pit, EntityUid user)
    {
        _menu?.Dispose();

        var pitUid = pit.Owner;
        var options = new List<RadialMenuOptionBase>();
        foreach (var (option, loc, icon) in Options)
        {
            options.Add(new RadialMenuActionOption<PitBuildOption>(_ => OnOptionChosen(pitUid, option), option)
            {
                ToolTip = Loc.GetString(loc),
                IconSpecifier = RadialMenuIconSpecifier.With(icon),
            });
        }

        var menu = new SimpleRadialMenu();
        menu.SetButtons(options);
        menu.Track(pitUid);
        menu.OnClose += () => _menu = null;
        menu.OpenOverMouseScreenPosition();
        _menu = menu;
    }

    private void OnOptionChosen(EntityUid pit, PitBuildOption option)
    {
        RaiseNetworkEvent(new PitBuildEvent(GetNetEntity(pit), option));
    }
}
