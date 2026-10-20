using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical;

/// <summary>
/// Tells a mob it can't breathe while it's actually suffocating (out of breath, not just taking oxygen damage
/// from something else), and shows it on examine. Breathing is only tracked on the server, so the examine line
/// comes from the server's half of the examine, and the health scanner's advice to put them on internals is set here.
/// </summary>
public sealed partial class CMUSuffocationMessageSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan MessageCooldown = TimeSpan.FromSeconds(8);

    /// <summary>Suffocation fires every breath cycle (2 seconds), so this covers a missed or slowed cycle.</summary>
    private static readonly TimeSpan SuffocatingWindow = TimeSpan.FromSeconds(5);

    private static readonly LocId[] Messages =
    [
        "cmu-suffocation-cant-breathe-1",
        "cmu-suffocation-cant-breathe-2",
        "cmu-suffocation-cant-breathe-3",
    ];

    private readonly Dictionary<EntityUid, TimeSpan> _nextMessage = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastSuffocation = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RespiratorComponent, SuffocationEvent>(OnSuffocation);
        SubscribeLocalEvent<RespiratorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RespiratorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RespiratorComponent, CMUHealthScannerReadingEvent>(OnScannerReading);
    }

    private void OnSuffocation(Entity<RespiratorComponent> ent, ref SuffocationEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        var now = _timing.CurTime;
        _lastSuffocation[ent] = now;

        if (_nextMessage.TryGetValue(ent, out var next) && now < next)
            return;

        _nextMessage[ent] = now + MessageCooldown;
        _popup.PopupEntity(Loc.GetString(_random.Pick(Messages)), ent, ent, PopupType.MediumCaution);
    }

    private void OnShutdown(Entity<RespiratorComponent> ent, ref ComponentShutdown args)
    {
        _nextMessage.Remove(ent);
        _lastSuffocation.Remove(ent);
    }

    private void OnExamined(Entity<RespiratorComponent> ent, ref ExaminedEvent args)
    {
        if (!IsSuffocating(ent))
            return;

        args.PushMarkup(Loc.GetString("cmu-examine-cant-breathe", ("target", ent.Owner)), -1);
    }

    private void OnScannerReading(Entity<RespiratorComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        // Nobody breathes in crit, so there the advice is CPR, not a tank.
        if (_mobState.IsAlive(ent) && IsSuffocating(ent))
            args.State.Advice.CMUShowInternals = true;
    }

    /// <summary>
    ///     Whether a can't-breathe message popped up for this mob within <paramref name="window"/>, so other status
    ///     messages can wait their turn instead of stacking on top of it.
    /// </summary>
    public bool ShowedMessageWithin(EntityUid uid, TimeSpan window)
    {
        return _nextMessage.TryGetValue(uid, out var next) &&
               _timing.CurTime - (next - MessageCooldown) < window;
    }

    /// <summary>Whether the mob is alive and has run out of breath within the last few breath cycles.</summary>
    public bool IsSuffocating(EntityUid uid)
    {
        return !_mobState.IsDead(uid) &&
               _lastSuffocation.TryGetValue(uid, out var last) &&
               _timing.CurTime - last <= SuffocatingWindow;
    }
}
