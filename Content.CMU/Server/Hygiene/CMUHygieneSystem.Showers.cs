using System;
using System.Collections.Generic;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared.Coordinates;
using Content.Shared.Examine;
using Content.Shared.Interaction;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    private readonly HashSet<EntityUid> _showerNearby = new();
    private readonly List<EntityUid> _staleShowerProgress = new();

    private void OnShowerInit(Entity<CMUShowerComponent> ent, ref MapInitEvent args)
    {
        SetShowerRunning(ent, ent.Comp.Running);
    }

    private void SetShowerRunning(Entity<CMUShowerComponent> ent, bool running)
    {
        ent.Comp.Running = running;
        if (running)
            EnsureComp<CMUActiveShowerComponent>(ent.Owner);
        else
            RemComp<CMUActiveShowerComponent>(ent.Owner);
        ent.Comp.SecondsRemaining = running ? ent.Comp.AutoShutoffSeconds : 0f;
        ent.Comp.Progress.Clear();
        ent.Comp.UntilTick = 1f;
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Running, running);
        if (TryComp<CMUShowerControlsComponent>(ent.Owner, out var controls))
        {
            controls.Running = running;
            Dirty(ent.Owner, controls);
        }
    }

    private void OnShowerActivate(Entity<CMUShowerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        ToggleShower(ent, args.User);
    }

    private void OnShowerToggleRequested(EntityUid uid, CMUShowerComponent component, CMUShowerToggleRequestEvent args)
        => ToggleShower((uid, component), args.User);

    private void ToggleShower(Entity<CMUShowerComponent> ent, EntityUid user)
    {
        SetShowerRunning(ent, !ent.Comp.Running);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Running ? "cmu-shower-on" : "cmu-shower-off"), ent, user);
        if (ent.Comp.Running)
            _audio.PlayPvs(WaterSound, ent);
    }

    private void OnShowerExamine(Entity<CMUShowerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Running ? "cmu-shower-running" : "cmu-shower-idle"));
        if (ent.Comp.Running)
            args.PushMarkup(Loc.GetString("cmu-shower-time-left", ("seconds", (int) Math.Ceiling(ent.Comp.SecondsRemaining))));
    }

    public bool CanWashBody(EntityUid uid)
    {
        return !HasClothing(uid, CMUItemStainSystem.JumpsuitSlot) &&
               !HasClothing(uid, CMUItemStainSystem.OuterClothingSlot);
    }

    private bool HasClothing(EntityUid uid, string slot)
    {
        return _inventory.TryGetSlotEntity(uid, slot, out var clothing) && clothing != null;
    }

    private void UpdateShowers(float frameTime)
    {
        var showers = EntityQueryEnumerator<CMUActiveShowerComponent, CMUShowerComponent>();
        while (showers.MoveNext(out var uid, out _, out var shower))
        {
            if (!shower.Running)
                continue;
            shower.SecondsRemaining -= frameTime;
            if (shower.SecondsRemaining <= 0f)
            {
                SetShowerRunning((uid, shower), false);
                _popup.PopupEntity(Loc.GetString("cmu-shower-auto-off"), uid);
                continue;
            }
            shower.UntilTick -= frameTime;
            if (shower.UntilTick > 0)
                continue;
            shower.UntilTick = 1f;
            var nearby = _showerNearby;
            nearby.Clear();
            _lookup.GetEntitiesInRange(uid.ToCoordinates(), shower.Radius, nearby, LookupFlags.Uncontained);
            _staleShowerProgress.Clear();
            foreach (var previous in shower.Progress.Keys)
            {
                if (!nearby.Contains(previous) || !CanWashBody(previous))
                    _staleShowerProgress.Add(previous);
            }
            foreach (var previous in _staleShowerProgress)
                shower.Progress.Remove(previous);
            foreach (var target in nearby)
            {
                if (!TryComp<CMUResidueComponent>(target, out var residue) || residue.Color == null)
                {
                    shower.Progress.Remove(target);
                    continue;
                }
                if (!CanWashBody(target))
                    continue;
                shower.Progress.TryGetValue(target, out var progress);
                progress += 1f;
                if (progress < shower.WashSeconds)
                {
                    shower.Progress[target] = progress;
                    continue;
                }
                _residue.Clean(target);
                shower.Progress.Remove(target);
                _popup.PopupEntity(Loc.GetString("cmu-shower-clean"), target, target);
            }
        }

    }
}
