using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Traits.SubstanceAddiction;

public sealed class CMUSubstanceAddictionSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSubstanceAddictionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUSubstanceAddictionComponent, CureChemicalAddictionEvent>(OnCure);
    }

    private void OnStartup(Entity<CMUSubstanceAddictionComponent> ent, ref ComponentStartup args)
    {
        ResetCraving(ent);
    }

    private void OnCure(Entity<CMUSubstanceAddictionComponent> ent, ref CureChemicalAddictionEvent args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.CravingAlert);
        RemCompDeferred<CMUSubstanceAddictionComponent>(ent);
    }

    private void ResetCraving(Entity<CMUSubstanceAddictionComponent> ent)
    {
        var time = _timing.CurTime;
        ent.Comp.LastUsed = time;
        ent.Comp.NextShake = time + ent.Comp.ShakeThreshold;
        ent.Comp.Craving = false;
        Dirty(ent);

        _alerts.ClearAlert(ent.Owner, ent.Comp.CravingAlert);
    }

    /// <summary>Whether any reagent that satisfies this addiction is in the mob's blood or metabolites.</summary>
    private bool HasSubstance(Entity<CMUSubstanceAddictionComponent> ent)
    {
        if (_rmcBloodstream.TryGetChemicalSolution(ent, out _, out var chemicals) && Contains(ent.Comp, chemicals))
            return true;

        // Alcohol breaks down into ethanol, which sits in the metabolites solution.
        return TryComp(ent, out BloodstreamComponent? bloodstream) &&
               _solution.TryGetSolution(ent.Owner, bloodstream.MetabolitesSolutionName, out _, out var metabolites) &&
               Contains(ent.Comp, metabolites);
    }

    private bool Contains(CMUSubstanceAddictionComponent comp, Solution solution)
    {
        foreach (var reagent in solution.Contents)
        {
            var id = reagent.Reagent.Prototype;
            if (comp.Reagents.Contains(id))
                return true;

            if (comp.AnyAlcohol && _reagents.TryIndex(id, out var proto) && proto.Alcohol)
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUSubstanceAddictionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (time < comp.NextCheck)
                continue;

            comp.NextCheck = time + comp.TimeBetweenChecks;
            var ent = (uid, comp);

            if (HasSubstance(ent))
            {
                var wasCraving = comp.Craving;
                ResetCraving(ent);
                if (wasCraving)
                    _popup.PopupEntity(Loc.GetString(comp.SatisfiedMessage), uid, uid, PopupType.Medium);

                continue;
            }

            var elapsed = time - comp.LastUsed;
            var craving = elapsed >= comp.CravingThreshold;
            if (craving != comp.Craving)
            {
                comp.Craving = craving;
                Dirty(uid, comp);

                if (craving)
                {
                    _alerts.ShowAlert(uid, comp.CravingAlert);
                    _popup.PopupEntity(Loc.GetString(comp.OnsetMessage), uid, uid, PopupType.Medium);
                }
                else
                {
                    _alerts.ClearAlert(uid, comp.CravingAlert);
                }
            }

            if (craving && time >= comp.NextCravingMessage)
            {
                comp.NextCravingMessage = time + comp.CravingMessageCooldown;
                _popup.PopupEntity(Loc.GetString(comp.CravingMessage), uid, uid, PopupType.Small);
            }

            if (elapsed >= comp.ShakeThreshold && time >= comp.NextShake)
            {
                comp.NextShake = time + TimeSpan.FromSeconds(
                    _random.NextFloat((float) comp.ShakeIntervalMin.TotalSeconds, (float) comp.ShakeIntervalMax.TotalSeconds));
                Dirty(uid, comp);

                _jitter.DoJitter(uid, comp.ShakeDuration, true, 8, 4);
                _popup.PopupEntity(Loc.GetString(comp.ShakeMessage), uid, uid, PopupType.SmallCaution);
            }
        }
    }
}
