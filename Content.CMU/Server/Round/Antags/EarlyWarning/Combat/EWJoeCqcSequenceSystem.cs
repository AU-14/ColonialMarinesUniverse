using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-06 (node N13): CQC sequences. Keeps the Joe's last few inputs (<see cref="EWJoeLiturgyComponent.Buffer"/>)
/// and, after each one, looks for the longest <see cref="EWJoeCqcSequencePrototype"/> the buffer ends in. A
/// match runs its effect through <see cref="EWJoeCqcEffectsSystem"/>. Sequences are free (ARCH-12): they spend no
/// Liturgy stacks and rely only on the input. An Exalted Joe's sequence plays its empowered version, when its effect
/// has one, and spends the Exalted state.
/// </summary>
/// <remarks>
/// Buffer rules (CqcSequences §3): at most <see cref="EWJoeLiturgyComponent.BufferCap"/> entries; a pause longer
/// than <see cref="EWJoeLiturgyComponent.MaxGap"/> or a new target starts it over; a miss clears it (the meter
/// does that); a pull upgraded to an aggressive grab is one Grab step, not two. A step on no target (an
/// untargeted ability) matches any target. A Wide step of a <see cref="EWJoeCqcSequencePrototype.RequiresBlade"/>
/// sequence only matches a swing made with a blade. Server only.
/// </remarks>
public sealed class EWJoeCqcSequenceSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EWJoeCombatSystem _combat = default!;
    [Dependency] private EWJoeCqcEffectsSystem _effects = default!;
    [Dependency] private EWJoeLiturgySystem _liturgy = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Every sequence, longest first, then by id so equal lengths match in a stable order.</summary>
    private readonly List<EWJoeCqcSequencePrototype> _sequences = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        CacheSequences();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EWJoeCqcSequencePrototype>())
            CacheSequences();
    }

    private void CacheSequences()
    {
        _sequences.Clear();
        _sequences.AddRange(_proto.EnumeratePrototypes<EWJoeCqcSequencePrototype>()
            .OrderByDescending(s => s.Steps.Count)
            .ThenBy(s => s.ID, StringComparer.Ordinal));
    }

    /// <summary>The sequences unlocked at <paramref name="level"/> or below, cheapest level first.</summary>
    public IEnumerable<EWJoeCqcSequencePrototype> Unlocked(int level)
    {
        return _sequences.Where(s => s.MinLevel <= level).OrderBy(s => s.MinLevel).ThenBy(s => s.ID, StringComparer.Ordinal);
    }

    /// <summary>Records one input in the buffer and fires the sequence it completes, if any.</summary>
    public void Push(Entity<EWJoeLiturgyComponent> liturgy, Entity<EWJoeComponent> joe, EWJoeCqcInput input, EntityUid? target, TimeSpan now, bool fromBlade = false)
    {
        var buffer = liturgy.Comp.Buffer;
        if (buffer.Count > 0)
        {
            if (now - buffer[^1].Time > liturgy.Comp.MaxGap)
                buffer.Clear();
            else if (target is { } t && EWJoeLiturgySystem.CurrentTarget(liturgy.Comp) is { } current && current != t)
                buffer.Clear(); // the same-target rule: a new body starts a new sequence
        }

        // a pull upgraded to an aggressive grab is one grab, not two steps
        if (input == EWJoeCqcInput.AggressiveGrab &&
            buffer.Count > 0 &&
            buffer[^1].Input == EWJoeCqcInput.Grab &&
            buffer[^1].Target == target)
        {
            buffer.RemoveAt(buffer.Count - 1);
        }

        buffer.Add(new EWJoeCqcBufferEntry(input, target, now, fromBlade));
        while (buffer.Count > Math.Max(1, liturgy.Comp.BufferCap))
        {
            buffer.RemoveAt(0);
        }

        TryMatch(liturgy, joe);
    }

    /// <summary>
    /// Fires the longest sequence the buffer ends in that the Joe's level, grab, blade and the target's CC
    /// immunity allow. Returns whether one fired.
    /// </summary>
    public bool TryMatch(Entity<EWJoeLiturgyComponent> liturgy, Entity<EWJoeComponent> joe)
    {
        var buffer = liturgy.Comp.Buffer;
        if (buffer.Count == 0 || EWJoeLiturgySystem.CurrentTarget(liturgy.Comp) is not { } target)
            return false;

        // RB-14: one shared cooldown after any sequence fires; the input is kept in the buffer meanwhile
        if (_timing.CurTime < liturgy.Comp.NextSequence)
            return false;

        foreach (var sequence in _sequences)
        {
            if (sequence.Steps.Count == 0 ||
                sequence.Steps.Count > buffer.Count ||
                sequence.MinLevel > joe.Comp.Level ||
                !EndsWith(buffer, sequence, target))
            {
                continue;
            }

            if (sequence.RequiresGrab && !_combat.IsGrabbedBy(joe, target))
                continue;

            // FLOW-06: no sequence hard CC on a body still inside its immunity window
            if (sequence.HardCc && _liturgy.IsCcImmune((liturgy.Owner, liturgy.Comp), target))
                continue;

            if (!_effects.Has(sequence.Effect))
                continue;

            if (!Fire(liturgy, joe, sequence, target))
                continue;

            return true;
        }

        return false;
    }

    private bool Fire(Entity<EWJoeLiturgyComponent> liturgy, Entity<EWJoeComponent> joe, EWJoeCqcSequencePrototype sequence, EntityUid target)
    {
        // the effect can raise new inputs (a grab), so the buffer is emptied before it runs
        var buffer = liturgy.Comp.Buffer;
        var saved = buffer.ToList();
        buffer.Clear();

        // ARCH-12: an Exalted Joe's sequence is empowered, under the same CC-immunity rule as the hotbar
        // (FLOW-06). The flag is read before the effect and spent only once it lands, so an effect that raises
        // new stacks cannot spend an Exalted it did not use, and a miss keeps it. An effect with no empowered
        // branch leaves it for the next move.
        var empowered = _effects.UsesEmpowered(sequence.Effect) &&
                        _liturgy.CanConsumeEmpowered(joe, target, sequence.HardCc);

        // RB-14: the cooldown starts before the effect, so an input the effect raises (Seize's grab) cannot chain a
        // second sequence; a sequence that did not land gives it back
        var previousNext = liturgy.Comp.NextSequence;
        liturgy.Comp.NextSequence = _timing.CurTime + liturgy.Comp.SequenceCooldown;

        if (!_effects.TryApply(sequence.Effect, joe, target, sequence, empowered))
        {
            // nothing happened: the steps are kept for another sequence
            liturgy.Comp.NextSequence = previousNext;
            buffer.Clear();
            buffer.AddRange(saved);
            return false;
        }

        // eligibility (the CC-immunity rule) was settled before the effect; the effect's own hard CC has just
        // opened the target's window (RB-13), so checking it again here would refuse the spend
        if (empowered)
            _liturgy.TryConsumeEmpowered(joe, target, ccBonus: false, sequence.ID);

        if (sequence.HardCc)
            _liturgy.RecordHardCc((liturgy.Owner, liturgy.Comp), target);

        var name = Loc.GetString(sequence.Name);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-liturgy-sequence", ("name", name)), joe, PopupType.MediumCaution);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} used the CQC sequence {sequence.ID} on {ToPrettyString(target):target}{(empowered ? " (empowered)" : string.Empty)}");
        return true;
    }

    /// <summary>
    /// The buffer ends in the sequence's steps, every step on <paramref name="target"/> (or on no one), in time,
    /// and every Wide step of a blade sequence made with a blade.
    /// </summary>
    private static bool EndsWith(List<EWJoeCqcBufferEntry> buffer, EWJoeCqcSequencePrototype sequence, EntityUid target)
    {
        var offset = buffer.Count - sequence.Steps.Count;
        for (var i = 0; i < sequence.Steps.Count; i++)
        {
            var entry = buffer[offset + i];
            if (!StepMatches(sequence.Steps[i], entry.Input))
                return false;

            // ARCH-12: fists cannot wide-swing; a blade sequence's Wide step needs a bladed swing
            if (sequence.RequiresBlade && sequence.Steps[i] == EWJoeCqcInput.Wide && !entry.FromBlade)
                return false;

            if (entry.Target is { } stepTarget && stepTarget != target)
                return false;

            if (i > 0 && sequence.MaxGapOverride is { } gap && (entry.Time - buffer[offset + i - 1].Time).TotalSeconds > gap)
                return false;
        }

        return true;
    }

    /// <summary>A Grab step also accepts an aggressive grab.</summary>
    public static bool StepMatches(EWJoeCqcInput step, EWJoeCqcInput input)
    {
        return step == input || step == EWJoeCqcInput.Grab && input == EWJoeCqcInput.AggressiveGrab;
    }
}
