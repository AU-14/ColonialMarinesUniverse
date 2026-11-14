using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Gauss;

/// <summary>
///     Holds someone being vaporized by an M60 gauss gun in place, upright, silent and unable to act, until
///     the server removes them. The client darkens them and draws the embers and ash.
/// </summary>
public sealed class CMUGaussVaporizingSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGaussVaporizingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, UpdateCanMoveEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, ChangeDirectionAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, UseAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, AttackAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, ThrowAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, PickupAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, DownAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, SpeakAttemptEvent>(OnCancel);
        // Every emote, forced pain screams included, so the gun's Wilhelm scream is the only cry they make.
        SubscribeLocalEvent<CMUGaussVaporizingComponent, BeforeEmoteEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, IsEquippingAttemptEvent>(OnCancel);
        SubscribeLocalEvent<CMUGaussVaporizingComponent, IsUnequippingAttemptEvent>(OnCancel);
    }

    /// <summary>How far through the vaporizing someone is, from 0 to 1.</summary>
    public float GetProgress(CMUGaussVaporizingComponent comp)
    {
        var total = comp.End - comp.Start;
        if (total <= TimeSpan.Zero)
            return 1f;

        return Math.Clamp((float) ((_timing.CurTime - comp.Start) / total), 0f, 1f);
    }

    private void OnStartup(Entity<CMUGaussVaporizingComponent> ent, ref ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<CMUGaussVaporizingComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _actionBlocker.UpdateCanMove(ent);
    }

    private void OnInteractionAttempt(Entity<CMUGaussVaporizingComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnCancel<T>(Entity<CMUGaussVaporizingComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }
}
