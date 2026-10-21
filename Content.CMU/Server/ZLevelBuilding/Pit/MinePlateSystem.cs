// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// The plates with the code of a mine: built over a pit, they look like the floor, and they give way under whoever is not of the faction of the one who built them.
public sealed class MinePlateSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MinePlateComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<MinePlateComponent, StepTriggeredOnEvent>(OnStepped);
    }

    // Puts a plate on a tile, for the one who builds it (their factions are the plate's own).
    public EntityUid Lay(string prototype, EntityCoordinates coordinates, EntityUid placer)
    {
        var plate = Spawn(prototype, coordinates);
        var comp = EnsureComp<MinePlateComponent>(plate);
        comp.Placer = placer;

        if (TryComp<NpcFactionMemberComponent>(placer, out var member))
        {
            foreach (var faction in member.Factions)
                comp.Factions.Add(faction.Id);
        }

        return plate;
    }


    #region Stepping

    // The plate is for whoever is not of the faction of the one who laid it; only a living mob sets it off.
    private void OnStepAttempt(Entity<MinePlateComponent> plate, ref StepTriggerAttemptEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Tripper) || _mobState.IsDead(args.Tripper))
            return;

        args.Continue = IsForeign(plate, args.Tripper);
    }

    public bool IsForeign(Entity<MinePlateComponent> plate, EntityUid stepper)
    {
        if (plate.Comp.Placer == stepper)
            return false;

        // A plate laid by someone with no faction holds only for them.
        if (plate.Comp.Factions.Count == 0)
            return true;

        // Of a faction of the plate: it is not for them. Friends of those factions are not either.
        if (TryComp<NpcFactionMemberComponent>(stepper, out var member) && member.Factions.Any(faction => plate.Comp.Factions.Contains(faction.Id)))
            return false;

        foreach (var faction in plate.Comp.Factions)
        {
            if (_factions.IsFactionFriendly(faction, stepper))
                return false;
        }

        return true;
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
