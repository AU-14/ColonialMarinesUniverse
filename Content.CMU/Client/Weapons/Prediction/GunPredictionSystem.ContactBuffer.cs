namespace Content.Client._RMC14.Weapons.Ranged.Prediction;

public sealed partial class GunPredictionSystem
{
    // Scratch entity IDs are consumed before PredictHit publishes collision events.
    // The separate hit-event payload retains its own collection.
    private readonly HashSet<EntityUid> _cmuProjectileContacts = new();
}
