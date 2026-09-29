using System.Linq;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Shared.Player;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private sealed class Preload
    {
        public Survey? Survey;
        public TimeSpan NextRequest;
        public TimeSpan Expires;
    }

    private readonly Dictionary<ICommonSession, Preload> _preloads = new();
    private int _preloadCursor;

    private void OnPreload(CMUReconPreloadRequest message, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (message.Cancel)
        {
            if (_preloads.TryGetValue(session, out var pending) && pending.Survey?.RequestId == message.RequestId)
                pending.Survey = null;
            return;
        }
        if (message.RequestId <= 0 || session.AttachedEntity is not { } actor || !CanUse(actor, actor) ||
            !TryComp<TacticalMapUserComponent>(actor, out var user) || _ui.IsUiOpen(actor, TacticalMapUserUi.Key, actor)) return;
        if (!_preloads.TryGetValue(session, out var preload)) _preloads[session] = preload = new Preload();
        if (_timing.CurTime < preload.NextRequest) return;
        preload.NextRequest = _timing.CurTime + TimeSpan.FromSeconds(10);
        preload.Survey = null;
        EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().PrepareReconstructionMap((actor, user));
        if (!TryCreateSurvey(actor, new CMUReconViewMessage(Vector2i.Zero)
            { Actor = actor, RequestId = message.RequestId, PreferPlanetOnShip = message.PreferPlanetOnShip }, out var survey)) return;
        preload.Survey = survey;
        preload.Expires = _timing.CurTime + TimeSpan.FromMinutes(2);
        var snapshot = Snapshot(survey, actor, false, false);
        snapshot.Orders = [];
        RaiseNetworkEvent(new CMUReconPreloadState(message.RequestId, snapshot, null), session);
    }

    private void UpdatePreloads()
    {
        // A global background budget, below visible windows' transfers. Round-robin avoids starvation.
        var sessions = _preloads.Keys.ToArray();
        var bytes = 16 * 1024;
        var chunks = 8;
        for (var i = 0; i < sessions.Length; i++)
        {
            _preloadCursor %= sessions.Length;
            var session = sessions[_preloadCursor++];
            var pending = _preloads[session];
            if (!session.Channel.IsConnected) { _preloads.Remove(session); continue; }
            if (pending.Survey is not { } survey) continue;
            var actor = survey.Actor;
            if (session.AttachedEntity != actor || !CanUse(actor, actor) || !IsCurrentSurvey(actor, survey) ||
                _ui.IsUiOpen(actor, TacticalMapUserUi.Key, actor) || _timing.CurTime >= pending.Expires)
            {
                pending.Survey = null;
                continue;
            }
            var patch = GeometryPatch(survey, chunks, bytes);
            if (patch.Chunks.Length > 0 || patch.Surfaces.Length > 0)
                RaiseNetworkEvent(new CMUReconPreloadState(survey.RequestId, null, patch), session);
            if (survey.Loaded == survey.Atlas.Revisions.Length) pending.Survey = null;
            chunks -= patch.Chunks.Length;
            bytes -= patch.Chunks.Sum(CMUReconChunkEncoding.EstimatedBytes);
            if (chunks <= 0 || bytes <= 0) break;
        }
    }

    private static CMUReconPatchMessage GeometryPatch(Survey survey, int maxChunks, int maxBytes)
    {
        var atlas = survey.Atlas;
        var chunks = new List<CMUReconChunk>();
        var bytes = 0;
        for (var scanned = 0; survey.ScannedVersion != atlas.Version && scanned < atlas.Revisions.Length && chunks.Count < maxChunks; scanned++)
        {
            survey.Cursor %= atlas.Revisions.Length;
            var id = survey.ChunkOrder[survey.Cursor++];
            if (survey.Sent[id] == atlas.Revisions[id]) continue;
            var chunk = CopyChunk(atlas, id);
            var cost = CMUReconChunkEncoding.EstimatedBytes(chunk);
            if (bytes + cost > maxBytes) { survey.Cursor--; break; }
            chunks.Add(chunk);
            bytes += cost;
            if (survey.Sent[id] == 0) survey.Loaded++;
            survey.Sent[id] = atlas.Revisions[id];
        }
        // A budget stop is not proof that the atlas has been fully scanned.
        if (chunks.Count == 0 && maxBytes >= 44 * 1024) survey.ScannedVersion = atlas.Version;
        var palette = atlas.Surfaces.Skip(survey.SurfaceCount).ToArray();
        survey.SurfaceCount = atlas.Surfaces.Count;
        return new CMUReconPatchMessage(survey.Generation, chunks.ToArray(), [], false)
        {
            OrdersChanged = false, Surfaces = palette,
            LoadedChunks = survey.Loaded, TotalChunks = atlas.Revisions.Length,
        };
    }
}
