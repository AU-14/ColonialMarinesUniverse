using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

// cmss13 /obj/item/stack/yautja_rope: hang a dead human from the ceiling
[RegisterComponent, NetworkedComponent]
[Access(typeof(YautjaRopeSystem))]
public sealed partial class YautjaRopeComponent : Component
{
    // cmss13 is 1s grab + 4s tying it to the ceiling, then 1s + 3s hoisting the body
    [DataField]
    public TimeSpan SecureDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan HangDelay = TimeSpan.FromSeconds(4);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(YautjaRopeSystem))]
public sealed partial class YautjaHungComponent : Component
{
    // how far the body sways each way, in tiles. cmss13 rolls 3 to 6 pixels
    [DataField, AutoNetworkedField]
    public float Swing = 0.15f;
}
