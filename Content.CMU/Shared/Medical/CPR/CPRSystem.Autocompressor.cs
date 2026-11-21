// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.PowerCell;

namespace Content.Shared._RMC14.Medical.CPR;

public sealed partial class CPRSystem
{
    [Dependency] private PowerCellSystem _cmuCompressionPower = default!;

    public bool CMUCanWearMechanicalCPR(EntityUid patient)
    {
        if (HasComp<CPRDummyComponent>(patient))
            return Transform(patient).Anchored;
        if (!TryComp<HumanoidProfileComponent>(patient, out var profile) || profile.Species != "Human"
            || !HasComp<MarineComponent>(patient) || !HasComp<RMCRevivableComponent>(patient)
            || HasComp<SynthComponent>(patient) || HasComp<XenoComponent>(patient) || HasComp<YautjaComponent>(patient)
            || _unrevivable.IsUnrevivable(patient))
            return false;

        return !_mobState.IsDead(patient) || _unrevivable.GetTimeUntilUnrevivable(patient) is { } remaining
            && remaining > TimeSpan.Zero;
    }

    public bool CMUCanReceiveMechanicalCPR(EntityUid patient)
    {
        return CMUCanWearMechanicalCPR(patient)
            && (HasComp<CPRDummyComponent>(patient) || _mobState.IsCritical(patient) || _mobState.IsDead(patient));
    }

    public bool CMUTryMechanicalCPR(EntityUid patient, EntityUid device, float charge)
    {
        if (_net.IsClient || !float.IsFinite(charge) || charge <= 0 || !CMUCanReceiveMechanicalCPR(patient))
            return false;

        var now = _timing.CurTime;
        if (TryComp<ReceivingCPRComponent>(patient, out var receiving) && now - receiving.StartTime <= CPRCooldownSeconds
            || TryComp<CPRReceivedComponent>(patient, out var previous) && now - previous.Last < CPRCooldownSeconds)
            return false;

        if (!_cmuCompressionPower.TryUseCharge(device, charge))
            return false;

        if (TryComp<CPRDummyComponent>(patient, out var dummy))
        {
            dummy.CPRSuccess++;
            Dirty(patient, dummy);
        }
        else
        {
            _unrevivable.ReverseRevivalTimer(patient, CPRCooldownSeconds);
            if (TryComp<DamageableComponent>(patient, out var damageable)
                && _damageable.GetAllDamage((patient, damageable)).DamageDict.TryGetValue(HealType, out var damage))
            {
                var healing = new DamageSpecifier();
                healing.DamageDict.Add(HealType, -FixedPoint2.Min(damage, HealAmount));
                _damageable.TryChangeDamage(patient, healing, true);
            }
        }

        var received = EnsureComp<CPRReceivedComponent>(patient);
        received.Last = now;
        Dirty(patient, received);
        return true;
    }
}
