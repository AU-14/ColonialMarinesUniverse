// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared._AU14.SavedBuilds;
using Content.Shared.Administration.Logs;
using Content.Shared.Construction;
using Content.Shared.Database;
using Content.Shared.Examine;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._AU14.SavedBuilds;

/// <summary>
/// Stamps <see cref="PlayerBuiltComponent"/> on entities players construct and surfaces the
/// builder's character name on examine. The hidden user id is recorded for the saved-builds
/// whitelist and admin logging only. See <c>ConstructionSystem.Construct</c> /
/// <c>ConstructionSystem.ChangeEntity</c> for the call sites.
/// </summary>
public sealed partial class PlayerBuiltSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>Building overhaul: a light "thunk/ratchet" played at the spot whenever a player finishes a build
    /// step, so construction has some audible feedback. Guarded so a bad path can never crash the build.</summary>
    private static readonly SoundSpecifier BuildSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerBuiltComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ConstructionCompletedEvent>(OnConstructionCompleted);
    }

    private void OnConstructionCompleted(ref ConstructionCompletedEvent args)
        => MarkBuilt(args.Built, args.Builder);

    /// <summary>
    /// Records <paramref name="builder"/> as the player who built <paramref name="target"/>.
    /// No-op when the builder is not a connected player (NPCs, environmental triggers), so an
    /// existing player mark is never clobbered by a non-player interaction.
    /// </summary>
    public void MarkBuilt(EntityUid target, EntityUid? builder)
    {
        if (builder is not { } builderUid)
            return;

        if (!_playerManager.TryGetSessionByEntity(builderUid, out var session))
            return;

        var comp = EnsureComp<PlayerBuiltComponent>(target);
        comp.BuilderName = Name(builderUid);
        comp.BuilderUserId = session.UserId.UserId;
        comp.BuiltAt = _timing.CurTime;
        // Not networked (BuilderUserId is server-only) — examine is pushed server-side, so no Dirty.

        // Audible build feedback at the spot. Guarded: a missing audio path must never crash a build.
        try
        {
            _audio.PlayPvs(BuildSound, target);
        }
        catch (Exception e)
        {
            Log.Warning($"[playerbuilt] build sfx failed: {e.Message}");
        }

        _adminLog.Add(LogType.Construction, LogImpact.Low,
            $"{ToPrettyString(builderUid):player} (user {session.UserId}) built {ToPrettyString(target)}");
    }

    private void OnExamined(Entity<PlayerBuiltComponent> ent, ref ExaminedEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.BuilderName))
            return;

        args.PushMarkup(Loc.GetString("construction-player-built-examine",
            ("name", ent.Comp.BuilderName)));
    }
}
