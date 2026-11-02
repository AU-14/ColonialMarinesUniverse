using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.CaveSurvey;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server.CMU14.CaveSurvey;

public sealed partial class CMUCaveSurveySystem
{
    public const int ViewSize = 64;
    private const int ViewLevels = 8;
    private int _generation;
    private readonly Dictionary<EntityUid, int> _viewGenerations = new();
    private readonly Queue<Delivery> _outgoing = new();

    private sealed class Delivery
    {
        public required EntityUid Actor;
        public required int Generation;
        public required CMUReconChunk[] Chunks;
        public int Sent;
    }

    private void OnSurface(Entity<CMUCaveSurveyAccessComponent> ent, ref CMUCaveSurveySurfaceMessage args)
    {
        if (args.Actor != ent.Owner || !_ui.IsUiOpen(ent.Owner, Key, args.Actor) || !HasComp<TacticalMapUserComponent>(args.Actor)) return;
        _ui.CloseUi(ent.Owner, Key, args.Actor);
        _ui.TryOpenUi(args.Actor, TacticalMapUserUi.Key, args.Actor);
    }

    private void OnView(Entity<CMUCaveSurveyAccessComponent> ent, ref CMUCaveSurveyViewMessage args)
    {
        if (args.Actor != ent.Owner || !_ui.IsUiOpen(ent.Owner, Key, args.Actor) || args.RequestId < 0 ||
            Math.Abs((long) args.Origin.X) > 1_000_000 || Math.Abs((long) args.Origin.Y) > 1_000_000 ||
            args.MinDepth is < -4096 or > -8 ||
            !TryComp<CMUCaveSurveyMemoryComponent>(ent, out var memory) || memory.Worlds.Count == 0 ||
            _timing.CurTime < memory.NextRequest) return;
        memory.NextRequest = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        if (args.NextWorld && args.RequestId != memory.LastViewRequest)
        {
            var worlds = memory.Worlds.Keys.ToArray();
            memory.SelectedWorld = worlds[(Array.IndexOf(worlds, memory.SelectedWorld) + 1) % worlds.Length];
        }
        memory.LastViewRequest = args.RequestId;
        if (!memory.Worlds.TryGetValue(memory.SelectedWorld, out var data)) return;

        Vector2? operatorPosition = null;
        var operatorDepth = 0;
        if (Transform(ent).MapUid is { } map && TryComp<CMUZLevelMapComponent>(map, out var level) &&
            level.NetworkUid == memory.SelectedWorld)
        {
            operatorPosition = _transform.GetWorldPosition(ent);
            operatorDepth = level.Depth;
        }
        var origin = args.Origin;
        var minDepth = args.MinDepth;
        if (args.Center || args.NextWorld)
        {
            if (!data.TryFirst(out var center, out var depth)) return;
            if (operatorPosition is { } position && operatorDepth < 0)
            {
                center = new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
                depth = operatorDepth;
            }
            origin = new Vector2i(CMUCaveSurveyData.FloorDiv(center.X, ViewSize), CMUCaveSurveyData.FloorDiv(center.Y, ViewSize)) * ViewSize;
            minDepth = CMUCaveSurveyData.FloorDiv(depth, ViewLevels) * ViewLevels;
        }

        const int size = CMUReconGeometry.ChunkSize;
        var cells = new Dictionary<(int Level, int X, int Y), byte[]>();
        foreach (var sample in data.Section(origin, ViewSize, minDepth, ViewLevels))
        {
            var relative = sample.Tile - origin;
            var chunk = (sample.Depth - minDepth, relative.X / size, relative.Y / size);
            if (!cells.TryGetValue(chunk, out var contents)) cells.Add(chunk, contents = new byte[size * size]);
            contents[(relative.Y % size) * size + relative.X % size] = (byte) sample.Material;
        }
        var chunks = cells.Select(p => new CMUReconChunk(p.Key.Level, p.Key.X, p.Key.Y, p.Value, Revision: data.Version)).ToArray();
        var generation = ++_generation;
        _viewGenerations[ent.Owner] = generation;
        var scene = new CMUReconSnapshotMessage(generation, origin, minDepth, ViewLevels, [], [], false, ViewSize, ViewSize)
        {
            RequestId = args.RequestId,
            AtlasId = -generation,
            OperatorPosition = operatorPosition,
            OperatorTile = operatorPosition is { } pos ? new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y)) : default,
            OperatorDepth = operatorDepth,
            TotalChunks = chunks.Length,
        };
        _ui.ServerSendUiMessage(ent.Owner, Key, scene, args.Actor);
        var name = MetaData(memory.SelectedWorld).EntityName;
        if (TryComp<CMUZLevelsNetworkComponent>(memory.SelectedWorld, out var network))
        {
            IReadOnlyDictionary<int, EntityUid?> levels = network.ZLevels;
            if (levels.TryGetValue(0, out var ground) && ground is { } root && Exists(root)) name = Name(root);
        }
        _ui.ServerSendUiMessage(ent.Owner, Key,
            new CMUCaveSurveySectionMessage(args.RequestId, GetNetEntity(memory.SelectedWorld), name, data.BlockCount), args.Actor);
        _outgoing.Enqueue(new Delivery { Actor = ent.Owner, Generation = generation, Chunks = chunks });
    }

    private void SendPending()
    {
        // At most eight small chunks per frame across all viewers, round-robin between open surveys.
        var budget = 8;
        var visits = _outgoing.Count;
        while (budget > 0 && visits-- > 0 && _outgoing.TryDequeue(out var delivery))
        {
            if (!Exists(delivery.Actor) || !_ui.IsUiOpen(delivery.Actor, Key, delivery.Actor) ||
                !_viewGenerations.TryGetValue(delivery.Actor, out var generation) || generation != delivery.Generation) continue;
            var count = Math.Min(4, Math.Min(budget, delivery.Chunks.Length - delivery.Sent));
            if (count == 0) continue;
            var packet = delivery.Chunks.AsSpan(delivery.Sent, count).ToArray();
            delivery.Sent += count;
            budget -= count;
            _ui.ServerSendUiMessage(delivery.Actor, Key, new CMUReconPatchMessage(delivery.Generation, packet, [], false)
            {
                OrdersChanged = false,
                LoadedChunks = delivery.Sent,
                TotalChunks = delivery.Chunks.Length,
            }, delivery.Actor);
            if (delivery.Sent < delivery.Chunks.Length) _outgoing.Enqueue(delivery);
        }
    }
}
