using System.Buffers;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.CMU14.Chemistry;

/// <summary>
/// A shallow reagent snapshot with its own pooled buffer. Each lease must be disposed;
/// nested metabolism calls must not share or overwrite an active snapshot.
/// Do not copy the lease or retain its span after disposal.
/// </summary>
public ref struct CMUReagentSnapshot
{
    private ReagentQuantity[]? _buffer;
    private readonly int _count;

    public CMUReagentSnapshot(List<ReagentQuantity> reagents)
    {
        _count = reagents.Count;
        _buffer = ArrayPool<ReagentQuantity>.Shared.Rent(_count);
        reagents.CopyTo(_buffer);
    }

    /// <summary>The captured entries, excluding unused capacity in the rented buffer.</summary>
    public readonly Span<ReagentQuantity> Span => _buffer.AsSpan(0, _count);

    public void Dispose()
    {
        if (_buffer is not { } buffer)
            return;

        _buffer = null;
        // Reagent IDs can retain custom data; do not keep those references in the pool.
        ArrayPool<ReagentQuantity>.Shared.Return(buffer, clearArray: true);
    }
}
