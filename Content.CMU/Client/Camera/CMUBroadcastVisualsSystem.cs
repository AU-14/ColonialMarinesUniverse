using Content.Client.Items.Systems;
using Content.Shared.CMU14.Camera;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Camera;

/// <summary>
/// Updates the broadcasting camera's indicator between green and red.
/// </summary>
public sealed partial class CMUBroadcastVisualsSystem : VisualizerSystem<CMUBroadcastVisualsComponent>
{
    private const string OfflineInhandRsi = "_CMU14/Objects/Devices/broadcasting_camera_off.rsi";

    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBroadcastVisualsComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: [typeof(ItemSystem)]);
    }

    protected override void OnAppearanceChange(
        EntityUid uid,
        CMUBroadcastVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
        {
            SpriteSystem.LayerSetRsiState(
                (uid, args.Sprite),
                "broadcast",
                IsBroadcasting(uid) ? "icon" : "icon-off");
        }

        _item.VisualsChanged(uid);
    }

    private void OnGetInhandVisuals(Entity<CMUBroadcastVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (IsBroadcasting(ent) || args.Layers.Count == 0)
            return;

        // Reuse the item system's selected left/right and wielded state in the red-light RSI.
        var (key, layer) = args.Layers[0];
        args.Layers[0] = (key, new PrototypeLayerData { RsiPath = OfflineInhandRsi, State = layer.State });
    }

    private bool IsBroadcasting(EntityUid camera)
        => !AppearanceSystem.TryGetData<bool>(camera, CMUBroadcastVisuals.Broadcasting, out var broadcasting) || broadcasting;
}
