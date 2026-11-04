using Robust.Client.GameObjects;

namespace Content.Client.Clickable;

public sealed partial class ClickableSystem
{
    /// <summary>
    /// Use the public collection's indexed view without boxing its list enumerator.
    /// Keep an owning fallback enumerator if the engine supplies a different collection.
    /// </summary>
    private struct CMUSpriteLayers : IDisposable
    {
        private readonly IReadOnlyList<ISpriteLayer>? _layers;
        private readonly IEnumerator<ISpriteLayer>? _fallback;
        private int _index;

        public CMUSpriteLayers(IEnumerable<ISpriteLayer> layers)
        {
            _layers = layers as IReadOnlyList<ISpriteLayer>;
            _fallback = _layers == null ? layers.GetEnumerator() : null;
            _index = -1;
        }

        public CMUSpriteLayers GetEnumerator() => this;
        public ISpriteLayer Current => _layers != null ? _layers[_index] : _fallback!.Current;
        public bool MoveNext() => _layers != null ? ++_index < _layers.Count : _fallback!.MoveNext();
        public void Dispose() => _fallback?.Dispose();
    }
}
