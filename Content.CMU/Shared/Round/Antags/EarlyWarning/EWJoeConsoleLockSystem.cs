using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// An assimilated console refuses its UI to everyone until it is repaired. Shared so the client
/// predicts the refusal instead of flashing the window open; the marker is networked for that.
/// </summary>
public sealed class EWJoeConsoleLockSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeAssimilatedConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<EWJoeAssimilatedConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();

        if (!args.Silent)
            // predicted: shown once on the opener's client, not again from the server
            _popup.PopupClient(Loc.GetString("cmu-ew-joe-console-locked", ("console", ent.Owner)), ent, args.User);
    }
}
