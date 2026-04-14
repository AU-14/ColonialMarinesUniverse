using Content.Shared.AU14.Origin;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.AU14.Origin;

/// <summary>
/// Applies origin effects (components, accents, starting items, traits) to a spawned character entity.
/// </summary>
public sealed class OriginSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;

    /// <summary>
    /// Apply origin effects to the given mob entity based on the character profile's selected origin.
    /// Should be called after the entity is spawned and before the player takes control.
    /// </summary>
    public void ApplyOrigin(EntityUid mob, HumanoidCharacterProfile profile)
    {
        if (profile.Origin == null)
            return;

        if (!_prototypeManager.TryIndex<OriginPrototype>(profile.Origin.Value, out var origin))
            return;

        // Add components from the origin prototype
        foreach (var (name, entry) in origin.Components)
        {
            if (HasComp(mob, entry.Component.GetType()))
                continue;

            var comp = _componentFactory.GetComponent(name);
            var temp = (object) comp;
            _serializationManager.CopyTo(entry.Component, ref temp);
            EntityManager.AddComponent(mob, (Component) temp!);
        }

        // Add accent components
        foreach (var accentId in origin.Accents)
        {
            if (!_componentFactory.TryGetRegistration(accentId, out var reg))
                continue;

            if (HasComp(mob, reg.Type))
                continue;

            var comp = _componentFactory.GetComponent(reg);
            EntityManager.AddComponent(mob, comp);
        }

        // Spawn starting items and try to put them in the entity's hands/inventory
        foreach (var itemProto in origin.StartingItems)
        {
            var item = Spawn(itemProto, Transform(mob).Coordinates);
            // Try to place in hands; if that fails, item just spawns at their feet
            // A more robust implementation could try inventory slots
        }
    }
}

