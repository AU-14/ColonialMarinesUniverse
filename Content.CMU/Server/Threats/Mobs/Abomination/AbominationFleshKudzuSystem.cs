using Content.Server.Chat.Systems;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationFleshKudzuComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationFleshKudzuComponent;

namespace Content.Server._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Periodic heal-tick for abominations standing on a flesh kudzu tile, plus
///     occasional sob/cry/scream emotes. Damage tick for non-abominations is
///     handled by upstream DamageContacts on the kudzu prototype. Abomination
///     melee attacks on tendons are rejected here so the threat can't trash its
///     own coverage. Also drives the tiny everywhere-passive heal on every
///     abomination (see AbominationComponent.PassiveHeal).
/// </summary>
public sealed partial class AbominationFleshKudzuSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private AbominationInfectionSystem _infection = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationComponent, AttackAttemptEvent>(OnAbominationAttackAttempt);
    }

    public override void Update(float frameTime)
    {
        TimeSpan now = _timing.CurTime;

        // Passive heal — applies to every abomination everywhere, separate
        // from the much stronger tendon-contact heal below.
        EntityQueryEnumerator<AbominationComponent> passive = EntityQueryEnumerator<AbominationComponent>();
        while (passive.MoveNext(out EntityUid passiveUid, out AbominationComponent? abom))
        {
            if (abom.NextPassiveHealAt > now)
                continue;

            abom.NextPassiveHealAt = now + abom.PassiveHealInterval;
            _damageable.TryChangeDamage(passiveUid, abom.PassiveHeal, true);
        }

        EntityQueryEnumerator<AbominationFleshKudzuComponent, PhysicsComponent> query
            = EntityQueryEnumerator<AbominationFleshKudzuComponent, PhysicsComponent>();
        while (query.MoveNext(out EntityUid uid, out AbominationFleshKudzuComponent? kudzu,
            out PhysicsComponent? physics))
        {
            if (kudzu.NextHealAt <= now)
            {
                kudzu.NextHealAt = now + kudzu.HealInterval;
                HealContacts((uid, kudzu, physics));
            }

            // Anyone knocked out / critted while on the tendons gets seeded
            // with the infection. Drag-and-dump play is intended.
            if (kudzu.NextInfectAt <= now)
            {
                kudzu.NextInfectAt = now + kudzu.InfectInterval;
                InfectIncapacitatedContacts((uid, kudzu, physics));
            }

            if (kudzu.NextEmoteAt <= now)
            {
                kudzu.NextEmoteAt = now
                    + TimeSpan.FromSeconds(_random.NextDouble(kudzu.EmoteIntervalMin.TotalSeconds,
                        kudzu.EmoteIntervalMax.TotalSeconds));

                AudioParams audioParams = AudioParams.Default.WithVolume(kudzu.EmoteVolume);

                // Most of the time the kudzu cries; the rest of the time it
                // picks a non-cry emote (gasp, scream, etc.). forceEmote +
                // ignoreActionBlocker so the kudzu (no Speech/Vocal) can still
                // emit the chat + sound.
                if (_random.Prob(kudzu.CryChance))
                {
                    _chat.TryEmoteWithoutChat(uid, kudzu.CryEmote, true);
                    _audio.PlayPvs(kudzu.CrySound, uid, audioParams);
                }
                else if (kudzu.Emotes.Count > 0)
                {
                    _chat.TryEmoteWithoutChat(uid, _random.Pick(kudzu.Emotes), true);
                    if (kudzu.EmoteSounds.Count > 0)
                        _audio.PlayPvs(_random.Pick(kudzu.EmoteSounds), uid, audioParams);
                }
            }
        }
    }

    /// <summary>
    ///     Block abominations from melee-attacking flesh kudzu — they kept
    ///     destroying their own coverage in playtest.
    /// </summary>
    private void OnAbominationAttackAttempt(Entity<AbominationComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target is { } target && HasComp<AbominationFleshKudzuComponent>(target))
            args.Cancel();
    }

    private void HealContacts(Entity<AbominationFleshKudzuComponent, PhysicsComponent> ent)
    {
        foreach (EntityUid contact in _physics.GetContactingEntities(ent.Owner, ent.Comp2))
        {
            if (!HasComp<AbominationComponent>(contact))
                continue;

            _damageable.TryChangeDamage(contact, ent.Comp1.Heal, true);
        }
    }

    private void InfectIncapacitatedContacts(Entity<AbominationFleshKudzuComponent, PhysicsComponent> ent)
    {
        foreach (EntityUid contact in _physics.GetContactingEntities(ent.Owner, ent.Comp2))
        {
            if (!_mobState.IsIncapacitated(contact))
                continue;

            _infection.TryInfect(contact);
        }
    }
}
