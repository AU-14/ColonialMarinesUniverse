using Content.Shared.CMU14.ThreeD;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.ThreeD;

/// <summary>
/// Loads presentation-only prototypes when needed. The server and ordinary 2D clients
/// do not need to parse or retain model geometry, surfaces, or equipment poses.
/// </summary>
public sealed class CMU3DModelLibrary : IPostInjectInit
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private bool _worldLoaded;
    private bool _equipmentLoaded;

    public void PostInject()
    {
        // Prototype lifetime spans connections, unlike entity systems. A replay/prototype
        // reset removes these optional definitions and must allow them to load again.
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
    }

    public void LoadWorld()
    {
        if (_worldLoaded)
            return;

        Load(new ResPath("/ThreeD/Prototypes/World"));
        _worldLoaded = true;
    }

    public void LoadWorkbench()
    {
        LoadWorld();
        if (_equipmentLoaded)
            return;

        Load(new ResPath("/ThreeD/Prototypes/Equipment"));
        _equipmentLoaded = true;
    }

    private void Load(ResPath directory)
    {
        var changed = new Dictionary<Type, HashSet<string>>();
        // Overwrite permits retry after a partially failed load or prototype reset.
        _prototypes.LoadDirectory(directory, overwrite: true, changed: changed);
        if (changed.Count == 0)
            throw new InvalidOperationException($"No 3D definitions found at {directory}.");

        // Resolve only this library; rebuilding all entity prototypes would stall gameplay.
        _prototypes.ReloadPrototypes(changed);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.Removed == null ||
            !(args.Removed.ContainsKey(typeof(CMU3DModelPrototype)) ||
              args.Removed.ContainsKey(typeof(CMU3DSurfacePrototype)) ||
              args.Removed.ContainsKey(typeof(CMU3DTileMaterialPrototype)) ||
              args.Removed.ContainsKey(typeof(CMU3DEquipmentPosePrototype))))
            return;

        _worldLoaded = false;
        _equipmentLoaded = false;
    }
}
