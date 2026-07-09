using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._RMC14.Medical.Defibrillator;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Heart;

public sealed partial class HeartDefibrillatorPatchSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedHeartSystem _heart = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, RMCDefibrillatorAttemptEvent>(OnDefibAttempt);
    }

    private void OnDefibAttempt(Entity<CMUHumanMedicalComponent> ent, ref RMCDefibrillatorAttemptEvent args)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) || !_cfg.GetCVar(CMUMedicalCCVars.OrganEnabled))
            return;

        if (!TryFindHeart(ent, out var heartId, out var heart, out var heartHealth))
        {
            args.Cancel("cmu-medical-defib-no-heart");
            return;
        }

        if (heartHealth.Stage == OrganDamageStage.Dead)
        {
            args.Cancel("cmu-medical-defib-heart-destroyed");
            return;
        }

        if (!heart.Stopped)
        {
            args.Cancel("cmu-medical-defib-heart-beating");
            return;
        }

        // Restart the CMU heart in the same handler — without this the zap
        // clears entity damage but leaves Stopped=true, perfusion stays at
        // 0, asphyx accumulates, and the patient re-dies seconds later.
        _heart.TryRestartHeart((heartId, heart));
    }

    private bool TryFindHeart(EntityUid body, out EntityUid heartId, out HeartComponent heart, out OrganHealthComponent health)
    {
        heartId = default;
        heart = default!;
        health = default!;
        if (!_medicalIndex.TryGetOrgan<HeartComponent>(body, out var organId) ||
            !TryComp<HeartComponent>(organId, out var h) ||
            !TryComp<OrganHealthComponent>(organId, out var oh))
            return false;

        heartId = organId;
        heart = h;
        health = oh;
        return true;
    }
}
