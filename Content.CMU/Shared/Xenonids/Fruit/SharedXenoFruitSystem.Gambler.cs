using Content.Shared._RMC14.Xenonids.Fruit.Components;

namespace Content.Shared._RMC14.Xenonids.Fruit;

//yeah
public sealed partial class SharedXenoFruitSystem
{
    public void MakeHarvested(Entity<XenoFruitComponent> fruit)
    {
        SetFruitState(fruit, XenoFruitState.Item);
    }
}
