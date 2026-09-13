using System.Linq;
using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.Humanoid.Markings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Enums;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Humanoid.Markings;

public sealed partial class RMCConditionalMarkingsSystem : EntitySystem
{
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCConditionalMarkingsComponent, MapInitEvent>(OnMapInit,
            after: new[] { typeof(RandomHumanoidAppearanceSystem), typeof(RandomHumanoidSystem) }
        );
    }

    private void OnMapInit(Entity<RMCConditionalMarkingsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(ent.Owner, out var humanoid))
            return;

        var listToUse = humanoid.Gender switch
        {
            Gender.Female => ent.Comp.Markings[Sex.Female],
            Gender.Male => ent.Comp.Markings[Sex.Male],
            Gender.Epicene or Gender.Neuter or _ => ent.Comp.Markings[Sex.Male]
        };

        listToUse = humanoid.Sex switch
        {
            Sex.Female => ent.Comp.Markings[Sex.Female],
            Sex.Male => ent.Comp.Markings[Sex.Male],
            _ => listToUse // Sexless mobs use gender
        };

        var pickedMarking = _random.Pick(listToUse);
        if (!ProtoMan.TryIndex<MarkingPrototype>(pickedMarking, out var prototype) ||
            !_humanoidAppearance.TryGetMarkings(
                ent,
                prototype.BodyPart,
                out var organ,
                out _,
                out var markings))
        {
            return;
        }

        if (markings.Count == 0)
        {
            _humanoidAppearance.SetMarkings(ent, organ, prototype.BodyPart, [prototype.AsMarking()]);
            return;
        }

        var updated = markings.ToList();
        for (var idx = 0; idx < updated.Count; idx++) // Replace existing markings
        {
            var replacement = prototype.AsMarking() with { Forced = updated[idx].Forced };
            for (var color = 0; color < replacement.MarkingColors.Count && color < updated[idx].MarkingColors.Count; color++)
            {
                replacement = replacement.WithColorAt(color, updated[idx].MarkingColors[color]);
            }

            updated[idx] = replacement;
        }

        _humanoidAppearance.SetMarkings(ent, organ, prototype.BodyPart, updated);
    }
}
