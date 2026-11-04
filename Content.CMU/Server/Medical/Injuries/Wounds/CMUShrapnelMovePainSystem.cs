using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// Someone carrying shrapnel feels it when they set off: "You feel shrapnel digging into your left
/// leg as you move." Only on starting to move, and at most once every <see cref="Cooldown"/>.
/// </summary>
public sealed class CMUShrapnelMovePainSystem : EntitySystem
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextMessage = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHumanMedicalComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<CMUHumanMedicalComponent, ComponentShutdown>((uid, _, _) => _nextMessage.Remove(uid));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _nextMessage.Clear());
    }

    private void OnMoveInput(Entity<CMUHumanMedicalComponent> ent, ref MoveInputEvent args)
    {
        // Only when they set off, not on every change of direction while already moving.
        if (!args.HasDirectionalMovement ||
            (args.OldMovement & MoveButtons.AnyDirection) != MoveButtons.None)
        {
            return;
        }

        var now = _timing.CurTime;
        if (_nextMessage.TryGetValue(ent, out var next) && now < next)
            return;

        if (!_mobState.IsAlive(ent))
            return;

        BodyPartComponent? worst = null;
        var worstSeverity = 0f;
        foreach (var (part, partComp) in _medicalIndex.GetBodyParts(ent))
        {
            if (!TryComp(part, out CMUShrapnelComponent? shrapnel) || shrapnel.Fragments <= 0)
                continue;

            if (worst == null || shrapnel.Severity > worstSeverity)
            {
                worst = partComp;
                worstSeverity = shrapnel.Severity;
            }
        }

        if (worst == null)
            return;

        _nextMessage[ent] = now + Cooldown;

        var partName = CMUBodyPartNames.Describe(worst.PartType, worst.Symmetry);

        _popup.PopupEntity(Loc.GetString("cmu-shrapnel-move-pain", ("part", partName)), ent, ent, PopupType.SmallCaution);
    }
}
