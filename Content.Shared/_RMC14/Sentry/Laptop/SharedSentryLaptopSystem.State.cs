using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Sentry.Laptop;

public abstract partial class SharedSentryLaptopSystem
{
    private void OnLaptopGetState(Entity<SentryLaptopComponent> ent, ref ComponentGetState args)
    {
        // PVS builds states in parallel. Filter copies rather than repairing gameplay collections here.
        // Several deleted dictionary keys would otherwise all become NetEntity.Invalid and collide.
        var sentries = new HashSet<NetEntity>();
        foreach (var sentry in ent.Comp.LinkedSentries)
        {
            if (TryGetNetEntity(sentry, out var net) && net != NetEntity.Invalid)
                sentries.Add(net.Value);
        }

        var names = new Dictionary<NetEntity, string>();
        foreach (var (sentry, name) in ent.Comp.SentryCustomNames)
        {
            if (TryGetNetEntity(sentry, out var net) && net != NetEntity.Invalid)
                names.Add(net.Value, name);
        }

        var watchers = new List<NetEntity>();
        foreach (var watcher in ent.Comp.Watchers)
        {
            if (TryGetNetEntity(watcher, out var net) && net != NetEntity.Invalid)
                watchers.Add(net.Value);
        }

        TryGetNetEntity(ent.Comp.CurrentCamera, out var camera);
        args.State = new SentryLaptopComponentState
        {
            IsOpen = ent.Comp.IsOpen,
            IsPowered = ent.Comp.IsPowered,
            Range = ent.Comp.Range,
            LinkedSentries = sentries,
            MaxLinkedSentries = ent.Comp.MaxLinkedSentries,
            SentryCustomNames = names,
            Watchers = watchers,
            CurrentCamera = camera,
        };
    }

    private void OnLaptopHandleState(Entity<SentryLaptopComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not SentryLaptopComponentState state)
            return;

        ent.Comp.IsOpen = state.IsOpen;
        ent.Comp.IsPowered = state.IsPowered;
        ent.Comp.Range = state.Range;
        EnsureEntitySet<SentryLaptopComponent>(state.LinkedSentries, ent, ent.Comp.LinkedSentries);
        ent.Comp.MaxLinkedSentries = state.MaxLinkedSentries;
        EnsureEntityDictionary<SentryLaptopComponent, string>(state.SentryCustomNames, ent, ent.Comp.SentryCustomNames);
        EnsureEntityList<SentryLaptopComponent>(state.Watchers, ent, ent.Comp.Watchers);
        ent.Comp.CurrentCamera = EnsureEntity<SentryLaptopComponent>(state.CurrentCamera, ent);
    }
}

[Serializable, NetSerializable]
public sealed class SentryLaptopComponentState : ComponentState
{
    public bool IsOpen { get; init; }
    public bool IsPowered { get; init; }
    public float Range { get; init; }
    public HashSet<NetEntity> LinkedSentries { get; init; } = new();
    public int MaxLinkedSentries { get; init; }
    public Dictionary<NetEntity, string> SentryCustomNames { get; init; } = new();
    public List<NetEntity> Watchers { get; init; } = new();
    public NetEntity? CurrentCamera { get; init; }
}
