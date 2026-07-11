using System.Numerics;
using Content.Client._RMC14.Medical.HUD;
using Content.Client._RMC14.NightVision;
using Content.Shared._RMC14.Mobs;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Energy;
using Content.Shared._RMC14.Xenonids.Dancer;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Stacks;
using Content.Shared._RMC14.Xenonids.Rank;
using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared._RMC14.Xenonids.Finesse;
using static Robust.Shared.Utility.SpriteSpecifier;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Synth;
using Content.Shared.FixedPoint;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationMimicTransformedComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicTransformedComponent;

namespace Content.Client._RMC14.Xenonids.Hud;

public sealed partial class XenoHudOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ResPath RsiPath = new("/Textures/_RMC14/Interface/xeno_hud.rsi");
    private static readonly ResPath DancerMarksRsiPath = new("/Textures/_CM13/Interface/Hud/dancer_marks.rsi");
    private static readonly ResPath RsiPathSlow = new("/Textures/_RMC14/Effects/xeno_stomp.rsi");
    private static readonly ResPath RsiPathFreeze = new("/Textures/_RMC14/Effects/xeno_freeze.rsi");
    private static readonly ResPath RsiPathHypertension = new("/Textures/_RMC14/Interface/Alerts/hypertension.rsi");

    private static readonly Rsi[] AcidStackIcons =
    [
        new(RsiPath, "acid_stacks0"),
        new(RsiPath, "acid_stacks1"),
        new(RsiPath, "acid_stacks2"),
        new(RsiPath, "acid_stacks3"),
        new(RsiPath, "acid_stacks4"),
    ];

    private static readonly Rsi[] RankIcons =
    [
        new(RsiPath, "hudxenoupgrade0"),
        new(RsiPath, "hudxenoupgrade1"),
        new(RsiPath, "hudxenoupgrade2"),
        new(RsiPath, "hudxenoupgrade3"),
        new(RsiPath, "hudxenoupgrade4"),
        new(RsiPath, "hudxenoupgrade5"),
        new(RsiPath, "hudxenoupgrade7"),
        new(RsiPath, "hudxenoupgrade8"),
    ];

    private static readonly Rsi DancerMarkedIcon = new(DancerMarksRsiPath, "prae_tag");
    private static readonly Rsi DancerYellowMarkedIcon = new(DancerMarksRsiPath, "prae_tag_yellow");
    private static readonly Rsi SlowIcon = new(RsiPathSlow, "stomp");
    private static readonly Rsi StunIcon = new(RsiPathFreeze, "freeze");
    private static readonly Rsi SynthIcon = new(RsiPath, "fake_tall");

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly ContainerSystem _container;
    private readonly CMHealthIconsSystem _healthIcons;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _mobThresholds;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private readonly EntityQuery<DamageableComponent> _damageableQuery;
    private readonly EntityQuery<XenoParasiteComponent> _xenoParasiteQuery;
    private readonly EntityQuery<MobStateComponent> _mobStateQuery;
    private readonly EntityQuery<MobThresholdsComponent> _mobThresholdsQuery;
    private readonly EntityQuery<XenoEnergyComponent> _xenoEnergyQuery;
    private readonly EntityQuery<XenoMaturingComponent> _xenoMaturingQuery;
    private readonly EntityQuery<XenoPlasmaComponent> _xenoPlasmaQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<XenoShieldComponent> _xenoShieldQuery;
    private readonly EntityQuery<EntityActiveInvisibleComponent> _invisQuery;
    private readonly EntityQuery<XenoComponent> _xenoQuery;
    private readonly EntityQuery<XenoDespoilerHypertensionComponent> _hyperQuery;

    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => _overlay.HasOverlay<NightVisionOverlay>()
        ? OverlaySpace.WorldSpace
        : OverlaySpace.WorldSpaceBelowFOV;

    public XenoHudOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _healthIcons = _entity.System<CMHealthIconsSystem>();
        _mobState = _entity.System<MobStateSystem>();
        _mobThresholds = _entity.System<MobThresholdSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();

        _damageableQuery = _entity.GetEntityQuery<DamageableComponent>();
        _xenoParasiteQuery = _entity.GetEntityQuery<XenoParasiteComponent>();
        _mobStateQuery = _entity.GetEntityQuery<MobStateComponent>();
        _mobThresholdsQuery = _entity.GetEntityQuery<MobThresholdsComponent>();
        _xenoEnergyQuery = _entity.GetEntityQuery<XenoEnergyComponent>();
        _xenoMaturingQuery = _entity.GetEntityQuery<XenoMaturingComponent>();
        _xenoPlasmaQuery = _entity.GetEntityQuery<XenoPlasmaComponent>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _xenoShieldQuery = _entity.GetEntityQuery<XenoShieldComponent>();
        _invisQuery = _entity.GetEntityQuery<EntityActiveInvisibleComponent>();
        _xenoQuery = _entity.GetEntityQuery<XenoComponent>();
        _hyperQuery = _entity.GetEntityQuery<XenoDespoilerHypertensionComponent>();

        _shader = _prototype.Index(UnshadedShader).Instance();
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var isAdminGhost = _entity.TryGetComponent(_players.LocalEntity, out GhostComponent? ghost) &&
                           ghost.CanGhostInteract;
        var isXeno = _entity.HasComponent<XenoComponent>(_players.LocalEntity);
        var isAbomination = _entity.HasComponent<AbominationComponent>(_players.LocalEntity) ||
                             _entity.HasComponent<AbominationMimicTransformedComponent>(_players.LocalEntity);
        var isGhost = false;

        if (!_entity.HasComponent<CMGhostXenoHudComponent>(_players.LocalEntity))
        {
            if (!isXeno && !isAdminGhost && !isAbomination)
                return;
        }
        else
        {
            if (_entity.HasComponent<CMGhostXenoHudComponent>(_players.LocalEntity))
                isGhost = true;
            isXeno = true;
        }
        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        if (isXeno)
        {
            DrawBars(in args, scaleMatrix, rotationMatrix);
            if (!isGhost)
                DrawDeadIcon(in args, scaleMatrix, rotationMatrix);

            DrawAcidStacks(in args, scaleMatrix, rotationMatrix);
            DrawMarkedIcons(in args, scaleMatrix, rotationMatrix);
            DrawYellowMarkedIcons(in args, scaleMatrix, rotationMatrix);
            DrawRank(in args, scaleMatrix, rotationMatrix);

            DrawSlow(in args, scaleMatrix, rotationMatrix);
            DrawStun(in args, scaleMatrix, rotationMatrix);
        }

        if (isXeno || isAdminGhost)
            DrawInfectedIcon(in args, scaleMatrix, rotationMatrix);

        if (isXeno || isAdminGhost || isAbomination)
            DrawSynthIcon(in args, scaleMatrix, rotationMatrix);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawBars(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var xenos = _entity.AllEntityQueryEnumerator<XenoComponent, SpriteComponent, TransformComponent>();
        while (xenos.MoveNext(out var uid, out var xeno, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            UpdateHealth((uid, xeno, sprite, mobState), handle);
            UpdatePlasma((uid, xeno, sprite), handle);
            UpdateShields((uid, xeno, sprite), handle);
            UpdateEnergy((uid, xeno, sprite), handle);
            UpdateHypertension((uid, xeno, sprite), handle);
        }
    }

    private void DrawDeadIcon(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var icon = _healthIcons.GetDeadIcon().Icon;
        var handle = args.WorldHandle;
        var infected = _entity.AllEntityQueryEnumerator<MobStateComponent, SpriteComponent, TransformComponent>();
        while (infected.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (comp.CurrentState != MobState.Dead)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_xenoParasiteQuery.HasComp(uid))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawAcidStacks(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var stacks = _entity
            .AllEntityQueryEnumerator<VictimXenoAcidStacksComponent, SpriteComponent, TransformComponent>();
        while (stacks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var level = Math.Clamp(comp.Current, 0, 4);
            var icon = AcidStackIcons[level];
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawRank(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var ranks = _entity.EntityQueryEnumerator<XenoRankComponent, SpriteComponent, TransformComponent>();
        while (ranks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (comp.Rank < 2 || _xenoMaturingQuery.HasComp(uid))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var rankIndex = Math.Clamp(comp.Rank, 0, 7);
            var icon = RankIcons[rankIndex];
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawMarkedIcons(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var stacks = _entity
            .AllEntityQueryEnumerator<XenoMarkedComponent, SpriteComponent, TransformComponent>();

        while (stacks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(DancerMarkedIcon, _timing.CurTime - comp.TimeAdded, false);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawYellowMarkedIcons(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var stacks = _entity
            .AllEntityQueryEnumerator<XenoYellowMarkedComponent, SpriteComponent, TransformComponent>();

        while (stacks.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(DancerYellowMarkedIcon, _timing.CurTime, false);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawSlow(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var slows = _entity
            .AllEntityQueryEnumerator<XenoSlowVisualsComponent, SpriteComponent, TransformComponent>();

        while (slows.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            if (_xenoQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(SlowIcon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawStun(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var slows = _entity
            .AllEntityQueryEnumerator<XenoImmobileVisualsComponent, SpriteComponent, TransformComponent>();

        while (slows.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            if (_xenoQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(StunIcon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawInfectedIcon(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var infected = _entity.AllEntityQueryEnumerator<VictimInfectedComponent, SpriteComponent, TransformComponent>();
        while (infected.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var level = Math.Min(comp.CurrentStage, comp.InfectedIcons.Length - 1);
            var icon = comp.InfectedIcons[level];
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawSynthIcon(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var synth = _entity.AllEntityQueryEnumerator<SynthComponent, SpriteComponent, TransformComponent>();
        while (synth.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (!ShouldDrawSynthIcon(comp))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(SynthIcon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    public static bool ShouldDrawSynthIcon(SynthComponent synth)
    {
        return !synth.HideXenoSynthIcon;
    }

    private void UpdateHealth(Entity<XenoComponent, SpriteComponent, MobStateComponent?> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite, mobState) = ent;
        if (!_damageableQuery.TryComp(uid, out var damageable))
            return;

        var damage = damageable.TotalDamage;

        FixedPoint2? critThresholdNullable = null;
        FixedPoint2? deadThresholdNullable = null;
        if (_mobThresholdsQuery.TryComp(uid, out var mobThresholds))
        {
            _mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out critThresholdNullable, mobThresholds);
            _mobThresholds.TryGetDeadThreshold(uid, out deadThresholdNullable, mobThresholds);
        }

        string state;
        if (_mobState.IsCritical(uid, mobState) ||
            _mobState.IsAlive(uid) &&
            critThresholdNullable != null &&
            damageable.TotalDamage > critThresholdNullable)
        {
            if (critThresholdNullable is not { } critThreshold || deadThresholdNullable is not { } deadThreshold)
                return;

            deadThreshold -= critThreshold;
            damage -= critThreshold;
            var level = ContentHelpers.RoundToLevels(damage.Double(), deadThreshold.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "1";
            state = $"xenohealth-{name}";
        }
        else
        {
            critThresholdNullable ??= deadThresholdNullable;
            if (critThresholdNullable == null)
                return;

            var level = ContentHelpers.RoundToLevels((critThresholdNullable - damage).Value.Double(), critThresholdNullable.Value.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "0";
            state = $"xenohealth{name}";
        }

        var icon = new Rsi(RsiPath, state);
        var rsi = _resourceCache.GetResource<RSIResource>(icon.RsiPath).RSI;
        if (!rsi.TryGetState(icon.RsiState, out _))
            return;

        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }

    private void UpdatePlasma(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;
        if (!_xenoPlasmaQuery.TryComp(uid, out var comp) ||
            comp.MaxPlasma == 0)
        {
            return;
        }

        var plasma = comp.Plasma;
        var max = comp.MaxPlasma;
        var level = ContentHelpers.RoundToLevels(plasma.Double(), max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"plasma{name}";
        var icon = new Rsi(RsiPath, state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }

    private void UpdateHypertension(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;
        if (!_hyperQuery.TryComp(uid, out var hyper))
            return;

        var level = Math.Clamp(hyper.Stacks, 0, hyper.MaxStacks);
        var icon = new Rsi(RsiPathHypertension, $"level_{level}");
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X + (float) texture.Width / EyeManager.PixelsPerMeter;

        handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
    }

    private void UpdateShields(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;

        FixedPoint2 shieldAmount = 0;

        // Check for regular xeno shield
        if (!_xenoShieldQuery.TryComp(uid, out var xenoShield))
            return;

        FixedPoint2? critThresholdNullable = null;
        FixedPoint2? deadThresholdNullable = null;
        if (_mobThresholdsQuery.TryComp(uid, out var mobThresholds))
        {
            _mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out critThresholdNullable, mobThresholds);
            _mobThresholds.TryGetDeadThreshold(uid, out deadThresholdNullable, mobThresholds);
        }

        critThresholdNullable ??= deadThresholdNullable;
        if (critThresholdNullable == null)
            return;

        var shield = xenoShield.ShieldAmount;
        var max = critThresholdNullable.Value.Double();
        var level = ContentHelpers.RoundToLevels(shield.Double(), max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"xenoshield{name}";
        var icon = new Rsi(RsiPath, state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }

    private void UpdateEnergy(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        if (!_xenoEnergyQuery.TryComp(ent, out var comp) ||
            comp.Max == 0)
        {
            return;
        }

        UpdatePurpleBar(ent, handle, comp.Current, comp.Max, comp.GenerationCap);
    }

    private void UpdatePurpleBar(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle, double energy, double max, int? generationCap)
    {
        var (uid, xeno, sprite) = ent;
        var level = ContentHelpers.RoundToLevels(energy, max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"xenoenergy{name}";
        var icon = new Rsi(RsiPath, state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);

        if (generationCap != null && energy >= generationCap)
        {
            var level2 = ContentHelpers.RoundToLevels(generationCap.Value, max, 11);
            var name2 = level2 > 0 ? $"{level2 * 10}" : "0";
            var state2 = $"cap{name2}";
            var icon2 = new Rsi(RsiPath, state2);
            var texture2 = _sprite.GetFrame(icon2, _timing.CurTime);
            handle.DrawTexture(texture2, position);
        }
    }
}
