using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Stealth;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Aura;

public sealed partial class AuraSystem : SharedAuraSystem
{
    private static readonly ProtoId<ShaderPrototype> AuraOutlineShader = "RMCAuraOutline";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AuraComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AuraComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<AuraComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        sprite.PostShader = _prototypes.Index(AuraOutlineShader).InstanceUnique();
    }

    private void OnShutdown(Entity<AuraComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (HasComp<EntityActiveInvisibleComponent>(ent))
            return;

        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        sprite.PostShader = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var auraQuery = EntityQueryEnumerator<AuraComponent, SpriteComponent>();

        while (auraQuery.MoveNext(out var uid, out var aura, out var sprite))
        {
            var color = aura.Color;
            if (aura.Flash && aura.FlashFrequency > 0)
            {
                var pulse = (MathF.Sin((float) _timing.CurTime.TotalSeconds * MathF.Tau * aura.FlashFrequency) + 1) * 0.5f;
                var alpha = aura.FlashMinAlpha + (aura.FlashMaxAlpha - aura.FlashMinAlpha) * pulse;
                color = color.WithAlpha(color.A * alpha);
            }

            sprite.PostShader?.SetParameter("outline_color", color);
            sprite.PostShader?.SetParameter("outline_width", aura.OutlineWidth);
        }
    }
}
