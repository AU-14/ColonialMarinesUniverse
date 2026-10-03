using System.Linq;
using System.Text;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;

namespace Content.Client.CMU14.Diagnostics.Performance;

public sealed partial class CMUClientPerformanceSystem
{
    private void AppendAudioInventory(StringBuilder text)
    {
        // The general component inventory can truncate before it reaches any audio entities.
        // Sample audio independently, on inventory reports only, without querying the audio backend.
        var listenerMap = EntityManager.System<AudioSystem>().GetListenerCoordinates().MapId;
        var files = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;
        var loaded = 0;
        var loops = 0;
        var playingState = 0;
        var pausedEntities = 0;
        var global = 0;
        var otherMap = 0;
        var noOcclusion = 0;
        var truncated = false;
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var transform))
        {
            if (total >= MaxInventoryEntities)
            {
                truncated = true;
                break;
            }

            total++;
            if (audio.Loaded)
                loaded++;
            if (audio.Params.Loop)
                loops++;
            if (audio.State == AudioState.Playing)
                playingState++;
            if (Paused(uid))
                pausedEntities++;
            if (audio.Global)
                global++;
            if (transform.MapID != listenerMap)
                otherMap++;
            if ((audio.Flags & AudioFlags.NoOcclusion) != 0)
                noOcclusion++;
            Increment(files, audio.FileName);
        }

        text.AppendLine($"audio-inventory: entities={total} loaded={loaded} loops={loops} playingState={playingState} pausedEntities={pausedEntities} global={global} otherMap={otherMap} noOcclusion={noOcclusion} listenerMap={listenerMap} truncated={truncated}; report-time snapshot, categories overlap, playback state does not imply audibility");
        var rows = files.OrderByDescending(row => row.Value).ThenBy(row => row.Key, StringComparer.Ordinal).Take(TopRows).ToArray();
        text.AppendLine($"audio-files: shown={rows.Length} omitted={files.Count - rows.Length}; counts include stopped, paused and unloaded sources");
        foreach (var (file, count) in rows)
            text.AppendLine($"  {file} count={count}");
    }
}
