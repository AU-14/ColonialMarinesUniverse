using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Decals
{
    public abstract partial class SharedDecalSystem : EntitySystem
    {
        [Dependency] protected ChunkEntitySystem ChunkEntities = default!;
        [Dependency] protected EntityQuery<DecalChunkComponent> DecalChunkQuery = default!;

        private readonly List<ushort> _predictedSweep = new(); // CMU14: scratch for the predicted-id sweep in OnChunkHandleState

        protected bool PvsEnabled;

        // Legacy DecalGridComponent data was serialized in 32x32 chunks. Loading code must treat those keys as old
        // storage buckets and re-chunk decals by coordinates before migrating them to chunk entities.
        public const int LegacyChunkSize = 32;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DecalGridComponent, ComponentGetState>(OnGetState);
            SubscribeLocalEvent<DecalChunkComponent, ComponentStartup>(OnChunkStartup);
            // CMU14: per decal delta states, one entry per changed decal instead of the whole chunk dictionary
            SubscribeLocalEvent<DecalChunkComponent, ComponentGetState>(OnChunkGetState);
            SubscribeLocalEvent<DecalChunkComponent, ComponentHandleState>(OnChunkHandleState);
            SubscribeAllEvent<RequestDecalPlacementEvent>(OnDecalPlacementRequest);
            SubscribeAllEvent<RequestDecalRemovalEvent>(OnDecalRemovalRequest);
        }

        protected abstract void OnDecalPlacementRequest(RequestDecalPlacementEvent ev, EntitySessionEventArgs eventArgs);

        protected abstract void OnDecalRemovalRequest(RequestDecalRemovalEvent ev, EntitySessionEventArgs eventArgs);

        private void OnChunkStartup(Entity<DecalChunkComponent> ent, ref ComponentStartup args)
        {
            RebuildFreeDecalIds(ent.Comp);
        }

        // CMU14 method
        private void OnChunkGetState(EntityUid uid, DecalChunkComponent component, ref ComponentGetState args)
        {
            // Entering entities are sent from their per entity ack tick, fresh ones from zero, so a delta is always
            // built against a base the receiver actually has.
            if (args.FromTick <= component.CreationTick)
            {
                args.State = new DecalChunkState(component.Decals);
                return;
            }

            var modified = new Dictionary<ushort, Decal>();
            foreach (var (id, tick) in component.ModifiedTicks)
            {
                if (tick >= args.FromTick)
                    modified.Add(id, component.Decals[id]);
            }

            var removed = new List<ushort>();
            foreach (var (id, tick) in component.RemovedTicks)
            {
                if (tick >= args.FromTick)
                    removed.Add(id);
            }

            args.State = new DecalChunkDeltaState(modified, removed);
        }

        // CMU14 method
        private void OnChunkHandleState(EntityUid uid, DecalChunkComponent component, ref ComponentHandleState args)
        {
            switch (args.Current)
            {
                case DecalChunkDeltaState delta:
                    foreach (var (id, decal) in delta.ModifiedDecals)
                        component.Decals[id] = decal;

                    foreach (var id in delta.RemovedDecals)
                        component.Decals.Remove(id);

                    // CMU14: Auto state handling used to replace the dictionary wholesale, which is what reconciled
                    // client-predicted ids once the server ack arrived. Delta merge has to sweep the band itself.
                    _predictedSweep.Clear();
                    foreach (var id in component.Decals.Keys)
                    {
                        if (id >= DecalChunkComponent.MinPredictedDecalId)
                            _predictedSweep.Add(id);
                    }

                    foreach (var id in _predictedSweep)
                        component.Decals.Remove(id);

                    break;
                case DecalChunkState full:
                    // CMU14: clone it, the state dict is retained as the rollback base and prediction writes into the component copy
                    component.Decals = new Dictionary<ushort, Decal>(full.Decals);
                    break;
                default:
                    return;
            }

            var ev = new AfterAutoHandleStateEvent(args.Current);
            EntityManager.EventBus.RaiseComponentEvent(uid, component, ref ev);
        }

        private void OnGetState(EntityUid uid, DecalGridComponent component, ref ComponentGetState args)
        {
            if (PvsEnabled && !args.ReplayState)
                return;

            // Should this be a full component state or a delta-state?
            if (args.FromTick <= component.CreationTick || args.FromTick <= component.ForceTick)
            {
                args.State = new DecalGridState(component.ChunkCollection.ChunkCollection);
                return;
            }

            var data = new Dictionary<Vector2i, DecalGridComponent.DecalChunk>();
            foreach (var (index, chunk) in component.ChunkCollection.ChunkCollection)
            {
                if (chunk.LastModified >= args.FromTick)
                    data[index] = chunk;
            }

            args.State = new DecalGridDeltaState(data, new(component.ChunkCollection.ChunkCollection.Keys));
        }

        public HashSet<(DecalIndex Index, Decal Decal)> GetDecalsInRange(EntityUid gridId, Vector2 position, float distance = 0.75f, Func<Decal, bool>? validDelegate = null)
        {
            var bounds = new Box2(position - new Vector2(distance + 1f), position + new Vector2(distance + 1f));
            var decalIds = GetDecalsIntersecting(gridId, bounds);

            decalIds.RemoveWhere(set =>
                (position - set.Decal.Coordinates - new Vector2(0.5f, 0.5f)).Length() > distance ||
                validDelegate != null && !validDelegate(set.Decal));

            return decalIds;
        }

        public HashSet<(DecalIndex Index, Decal Decal)> GetDecalsIntersecting(EntityUid gridUid, Box2 bounds)
        {
            var decalIds = new HashSet<(DecalIndex, Decal)>();

            foreach (var chunk in ChunkEntities.GetChunksIntersecting(gridUid, bounds, DecalChunkQuery))
            {
                foreach (var (id, decal) in chunk.Comp2.Decals)
                {
                    if (!bounds.Contains(decal.Coordinates))
                        continue;

                    decalIds.Add((new DecalIndex(chunk.Comp1.Chunk, id), decal));
                }
            }

            return decalIds;
        }

        public virtual bool RemoveDecal(EntityUid gridId, DecalIndex decal)
        {
            // NOOP on client atm.
            return true;
        }

        /// <summary>
        /// Adds a decal.
        /// </summary>
        public abstract bool TryAddDecal(Decal decal, EntityCoordinates coordinates, out DecalIndex decalId);

        private static void RebuildFreeDecalIds(DecalChunkComponent component)
        {
            component.FreeDecalIds.Clear();

            // Saves only carry decal data and the highest allocated server id. The free list is a runtime cache.
            foreach (var id in component.Decals.Keys)
            {
                if (id <= DecalChunkComponent.MaxServerDecalId)
                    component.MaxDecalId = Math.Max(component.MaxDecalId, id);
            }

            component.FreeDecalIds.EnsureCapacity(component.MaxDecalId + 1 - component.Decals.Count);

            for (var id = 0; id <= component.MaxDecalId; id++)
            {
                var decalId = (ushort) id;
                if (!component.Decals.ContainsKey(decalId))
                    component.FreeDecalIds.Add(decalId);
            }

            // Allocation pops from the end, so keep the lowest free id there for stable decal ids.
            component.FreeDecalIds.Sort((x, y) => y.CompareTo(x));
        }
    }

    /// <summary>
    ///     Sent by clients to request that a decal is placed on the server.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed partial class RequestDecalPlacementEvent : EntityEventArgs
    {
        public Decal Decal;
        public NetCoordinates Coordinates;

        public RequestDecalPlacementEvent(Decal decal, NetCoordinates coordinates)
        {
            Decal = decal;
            Coordinates = coordinates;
        }
    }

    [Serializable, NetSerializable]
    public sealed partial class RequestDecalRemovalEvent : EntityEventArgs
    {
        public NetCoordinates Coordinates;

        public RequestDecalRemovalEvent(NetCoordinates coordinates)
        {
            Coordinates = coordinates;
        }
    }
}
