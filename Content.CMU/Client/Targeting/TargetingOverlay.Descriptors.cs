using Robust.Shared.Utility;

namespace Content.Client._RMC14.Weapons.Ranged.Targeting;

public sealed partial class TargetingOverlay
{
    private readonly Dictionary<(ResPath Path, string State), SpriteSpecifier.Rsi> _cmuDescriptors = new();

    private SpriteSpecifier.Rsi CMUGetDescriptor(ResPath path, string state)
    {
        var key = (path, state);
        if (_cmuDescriptors.TryGetValue(key, out var descriptor))
            return descriptor;

        // Bound retention even if custom prototypes introduce many different state names.
        if (_cmuDescriptors.Count >= 256)
            _cmuDescriptors.Clear();
        descriptor = new SpriteSpecifier.Rsi(path, state);
        _cmuDescriptors.Add(key, descriptor);
        return descriptor;
    }
}
