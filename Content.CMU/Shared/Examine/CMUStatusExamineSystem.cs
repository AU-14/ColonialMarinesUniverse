using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.WallLean;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;

namespace Content.Shared.CMU14.Examine;

/// <summary>
/// Shift-click lines for what someone is visibly doing or going through: leaning on a wall, or being in pain.
/// Mild pain doesn't show.
/// </summary>
public sealed class CMUStatusExamineSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWallLeaningComponent, ExaminedEvent>(OnLeaningExamined);
        SubscribeLocalEvent<PainShockComponent, ExaminedEvent>(OnPainExamined);
    }

    private void OnLeaningExamined(Entity<CMUWallLeaningComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Wall is not { } wall || !Exists(wall))
            return;

        args.PushMarkup(Loc.GetString("cmu-examine-leaning", ("target", ent.Owner), ("wall", wall)), -1);
    }

    private void OnPainExamined(Entity<PainShockComponent> ent, ref ExaminedEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        LocId? text = ent.Comp.Tier switch
        {
            PainTier.Moderate => "cmu-examine-in-pain",
            PainTier.Severe or PainTier.Shock => "cmu-examine-in-excruciating-pain",
            _ => null,
        };

        if (text is { } id)
            args.PushMarkup(Loc.GetString(id, ("target", ent.Owner)), -1);
    }
}
