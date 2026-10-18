using Content.Shared.CMU14.Crayon;
using Content.Shared.Crayon;
using Robust.Client.Player;

namespace Content.Client.Crayon;

public sealed partial class CrayonSystem
{
    /// <summary>
    /// What a held crayon's status says it draws: the player's custom symbol while they have one picked,
    /// since every crayon stamps it then, else the crayon's own decal.
    /// </summary>
    private static string CMUState(Entity<CrayonComponent> crayon)
    {
        // The status control has no system of its own to ask.
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        var stencil = IoCManager.Resolve<IEntityManager>().System<CMUCrayonStencilSystem>();
        return player is { } user && stencil.TryGetStencil(user, out _)
            ? Robust.Shared.Localization.Loc.GetString("cmu-crayon-status-custom")
            : crayon.Comp.SelectedState;
    }
}
