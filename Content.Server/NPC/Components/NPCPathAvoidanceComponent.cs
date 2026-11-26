using System.Numerics;

namespace Content.Server.NPC.Components;

[RegisterComponent]
public sealed partial class NPCPathAvoidanceComponent : Component
{
    public NPCPathAvoidanceArea[] Areas = [];
}

public readonly record struct NPCPathAvoidanceArea(EntityUid Grid, Vector2 Center, float Radius);
