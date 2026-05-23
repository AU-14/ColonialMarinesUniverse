using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Engineering;

[Serializable, NetSerializable]
public sealed partial class SpawnAfterInteractDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Token;

    private SpawnAfterInteractDoAfterEvent()
    {
    }

    public SpawnAfterInteractDoAfterEvent(int token)
    {
        Token = token;
    }

    public override DoAfterEvent Clone() => new SpawnAfterInteractDoAfterEvent(Token);
}
