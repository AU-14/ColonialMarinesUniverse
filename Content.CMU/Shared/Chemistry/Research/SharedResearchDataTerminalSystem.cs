using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared.CMU14.Chemistry.Research;

public abstract partial class SharedResearchDataTerminalSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private INetManager _net = default!;


    // Legacy callers and admin commands use the corporate research account.
    public int Clearance { get => GetClearance("corporate"); set => _clearance["corporate"] = value; }
    public int Credits { get => GetCredits("corporate"); set => _credits["corporate"] = value; }
    public bool DDIDiscovered = false;

    private readonly Dictionary<string, int> _credits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _clearance = new(StringComparer.OrdinalIgnoreCase);
    // Sample data is the synthesis simulator's own currency. It is earned by XRF scans alongside research
    // credits so that editing chemicals no longer competes with buying clearance.
    private readonly Dictionary<string, int> _sampleData = new(StringComparer.OrdinalIgnoreCase);

    public int GetCredits(string faction) => _credits.GetValueOrDefault(faction);
    public int GetClearance(string faction) => _clearance.GetValueOrDefault(faction, 1);
    public int GetSampleData(string faction) => _sampleData.GetValueOrDefault(faction);

    /// <summary>Unassigned synthesis machines use their nearest research terminal on the same map.</summary>
    public string GetFaction(EntityUid machine)
    {
        if (TryComp<ResearchDataTerminalComponent>(machine, out var terminal))
            return terminal.Faction;
        if (TryComp<XRFScannerComponent>(machine, out var scanner))
            return scanner.Faction;
        var transform = Transform(machine);
        var transforms = EntityManager.System<SharedTransformSystem>();
        var position = transforms.GetWorldPosition(machine);
        var distance = float.MaxValue;
        var faction = "corporate";
        var query = EntityQueryEnumerator<ResearchDataTerminalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var other))
        {
            if (other.MapUid != transform.MapUid)
                continue;
            var candidate = System.Numerics.Vector2.DistanceSquared(position, transforms.GetWorldPosition(uid));
            if (candidate >= distance)
                continue;
            distance = candidate;
            faction = comp.Faction;
        }
        return faction;
    }

    protected readonly int _researchLevelIncreaseMult = 3;
    public override void Initialize()
    {
        base.Initialize();
        // Research balances are server-authoritative: the server only applies its own updates,
        // clients mirror the ones it broadcasts.
        if (_net.IsClient)
            SubscribeAllEvent<UpdateDataTerminalClearanceEvent>(OnUpdateClearance);
        else
            SubscribeLocalEvent<UpdateDataTerminalClearanceEvent>(OnUpdateClearance);

    }

    protected void ResetResearchAccounts()
    {
        _credits.Clear();
        _clearance.Clear();
        _sampleData.Clear();
    }

    protected virtual void OnResearchBalanceChanged(string faction) { }

    /// <summary>
    /// Sets a faction's research balances. <paramref name="points"/> is absolute; <paramref name="clearance"/>
    /// and <paramref name="sampleData"/> are left unchanged when -1.
    /// </summary>
    public void UpdateClearance(int points, int clearance, string faction = "corporate", int sampleData = -1)
    {
        var ev = new UpdateDataTerminalClearanceEvent(clearance, points, faction, sampleData);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
    }

    /// <summary>Sets a faction's sample data without touching credits or clearance.</summary>
    public void UpdateSampleData(int sampleData, string faction)
        => UpdateClearance(GetCredits(faction), -1, faction, Math.Max(0, sampleData));


    private void OnUpdateClearance(UpdateDataTerminalClearanceEvent args)
    {
        var oldClearance = GetClearance(args.Faction);
        if(args.Clearance != -1)
        {
            _clearance[args.Faction] = args.Clearance;
        }
        if (args.SampleData != -1)
        {
            _sampleData[args.Faction] = args.SampleData;
        }
        _credits[args.Faction] = args.Credits;
        OnResearchBalanceChanged(args.Faction);
        if (args.Clearance != -1 && args.Clearance != oldClearance)
            OnClearanceChanged(args.Faction, oldClearance, args.Clearance);
    }

    /// <summary>Called after a faction's research clearance changes. Only the server override acts.</summary>
    protected virtual void OnClearanceChanged(string faction, int oldClearance, int newClearance) { }
}
