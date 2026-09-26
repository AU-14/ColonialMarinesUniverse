using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.ForceOnForce;

[Serializable, NetSerializable]
public sealed class ForceOnForceBombardmentMessage(int variant) : BoundUserInterfaceMessage
{
    public int Variant = variant;
}

[Prototype]
public sealed partial class ForceOnForceBombardmentPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromMinutes(5);
    [DataField] public TimeSpan Warning = TimeSpan.FromSeconds(8);
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(.75);
    [DataField] public int Passes = 4;
    [DataField] public int EffectsPerInterval = 8;
    [DataField] public float MinimumDistance = 6;
    [DataField] public float MaximumDistance = 14;
    [DataField] public SoundSpecifier Siren = new SoundCollectionSpecifier("CMUFoFSirens");
    [DataField] public SoundSpecifier Laser = new SoundCollectionSpecifier("CMUFoFLasers");
    [DataField] public SoundSpecifier Impact = new SoundCollectionSpecifier("CMUFoFImpacts");
}

/// <summary>Cosmetic orbital descent and confirmed impact; never creates a damaging payload.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ForceOnForceBombardmentVisualComponent : Component
{
    [DataField, AutoNetworkedField] public int Variant;
    [DataField, AutoNetworkedField] public int Variation;
    [DataField, AutoNetworkedField] public Vector2 Direction;
    [DataField, AutoNetworkedField] public float Scale = 1;
    [DataField, AutoNetworkedField] public bool Impacted;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ImpactAt;
}

public static class ForceOnForceBombardment
{
    public static bool IsSafe(Vector2 position, IReadOnlyList<Vector2> occupants, float minimumDistance)
    {
        var distanceSquared = minimumDistance * minimumDistance;
        foreach (var occupant in occupants)
        {
            if (Vector2.DistanceSquared(position, occupant) < distanceSquared)
                return false;
        }
        return true;
    }
}
