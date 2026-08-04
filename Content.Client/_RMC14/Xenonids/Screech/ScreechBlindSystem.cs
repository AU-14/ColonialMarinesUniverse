using Content.Shared._RMC14.Xenonids.Screech;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._RMC14.Xenonids.Screech;

public sealed partial class ScreechBlindSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScreechBlindComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ScreechBlindComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<ScreechBlindComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlay.AddOverlay(new ScreechBlindOverlay());
    }

    private void OnRemove(Entity<ScreechBlindComponent> ent, ref ComponentRemove args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlay.RemoveOverlay<ScreechBlindOverlay>();
    }
}
