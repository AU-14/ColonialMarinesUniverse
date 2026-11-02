// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Forge;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Forge;

// Server side of the anvil: the player strikes a workpiece with a hammer to shape it into the chosen recipe; wears the tool and plays the sounds.
public sealed class AnvilSystem : SharedAnvilSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly ToolWearSystem _wear = default!;

    private static readonly SoundSpecifier HammerHitSound = new SoundPathSpecifier("/Audio/CMU14/Production/anvil.ogg");
    // The sparks that fly off the hot iron under the hammer: the sparks of the game, which are gone in half a second.
    private static readonly EntProtoId SparksEffect = "EffectSparks";

    private static readonly SoundSpecifier FinishedSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AnvilSelectRecipeEvent>(OnSelectRecipe);
    }

    private void OnSelectRecipe(AnvilSelectRecipeEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var workpieceUid = GetEntity(msg.Workpiece);
        if (!TryComp<AnvilWorkpieceComponent>(workpieceUid, out var workpiece) || workpiece.Forging)
            return;

        if (!TryComp<ForgeableComponent>(workpieceUid, out var forgeable) ||
            forgeable.SeatedOn is not { } anvil ||
            !HasComp<AnvilComponent>(anvil))
        {
            return;
        }

        if (!_interaction.InRangeUnobstructed(user, Transform(workpieceUid).Coordinates))
            return;

        if (!_proto.TryIndex(msg.Recipe, out var recipe) || !RecipeFits(recipe, workpieceUid))
            return;

        workpiece.Forging = true;
        workpiece.Recipe = msg.Recipe;
        workpiece.StepIndex = 0;
        Dirty(workpieceUid, workpiece);

        _popup.PopupEntity(
            Loc.GetString("au14-anvil-forging-started", ("name", Loc.GetString(recipe.Name))),
            workpieceUid,
            user);
    }

    protected override void StrikeWorkpiece(Entity<AnvilWorkpieceComponent> workpiece, EntityUid user, EntityUid tool)
    {
        if (workpiece.Comp.Recipe is not { } recipeId || !_proto.TryIndex(recipeId, out var recipe))
            return;

        if (!TryComp<ForgeableComponent>(workpiece.Owner, out var forgeable) ||
            forgeable.SeatedOn is not { } seat || !HasComp<AnvilComponent>(seat))
        {
            _popup.PopupEntity(Loc.GetString("au14-anvil-not-seated"), workpiece, user, PopupType.SmallCaution);
            return;
        }

        if (forgeable.Temperature < workpiece.Comp.MinWorkTemperature)
        {
            _popup.PopupEntity(Loc.GetString("au14-anvil-too-cold"), workpiece, user, PopupType.SmallCaution);
            return;
        }

        var requiredQuality = recipe.Steps[workpiece.Comp.StepIndex];
        if (!_tool.HasQuality(tool, requiredQuality))
        {
            var toolName = _proto.TryIndex(requiredQuality, out var qualityProto) ? Loc.GetString(qualityProto.ToolName) : requiredQuality.Id;
            _popup.PopupEntity(Loc.GetString("au14-anvil-wrong-tool", ("tool", toolName)), workpiece, user, PopupType.SmallCaution);
            return;
        }

        forgeable.Temperature = Math.Max(forgeable.AmbientTemperature, forgeable.Temperature - workpiece.Comp.HeatLossPerHit);
        Dirty(workpiece.Owner, forgeable);

        workpiece.Comp.StepIndex++;
        Dirty(workpiece);

        _audio.PlayPvs(HammerHitSound, workpiece);
        Spawn(SparksEffect, Transform(workpiece).Coordinates);
        _wear.Use(tool, user);

        var remaining = recipe.Steps.Count - workpiece.Comp.StepIndex;
        if (remaining > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("au14-anvil-hit", ("remaining", remaining)),
                workpiece,
                user);
            return;
        }

        FinishForging(workpiece, user);
    }

    private void FinishForging(Entity<AnvilWorkpieceComponent> workpiece, EntityUid user)
    {
        var recipeId = workpiece.Comp.Recipe;
        var coords = Transform(workpiece).Coordinates;

        Del(workpiece.Owner);

        if (recipeId == null || !_proto.TryIndex(recipeId.Value, out var recipe))
            return;

        for (var i = 0; i < recipe.OutputCount; i++)
            Spawn(recipe.Output, coords);

        _audio.PlayPvs(FinishedSound, coords);
        _popup.PopupCoordinates(Loc.GetString("au14-anvil-finished", ("name", Loc.GetString(recipe.Name))), coords, user, PopupType.Medium);
    }
}
