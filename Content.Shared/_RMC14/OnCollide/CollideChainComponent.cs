using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.OnCollide;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedOnCollideSystem))]
public sealed partial class CollideChainComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Hit
    {
        get => _hit;
        set
        {
            _hit = value;
            HitsReplaced?.Invoke(this);
        }
    }

    private HashSet<EntityUid> _hit = new();
    internal EntityUid IndexedOwner;
    internal readonly HashSet<EntityUid> IndexedHits = new();
    internal Action<CollideChainComponent>? HitsReplaced;
}
