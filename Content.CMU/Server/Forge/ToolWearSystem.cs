// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Forge;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Forge;

// Tools of rough stone give out after a few uses: the forge and the anvil call Use whenever such a tool does its work.
public sealed class ToolWearSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier BreakSound = new SoundCollectionSpecifier("MetalBreak");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreaksAfterUsesComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>One use of the tool. When the last one is gone it breaks (and lets go of what it holds).</summary>
    public void Use(EntityUid tool, EntityUid user)
    {
        if (!TryComp<BreaksAfterUsesComponent>(tool, out var wear) || TerminatingOrDeleted(tool))
            return;

        wear.UsesLeft--;
        if (wear.UsesLeft > 0)
            return;

        _audio.PlayPvs(BreakSound, tool);
        _popup.PopupEntity(Loc.GetString("au14-tool-wear-broke", ("name", Name(tool))), tool, user, PopupType.MediumCaution);

        // Destroying it, not just deleting it, so that a pair of tongs lets its piece go.
        if (!_destructible.DestroyEntity(tool))
            QueueDel(tool);
    }

    private void OnExamined(Entity<BreaksAfterUsesComponent> tool, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("au14-tool-wear-examine", ("count", tool.Comp.UsesLeft)));
    }
}
