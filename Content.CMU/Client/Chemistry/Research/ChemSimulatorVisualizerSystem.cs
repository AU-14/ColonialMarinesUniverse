using Content.Shared._AU14.Chemistry.Research;
using Robust.Client.GameObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Client._AU14.Chemistry.Research;

public sealed partial class ChemSimulatorVisualizerSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _player = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChemSimulatorComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }


    private void OnAppearanceChanged(Entity<ChemSimulatorComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
    }
}
