using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Presentation;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Medical.Presentation;

public sealed partial class HumanMedicalVisualsSystem : EntitySystem
{
    private static readonly ResPath OverlayRsi = new("/Textures/_CMU14/Medical/Overlays/human_medical.rsi");

    private static readonly BodyRegion[] Regions =
    {
        BodyRegion.Head,
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
    };

    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HumanMedicalVisualsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HumanMedicalVisualsComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<HumanMedicalVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(Entity<HumanMedicalVisualsComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<HumanMedicalVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        foreach (var region in Regions)
        {
            RemoveLayer(ent.Owner, sprite, region, MedicalOverlayKind.Prosthetic);
            RemoveLayer(ent.Owner, sprite, region, MedicalOverlayKind.Bandage);
            RemoveLayer(ent.Owner, sprite, region, MedicalOverlayKind.Splint);
            RemoveLayer(ent.Owner, sprite, region, MedicalOverlayKind.Cast);
        }
    }

    private void OnAfterState(Entity<HumanMedicalVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent.Owner, ent.Comp);
    }

    private void OnAppearanceChange(Entity<HumanMedicalVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateVisuals(ent.Owner, ent.Comp, args.Sprite);
    }

    private void UpdateVisuals(
        EntityUid uid,
        HumanMedicalVisualsComponent visuals,
        SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref sprite, false))
            return;

        foreach (var region in Regions)
        {
            var flags = GetFlags(visuals, region);
            SetLayer(uid, sprite, region, MedicalOverlayKind.Prosthetic, flags.HasFlag(HumanMedicalRegionVisualFlags.Prosthetic));
            SetLayer(uid, sprite, region, MedicalOverlayKind.Bandage, flags.HasFlag(HumanMedicalRegionVisualFlags.Bandaged));
            SetLayer(uid, sprite, region, MedicalOverlayKind.Splint, flags.HasFlag(HumanMedicalRegionVisualFlags.Splinted));
            SetLayer(uid, sprite, region, MedicalOverlayKind.Cast, flags.HasFlag(HumanMedicalRegionVisualFlags.Casted));
        }
    }

    private HumanMedicalRegionVisualFlags GetFlags(
        HumanMedicalVisualsComponent visuals,
        BodyRegion region)
    {
        var index = (int) region;
        if (index <= 0 || index >= visuals.RegionFlags.Length)
            return HumanMedicalRegionVisualFlags.None;

        return visuals.RegionFlags[index];
    }

    private void SetLayer(
        EntityUid uid,
        SpriteComponent sprite,
        BodyRegion region,
        MedicalOverlayKind kind,
        bool visible)
    {
        var key = LayerKey(region, kind);
        if (!_sprite.LayerMapTryGet((uid, sprite), key, out var layer, false))
        {
            if (!visible)
                return;

            layer = _sprite.LayerMapReserve((uid, sprite), key);
        }

        if (visible)
        {
            _sprite.LayerSetRsi((uid, sprite), layer, OverlayRsi, StateFor(uid, region, kind));
            _sprite.LayerSetVisible((uid, sprite), layer, true);
            return;
        }

        _sprite.LayerSetVisible((uid, sprite), layer, false);
    }

    private void RemoveLayer(
        EntityUid uid,
        SpriteComponent sprite,
        BodyRegion region,
        MedicalOverlayKind kind)
    {
        var key = LayerKey(region, kind);
        if (_sprite.LayerMapTryGet((uid, sprite), key, out var _, false))
            _sprite.RemoveLayer((uid, sprite), key);
    }

    private static string StateFor(EntityUid uid, BodyRegion region, MedicalOverlayKind kind)
    {
        if (kind == MedicalOverlayKind.Bandage || kind == MedicalOverlayKind.Splint)
            return TreatmentOverlayStateFor(uid, region, kind);

        return $"{KindPrefix(kind)}_{RegionSuffix(region)}";
    }

    private static string TreatmentOverlayStateFor(
        EntityUid uid,
        BodyRegion region,
        MedicalOverlayKind kind)
    {
        var prefix = kind == MedicalOverlayKind.Bandage ? "gauze" : "splint";
        var suffix = CM13TreatmentRegionSuffix(region);
        var variants = CM13TreatmentVariantCount(region, kind);
        if (variants <= 1)
            return $"{prefix}_{suffix}";

        return $"{prefix}_{suffix}_{StableVariant(uid, region, kind, variants)}";
    }

    private static int StableVariant(
        EntityUid uid,
        BodyRegion region,
        MedicalOverlayKind kind,
        int variants)
    {
        if (variants <= 1)
            return 1;

        unchecked
        {
            var hash = (uint) uid.GetHashCode();
            hash = (hash * 397) ^ (uint) region;
            hash = (hash * 397) ^ (uint) kind;
            return (int) (hash % (uint) variants) + 1;
        }
    }

    private static int CM13TreatmentVariantCount(BodyRegion region, MedicalOverlayKind kind)
    {
        return kind switch
        {
            MedicalOverlayKind.Bandage => region switch
            {
                BodyRegion.Head => 4,
                BodyRegion.Chest => 4,
                BodyRegion.Groin => 2,
                _ => 1,
            },
            MedicalOverlayKind.Splint => region switch
            {
                BodyRegion.Head => 4,
                BodyRegion.Chest => 4,
                _ => 1,
            },
            _ => 1,
        };
    }

    private static string CM13TreatmentRegionSuffix(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => "head",
            BodyRegion.Chest => "torso",
            BodyRegion.Groin => "groin",
            BodyRegion.LeftArm => "l_arm",
            BodyRegion.RightArm => "r_arm",
            BodyRegion.LeftHand => "l_hand",
            BodyRegion.RightHand => "r_hand",
            BodyRegion.LeftLeg => "l_leg",
            BodyRegion.RightLeg => "r_leg",
            BodyRegion.LeftFoot => "l_foot",
            BodyRegion.RightFoot => "r_foot",
            _ => "torso",
        };
    }

    private static MedicalOverlayLayer LayerKey(BodyRegion region, MedicalOverlayKind kind)
    {
        return (MedicalOverlayLayer) ((int) kind * Regions.Length + RegionSortIndex(region) + 1);
    }

    private static int RegionSortIndex(BodyRegion region)
    {
        for (var i = 0; i < Regions.Length; i++)
        {
            if (Regions[i] == region)
                return i;
        }

        return 0;
    }

    private static string KindPrefix(MedicalOverlayKind kind)
    {
        return kind switch
        {
            MedicalOverlayKind.Bandage => "bandage",
            MedicalOverlayKind.Splint => "splint",
            MedicalOverlayKind.Cast => "cast",
            MedicalOverlayKind.Prosthetic => "prosthetic",
            _ => "bandage",
        };
    }

    private static string RegionSuffix(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => "head",
            BodyRegion.Chest => "chest",
            BodyRegion.Groin => "groin",
            BodyRegion.LeftArm => "l_arm",
            BodyRegion.RightArm => "r_arm",
            BodyRegion.LeftHand => "l_hand",
            BodyRegion.RightHand => "r_hand",
            BodyRegion.LeftLeg => "l_leg",
            BodyRegion.RightLeg => "r_leg",
            BodyRegion.LeftFoot => "l_foot",
            BodyRegion.RightFoot => "r_foot",
            _ => "chest",
        };
    }

    private enum MedicalOverlayKind : byte
    {
        Bandage,
        Splint,
        Cast,
        Prosthetic,
    }

    private enum MedicalOverlayLayer : byte
    {
        None = 0,
    }
}
