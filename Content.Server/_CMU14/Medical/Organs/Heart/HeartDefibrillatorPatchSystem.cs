using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Body.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._CMU14.Medical.Organs.Heart;

public sealed class HeartDefibrillatorPatchSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHeartSystem _heart = default!;

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
        foreach (var (organId, _) in _body.GetBodyOrgans(body))
        {
            if (!TryComp<HeartComponent>(organId, out var h))
                continue;
            if (!TryComp<OrganHealthComponent>(organId, out var oh))
                continue;
            heartId = organId;
            heart = h;
            health = oh;
            return true;
        }
        return false;
    }
}
