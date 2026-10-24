using System;
using System.Collections.Generic;
using Content.Shared.GameTicking;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    private const int CookoffEntityBudget = 64;
    private const int CookoffRoundBudget = 32;
    private const int CookoffGrenadeBudget = 2;
    private const int CookoffBatchSize = 8;

    [Dependency] private SharedContainerSystem _laundryContainers = default!;

    private readonly Queue<LaundryCookoffJob> _cookoffJobs = new();

    private sealed class LaundryCookoffJob
    {
        public readonly EntityUid Washer;
        public readonly uint CycleId;
        public readonly bool Ammunition;
        public readonly Stack<(BaseContainer Container, int Index)> Pending = new();
        public readonly HashSet<EntityUid> Visited = new();
        public EntityUid? Magazine;
        public int MagazineRemaining;
        public int MagazineConsumed;
        public bool Announced;

        public LaundryCookoffJob(EntityUid washer, uint cycleId, bool ammunition, BaseContainer contents)
        {
            Washer = washer;
            CycleId = cycleId;
            Ammunition = ammunition;
            Pending.Push((contents, contents.ContainedEntities.Count - 1));
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _cookoffJobs.Clear();
        _popcornSources.Clear();
        _popcornQueue.Clear();
        _washerPriming.Clear();
    }

    private void UpdateLaundryCookoff()
    {
        var entityBudget = CookoffEntityBudget;
        var roundBudget = CookoffRoundBudget;
        var grenadeBudget = CookoffGrenadeBudget;
        var jobs = Math.Min(_cookoffJobs.Count, CookoffEntityBudget);
        while (jobs-- > 0 && entityBudget > 0 && roundBudget > 0 && grenadeBudget > 0)
        {
            var job = _cookoffJobs.Dequeue();
            if (TerminatingOrDeleted(job.Washer) || EntityManager.IsQueuedForDeletion(job.Washer)
                || !TryComp<CMULaundryComponent>(job.Washer, out var laundry)
                || (laundry.FinishAt == null || laundry.CycleId != job.CycleId))
                continue;
            if (Paused(job.Washer))
            {
                _cookoffJobs.Enqueue(job);
                continue;
            }

            if (!ProcessCookoffJob(job, (job.Washer, laundry), ref entityBudget, ref roundBudget, ref grenadeBudget))
                _cookoffJobs.Enqueue(job);
        }
    }

    private bool ProcessCookoffJob(LaundryCookoffJob job, Entity<CMULaundryComponent> washer,
        ref int entityBudget, ref int roundBudget, ref int grenadeBudget)
    {
        var user = washer.Comp.CycleUser is { } actor && !TerminatingOrDeleted(actor) ? actor : (EntityUid?) null;
        for (var step = 0; step < CookoffBatchSize && entityBudget > 0 && roundBudget > 0 && grenadeBudget > 0; step++)
        {
            entityBudget--;
            if (job.Magazine is { } magazine)
            {
                if (!TerminatingOrDeleted(magazine) && IsInsideWasher(magazine, washer.Owner)
                    && TryComp<BallisticAmmoProviderComponent>(magazine, out var provider))
                {
                    if (!ProcessLaundryMagazine(job, washer, (magazine, provider), user, ref roundBudget, ref grenadeBudget))
                        return true;
                    if (job.MagazineRemaining > 0)
                        return false;
                    if (job.MagazineConsumed > 0)
                    {
                        var count = new GetAmmoCountEvent();
                        RaiseLocalEvent(magazine, ref count);
                        if (count.Count == 0)
                            QueueDel(magazine);
                    }
                }
                job.Magazine = null;
                continue;
            }

            if (!job.Pending.TryPop(out var cursor))
                return true;
            if (TerminatingOrDeleted(cursor.Container.Owner))
                continue;
            var index = Math.Min(cursor.Index, cursor.Container.ContainedEntities.Count - 1);
            if (index < 0)
                continue;
            var item = cursor.Container.ContainedEntities[index];
            if (index > 0)
                job.Pending.Push((cursor.Container, index - 1));
            if (!job.Visited.Add(item) || TerminatingOrDeleted(item) || !IsInsideWasher(item, washer.Owner))
                continue;
            if (IsLaundryGrenade(item))
            {
                if (!job.Ammunition)
                {
                    grenadeBudget--;
                    var rounds = 0;
                    PrimeLaundryGrenade(washer, item, user, ref rounds);
                    if (TerminatingOrDeleted(washer.Owner) || EntityManager.IsQueuedForDeletion(washer.Owner)
                        || (washer.Comp.FinishAt == null || washer.Comp.CycleId != job.CycleId))
                        return true;
                    if (rounds > 0 && GetPopcornCapacity(washer.Owner) > 0)
                        StartLaundryPopcorn(washer, rounds, user);
                }
                continue;
            }
            if (TryComp<BallisticAmmoProviderComponent>(item, out var magazineComp))
            {
                var launcherMagazine = magazineComp.Proto is { } proto && IsLaundryGrenadePrototype(proto);
                var count = new GetAmmoCountEvent();
                RaiseLocalEvent(item, ref count);
                // Inspect lazy ammunition only during its stage; spawned rounds always go through TakeAmmo.
                var inspectLazy = job.Ammunition ? !launcherMagazine : launcherMagazine;
                var remaining = inspectLazy ? count.Count : magazineComp.Container.ContainedEntities.Count;
                if (remaining > 0)
                {
                    job.Magazine = item;
                    job.MagazineRemaining = remaining;
                    job.MagazineConsumed = 0;
                }
                continue;
            }
            if (job.Ammunition && TryComp<CartridgeAmmoComponent>(item, out var cartridge))
            {
                if (!cartridge.Spent)
                {
                    if (GetPopcornCapacity(washer.Owner) == 0)
                    {
                        ReportCookoffLimit(washer);
                        return true;
                    }
                    QueueDel(item);
                    roundBudget--;
                    StartLaundryPopcorn(washer, 1, user);
                    AnnounceCookoff(job, washer);
                }
                continue;
            }
            if (TryComp<ContainerManagerComponent>(item, out var manager))
            {
                foreach (var container in manager.Containers.Values)
                    job.Pending.Push((container, container.ContainedEntities.Count - 1));
            }
        }
        return job.Magazine == null && job.Pending.Count == 0;
    }

    private bool ProcessLaundryMagazine(LaundryCookoffJob job, Entity<CMULaundryComponent> washer,
        Entity<BallisticAmmoProviderComponent> magazine, EntityUid? user, ref int roundBudget, ref int grenadeBudget)
    {
        var capacity = GetPopcornCapacity(washer.Owner);
        if (job.Ammunition && capacity == 0)
        {
            ReportCookoffLimit(washer);
            return false;
        }
        var amount = Math.Min(Math.Min(CookoffBatchSize, roundBudget), Math.Min(job.MagazineRemaining, capacity));
        if (!job.Ammunition)
            amount = 1;
        var take = new TakeAmmoEvent(amount, new(amount), Transform(washer.Owner).Coordinates, user);
        RaiseLocalEvent(magazine.Owner, take);
        roundBudget -= amount;
        job.MagazineRemaining -= amount;
        if (take.Ammo.Count == 0)
            job.MagazineRemaining = 0;
        var consumed = 0;
        foreach (var (ammo, _) in take.Ammo)
        {
            if (ammo is not { } round || TerminatingOrDeleted(round))
                continue;
            if (IsLaundryGrenade(round))
            {
                if (job.Ammunition)
                    _laundryGun.RestoreLaundryAmmo(magazine, round);
                else
                {
                    grenadeBudget--;
                    var rounds = 0;
                    if (!PrimeLaundryGrenade(washer, round, user, ref rounds) && !TerminatingOrDeleted(round))
                        _laundryGun.RestoreLaundryAmmo(magazine, round);
                    if (TerminatingOrDeleted(washer.Owner) || EntityManager.IsQueuedForDeletion(washer.Owner)
                        || (washer.Comp.FinishAt == null || washer.Comp.CycleId != job.CycleId))
                        return false;
                    if (rounds > 0)
                    {
                        StartLaundryPopcorn(washer, rounds, user);
                        AnnounceCookoff(job, washer);
                    }
                }
            }
            else if (job.Ammunition
                && TryComp<CartridgeAmmoComponent>(round, out var cartridge) && !cartridge.Spent)
            {
                consumed++;
                QueueDel(round);
            }
            else
                _laundryGun.RestoreLaundryAmmo(magazine, round);
        }
        if (consumed > 0)
        {
            job.MagazineConsumed += consumed;
            StartLaundryPopcorn(washer, consumed, user);
            AnnounceCookoff(job, washer);
        }
        return true;
    }

    private void AnnounceCookoff(LaundryCookoffJob job, Entity<CMULaundryComponent> washer)
    {
        if (job.Announced)
            return;
        job.Announced = true;
        _popup.PopupEntity(Loc.GetString("cmu-laundry-cookoff"), washer);
    }

    private void ReportCookoffLimit(Entity<CMULaundryComponent> washer)
        => _popup.PopupEntity(Loc.GetString("cmu-laundry-ammo-limit"), washer);

    private bool IsInsideWasher(EntityUid item, EntityUid washer)
    {
        while (_laundryContainers.TryGetContainingContainer(item, out var container))
        {
            if (container.Owner == washer)
                return true;
            item = container.Owner;
        }
        return false;
    }
}
