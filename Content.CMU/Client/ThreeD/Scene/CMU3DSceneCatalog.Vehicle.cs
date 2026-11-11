using System.Text;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneCatalog
{
    private readonly Dictionary<string, CMU3DModelPrototype> _vehicleTurrets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CMU3DModelPart[]> _vehicleCompositions = new(StringComparer.Ordinal);

    public CMU3DSceneMatch? ResolveVehicleTurret(string prototype)
    {
        return _vehicleTurrets.TryGetValue(prototype, out var model)
            ? new CMU3DSceneMatch(model, true, prototype)
            : null;
    }

    /// <summary>Removed hardpoints disappear with their source layer; unfamiliar equipment retains the sprite.</summary>
    public bool TryVehicleParts(CMU3DModelPrototype model, IReadOnlyList<(string Rsi, string State)> layers,
        out IReadOnlyList<CMU3DModelPart> parts)
    {
        parts = [];
        if (model.VehicleLayers.Count == 0 || layers.Count == 0)
            return false;
        var selected = new List<CMU3DVehicleLayer>(layers.Count);
        var key = new StringBuilder(model.ID);
        var count = 0;
        foreach (var (rsi, state) in layers)
        {
            CMU3DVehicleLayer? found = null;
            foreach (var candidate in model.VehicleLayers)
            {
                if (candidate.State != state || !CMU3DSourceReference.Matches(rsi, candidate.Rsi))
                    continue;
                if (found != null)
                    return false;
                found = candidate;
            }
            if (found == null || (count += found.Parts.Count) > 128)
                return false;
            selected.Add(found);
            key.Append('|').Append(found.Rsi).Append(':').Append(found.State);
        }
        if (count == 0)
            return false;
        var identity = key.ToString();
        if (!_vehicleCompositions.TryGetValue(identity, out var composition))
        {
            // Catalog lifetime is the optional 3D library lease; bound equipment combinations too.
            if (_vehicleCompositions.Count >= 256)
                _vehicleCompositions.Clear();
            composition = new CMU3DModelPart[count];
            var index = 0;
            foreach (var layer in selected)
                foreach (var part in layer.Parts)
                    composition[index++] = part;
            _vehicleCompositions.Add(identity, composition);
        }
        parts = composition;
        return true;
    }
}
