using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.ThreeD;

/// <summary>
/// Loads presentation-only prototypes when needed. The server and ordinary 2D clients
/// do not need to parse or retain model geometry, surfaces, or equipment poses.
/// </summary>
public sealed class CMU3DModelLibrary : IPostInjectInit
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Library _world = new(new ResPath("/ThreeD/Prototypes/World"));
    private readonly Library _equipment = new(new ResPath("/ThreeD/Prototypes/Equipment"));

    public void PostInject()
    {
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
    }

    public Lease AcquireWorld() => Acquire(false);

    public Lease AcquireWorkbench() => Acquire(true);

    private Lease Acquire(bool equipment)
    {
        _world.Users++;
        if (equipment)
            _equipment.Users++;
        var lease = new Lease(this, equipment);
        try
        {
            lease.EnsureLoaded();
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private void Load(Library library)
    {
        if (library.Loaded)
            return;

        var changed = new Dictionary<Type, HashSet<string>>();
        try
        {
            _prototypes.LoadDirectory(library.Directory, overwrite: true, changed: changed);
        }
        finally
        {
            // Track even a partial load so a failed open can release what it allocated.
            foreach (var (kind, ids) in changed)
                library.Definitions.GetOrNew(kind).UnionWith(ids);
        }
        if (changed.Count == 0)
            throw new InvalidOperationException($"No 3D definitions found at {library.Directory}.");

        // Resolve only this library; rebuilding all entity prototypes would stall gameplay.
        _prototypes.ReloadPrototypes(changed);
        library.Loaded = true;
    }

    private void Release(bool equipment)
    {
        if (equipment && --_equipment.Users == 0)
            Unload(_equipment);
        if (--_world.Users == 0)
            Unload(_world);
    }

    private void Unload(Library library)
    {
        if (library.Definitions.Count == 0)
            return;

        // RemoveString releases both prototype instances and their raw YAML mappings.
        // A small type/id manifest avoids reading the full geometry again to unload it.
        var manifest = new SequenceDataNode();
        foreach (var (kind, ids) in library.Definitions)
        {
            if (!_prototypes.TryGetKindFrom(kind, out var name))
                continue;
            foreach (var id in ids)
                manifest.Add(new MappingDataNode
                {
                    { "type", new ValueDataNode(name) },
                    { "id", new ValueDataNode(id) },
                });
        }
        _prototypes.RemoveString(manifest.ToString());
        var removed = library.Definitions;
        library.Definitions = [];
        library.Loaded = false;
        // Invalidate catalogs held by other 3D views without reloading ordinary entities.
        _prototypes.ReloadPrototypes(new(), removed);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.Removed == null)
            return;

        Invalidate(_world);
        Invalidate(_equipment);
        return;

        void Invalidate(Library library)
        {
            foreach (var (kind, ids) in library.Definitions)
            {
                if (args.Removed.TryGetValue(kind, out var removed) && ids.Overlaps(removed))
                    library.Loaded = false;
            }
        }
    }

    private sealed class Library(ResPath directory)
    {
        public readonly ResPath Directory = directory;
        public Dictionary<Type, HashSet<string>> Definitions = [];
        public bool Loaded;
        public int Users;
    }

    /// <summary>A live view owns its library until it closes. The last owner releases the data.</summary>
    public sealed class Lease : IDisposable
    {
        private CMU3DModelLibrary? _owner;
        private readonly bool _equipment;

        internal Lease(CMU3DModelLibrary owner, bool equipment)
        {
            _owner = owner;
            _equipment = equipment;
        }

        public void EnsureLoaded()
        {
            if (_owner is not { } owner)
                throw new InvalidOperationException("The 3D view has already released its model library.");
            owner.Load(owner._world);
            if (_equipment)
                owner.Load(owner._equipment);
        }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            owner?.Release(_equipment);
        }
    }
}
