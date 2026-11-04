using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Xenomorphs.Parasite;

/// <summary>
///     What a host feels while a parasite is on their face: as it latches on, now and then while it holds on, and
///     when it lets go. The host stays knocked out for as long as it holds on.
/// </summary>
public sealed class CMUParasiteMessagesSystem : EntitySystem
{
    private const int HuggedMessages = 4;
    private const int HoldingMessages = 8;
    private const int FellOffMessages = 4;

    private const float MinMessageDelay = 20f;
    private const float MaxMessageDelay = 40f;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>Topped up every update, so the host wakes shortly after the parasite lets go.</summary>
    private static readonly TimeSpan KnockOutTime = TimeSpan.FromSeconds(3);

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Hug> _hugs = new();
    private readonly List<EntityUid> _over = new();
    private TimeSpan _nextUpdate;

    private sealed class Hug
    {
        public EntityUid Parasite;
        public TimeSpan NextMessage;
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<InfectableComponent, XenoParasiteInfectEvent>(OnInfect);
    }

    private void OnInfect(Entity<InfectableComponent> ent, ref XenoParasiteInfectEvent args)
    {
        _hugs[ent] = new Hug { Parasite = args.Parasite, NextMessage = _timing.CurTime + MessageDelay() };
        Message(ent, $"cmu-xeno-parasite-hugged-{_random.Next(1, HuggedMessages + 1)}", args.Parasite, PopupType.LargeCaution);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        _over.Clear();
        foreach (var (victim, hug) in _hugs)
        {
            if (!Exists(victim) ||
                !TryComp(hug.Parasite, out XenoParasiteComponent? parasite) ||
                parasite.InfectedVictim != victim)
            {
                _over.Add(victim);
                continue;
            }

            if (parasite.FellOff)
            {
                Message(victim, $"cmu-xeno-parasite-fell-off-{_random.Next(1, FellOffMessages + 1)}", hug.Parasite, PopupType.MediumCaution);
                _over.Add(victim);
                continue;
            }

            if (!_mobState.IsDead(victim))
                _sizeStun.TryKnockOut(victim, KnockOutTime);

            if (now < hug.NextMessage)
                continue;

            hug.NextMessage = now + MessageDelay();
            Message(victim, $"cmu-xeno-parasite-holding-{_random.Next(1, HoldingMessages + 1)}", hug.Parasite, PopupType.SmallCaution);
        }

        foreach (var victim in _over)
        {
            _hugs.Remove(victim);
        }
    }

    private void Message(EntityUid victim, string locId, EntityUid parasite, PopupType type)
    {
        _popup.PopupEntity(Loc.GetString(locId, ("parasite", parasite)), victim, victim, type);
    }

    private TimeSpan MessageDelay()
    {
        return TimeSpan.FromSeconds(_random.NextFloat(MinMessageDelay, MaxMessageDelay));
    }
}
