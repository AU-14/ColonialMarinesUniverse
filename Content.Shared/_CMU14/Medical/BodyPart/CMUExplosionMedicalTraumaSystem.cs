using Content.Shared._CMU14.Medical;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.BodyPart;

public sealed class CMUExplosionMedicalTraumaSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedBodyPartHealthSystem _partHealth = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, ExplosionReceivedEvent>(OnExplosionReceived);
    }

    private void OnExplosionReceived(Entity<CMUHumanMedicalComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        foreach (var (partUid, part) in _body.GetBodyChildren(ent.Owner))
        {
            var scale = GetBlastScale(part.PartType);
            if (scale <= 0f)
                continue;

            _partHealth.TryApplyPartDamage(ent.Owner, partUid, args.Damage, scale);
        }
    }

    private static float GetBlastScale(BodyPartType type) => type switch
    {
        BodyPartType.Torso => 0.22f,
        BodyPartType.Head => 0.08f,
        BodyPartType.Arm => 0.12f,
        BodyPartType.Leg => 0.14f,
        _ => 0f,
    };
}
