using Content.Shared.CMU14.Vehicle.Open;

namespace Content.Server.CMU14.Vehicle.Open;

/// <summary>
/// Keeps each player's camera rotation, as their client reports it, on their character.
/// </summary>
public sealed class CMUViewRotationSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUViewRotationEvent>(OnViewRotation);
    }

    private void OnViewRotation(CMUViewRotationEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player || !double.IsFinite(msg.Rotation.Theta))
            return;

        EnsureComp<CMUViewRotationComponent>(player).Rotation = msg.Rotation.Reduced();
    }
}
