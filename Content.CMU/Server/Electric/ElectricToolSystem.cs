// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using System.Text;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Mining;
using Content.Shared.Interaction.Events;
using Content.Shared.PowerCell;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Electric;

// The tools on a power cell: a use is paid for from the cell before it starts, and the prospector scans for deposits.
public sealed class ElectricToolSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<OreDepositComponent>> _ores = new();
    private readonly HashSet<Entity<OilDepositComponent>> _oils = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ElectricToolComponent, ToolUseAttemptEvent>(OnToolUse);
        SubscribeLocalEvent<ProspectorComponent, UseInHandEvent>(OnProspect);
        SubscribeLocalEvent<ElectricToolComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<ElectricToolComponent, MeleeHitEvent>(OnMeleeHit);
    }

    // A tool of the tool system: the charge is taken when the work begins, or the work does not begin.
    private void OnToolUse(EntityUid tool, ElectricToolComponent comp, ToolUseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_cell.TryUseCharge(tool, comp.ChargePerUse, args.User))
        {
            _popup.PopupEntity(Loc.GetString("au14-tool-no-charge"), tool, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    // A blow with the tool needs a charge in the cell: without it the tool is a dead weight.
    private void OnAttemptMelee(Entity<ElectricToolComponent> tool, ref AttemptMeleeEvent args)
    {
        if (tool.Comp.ChargePerSwing <= 0f || args.Cancelled)
            return;

        if (!_cell.HasCharge(tool.Owner, tool.Comp.ChargePerSwing, args.User))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("au14-tool-no-charge");
        }
    }

    private void OnMeleeHit(Entity<ElectricToolComponent> tool, ref MeleeHitEvent args)
    {
        if (tool.Comp.ChargePerSwing > 0f)
            _cell.TryUseCharge(tool.Owner, tool.Comp.ChargePerSwing, args.User);
    }

    private void OnProspect(Entity<ProspectorComponent> device, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_cell.TryUseCharge(device.Owner, device.Comp.ChargePerScan, args.User))
        {
            _popup.PopupEntity(Loc.GetString("au14-tool-no-charge"), device, args.User, PopupType.SmallCaution);
            return;
        }

        var here = _transform.GetMapCoordinates(args.User);
        var text = new StringBuilder();

        _ores.Clear();
        _lookup.GetEntitiesInRange(Transform(args.User).Coordinates, device.Comp.Range, _ores);
        text.AppendLine(Describe(here, _ores, "au14-prospector-ore", o => o.Owner));

        _oils.Clear();
        _lookup.GetEntitiesInRange(Transform(args.User).Coordinates, device.Comp.Range, _oils);
        text.Append(Describe(here, _oils, "au14-prospector-oil", o => o.Owner));

        _popup.PopupEntity(text.ToString().TrimEnd(), device, args.User, PopupType.Medium);
    }

    private string Describe<T>(MapCoordinates here, HashSet<Entity<T>> found, string key, Func<Entity<T>, EntityUid> uid)
        where T : IComponent
    {
        if (found.Count == 0)
            return Loc.GetString(key + "-none");

        var nearest = EntityUid.Invalid;
        var best = float.MaxValue;
        foreach (var entity in found)
        {
            var delta = _transform.GetMapCoordinates(uid(entity)).Position - here.Position;
            var distance = delta.Length();
            if (distance < best)
            {
                best = distance;
                nearest = uid(entity);
            }
        }

        var direction = _transform.GetMapCoordinates(nearest).Position - here.Position;
        return Loc.GetString(key, ("count", found.Count), ("distance", (int) Math.Round(best)), ("direction", Compass(direction)));
    }

    private string Compass(Vector2 delta)
    {
        // The angle from the north, clockwise, in eight sides.
        var angle = Math.Atan2(delta.X, delta.Y) * 180.0 / Math.PI;
        if (angle < 0)
            angle += 360;

        var side = (int) Math.Round(angle / 45.0) % 8;
        return Loc.GetString($"au14-compass-{side}");
    }
}
