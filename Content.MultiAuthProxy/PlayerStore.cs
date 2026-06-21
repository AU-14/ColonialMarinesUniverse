using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

record PlayerRecord(string UserId, string? UserName, string Server, DateTimeOffset LastSeen);

sealed class PlayerStore
{
    private readonly ConcurrentDictionary<string, PlayerRecord> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _nameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly TimeSpan _ttl;
    private volatile bool _dirty;

    public bool IsDirty => _dirty;

    public PlayerStore(string path, TimeSpan ttl)
    {
        _path = path;
        _ttl = ttl;
    }

    public void Upsert(string userId, string? userName, string server)
    {
        // Evict stale name idx if it changed (usernames can be changed once per 30 days, sigh)
        if (_byId.TryGetValue(userId, out var old) && old.UserName != null && old.UserName != userName)
            _nameToId.TryRemove(old.UserName.ToLowerInvariant(), out _);

        _byId[userId] = new PlayerRecord(userId, userName, server, DateTimeOffset.UtcNow);
        if (userName != null)
            _nameToId[userName.ToLowerInvariant()] = userId;

        _dirty = true;
    }

    public PlayerRecord? GetById(string userId) => _byId.TryGetValue(userId, out var r) && !IsStale(r) ? r : null;

    public PlayerRecord? GetByName(string name) => _nameToId.TryGetValue(name.ToLowerInvariant(), out var id) ? GetById(id) : null;

    public void Invalidate(string userId)
    {
        if (_byId.TryRemove(userId, out var r) && r.UserName != null)
            _nameToId.TryRemove(r.UserName.ToLowerInvariant(), out _);
        _dirty = true;
    }

    public void Load(ILogger logger)
    {
        if (!File.Exists(_path)) return;
        try
        {
            var records = JsonSerializer.Deserialize<PlayerRecord[]>(File.ReadAllText(_path), JsonOpts) ?? [];
            var stale = 0;
            foreach (var r in records)
            {
                if (IsStale(r)) { stale++; continue; }
                _byId[r.UserId] = r;
                if (r.UserName != null)
                    _nameToId[r.UserName.ToLowerInvariant()] = r.UserId;
            }
            logger.LogInformation("player-store loaded {Loaded}/{Total} records from {Path} ({Stale} stale skipped)",
                records.Length - stale, records.Length, _path, stale);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load player-store from {Path}; starting fresh", _path);
        }
    }

    public void Save()
    {
        _dirty = false;
        //var records = _byId.Values.Where(r => !IsStale(r)).ToArray();
        var records = _byId.Values.ToArray(); // retain data
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(records, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }

    private bool IsStale(PlayerRecord r) => DateTimeOffset.UtcNow - r.LastSeen > _ttl;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
