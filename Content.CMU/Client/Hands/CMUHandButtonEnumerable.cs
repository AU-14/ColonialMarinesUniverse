using System.Collections;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Hands;

/// <summary>
/// Enumerates hands in their current display order without allocating an iterator for each HUD frame.
/// </summary>
public readonly struct CMUHandButtonEnumerable(Control.OrderedChildCollection children) : IEnumerable<HandButton>
{
    public Enumerator GetEnumerator() => new(children);

    IEnumerator<HandButton> IEnumerable<HandButton>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<HandButton>
    {
        private Control.OrderedChildCollection.Enumerator _children;
        public HandButton Current { get; private set; }
        object IEnumerator.Current => Current;

        internal Enumerator(Control.OrderedChildCollection children)
        {
            _children = children.GetEnumerator();
            Current = default!;
        }

        public bool MoveNext()
        {
            while (_children.MoveNext())
            {
                if (_children.Current is not HandButton hand)
                    continue;

                Current = hand;
                return true;
            }

            return false;
        }

        public void Dispose() => _children.Dispose();
        public void Reset() => throw new NotSupportedException();
    }
}
