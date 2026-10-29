using Content.Shared.GameTicking;

namespace Content.Server.CMU14.HelpRelay;

/// <summary>
/// Round lifecycle for relay state that must not outlive a round: conversation messages end with the
/// round, the next line starts a fresh one.
/// </summary>
public sealed class HelpRelaySystem : EntitySystem
{
    [Dependency] private readonly HelpRelayManager _relay = default!;

    public override void Initialize()
    {
        _relay.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _relay.ClearRoundState());
    }
}
