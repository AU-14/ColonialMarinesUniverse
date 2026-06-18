using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._RMC14.Emote;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Buckle.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanMedicalMovementDebuffSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private const float MovementDistanceThreshold = 0.75f;
    private const float MinimumMoveDistance = 0.05f;
    private const float FracturePainPulse = 2f;
    private const float InternalBleedPainPulse = 7f;
    private const float CrippledLegsParalyzeSeconds = 5f;
    private const float FractureMovementScreamChance = 0.25f;
    private const float FractureComplicationScreamChance = 0.65f;

    private static readonly SoundSpecifier FractureMovementSound = new SoundPathSpecifier(
        "/Audio/Effects/bone_rattle.ogg",
        AudioParams.Default.WithVariation(0.08f).WithVolume(-5f).WithMaxDistance(6f));

    private static readonly ProtoId<EmotePrototype> Scream = "Scream";
    private static readonly TimeSpan FracturePainEmoteCooldown = TimeSpan.FromSeconds(8);

    private readonly Dictionary<EntityUid, float> _movementAccumulators = new();

    private bool _medicalEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveUnsplintedFractureRiskComponent, MoveEvent>(OnRiskMove);
        SubscribeLocalEvent<ActiveUnsplintedFractureRiskComponent, StoodEvent>(OnRiskStood);
        SubscribeLocalEvent<ActiveUnsplintedFractureRiskComponent, ComponentShutdown>(OnRiskShutdown);

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, value => _medicalEnabled = value, true);
    }

    private void OnRiskMove(Entity<ActiveUnsplintedFractureRiskComponent> ent, ref MoveEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState || !_medicalEnabled)
            return;

        if (args.ParentChanged || args.OldPosition == args.NewPosition)
            return;

        if (!args.NewPosition.TryDistance(EntityManager, _transform, args.OldPosition, out var distance))
            return;

        if (distance <= MinimumMoveDistance)
            return;

        _movementAccumulators.TryGetValue(ent.Owner, out var accumulated);
        accumulated += (float) distance;
        if (accumulated < MovementDistanceThreshold)
        {
            _movementAccumulators[ent.Owner] = accumulated;
            return;
        }

        _movementAccumulators[ent.Owner] = accumulated % MovementDistanceThreshold;
        TryProcessFractureRisk(ent.Owner);
    }

    private void OnRiskStood(Entity<ActiveUnsplintedFractureRiskComponent> ent, ref StoodEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState || !_medicalEnabled)
            return;

        TryProcessFractureRisk(ent.Owner);
    }

    private void OnRiskShutdown(Entity<ActiveUnsplintedFractureRiskComponent> ent, ref ComponentShutdown args)
    {
        _movementAccumulators.Remove(ent.Owner);
    }

    private void TryProcessFractureRisk(EntityUid body)
    {
        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return;

        if (!IsEligibleStandingPatient(body))
            return;

        var result = HumanMovementDebuffRules.EvaluateUnsplintedFractureMovement(
            medical,
            CreateRng());

        if (result.Transaction is { Count: > 0 } transaction)
            _humanMedical.ApplyTransaction((body, medical), transaction);

        if (result.BonesMoved)
        {
            _pain.AddPainPulse(body, FixedPoint2.New(FracturePainPulse));
            _audio.PlayPvs(FractureMovementSound, body);
            _pain.TryCustomPain(
                body,
                "cmu-medical-fracture-bones-moving",
                1,
                ("part", GetRegionName(result.BonesMovedRegion)));
            TryFracturePainEmote(body, FractureMovementScreamChance);
        }

        if (result.BonesCut)
        {
            _pain.AddPainPulse(body, FixedPoint2.New(InternalBleedPainPulse));
            _pain.TryCustomPain(
                body,
                "cmu-medical-fracture-bones-cutting",
                1,
                ("part", GetRegionName(result.BonesCutRegion)));
            TryFracturePainEmote(body, FractureComplicationScreamChance);
        }

        if (!result.CrippledLegsShouldDrop)
            return;

        _pain.TryCustomPain(body, "cmu-medical-fracture-broken-legs-drop", 1);
        TryFracturePainEmote(body, FractureComplicationScreamChance);
        _stun.TryParalyze(body, TimeSpan.FromSeconds(CrippledLegsParalyzeSeconds), true);
    }

    private void TryFracturePainEmote(EntityUid body, float chance)
    {
        if (chance <= 0f || !_random.Prob(chance))
            return;

        _emote.TryEmoteWithChat(
            body,
            Scream,
            ignoreActionBlocker: true,
            forceEmote: true,
            cooldown: FracturePainEmoteCooldown);
    }

    private bool IsEligibleStandingPatient(EntityUid body)
    {
        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return false;

        if (_standing.IsDown(body))
            return false;

        return !TryComp<BuckleComponent>(body, out var buckle) || !buckle.Buckled;
    }

    private HumanMovementDebuffRng CreateRng()
    {
        return new HumanMovementDebuffRng(
            OrganDamageRoll: _random.NextFloat(),
            InternalBleedRoll: _random.NextFloat(),
            CrippledLegsRoll: _random.NextFloat(),
            OrganDamageAmountRoll: _random.NextFloat(),
            InternalBleedAmountRoll: _random.NextFloat(),
            OrganSlotRoll: _random.NextFloat(),
            BrokenRegionRoll: _random.NextFloat());
    }

    private string GetRegionName(BodyRegion region)
    {
        return Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(region));
    }
}
