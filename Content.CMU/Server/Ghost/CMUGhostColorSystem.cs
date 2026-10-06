using Content.Shared._RMC14.GhostColor;
using Content.Shared.CMU14.Ghost;
using Content.Shared.Ghost.Components;

namespace Content.Server.CMU14.Ghost;

public sealed class CMUGhostColorSystem : EntitySystem
{
    private const float GhostAlpha = 0x88 / 255f;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUSetGhostColorEvent>(OnSetGhostColor);
    }

    private void OnSetGhostColor(CMUSetGhostColorEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ghost || !HasComp<GhostComponent>(ghost))
            return;

        var comp = EnsureComp<GhostColorComponent>(ghost);
        comp.Color = ev.Color is { } color ? color.WithAlpha(GhostAlpha) : null;
        Dirty(ghost, comp);
    }
}
