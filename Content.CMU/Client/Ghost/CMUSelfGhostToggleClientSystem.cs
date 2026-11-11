using Content.Shared.CMU14.Ghost;
using Content.Shared.Popups;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client.CMU14.Ghost;

public sealed class CMUSelfGhostToggleClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSelfGhostToggleComponent, CMUToggleSelfGhostActionEvent>(OnToggle);
    }

    private void OnToggle(Entity<CMUSelfGhostToggleComponent> ent, ref CMUToggleSelfGhostActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Owner != _player.LocalEntity || !TryComp(ent, out SpriteComponent? sprite))
            return;

        var visible = !sprite.Visible;
        _sprite.SetVisible((ent, sprite), visible);
        var loc = visible ? "cmu-ghost-self-visibility-on" : "cmu-ghost-self-visibility-off";
        _popup.PopupEntity(Loc.GetString(loc), ent, ent);
    }
}
