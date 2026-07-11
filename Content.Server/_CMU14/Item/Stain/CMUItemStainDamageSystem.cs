using System;
using Content.Shared._CMU14.Item.Stain;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Item;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Item.Stain;

/// <summary>
/// Applies CMSS13-style item splatter from localized brute damage.
/// </summary>
public sealed partial class CMUItemStainDamageSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly Color OilColor = Color.FromHex("#030303");

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private CMUItemStainSystem _stains = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartDamagedEvent>(OnBodyPartDamaged);
    }

    private void OnBodyPartDamaged(ref BodyPartDamagedEvent args)
    {
        if (!_prototypes.TryIndex(BruteGroup, out var bruteGroup) ||
            !args.Delta.TryGetDamageInGroup(bruteGroup, out var brute) ||
            brute <= 0)
        {
            return;
        }

        CMUItemStainKind kind;
        Color color;
        if (HasComp<CMURoboticLimbComponent>(args.Part))
        {
            kind = CMUItemStainKind.Oil;
            color = OilColor;
        }
        else
        {
            if (!TryComp<BloodstreamComponent>(args.Body, out var bloodstream) ||
                !_reagents.TryIndex(bloodstream.BloodReagent, out var blood))
            {
                return;
            }

            kind = CMUItemStainKind.Blood;
            color = blood.SubstanceColor;
        }

        var stainChance = Math.Clamp((25f + 2f * brute.Float()) / 100f, 0f, 1f);
        if (!_random.Prob(stainChance))
            return;

        if (args.Tool is { } tool && HasComp<ItemComponent>(tool))
            _stains.TryStain(tool, kind, color, args.Body);

        if (!_random.Prob(0.33f))
            return;

        switch (args.Type)
        {
            case BodyPartType.Head:
                _stains.TryStainSlot(args.Body, CMUItemStainSystem.HeadSlot, kind, color, args.Body);
                _stains.TryStainSlot(args.Body, CMUItemStainSystem.MaskSlot, kind, color, args.Body);
                break;
            case BodyPartType.Torso:
            case BodyPartType.Arm:
            case BodyPartType.Leg:
                _stains.TryStainOuterBody(args.Body, kind, color, args.Body);
                break;
            case BodyPartType.Hand:
                _stains.TryStainSlot(args.Body, CMUItemStainSystem.GlovesSlot, kind, color, args.Body);
                break;
            case BodyPartType.Foot:
                _stains.TryStainSlot(args.Body, CMUItemStainSystem.ShoesSlot, kind, color, args.Body);
                break;
        }
    }
}
