using System.Numerics;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>Presentation-only motion from authorized samples. Stale predictions settle at the last known tile.</summary>
public sealed class CMUReconContactMotion
{
    private readonly Dictionary<int, Sample> _samples = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _removed = new();
    private readonly record struct Sample(Vector2 Position, Vector2 From, Vector2 Velocity, int Depth, TimeSpan Time);

    public void Update(CMUReconContact[] contacts, TimeSpan now)
    {
        _seen.Clear();
        foreach (var contact in contacts)
        {
            if (!contact.Live || contact.Id == 0) continue;
            _seen.Add(contact.Id);
            Vector2 position = contact.Blip.Indices;
            var from = position;
            var velocity = Vector2.Zero;
            if (_samples.TryGetValue(contact.Id, out var old) && old.Depth == contact.Depth)
            {
                var elapsed = (float) (now - old.Time).TotalSeconds;
                var delta = position - old.Position;
                if (elapsed is > 0 and <= 2 && delta.LengthSquared() <= 144)
                {
                    from = At(old, now);
                    velocity = delta / elapsed;
                    if (velocity.LengthSquared() > 36) velocity = Vector2.Normalize(velocity) * 6;
                }
            }
            _samples[contact.Id] = new Sample(position, from, velocity, contact.Depth, now);
        }
        _removed.Clear();
        foreach (var id in _samples.Keys)
            if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) _samples.Remove(id);
    }

    public Vector2 Position(CMUReconContact contact, TimeSpan now) => contact.Live && contact.Id != 0 &&
        _samples.TryGetValue(contact.Id, out var sample) ? At(sample, now) : contact.Blip.Indices;

    private static Vector2 At(Sample sample, TimeSpan now)
    {
        var age = Math.Max(0, (float) (now - sample.Time).TotalSeconds);
        // At most 250 ms ahead, then return to authoritative intel if the feed falls silent.
        var ahead = Math.Min(age, 0.25f) * Math.Clamp((0.75f - age) / 0.5f, 0, 1);
        return Vector2.Lerp(sample.From, sample.Position + sample.Velocity * ahead, Math.Min(age / 0.15f, 1));
    }
}
