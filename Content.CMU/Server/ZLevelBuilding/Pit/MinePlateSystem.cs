// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// The plates with the code of a mine: built over a pit, they look like the floor, and they give way under whoever steps on them. There is no friend or foe to them: everybody falls in,
// the one who laid the plate too. The ones of the CLF see where a plate is (see MinePlateMarkSystem on the client), and a mine detector finds one from a tile away.
public sealed class MinePlateSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MinePlateComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<MinePlateComponent, StepTriggeredOnEvent>(OnStepped);
    }

    // Puts a plate on a tile, for the one who builds it.
    public EntityUid Lay(string prototype, EntityCoordinates coordinates, EntityUid placer)
    {
        var plate = Spawn(prototype, coordinates);
        var comp = EnsureComp<MinePlateComponent>(plate);
        comp.Placer = placer;
        return plate;
    }

    #region Stepping

    // Only a living mob sets it off.
    private void OnStepAttempt(Entity<MinePlateComponent> plate, ref StepTriggerAttemptEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Tripper) || _mobState.IsDead(args.Tripper))
            return;

        args.Continue = true;
    }

    private void OnStepped(Entity<MinePlateComponent> plate, ref StepTriggeredOnEvent args)
    {
        Collapse(plate, args.Tripper);
    }

    // The plate gives way: it is gone, and a pit is in its place (whoever is over it falls).
    public void Collapse(Entity<MinePlateComponent> plate, EntityUid? victim)
    {
        var coordinates = Transform(plate).Coordinates;
        Spawn(plate.Comp.Collapse, coordinates);
        QueueDel(plate);

        if (victim != null)
            _popup.PopupEntity(Loc.GetString("au14-mine-plate-gave-way"), victim.Value, victim.Value, PopupType.LargeCaution);
    }

    #endregion
}
