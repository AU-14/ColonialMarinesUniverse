using Content.Shared.CMU14.Pointing;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Pointing;
using Content.Shared.Mobs.Components;
using Content.Shared.Pointing;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Pointing;

/// <summary>
///     Leaders pointing at a mob outline it for their squad, thicker the better their leadership.
/// </summary>
public sealed class CMUPointOutlineSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> Leadership = "RMCSkillLeadership";
    private static readonly Color NoSquadColor = Color.FromHex("#282d8f");
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(4.5);

    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, AfterPointedAtEvent>(OnPointedAt);
    }

    private void OnPointedAt(Entity<SkillsComponent> ent, ref AfterPointedAtEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Pointed) || !TryComp(ent, out ActorComponent? _))
            return;

        // Same rule as the big pointing arrow: squad leaders, or anyone with leadership training.
        var leadership = _skills.GetSkill((ent, ent.Comp), Leadership);
        if (!HasComp<RMCPointingComponent>(ent) && leadership < 1)
            return;

        var color = NoSquadColor;
        var filter = Filter.Empty();
        if (TryComp(ent, out SquadMemberComponent? member) && member.Squad is { } squad)
        {
            color = member.BackgroundColor;
            var squadmates = EntityQueryEnumerator<SquadMemberComponent, ActorComponent>();
            while (squadmates.MoveNext(out _, out var mate, out var actor))
            {
                if (mate.Squad == squad)
                    filter.AddPlayer(actor.PlayerSession);
            }
        }
        else if (TryComp(ent, out MarineComponent? marine) && !string.IsNullOrWhiteSpace(marine.Faction))
        {
            var marines = EntityQueryEnumerator<MarineComponent, ActorComponent>();
            while (marines.MoveNext(out _, out var other, out var actor))
            {
                if (string.Equals(other.Faction, marine.Faction, StringComparison.OrdinalIgnoreCase))
                    filter.AddPlayer(actor.PlayerSession);
            }
        }
        else
        {
            return;
        }

        // cmss13 uses 0.25px plus 0.25 per leadership level; our outline shader needs at least a pixel to show.
        var width = 1f + leadership * 0.25f;
        RaiseNetworkEvent(new CMUPointOutlineEvent(GetNetEntity(args.Pointed), color, width, Duration), filter);
    }
}
