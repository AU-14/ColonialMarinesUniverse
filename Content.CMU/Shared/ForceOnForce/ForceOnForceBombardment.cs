using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

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
    [DataField] public SoundSpecifier Flyby = new SoundCollectionSpecifier("CMUFoFFlybys");
    [DataField] public SoundSpecifier Laser = new SoundCollectionSpecifier("CMUFoFLasers");
    [DataField] public SoundSpecifier Impact = new SoundCollectionSpecifier("CMUFoFImpacts");
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
