// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Steam;
using Content.Shared.Examine;

namespace Content.Server.CMU14.Steam;

// The steam generator turns the power supply of its cable on while there is steam to spend and off when there is not.
public sealed class SteamGeneratorSystem : EntitySystem
{
    [Dependency] private readonly SteamGasSystem _steam = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SteamGeneratorComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SteamGeneratorComponent, PowerSupplierComponent>();
        while (query.MoveNext(out var uid, out var comp, out var supplier))
        {
            var running = _steam.TryUse(uid, comp.SteamPerSecond * frameTime);

            if (supplier.Enabled != running)
                supplier.Enabled = running;

            if (comp.Running != running)
            {
                comp.Running = running;
                Dirty(uid, comp);
            }
        }
    }

    private void OnExamined(Entity<SteamGeneratorComponent> generator, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(generator.Comp.Running ? "au14-generator-examine-running" : "au14-generator-examine-idle"));
    }
}
