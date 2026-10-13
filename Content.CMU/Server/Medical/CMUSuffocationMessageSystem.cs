using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical;

/// <summary>
/// Tells a mob it can't breathe while it's actually suffocating (out of breath, not just taking oxygen damage
/// from something else), and shows it on examine. Breathing is only tracked on the server, so the examine line
/// comes from the server's half of the examine.
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
        if (_mobState.IsDead(ent) ||
            !_lastSuffocation.TryGetValue(ent, out var last) ||
            _timing.CurTime - last > SuffocatingWindow)
        {
            return;
        }

        args.PushMarkup(Loc.GetString("cmu-examine-cant-breathe", ("target", ent.Owner)), -1);
    }
}
