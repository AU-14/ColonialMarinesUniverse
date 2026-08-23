using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Chunking;
using Content.Shared.Database;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Shared.Decals.DecalGridComponent;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Decals
{
    public sealed partial class DecalSystem : SharedDecalSystem
    {
        // A serialized decal is substantially larger than a uint ID, so only prefer a replacement when it is clearly smaller.
        private const int DecalToIdSizeRatio = 8;

        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IParallelManager _parMan = default!;
        [Dependency] private ChunkingSystem _chunking = default!;
        [Dependency] private IConfigurationManager _conf = default!;
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private SharedMapSystem _mapSystem = default!;
        [Dependency] private SharedTransformSystem _transform = default!;
        [Dependency] private TurfSystem _turf = default!;

        private readonly Dictionary<NetEntity, Dictionary<Vector2i, DecalChunkDelta>> _dirtyChunkDeltas = new();
        private readonly HashSet<NetEntity> _dirtyGrids = new();
        private readonly Dictionary<ICommonSession, Dictionary<NetEntity, HashSet<Vector2i>>> _previousSentChunks = new();
        private static readonly Vector2 _boundsMinExpansion = new(0.01f, 0.01f);
        private static readonly Vector2 _boundsMaxExpansion = new(1.01f, 1.01f);

        private UpdatePlayerJob _updateJob;
        private List<ICommonSession> _sessions = new();

        // If this ever gets parallelised then you'll want to increase the pooled count.
        private ObjectPool<HashSet<Vector2i>> _chunkIndexPool =
            new DefaultObjectPool<HashSet<Vector2i>>(
                new DefaultPooledObjectPolicy<HashSet<Vector2i>>(), 64);

        private ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> _chunkViewerPool =
            new DefaultObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>>(
                new DefaultPooledObjectPolicy<Dictionary<NetEntity, HashSet<Vector2i>>>(), 64);

        public override void Initialize()
        {
            base.Initialize();

            _updateJob = new UpdatePlayerJob()
            {
                System = this,
                Sessions = _sessions,
            };

            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
            SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);

            SubscribeNetworkEvent<RequestDecalPlacementEvent>(OnDecalPlacementRequest);
            SubscribeNetworkEvent<RequestDecalRemovalEvent>(OnDecalRemovalRequest);
            SubscribeLocalEvent<PostGridSplitEvent>(OnGridSplit);

            Subs.CVar(_conf, CVars.NetPVS, OnPvsToggle, true);
        }

        private void OnPvsToggle(bool value)
        {
            if (value == PvsEnabled)
                return;

            PvsEnabled = value;

            if (value)
                return;

            foreach (var playerData in _previousSentChunks.Values)
            {
                playerData.Clear();
            }

            var query = AllEntityQuery<DecalGridComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var grid, out var meta))
            {
                grid.ForceTick = _timing.CurTick;
                Dirty(uid, grid, meta);
            }
        }

        private void OnGridSplit(ref PostGridSplitEvent ev)
        {
            if (!TryComp(ev.OldGrid, out DecalGridComponent? oldComp))
                return;

            if (!TryComp(ev.Grid, out DecalGridComponent? newComp))
                return;

            // Transfer decals over to the new grid.
            var enumerator = _mapSystem.GetAllTilesEnumerator(ev.Grid, Comp<MapGridComponent>(ev.Grid));

            var oldChunkCollection = oldComp.ChunkCollection.ChunkCollection;
            var chunkCollection = newComp.ChunkCollection.ChunkCollection;

            while (enumerator.MoveNext(out var tile))
            {
                var tilePos = (Vector2)tile.Value.GridIndices;
                var chunkIndices = GetChunkIndices(tilePos);

                if (!oldChunkCollection.TryGetValue(chunkIndices, out var oldChunk))
                    continue;

                var bounds = new Box2(tilePos - _boundsMinExpansion, tilePos + _boundsMaxExpansion);
                var toRemove = new RemQueue<uint>();

                foreach (var (oldDecalId, decal) in oldChunk.Decals)
                {
                    if (!bounds.Contains(decal.Coordinates))
                        continue;

                    var newDecalId = newComp.ChunkCollection.NextDecalId++;
                    var newChunk = chunkCollection.GetOrNew(chunkIndices);
                    newChunk.Decals[newDecalId] = decal;
                    newComp.DecalIndex[newDecalId] = chunkIndices;
                    DirtyDecal(ev.Grid, chunkIndices, newChunk, newDecalId, decal);
                    toRemove.Add(oldDecalId);
                }

                foreach (var oldDecalId in toRemove)
                {
                    RemoveDecalInternal(ev.OldGrid, oldDecalId, out _, oldComp);
                }
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();

            _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        }

        private void OnTileChanged(ref TileChangedEvent args)
        {
            if (!TryComp(args.Entity, out DecalGridComponent? grid))
                return;

            var toDelete = new HashSet<uint>();

            foreach (var change in args.Changes)
            {
                if (!_turf.IsSpace(change.NewTile))
                    continue;

                var indices = GetChunkIndices(change.GridIndices);

                if (!grid.ChunkCollection.ChunkCollection.TryGetValue(indices, out var chunk))
                    continue;

                toDelete.Clear();

                foreach (var (uid, decal) in chunk.Decals)
                {
                    if (new Vector2((int)Math.Floor(decal.Coordinates.X), (int)Math.Floor(decal.Coordinates.Y)) ==
                        change.GridIndices)
                    {
                        toDelete.Add(uid);
                    }
                }

                if (toDelete.Count == 0)
                    continue;

                foreach (var decalId in toDelete)
                {
                    RemoveDecalInternal(args.Entity, decalId, out _, grid);
                }
            }
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
        {
            switch (e.NewStatus)
            {
                case SessionStatus.InGame:
                    _previousSentChunks[e.Session] = new();
                    break;
                case SessionStatus.Disconnected:
                    _previousSentChunks.Remove(e.Session);
                    break;
            }
        }

        private void OnDecalPlacementRequest(RequestDecalPlacementEvent ev, EntitySessionEventArgs eventArgs)
        {
            if (eventArgs.SenderSession is not { } session)
                return;

            // bad
            if (!_adminManager.HasAdminFlag(session, AdminFlags.Spawn))
                return;

            var coordinates = GetCoordinates(ev.Coordinates);

            if (!coordinates.IsValid(EntityManager))
                return;

            if (!TryAddDecal(ev.Decal, coordinates, out _))
                return;

            if (eventArgs.SenderSession.AttachedEntity != null)
            {
                _adminLogger.Add(LogType.CrayonDraw, LogImpact.Low,
                    $"{ToPrettyString(eventArgs.SenderSession.AttachedEntity.Value):actor} drew a {ev.Decal.Color} {ev.Decal.Id} at {ev.Coordinates}");
            }
            else
            {
                _adminLogger.Add(LogType.CrayonDraw, LogImpact.Low,
                    $"{eventArgs.SenderSession.Name} drew a {ev.Decal.Color} {ev.Decal.Id} at {ev.Coordinates}");
            }
        }

        private void OnDecalRemovalRequest(RequestDecalRemovalEvent ev, EntitySessionEventArgs eventArgs)
        {
            if (eventArgs.SenderSession is not { } session)
                return;

            // bad
            if (!_adminManager.HasAdminFlag(session, AdminFlags.Spawn))
                return;

            var coordinates = GetCoordinates(ev.Coordinates);

            if (!coordinates.IsValid(EntityManager))
                return;

            var gridId = _transform.GetGrid(coordinates);

            if (gridId == null)
                return;

            // remove all decals on the same tile
            foreach (var (decalId, decal) in GetDecalsInRange(gridId.Value, ev.Coordinates.Position))
            {
                if (eventArgs.SenderSession.AttachedEntity != null)
                {
                    _adminLogger.Add(LogType.CrayonDraw, LogImpact.Low,
                        $"{ToPrettyString(eventArgs.SenderSession.AttachedEntity.Value):actor} removed a {decal.Color} {decal.Id} at {ev.Coordinates}");
                }
                else
                {
                    _adminLogger.Add(LogType.CrayonDraw, LogImpact.Low,
                        $"{eventArgs.SenderSession.Name} removed a {decal.Color} {decal.Id} at {ev.Coordinates}");
                }

                RemoveDecal(gridId.Value, decalId);
            }
        }

        protected override void DirtyChunk(EntityUid uid, Vector2i chunkIndices, DecalChunk chunk)
        {
            var id = GetNetEntity(uid);
            chunk.LastModified = _timing.CurTick;
            _dirtyGrids.Add(id);
        }

        protected override void OnDecalRemoved(
            EntityUid gridId,
            uint decalId,
            DecalGridComponent component,
            Vector2i indices,
            DecalChunk chunk)
        {
            base.OnDecalRemoved(gridId, decalId, component, indices, chunk);
            QueueDecalRemoval(gridId, indices, chunk, decalId);
        }

        private DecalChunkDelta GetChunkDelta(EntityUid gridId, Vector2i chunkIndices)
        {
            var gridDeltas = _dirtyChunkDeltas.GetOrNew(GetNetEntity(gridId));
            return gridDeltas.GetOrNew(chunkIndices);
        }

        private void DirtyDecal(
            EntityUid gridId,
            Vector2i chunkIndices,
            DecalChunk chunk,
            uint decalId,
            Decal decal)
        {
            DirtyChunk(gridId, chunkIndices, chunk);

            if (!PvsEnabled)
                return;

            var delta = GetChunkDelta(gridId, chunkIndices);
            delta.RemovedDecals.Remove(decalId);
            delta.ModifiedDecals[decalId] = decal;
        }

        private void QueueDecalRemoval(
            EntityUid gridId,
            Vector2i chunkIndices,
            DecalChunk chunk,
            uint decalId)
        {
            if (!PvsEnabled)
                return;

            var delta = GetChunkDelta(gridId, chunkIndices);
            delta.ModifiedDecals.Remove(decalId);

            if (delta.Cleared)
                return;

            delta.RemovedDecals.Add(decalId);
            if ((long)delta.RemovedDecals.Count > (long)chunk.Decals.Count * DecalToIdSizeRatio)
                QueueChunkReplacement(gridId, chunkIndices, chunk);
        }

        private void QueueChunkReplacement(EntityUid gridId, Vector2i chunkIndices, DecalChunk chunk)
        {
            if (!PvsEnabled)
                return;

            var delta = GetChunkDelta(gridId, chunkIndices);
            delta.Cleared = true;
            delta.ModifiedDecals.Clear();
            delta.RemovedDecals.Clear();

            foreach (var (decalId, decal) in chunk.Decals)
            {
                delta.ModifiedDecals.Add(decalId, decal);
            }
        }

        public bool TryAddDecal(string id, EntityCoordinates coordinates, out uint decalId, Color? color = null, Angle? rotation = null, int zIndex = 0, bool cleanable = false)
        {
            rotation ??= Angle.Zero;
            var decal = new Decal(coordinates.Position, id, color, rotation.Value, zIndex, cleanable);

            return TryAddDecal(decal, coordinates, out decalId);
        }

        public bool TryAddDecal(Decal decal, EntityCoordinates coordinates, out uint decalId)
        {
            decalId = 0;

            if (!PrototypeManager.HasIndex<DecalPrototype>(decal.Id))
                return false;

            var gridId = _transform.GetGrid(coordinates);
            if (!TryComp(gridId, out MapGridComponent? grid))
                return false;

            if (_turf.IsSpace(_mapSystem.GetTileRef(gridId.Value, grid, coordinates)))
                return false;

            if (!TryComp(gridId, out DecalGridComponent? comp))
                return false;

            decalId = comp.ChunkCollection.NextDecalId++;
            var chunkIndices = GetChunkIndices(decal.Coordinates);
            var chunk = comp.ChunkCollection.ChunkCollection.GetOrNew(chunkIndices);
            chunk.Decals[decalId] = decal;
            comp.DecalIndex[decalId] = chunkIndices;
            DirtyDecal(gridId.Value, chunkIndices, chunk, decalId, decal);

            return true;
        }

        public override bool RemoveDecal(EntityUid gridId, uint decalId, DecalGridComponent? component = null)
            => RemoveDecalInternal(gridId, decalId, out _, component);

        public override HashSet<(uint Index, Decal Decal)> GetDecalsInRange(EntityUid gridId, Vector2 position, float distance = 0.75f, Func<Decal, bool>? validDelegate = null)
        {
            var decalIds = new HashSet<(uint, Decal)>();
            var chunkCollection = ChunkCollection(gridId);
            var chunkIndices = GetChunkIndices(position);
            if (chunkCollection == null || !chunkCollection.TryGetValue(chunkIndices, out var chunk))
                return decalIds;

            foreach (var (uid, decal) in chunk.Decals)
            {
                if ((position - decal.Coordinates - new Vector2(0.5f, 0.5f)).Length() > distance)
                    continue;

                if (validDelegate == null || validDelegate(decal))
                {
                    decalIds.Add((uid, decal));
                }
            }

            return decalIds;
        }

        public HashSet<(uint Index, Decal Decal)> GetDecalsIntersecting(EntityUid gridUid, Box2 bounds, DecalGridComponent? component = null)
        {
            var decalIds = new HashSet<(uint, Decal)>();
            var chunkCollection = ChunkCollection(gridUid, component);

            if (chunkCollection == null)
                return decalIds;

            var chunks = new ChunkIndicesEnumerator(bounds, ChunkSize);

            while (chunks.MoveNext(out var chunkOrigin))
            {
                if (!chunkCollection.TryGetValue(chunkOrigin.Value, out var chunk))
                    continue;

                foreach (var (id, decal) in chunk.Decals)
                {
                    if (!bounds.Contains(decal.Coordinates))
                        continue;

                    decalIds.Add((id, decal));
                }
            }

            return decalIds;
        }

        /// <summary>
        ///     Changes a decals position. Note this will actually result in a new decal being created, possibly on a new grid or chunk.
        /// </summary>
        /// <remarks>
        ///     If the new position is invalid, this will result in the decal getting deleted.
        /// </remarks>
        public bool SetDecalPosition(EntityUid gridId, uint decalId, EntityCoordinates coordinates, DecalGridComponent? comp = null)
        {
            if (!Resolve(gridId, ref comp))
                return false;

            if (!RemoveDecalInternal(gridId, decalId, out var removed, comp))
                return false;

            return TryAddDecal(removed.WithCoordinates(coordinates.Position), coordinates, out _);
        }

        private bool ModifyDecal(EntityUid gridId, uint decalId, Func<Decal, Decal> modifyDecal, DecalGridComponent? comp = null)
        {
            if (!Resolve(gridId, ref comp))
                return false;

            if (!comp.DecalIndex.TryGetValue(decalId, out var indices))
                return false;

            var chunk = comp.ChunkCollection.ChunkCollection[indices];
            var decal = chunk.Decals[decalId];
            var modified = modifyDecal(decal);
            chunk.Decals[decalId] = modified;
            DirtyDecal(gridId, indices, chunk, decalId, modified);
            return true;
        }

        public bool SetDecalColor(EntityUid gridId, uint decalId, Color? value, DecalGridComponent? comp = null)
            => ModifyDecal(gridId, decalId, x => x.WithColor(value), comp);

        public bool SetDecalRotation(EntityUid gridId, uint decalId, Angle value, DecalGridComponent? comp = null)
            => ModifyDecal(gridId, decalId, x => x.WithRotation(value), comp);

        public bool SetDecalZIndex(EntityUid gridId, uint decalId, int value, DecalGridComponent? comp = null)
            => ModifyDecal(gridId, decalId, x => x.WithZIndex(value), comp);

        public bool SetDecalCleanable(EntityUid gridId, uint decalId, bool value, DecalGridComponent? comp = null)
            => ModifyDecal(gridId, decalId, x => x.WithCleanable(value), comp);

        public bool SetDecalId(EntityUid gridId, uint decalId, string id, DecalGridComponent? comp = null)
        {
            if (!PrototypeManager.HasIndex<DecalPrototype>(id))
                throw new ArgumentOutOfRangeException($"Tried to set decal id to invalid prototypeid: {id}");

            return ModifyDecal(gridId, decalId, x => x.WithId(id), comp);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var ent in _dirtyGrids)
            {
                if (TryGetEntity(ent, out var uid) && TryComp(uid, out DecalGridComponent? decals))
                    Dirty(uid.Value, decals);
            }

            if (!PvsEnabled)
            {
                ClearDirtyDecals();
                return;
            }

            _sessions.Clear();

            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status != SessionStatus.InGame)
                    continue;

                _sessions.Add(session);
            }

            if (_sessions.Count > 0)
                _parMan.ProcessNow(_updateJob, _sessions.Count);

            ClearDirtyDecals();
        }

        private void ClearDirtyDecals()
        {
            _dirtyChunkDeltas.Clear();
            _dirtyGrids.Clear();
        }

        public void UpdatePlayer(ICommonSession player)
        {
            var chunksInRange = _chunking.GetChunksForSession(player, ChunkSize, _chunkIndexPool, _chunkViewerPool);
            var staleChunks = _chunkViewerPool.Get();
            var previouslySent = _previousSentChunks[player];

            // Get any chunks not in range anymore
            // Then, remove them from previousSentChunks (for stuff like grids out of range)
            // and also mark them as stale for networking.

            foreach (var (netGrid, oldIndices) in previouslySent)
            {
                // Mark the whole grid as stale and flag for removal.
                if (!chunksInRange.TryGetValue(netGrid, out var chunks))
                {
                    previouslySent.Remove(netGrid);

                    // Was the grid deleted?
                    if (TryGetEntity(netGrid, out var gridId) && HasComp<MapGridComponent>(gridId.Value))
                    {
                        // no -> add it to the list of stale chunks
                        staleChunks[netGrid] = oldIndices;
                    }
                    else
                    {
                        // If the grid was deleted then don't worry about telling the client to delete the chunk.
                        oldIndices.Clear();
                        _chunkIndexPool.Return(oldIndices);
                    }

                    continue;
                }

                var elmo = _chunkIndexPool.Get();

                // Get individual stale chunks.
                foreach (var chunk in oldIndices)
                {
                    if (chunks.Contains(chunk))
                        continue;

                    elmo.Add(chunk);
                }

                if (elmo.Count == 0)
                {
                    _chunkIndexPool.Return(elmo);
                    continue;
                }

                staleChunks.Add(netGrid, elmo);
            }

            var fullChunks = _chunkViewerPool.Get();
            var deltaChunks = _chunkViewerPool.Get();
            foreach (var (netGrid, gridChunks) in chunksInRange)
            {
                var newChunks = _chunkIndexPool.Get();
                var changedChunks = _chunkIndexPool.Get();
                _dirtyChunkDeltas.TryGetValue(netGrid, out var dirtyChunks);

                if (!previouslySent.TryGetValue(netGrid, out var previousChunks))
                {
                    newChunks.UnionWith(gridChunks);
                }
                else
                {
                    foreach (var index in gridChunks)
                    {
                        if (!previousChunks.Contains(index))
                            newChunks.Add(index);
                        else if (dirtyChunks != null && dirtyChunks.ContainsKey(index))
                            changedChunks.Add(index);
                    }

                    previousChunks.Clear();
                    _chunkIndexPool.Return(previousChunks);
                }

                previouslySent[netGrid] = gridChunks;

                if (newChunks.Count == 0)
                    _chunkIndexPool.Return(newChunks);
                else
                    fullChunks[netGrid] = newChunks;

                if (changedChunks.Count == 0)
                    _chunkIndexPool.Return(changedChunks);
                else
                    deltaChunks[netGrid] = changedChunks;
            }

            SendChunkUpdates(player, fullChunks, deltaChunks, staleChunks);
        }

        private void ReturnToPool(Dictionary<NetEntity, HashSet<Vector2i>> chunks)
        {
            foreach (var (_, previous) in chunks)
            {
                previous.Clear();
                _chunkIndexPool.Return(previous);
            }

            chunks.Clear();
            _chunkViewerPool.Return(chunks);
        }

        private void SendChunkUpdates(
            ICommonSession session,
            Dictionary<NetEntity, HashSet<Vector2i>> fullChunks,
            Dictionary<NetEntity, HashSet<Vector2i>> deltaChunks,
            Dictionary<NetEntity, HashSet<Vector2i>> staleChunks)
        {
            var fullDecals = new Dictionary<NetEntity, Dictionary<Vector2i, DecalChunk>>();
            foreach (var (netGrid, chunks) in fullChunks)
            {
                var gridId = GetEntity(netGrid);

                var collection = ChunkCollection(gridId);
                if (collection == null)
                    continue;

                var gridChunks = new Dictionary<Vector2i, DecalChunk>();
                foreach (var indices in chunks)
                {
                    gridChunks.Add(indices,
                        collection.TryGetValue(indices, out var chunk)
                            ? chunk
                            : new());
                }
                fullDecals[netGrid] = gridChunks;
            }

            var decalDeltas = new Dictionary<NetEntity, Dictionary<Vector2i, DecalChunkDelta>>();
            foreach (var (netGrid, chunks) in deltaChunks)
            {
                if (!_dirtyChunkDeltas.TryGetValue(netGrid, out var dirtyChunks))
                    continue;

                var gridDeltas = new Dictionary<Vector2i, DecalChunkDelta>();
                foreach (var indices in chunks)
                {
                    if (dirtyChunks.TryGetValue(indices, out var delta))
                        gridDeltas.Add(indices, delta);
                }

                if (gridDeltas.Count > 0)
                    decalDeltas[netGrid] = gridDeltas;
            }

            if (fullDecals.Count != 0 || decalDeltas.Count != 0 || staleChunks.Count != 0)
            {
                RaiseNetworkEvent(new DecalChunkUpdateEvent
                {
                    Data = fullDecals,
                    Deltas = decalDeltas,
                    RemovedChunks = staleChunks,
                }, session);
            }

            ReturnToPool(fullChunks);
            ReturnToPool(deltaChunks);
            ReturnToPool(staleChunks);
        }

        #region Jobs

        /// <summary>
        /// Updates per-player data for decals.
        /// </summary>
        private record struct UpdatePlayerJob : IParallelRobustJob
        {
            public int BatchSize => 2;

            public DecalSystem System;

            public List<ICommonSession> Sessions;

            public void Execute(int index)
            {
                System.UpdatePlayer(Sessions[index]);
            }
        }

        #endregion

        private readonly List<Vector2i> _emptyChunks = new(); // CMU14
        private readonly List<uint> _toRemove = new(); // CMU14

        // CMU14 method
        public (int Removed, int Skipped) RemoveDecals(EntityUid gridId, IReadOnlySet<string>? ids = null, bool cleanableOnly = true, DecalGridComponent? comp = null)
        {
            if (!Resolve(gridId, ref comp)) return (0, 0);
            _emptyChunks.Clear();
            var chunkCollection = comp.ChunkCollection.ChunkCollection;
            int removed = 0, skipped = 0;
            if (ids == null && !cleanableOnly)
            {
                foreach (var (chunkOrigin, chunk) in chunkCollection)
                {
                    if (chunk.Decals.Count == 0) continue;
                    removed += chunk.Decals.Count;
                    DirtyChunk(gridId, chunkOrigin, chunk);
                    chunk.Decals.Clear();
                    QueueChunkReplacement(gridId, chunkOrigin, chunk);
                }

                comp.DecalIndex.Clear();
                chunkCollection.Clear();
                return (removed, 0);
            }

            foreach (var (chunkOrigin, chunk) in chunkCollection)
            {
                _toRemove.Clear();
                foreach (var (decalId, decal) in chunk.Decals)
                {
                    if (ids != null && !ids.Contains(decal.Id)) continue;
                    if (cleanableOnly && !decal.Cleanable)
                    {
                        skipped++;
                        continue;
                    }

                    _toRemove.Add(decalId);
                }

                if (_toRemove.Count == 0) continue;
                DirtyChunk(gridId, chunkOrigin, chunk);
                foreach (var decalId in _toRemove)
                {
                    chunk.Decals.Remove(decalId);
                    comp.DecalIndex.Remove(decalId);
                }

                removed += _toRemove.Count;
                foreach (var decalId in _toRemove)
                {
                    QueueDecalRemoval(gridId, chunkOrigin, chunk, decalId);
                }

                if (chunk.Decals.Count == 0)
                    _emptyChunks.Add(chunkOrigin);
            }

            foreach (var chunkOrigin in _emptyChunks)
                chunkCollection.Remove(chunkOrigin);

            return (removed, skipped);
        }
    }
}
