using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Standing;
using Content.Shared.Body.Part;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Pool;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Pool;

public sealed partial class CMUPoolStickSystem : EntitySystem
{
    private const float KnockdownChance = 0.25f;
    private static readonly EntProtoId BrokenStick = "CMUPoolStickBroken";
    private static readonly ProtoId<EmotePrototype> Scream = "Scream";
    private static readonly SoundCollectionSpecifier Snap = new("CMUPoolStickSnap");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCStandingSystem _rest = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPoolStickComponent, ContainerGettingInsertedAttemptEvent>(OnStoreAttempt);
        SubscribeLocalEvent<CMUPoolStickComponent, MeleeHitEvent>(OnHit);
    }

    private void OnStoreAttempt(Entity<CMUPoolStickComponent> stick, ref ContainerGettingInsertedAttemptEvent args)
    {
        if (HasComp<StorageComponent>(args.Container.Owner) && !HasComp<CMUPoolStickHolderComponent>(args.Container.Owner))
            args.Cancel();
    }

    private void OnHit(Entity<CMUPoolStickComponent> stick, ref MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit || args.HitEntities.Count == 0)
            return;

        if (stick.Comp.Spent)
        {
            args.Handled = true;
            return;
        }

        stick.Comp.Spent = true;
        var user = args.User;
        var target = args.HitEntities[0];

        var attemptKnockdown = _random.Prob(KnockdownChance);
        Timer.Spawn(TimeSpan.Zero, () => FinishBreak(stick.Owner, user, target, attemptKnockdown));
    }

    private void FinishBreak(EntityUid stick, EntityUid user, EntityUid target, bool attemptKnockdown)
    {
        if (TerminatingOrDeleted(stick))
            return;

        var held = _hands.IsHolding(user, stick);
        if (held)
            _hands.TryDrop(user, stick, checkActionBlocker: false, doDropInteraction: false);

        var position = Transform(held ? user : stick).Coordinates;
        _audio.PlayPvs(Snap, position, AudioParams.Default.WithVolume(4f).WithReferenceDistance(3f).WithMaxDistance(10f));
        var broken = SpawnAtPosition(BrokenStick, position);
        QueueDel(stick);
        if (held)
            _hands.TryPickup(user, broken, animate: false);

        if (!attemptKnockdown || !_mob.IsAlive(target) || !TryComp<RMCRestComponent>(target, out var rest))
            return;

        var armor = new CMGetArmorEvent(SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES,
            TargetPart: BodyPartType.Head, TargetZone: TargetBodyZone.Head);
        RaiseLocalEvent(target, ref armor);
        if (armor.Melee * armor.ArmorModifier >= 10 || !_standing.Down(target, downedBy: user))
            return;

        _rest.SetRest((target, rest), true);
        _movement.RefreshMovementSpeedModifiers(target);
        _chat.TryEmoteWithChat(target, Scream);
    }
}
