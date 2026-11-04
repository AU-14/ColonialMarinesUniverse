using Content.Server.Chat.Systems;
using Content.Server.CMU14.Threats.Mobs.Biomorph;
using Content.Server.Spreader;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Mobs.Biomorph;

/// <summary>
///     Periodic heal-tick for abominations standing on a flesh kudzu tile, plus
///     occasional sob/cry/scream emotes. Damage tick for non-abominations is
///     handled by upstream DamageContacts on the kudzu prototype. Abomination
///     melee attacks on tendons are rejected here so the threat can't trash its
///     own coverage. Also drives the tiny everywhere-passive heal on every
///     abomination (see AbominationComponent.PassiveHeal).
/// </summary>
public sealed partial class BiomorphFleshKudzuSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BiomorphInfectionSystem _infection = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private const float FireProbeRadius = 0.5f;
    private readonly HashSet<Entity<TileFireComponent>> _fireBuffer = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BiomorphFleshKudzuComponent, SpreadNeighborsEvent>(OnSpreadNeighbors,
            before: [typeof(KudzuSystem)]);
    }

    private void OnSpreadNeighbors(Entity<BiomorphFleshKudzuComponent> ent, ref SpreadNeighborsEvent args)
    {
        for (var i = args.NeighborFreeTiles.Count - 1; i >= 0; i--)
        {
            var neighbor = args.NeighborFreeTiles[i];
            if (_weeds.CanSpreadWeedsPopup(
                    (neighbor.Tile.GridUid, neighbor.Grid),
                    neighbor.Tile.GridIndices,
                    null,
                    null))
            {
                continue;
            }

            args.NeighborFreeTiles.RemoveAt(i);
        }
    }

    public override void Update(float frameTime)
    {
        TimeSpan now = _timing.CurTime;

        // Passive heal — applies to every abomination everywhere, separate
        // from the much stronger tendon-contact heal below.
        EntityQueryEnumerator<BiomorphComponent> passive = EntityQueryEnumerator<BiomorphComponent>();
        while (passive.MoveNext(out EntityUid passiveUid, out BiomorphComponent? abom))
        {
            if (abom.NextPassiveHealAt > now)
                continue;

            abom.NextPassiveHealAt = now + abom.PassiveHealInterval;
            _damageable.TryChangeDamage(passiveUid, abom.PassiveHeal, true);
        }

        EntityQueryEnumerator<BiomorphFleshKudzuComponent, PhysicsComponent> query
            = EntityQueryEnumerator<BiomorphFleshKudzuComponent, PhysicsComponent>();
        while (query.MoveNext(out EntityUid uid, out BiomorphFleshKudzuComponent? kudzu,
            out PhysicsComponent? physics))
        {
            if (kudzu.NextHealAt <= now)
            {
                kudzu.NextHealAt = now + kudzu.HealInterval;
                HealContacts((uid, kudzu, physics));
            }

            if (kudzu.NextFireTickAt <= now)
            {
                kudzu.NextFireTickAt = now + kudzu.FireInterval;
                BurnFromTileFires((uid, kudzu));
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

    private void BurnFromTileFires(Entity<BiomorphFleshKudzuComponent> ent)
    {
        _fireBuffer.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMoverCoordinates(ent), FireProbeRadius, _fireBuffer);
        if (_fireBuffer.Count == 0)
            return;

        var dmg = new DamageSpecifier();
        dmg.DamageDict[HeatDamage] = ent.Comp.FireDamage;
        _damageable.TryChangeDamage(ent, dmg, true);
    }

    private void HealContacts(Entity<BiomorphFleshKudzuComponent, PhysicsComponent> ent)
    {
        foreach (EntityUid contact in _physics.GetContactingEntities(ent.Owner, ent.Comp2))
        {
            if (!HasComp<BiomorphComponent>(contact))
                continue;

            _damageable.TryChangeDamage(contact, ent.Comp1.Heal, true);
        }
    }

    private void InfectIncapacitatedContacts(Entity<BiomorphFleshKudzuComponent, PhysicsComponent> ent)
    {
        foreach (EntityUid contact in _physics.GetContactingEntities(ent.Owner, ent.Comp2))
        {
            if (!_mobState.IsIncapacitated(contact))
                continue;

            _infection.TryInfect(contact);
        }
    }
}
