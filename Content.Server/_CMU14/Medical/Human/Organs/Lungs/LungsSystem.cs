using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Organs.Lungs;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Emote;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Human.Organs.Lungs;

public sealed partial class LungsSystem : SharedLungsSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<EmotePrototype> Cough = "Cough";
    private static readonly ProtoId<EmotePrototype> Gasp = "Gasp";
    private static readonly TimeSpan BreathingEmoteCooldown = TimeSpan.FromSeconds(8);

    private const float LittleBruisedBreathingEmoteChance = 0.12f;
    private const float BruisedBreathingEmoteChance = 0.35f;
    private const float BrokenBreathingEmoteChance = 0.70f;

    private readonly Dictionary<EntityUid, TimeSpan> _nextBreathingEmote = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalShutdownEvent>(OnHumanMedicalShutdown);
    }

    private void OnHumanMedicalShutdown(ref HumanMedicalShutdownEvent args)
    {
        _nextBreathingEmote.Remove(args.Body);
    }

    /// <summary>
    ///     Bypasses resistances so a marine drowning on damaged lungs cannot
    ///     be saved by armour.
    /// </summary>
    protected override void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Asphyxiation, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Asphyxiation.Id] = amount } };
        Damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: lung);

        if (body == lung)
            TryBreathingEmote(body, Gasp, 1f);
    }

    protected override void ApplyBreathingSymptom(EntityUid body, EntityUid lung, OrganDamageStatus status)
    {
        switch (status)
        {
            case OrganDamageStatus.LittleBruised:
                TryBreathingEmote(body, Cough, LittleBruisedBreathingEmoteChance);
                break;
            case OrganDamageStatus.Bruised:
                TryBreathingEmote(body, Cough, BruisedBreathingEmoteChance);
                break;
            case OrganDamageStatus.Broken:
                TryBreathingEmote(body, Gasp, BrokenBreathingEmoteChance);
                break;
        }
    }

    private void TryBreathingEmote(EntityUid body, ProtoId<EmotePrototype> emote, float chance)
    {
        if (chance <= 0f)
            return;

        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        var now = _timing.CurTime;
        if (_nextBreathingEmote.TryGetValue(body, out var next) && next > now)
            return;

        if (chance < 1f && !_random.Prob(chance))
            return;

        _nextBreathingEmote[body] = now + BreathingEmoteCooldown;
        _emote.TryEmoteWithChat(
            body,
            emote,
            ignoreActionBlocker: true,
            forceEmote: true,
            cooldown: BreathingEmoteCooldown);
    }
}
