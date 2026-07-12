using System.Collections.Frozen;
using System.Collections.Generic;
using Content.Client.Damage;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Presentation.Visuals;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Medical.Presentation.Visuals;

public sealed partial class CMUMedicalOverlayVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private const int TreatmentOverlayKindCount = 2;
    private const int VariantCount = 4;

    private static readonly Color BruteDamageColor = Color.FromHex("#FF0000");
    private static readonly ResPath BruteDamageOverlays = new("Mobs/Effects/brute_damage.rsi");
    private static readonly ResPath BurnDamageOverlays = new("Mobs/Effects/burn_damage.rsi");
    private static readonly ResPath TreatmentOverlays = new("_CMU14/Mobs/Medical/treatment_overlays.rsi");

    private static readonly DamageOverlayKind[] DamageOverlayOrder =
    [
        DamageOverlayKind.Brute,
        DamageOverlayKind.Burn,
    ];

    private static readonly TreatmentOverlayKind[] TreatmentOverlayOrder =
    [
        TreatmentOverlayKind.Bandage,
        TreatmentOverlayKind.Splint,
    ];

    private static readonly HumanoidVisualLayers[] DamageOverlayLayers =
    [
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
    ];

    private static readonly HumanoidVisualLayers[] TreatmentOverlayLayers =
    [
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    ];

    private static readonly string[][] HeadStates =
    [
        ["gauze_head_1", "gauze_head_2", "gauze_head_3", "gauze_head_4"],
        ["splint_head_1", "splint_head_2", "splint_head_3", "splint_head_4"],
    ];

    private static readonly string[][] TorsoStates =
    [
        ["gauze_torso_1", "gauze_torso_2", "gauze_torso_3", "gauze_torso_4"],
        ["splint_torso_1", "splint_torso_2", "splint_torso_3", "splint_torso_4"],
    ];

    private static readonly string[] LArmStates = ["gauze_l_arm", "splint_l_arm"];
    private static readonly string[] RArmStates = ["gauze_r_arm", "splint_r_arm"];
    private static readonly string[] LHandStates = ["gauze_l_hand", "splint_l_hand"];
    private static readonly string[] RHandStates = ["gauze_r_hand", "splint_r_hand"];
    private static readonly string[] LLegStates = ["gauze_l_leg", "splint_l_leg"];
    private static readonly string[] RLegStates = ["gauze_r_leg", "splint_r_leg"];
    private static readonly string[] LFootStates = ["gauze_l_foot", "splint_l_foot"];
    private static readonly string[] RFootStates = ["gauze_r_foot", "splint_r_foot"];

    private static readonly FrozenDictionary<DamageOverlayMapKey, string> DamageMapKeys = CreateDamageMapKeys();
    private static readonly FrozenDictionary<TreatmentOverlayMapKey, string> TreatmentMapKeys = CreateTreatmentMapKeys();
    private static readonly string[] AllOverlayMapKeys = CreateAllOverlayMapKeys();

    private readonly List<DesiredOverlay> _desiredOverlays = new(AllOverlayMapKeys.Length);
    private readonly HashSet<string> _desiredOverlayKeys = new(AllOverlayMapKeys.Length, StringComparer.Ordinal);
    private readonly HashSet<EntityUid> _queuedBodies = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, ComponentStartup>(OnMedicalBodyStartup);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentStartup>(OnHumanoidChanged);
        SubscribeLocalEvent<HumanoidAppearanceComponent, BodyPartAddedEvent>(OnHumanoidChanged);
        SubscribeLocalEvent<HumanoidAppearanceComponent, BodyPartRemovedEvent>(OnHumanoidChanged);

        SubscribeLocalEvent<CMUMedicalOverlayVisualsComponent, ComponentStartup>(OnMedicalVisualsChanged);
        SubscribeLocalEvent<CMUMedicalOverlayVisualsComponent, ComponentRemove>(OnMedicalVisualsChanged);
        SubscribeLocalEvent<CMUMedicalOverlayVisualsComponent, AfterAutoHandleStateEvent>(OnMedicalVisualsState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_queuedBodies.Count == 0)
            return;

        foreach (var body in _queuedBodies)
        {
            UpdateBody(body);
        }

        _queuedBodies.Clear();
    }

    private void OnMedicalBodyStartup(Entity<CMUHumanMedicalComponent> ent, ref ComponentStartup args)
    {
        QueueBody(ent.Owner);
    }

    private void OnHumanoidChanged<TEvent>(Entity<HumanoidAppearanceComponent> ent, ref TEvent args)
    {
        QueueBody(ent.Owner);
    }

    private void QueueBody(EntityUid body)
    {
        if (TerminatingOrDeleted(body))
            return;

        _queuedBodies.Add(body);
    }

    private void UpdateBody(EntityUid body)
    {
        if (!TryComp<SpriteComponent>(body, out var sprite))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(body))
            return;

        Entity<SpriteComponent> bodySprite = (body, sprite);
        DisableAggregateDamageVisuals(bodySprite);
        _desiredOverlays.Clear();
        _desiredOverlayKeys.Clear();

        if (TryComp<CMUMedicalOverlayVisualsComponent>(body, out var medicalVisuals))
        {
            foreach (var part in medicalVisuals.Parts)
            {
                if (!_sprite.LayerMapTryGet(bodySprite.AsNullable(), part.Layer, out _, false))
                    continue;

                AddDamageOverlays(part);
                AddTreatmentOverlays(part);
            }
        }

        RemoveStaleOverlayLayers(bodySprite);
        ApplyDesiredOverlays(bodySprite);
    }

    private void AddDamageOverlays(CMUMedicalOverlayPartVisual part)
    {
        foreach (var kind in DamageOverlayOrder)
        {
            var level = kind switch
            {
                DamageOverlayKind.Brute => part.BruteDamageLevel,
                DamageOverlayKind.Burn => part.BurnDamageLevel,
                _ => (byte) 0,
            };

            if (!TryGetDamageOverlayState(part.Layer, kind, level, out var rsi, out var state, out var color))
                continue;

            AddDesiredOverlay(DamageOverlayKey(part.Layer, kind), rsi, state, color);
        }
    }

    private void AddTreatmentOverlays(CMUMedicalOverlayPartVisual part)
    {
        foreach (var kind in TreatmentOverlayOrder)
        {
            var hasOverlay = kind switch
            {
                TreatmentOverlayKind.Bandage => part.Bandaged,
                TreatmentOverlayKind.Splint => part.Splinted,
                _ => false,
            };

            if (!hasOverlay)
                continue;

            if (!TryGetTreatmentOverlayState(part.VariantSeed, part.Layer, kind, out var state))
                continue;

            AddDesiredOverlay(TreatmentOverlayKey(part.Layer, kind), TreatmentOverlays, state);
        }
    }

    private void DisableAggregateDamageVisuals(Entity<SpriteComponent> body)
    {
        if (!TryComp<DamageVisualsComponent>(body.Owner, out var damageVisuals))
            return;

        damageVisuals.Disabled = true;

        if (damageVisuals.DamageOverlayGroups is null)
            return;

        if (damageVisuals.TargetLayers is null)
        {
            foreach (var group in damageVisuals.DamageOverlayGroups.Keys)
            {
                if (_sprite.LayerMapTryGet(body.AsNullable(), $"DamageOverlay{group}", out var index, false))
                    _sprite.LayerSetVisible(body.AsNullable(), index, false);
            }

            return;
        }

        foreach (var layer in damageVisuals.TargetLayerMapKeys)
        {
            foreach (var group in damageVisuals.DamageOverlayGroups.Keys)
            {
                if (_sprite.LayerMapTryGet(body.AsNullable(), $"{layer}{group}", out var index, false))
                    _sprite.LayerSetVisible(body.AsNullable(), index, false);
            }
        }
    }

    private void AddDesiredOverlay(string mapKey, ResPath rsi, string state, Color? color = null)
    {
        if (!_desiredOverlayKeys.Add(mapKey))
            return;

        _desiredOverlays.Add(new DesiredOverlay(mapKey, rsi, state, color));
    }

    private void RemoveStaleOverlayLayers(Entity<SpriteComponent> body)
    {
        var bodySprite = body.AsNullable();
        foreach (var mapKey in AllOverlayMapKeys)
        {
            if (!_desiredOverlayKeys.Contains(mapKey))
                _sprite.RemoveLayer(bodySprite, mapKey, false);
        }
    }

    private void ApplyDesiredOverlays(Entity<SpriteComponent> body)
    {
        var bodySprite = body.AsNullable();
        var insertIndex = GetOverlayStartIndex(body);
        // Desired overlays are built in deterministic render order. Stale layers are
        // removed first, so new layers can be inserted without rebuilding unchanged ones.
        foreach (var overlay in _desiredOverlays)
        {
            var applied = true;
            if (_sprite.LayerMapTryGet(bodySprite, overlay.MapKey, out var layerIndex, false))
            {
                if (insertIndex is { } expectedIndex && layerIndex != expectedIndex)
                {
                    _sprite.RemoveLayer(bodySprite, overlay.MapKey, false);
                    applied = AddOverlay(body, expectedIndex, overlay);
                }
                else
                {
                    _sprite.LayerSetRsiState(bodySprite, layerIndex, overlay.State);
                    if (overlay.Color is { } layerColor)
                        _sprite.LayerSetColor(bodySprite, layerIndex, layerColor);
                }
            }
            else
            {
                applied = AddOverlay(body, insertIndex, overlay);
            }

            if (insertIndex is not null && applied)
                insertIndex++;
        }
    }

    private int? GetOverlayStartIndex(Entity<SpriteComponent> body)
    {
        var bodySprite = body.AsNullable();
        int? startIndex = null;
        foreach (var overlay in _desiredOverlays)
        {
            if (!_sprite.LayerMapTryGet(bodySprite, overlay.MapKey, out var layerIndex, false))
                continue;

            if (startIndex is null || layerIndex < startIndex)
                startIndex = layerIndex;
        }

        if (startIndex is not null)
            return startIndex;

        return _sprite.LayerMapTryGet(bodySprite, HumanoidVisualLayers.Handcuffs, out var handcuffsIndex, false)
            ? handcuffsIndex
            : null;
    }

    private bool AddOverlay(Entity<SpriteComponent> body, int? index, DesiredOverlay overlay)
    {
        var bodySprite = body.AsNullable();
        var layerIndex = _sprite.AddLayer(
            body.AsNullable(),
            new SpriteSpecifier.Rsi(overlay.Rsi, overlay.State),
            index);

        if (layerIndex < 0)
            return false;

        _sprite.LayerMapSet(bodySprite, overlay.MapKey, layerIndex);
        if (overlay.Color is { } layerColor)
            _sprite.LayerSetColor(bodySprite, layerIndex, layerColor);

        return true;
    }

    private void OnMedicalVisualsChanged<TEvent>(Entity<CMUMedicalOverlayVisualsComponent> ent, ref TEvent args)
    {
        QueueBody(ent.Owner);
    }

    private void OnMedicalVisualsState(Entity<CMUMedicalOverlayVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        QueueBody(ent.Owner);
    }

    private static bool TryGetDamageOverlayState(
        HumanoidVisualLayers layer,
        DamageOverlayKind kind,
        byte damageLevel,
        out ResPath rsi,
        out string state,
        out Color? color)
    {
        rsi = kind == DamageOverlayKind.Brute ? BruteDamageOverlays : BurnDamageOverlays;
        color = kind == DamageOverlayKind.Brute ? BruteDamageColor : null;

        if (damageLevel == 0 || !IsDamageOverlayLayer(layer))
        {
            state = string.Empty;
            return false;
        }

        state = $"{layer}_{kind}_{damageLevel}";
        return true;
    }

    private static bool IsDamageOverlayLayer(HumanoidVisualLayers layer)
    {
        return layer is HumanoidVisualLayers.Chest
            or HumanoidVisualLayers.Head
            or HumanoidVisualLayers.LArm
            or HumanoidVisualLayers.RArm
            or HumanoidVisualLayers.LLeg
            or HumanoidVisualLayers.RLeg;
    }

    private static bool TryGetTreatmentOverlayState(
        int variantSeed,
        HumanoidVisualLayers layer,
        TreatmentOverlayKind kind,
        out string state)
    {
        var kindIndex = KindIndex(kind);
        switch (layer)
        {
            case HumanoidVisualLayers.Chest:
                state = TorsoStates[kindIndex][PickVariantIndex(variantSeed, VariantCount, kind, layer)];
                return true;
            case HumanoidVisualLayers.Head:
                state = HeadStates[kindIndex][PickVariantIndex(variantSeed, VariantCount, kind, layer)];
                return true;
            case HumanoidVisualLayers.LArm:
                state = LArmStates[kindIndex];
                return true;
            case HumanoidVisualLayers.RArm:
                state = RArmStates[kindIndex];
                return true;
            case HumanoidVisualLayers.LHand:
                state = LHandStates[kindIndex];
                return true;
            case HumanoidVisualLayers.RHand:
                state = RHandStates[kindIndex];
                return true;
            case HumanoidVisualLayers.LLeg:
                state = LLegStates[kindIndex];
                return true;
            case HumanoidVisualLayers.RLeg:
                state = RLegStates[kindIndex];
                return true;
            case HumanoidVisualLayers.LFoot:
                state = LFootStates[kindIndex];
                return true;
            case HumanoidVisualLayers.RFoot:
                state = RFootStates[kindIndex];
                return true;
            default:
                state = string.Empty;
                return false;
        }
    }

    private static int KindIndex(TreatmentOverlayKind kind)
    {
        if ((uint) kind >= TreatmentOverlayKindCount)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

        return (int) kind;
    }

    private static int PickVariantIndex(
        int variantSeed,
        int count,
        TreatmentOverlayKind kind,
        HumanoidVisualLayers layer)
    {
        var hash = (uint) HashCode.Combine(variantSeed, kind, layer);
        return (int) (hash % count);
    }

    private static string DamageOverlayKey(HumanoidVisualLayers layer, DamageOverlayKind kind)
    {
        return DamageMapKeys[new DamageOverlayMapKey(layer, kind)];
    }

    private static string TreatmentOverlayKey(HumanoidVisualLayers layer, TreatmentOverlayKind kind)
    {
        return TreatmentMapKeys[new TreatmentOverlayMapKey(layer, kind)];
    }

    private static FrozenDictionary<DamageOverlayMapKey, string> CreateDamageMapKeys()
    {
        var keys = new Dictionary<DamageOverlayMapKey, string>(
            DamageOverlayLayers.Length * DamageOverlayOrder.Length);
        foreach (var layer in DamageOverlayLayers)
        {
            foreach (var kind in DamageOverlayOrder)
            {
                keys.Add(
                    new DamageOverlayMapKey(layer, kind),
                    $"cmu-medical-damage-{kind}-{layer}");
            }
        }

        return keys.ToFrozenDictionary();
    }

    private static FrozenDictionary<TreatmentOverlayMapKey, string> CreateTreatmentMapKeys()
    {
        var keys = new Dictionary<TreatmentOverlayMapKey, string>(
            TreatmentOverlayLayers.Length * TreatmentOverlayOrder.Length);
        foreach (var layer in TreatmentOverlayLayers)
        {
            foreach (var kind in TreatmentOverlayOrder)
            {
                keys.Add(
                    new TreatmentOverlayMapKey(layer, kind),
                    $"cmu-medical-treatment-{kind}-{layer}");
            }
        }

        return keys.ToFrozenDictionary();
    }

    private static string[] CreateAllOverlayMapKeys()
    {
        var keys = new string[DamageMapKeys.Count + TreatmentMapKeys.Count];
        var index = 0;
        foreach (var layer in DamageOverlayLayers)
        {
            foreach (var kind in DamageOverlayOrder)
            {
                keys[index++] = DamageOverlayKey(layer, kind);
            }
        }

        foreach (var layer in TreatmentOverlayLayers)
        {
            foreach (var kind in TreatmentOverlayOrder)
            {
                keys[index++] = TreatmentOverlayKey(layer, kind);
            }
        }

        return keys;
    }

    private readonly record struct DesiredOverlay(string MapKey, ResPath Rsi, string State, Color? Color);

    private readonly record struct DamageOverlayMapKey(HumanoidVisualLayers Layer, DamageOverlayKind Kind);

    private readonly record struct TreatmentOverlayMapKey(HumanoidVisualLayers Layer, TreatmentOverlayKind Kind);

    private enum DamageOverlayKind : byte
    {
        Brute = 0,
        Burn = 1,
    }

    private enum TreatmentOverlayKind : byte
    {
        Bandage = 0,
        Splint = 1,
    }
}
