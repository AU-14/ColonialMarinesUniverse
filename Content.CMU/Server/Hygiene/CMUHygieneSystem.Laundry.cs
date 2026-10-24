using System;
using Content.Server.Power.Components;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Nutrition.Components;
using Content.Shared.Power;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    private void OnLaundrySignal(Entity<CMULaundryComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port != "Timer" || ent.Comp.FinishAt != null)
            return;

        if (TryStartLaundry(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-laundry-remote-start"), ent);
            return;
        }

        var message = ent.Comp.Broken ? "cmu-laundry-broken" :
            !HasLaundryPower(ent.Owner) ? "cmu-laundry-no-power" : "cmu-laundry-empty";
        _popup.PopupEntity(Loc.GetString(message), ent);
    }

    private void OnLaundryInit(Entity<CMULaundryComponent> ent, ref MapInitEvent args)
    {
        RefreshLaundryDamage(ent, announce: false);
        if (ent.Comp.FinishAt != null && !ent.Comp.Broken && HasLaundryPower(ent.Owner))
            EnsureComp<CMUActiveLaundryComponent>(ent.Owner);
        else
            StopLaundry(ent);
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Powered, HasLaundryPower(ent.Owner) && !ent.Comp.Broken);
        _appearance.SetData(ent.Owner, OpenableVisuals.Opened, ent.Comp.FinishAt == null);
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Running, ent.Comp.FinishAt != null);
    }

    private bool HasLaundryPower(EntityUid uid)
        => TryComp<ApcPowerReceiverComponent>(uid, out var power) && power.Powered;

    private void OnLaundryPower(Entity<CMULaundryComponent> ent, ref PowerChangedEvent args)
    {
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Powered, args.Powered && !ent.Comp.Broken);
        if (!args.Powered && ent.Comp.FinishAt != null)
        {
            StopLaundry(ent);
            _popup.PopupEntity(Loc.GetString("cmu-laundry-power-lost"), ent);
        }
    }

    private void OnLaundryVerbs(Entity<CMULaundryComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.FinishAt == null ? "cmu-laundry-start" : "cmu-laundry-cancel"),
            Priority = 10,
            Act = () =>
            {
                if (ent.Comp.FinishAt != null)
                {
                    StopLaundry(ent);
                    _popup.PopupEntity(Loc.GetString("cmu-laundry-off"), ent, user);
                }
                else if (ent.Comp.Broken)
                    _popup.PopupEntity(Loc.GetString("cmu-laundry-broken"), ent, user);
                else if (!HasLaundryPower(ent.Owner))
                    _popup.PopupEntity(Loc.GetString("cmu-laundry-no-power"), ent, user);
                else if (TryStartLaundry(ent.Owner, user))
                    _popup.PopupEntity(Loc.GetString("cmu-laundry-on"), ent, user);
                else
                    _popup.PopupEntity(Loc.GetString("cmu-laundry-empty"), ent, user);
            },
        });
    }

    public bool TryStartLaundry(EntityUid uid, EntityUid? user = null)
    {
        if (!HasLaundryPower(uid))
            return false;
        if (!TryComp<CMULaundryComponent>(uid, out var laundry) || laundry.Broken || laundry.FinishAt != null ||
            !TryComp<StorageComponent>(uid, out var storage) || storage.Container.ContainedEntities.Count == 0)
            return false;
        EnsureComp<CMUActiveLaundryComponent>(uid);
        laundry.CycleId++;
        laundry.FinishAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0.1f, laundry.CycleSeconds));
        laundry.AmmoCookoffAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0f, laundry.AmmoCookoffSeconds));
        laundry.CookoffAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0f, laundry.CookoffSeconds));
        laundry.CycleUser = user;
        _appearance.SetData(uid, OpenableVisuals.Opened, false);
        _appearance.SetData(uid, CMUHygieneVisuals.Running, true);
        _ambient.SetAmbience(uid, true);
        return true;
    }

    public void StopLaundry(Entity<CMULaundryComponent> ent)
    {
        RemComp<CMUActiveLaundryComponent>(ent.Owner);
        ent.Comp.FinishAt = null;
        ent.Comp.CookoffAt = null;
        ent.Comp.AmmoCookoffAt = null;
        ent.Comp.CycleUser = null;
        _ambient.SetAmbience(ent.Owner, false);
        _appearance.SetData(ent.Owner, CMUHygieneVisuals.Running, false);
        _appearance.SetData(ent.Owner, OpenableVisuals.Opened, true);
    }

    private void OnLaundryExamine(Entity<CMULaundryComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Broken)
        {
            args.PushMarkup(Loc.GetString("cmu-laundry-broken"));
            return;
        }
        if (!HasLaundryPower(ent.Owner))
        {
            args.PushMarkup(Loc.GetString("cmu-laundry-no-power"));
            return;
        }
        args.PushMarkup(Loc.GetString(ent.Comp.FinishAt == null ? "cmu-laundry-idle" : "cmu-laundry-running"));
        if (ent.Comp.FinishAt is { } finish)
            args.PushMarkup(Loc.GetString("cmu-laundry-time-left",
                ("seconds", Math.Max(0, (int) Math.Ceiling((finish - _timing.CurTime).TotalSeconds)))));
    }

    private void OnInsert(Entity<CMULaundryComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (ent.Comp.FinishAt != null && args.Container.ID == StorageComponent.ContainerId)
            args.Cancel();
    }

    private void OnRemove(Entity<CMULaundryComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.FinishAt != null && args.Container.ID == StorageComponent.ContainerId)
            args.Cancel();
    }

    private void UpdateLaundry()
    {
        var machines = EntityQueryEnumerator<CMUActiveLaundryComponent, CMULaundryComponent, StorageComponent>();
        while (machines.MoveNext(out var uid, out _, out var laundry, out var storage))
        {
            if (laundry.FinishAt is not { } finish)
                continue;
            if (laundry.Broken || !HasLaundryPower(uid))
            {
                StopLaundry((uid, laundry));
                continue;
            }
            if (laundry.AmmoCookoffAt is { } ammoCookoff && _timing.CurTime >= ammoCookoff)
            {
                laundry.AmmoCookoffAt = null;
                CookOffLaundry((uid, laundry), storage, ammunition: true);
                if (TerminatingOrDeleted(uid) || laundry.FinishAt == null)
                    continue;
            }
            if (laundry.CookoffAt is { } cookoff && _timing.CurTime >= cookoff)
            {
                // Clear first: a destructive payload must not be processed again next tick.
                laundry.CookoffAt = null;
                CookOffLaundry((uid, laundry), storage, ammunition: false);
                if (TerminatingOrDeleted(uid) || laundry.FinishAt == null)
                    continue;
            }
            if (_timing.CurTime < finish)
                continue;
            foreach (var item in storage.Container.ContainedEntities)
                _stains.TryClean(item);
            StopLaundry((uid, laundry));
            _audio.PlayPvs(laundry.CompletionSound, uid);
            _popup.PopupEntity(Loc.GetString("cmu-laundry-done"), uid);
        }
    }
}
