using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical;

/// <summary>
/// Tells a mob it can't breathe while it's actually suffocating (out of breath, not just taking oxygen damage
/// from something else).
/// </summary>
public sealed partial class CMUSuffocationMessageSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan MessageCooldown = TimeSpan.FromSeconds(8);

    private static readonly LocId[] Messages =
    [
        "cmu-suffocation-cant-breathe-1",
        "cmu-suffocation-cant-breathe-2",
        "cmu-suffocation-cant-breathe-3",
    ];

    private readonly Dictionary<EntityUid, TimeSpan> _nextMessage = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RespiratorComponent, SuffocationEvent>(OnSuffocation);
        SubscribeLocalEvent<RespiratorComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnSuffocation(Entity<RespiratorComponent> ent, ref SuffocationEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        var now = _timing.CurTime;
        if (_nextMessage.TryGetValue(ent, out var next) && now < next)
            return;

        _nextMessage[ent] = now + MessageCooldown;
        _popup.PopupEntity(Loc.GetString(_random.Pick(Messages)), ent, ent, PopupType.MediumCaution);
    }

    private void OnShutdown(Entity<RespiratorComponent> ent, ref ComponentShutdown args)
    {
        _nextMessage.Remove(ent);
    }
}
