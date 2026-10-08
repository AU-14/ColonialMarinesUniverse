using Content.Shared.Body.Components;

namespace Content.Shared.Atmos.EntitySystems;

/// <summary>
/// Lets gas tanks feed internals from inside worn storage (satchels, pouches and the like) instead of only from a
/// slot or hand directly on the wearer.
/// </summary>
public abstract partial class SharedGasTankSystem
{
    /// <summary>How many containers deep a tank can be, e.g. tank -> pouch -> satchel -> wearer.</summary>
    private const int CMUMaxTankNesting = 4;

    /// <summary>Whether the tank is somewhere on the user, directly or inside storage they're carrying.</summary>
    public bool CMUIsCarriedBy(EntityUid tank, EntityUid user)
    {
        var current = tank;
        for (var i = 0; i < CMUMaxTankNesting; i++)
        {
            if (!_containers.TryGetContainingContainer((current, Transform(current)), out var container))
                return false;

            if (container.Owner == user)
                return true;

            current = container.Owner;
        }

        return false;
    }

    /// <summary>The nearest entity with internals that is carrying the tank, through any storage in between.</summary>
    private bool CMUTryGetCarrierInternals(EntityUid tank, out EntityUid? internalsUid, out InternalsComponent? internalsComp)
    {
        internalsUid = null;
        internalsComp = null;

        var current = tank;
        for (var i = 0; i < CMUMaxTankNesting; i++)
        {
            if (!_containers.TryGetContainingContainer((current, Transform(current)), out var container))
                return false;

            if (TryComp(container.Owner, out InternalsComponent? comp))
            {
                internalsUid = container.Owner;
                internalsComp = comp;
                return true;
            }

            current = container.Owner;
        }

        return false;
    }
}
