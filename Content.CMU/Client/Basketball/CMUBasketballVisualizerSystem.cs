using Content.Shared.CMU14.Basketball;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Basketball;

public sealed partial class CMUBasketballVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBasketballScoreboardComponent, ComponentStartup>(OnScoreboardStartup);
        SubscribeLocalEvent<CMUBasketballScoreboardComponent, AfterAutoHandleStateEvent>(OnScoreboardState);

        SubscribeLocalEvent<CMUBasketballResetComponent, ComponentStartup>(OnResetStartup);
        SubscribeLocalEvent<CMUBasketballResetComponent, AfterAutoHandleStateEvent>(OnResetState);
    }

    private void OnScoreboardStartup(Entity<CMUBasketballScoreboardComponent> ent, ref ComponentStartup args)
    {
        UpdateScoreboard(ent);
    }

    private void OnScoreboardState(Entity<CMUBasketballScoreboardComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateScoreboard(ent);
    }

    private void OnResetStartup(Entity<CMUBasketballResetComponent> ent, ref ComponentStartup args)
    {
        UpdateReset(ent);
    }

    private void OnResetState(Entity<CMUBasketballResetComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateReset(ent);
    }

    private void UpdateScoreboard(Entity<CMUBasketballScoreboardComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        Entity<SpriteComponent?> spriteEnt = (ent, sprite);
        SetDigit(spriteEnt, CMUBasketballScoreboardLayers.LeftTens, ent.Comp.LeftScore / 10, 'a');
        SetDigit(spriteEnt, CMUBasketballScoreboardLayers.LeftOnes, ent.Comp.LeftScore % 10, 'b');
        SetDigit(spriteEnt, CMUBasketballScoreboardLayers.RightTens, ent.Comp.RightScore / 10, 'c');
        SetDigit(spriteEnt, CMUBasketballScoreboardLayers.RightOnes, ent.Comp.RightScore % 10, 'd');
    }

    private void SetDigit(Entity<SpriteComponent?> sprite, CMUBasketballScoreboardLayers layerKey, int digit, char position)
    {
        if (_sprite.LayerMapTryGet(sprite, layerKey, out var layer, true))
            _sprite.LayerSetRsiState(sprite, layer, GetDigitState(digit, position));
    }

    private void UpdateReset(Entity<CMUBasketballResetComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((ent, sprite), CMUBasketballResetLayers.Base, out var layer, true))
        {
            return;
        }

        _sprite.LayerSetRsiState((ent, sprite), layer, ent.Comp.Pressed ? "launcheract" : "launcherbtt");
    }

    public static string GetDigitState(int digit, char position)
    {
        return $"s{Math.Clamp(digit, 0, 9)}{position}";
    }
}
