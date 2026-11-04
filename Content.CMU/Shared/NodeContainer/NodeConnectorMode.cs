namespace Content.Shared.NodeContainer;

// How a cable node joins others: with the cables next to it (Adjacent), only with the ones run by hand (Direct), or both.
public enum NodeConnectorMode : byte
{
    Adjacent = 0,

    Direct = 1,

    // Both the cables under it and the ones run by hand: the node of a generator or of a machine takes either.
    Both = 2,
}
