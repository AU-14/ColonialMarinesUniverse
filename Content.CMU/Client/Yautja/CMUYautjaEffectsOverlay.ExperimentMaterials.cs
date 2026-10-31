using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Yautja;

public partial class CMUYautjaEffectsOverlay
{
    private static readonly ProtoId<ShaderPrototype> ExperimentRibbon = "CMUYautjaExperimentRibbon";
    private static readonly ProtoId<ShaderPrototype> ExperimentField = "CMUYautjaExperimentField";
    private static readonly ProtoId<ShaderPrototype> ExperimentProjection = "CMUYautjaExperimentProjection";
    private static readonly ProtoId<ShaderPrototype> ExperimentRefraction = "CMUYautjaExperimentRefraction";
    private ShaderInstance _experimentRibbon = default!;
    private ShaderInstance _experimentField = default!;
    private ShaderInstance _experimentProjection = default!;
    private ShaderInstance _experimentRefraction = default!;
    private CMUYautjaExperimentPrototype _experimentRecipe = default!;
    private float _experimentTime;

    private void InitializeExperimentMaterials()
    {
        if (_behind)
            return;
        _experimentRibbon = _prototypes.Index(ExperimentRibbon).InstanceUnique();
        _experimentField = _prototypes.Index(ExperimentField).InstanceUnique();
        _experimentProjection = _prototypes.Index(ExperimentProjection).InstanceUnique();
        _experimentRefraction = _prototypes.Index(ExperimentRefraction).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        RequestScreenTexture = false;
        if (_behind)
            return true;
        // Only an on-screen, live experiment needs a copy of the world for its local lens/heat field.
        var query = _entities.EntityQueryEnumerator<CMUYautjaEffectComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var effect, out var xform))
        {
            if (effect.Kind == CMUYautjaEffect.Experiment && xform.MapID == args.MapId &&
                effect.EndsAt > _timing.CurTime && args.WorldAABB.Enlarged(4).Contains(_transform.GetWorldPosition(uid)))
            {
                RequestScreenTexture = true;
                break;
            }
        }
        return true;
    }

    private void DrawExperimentMedium(DrawingHandleWorld h, Vector2 at, Vector2 rise, float p, float fade)
    {
        var recipe = _experimentRecipe;
        var charge = CMUYautjaPresentation.Segment(p, 0, CMUYautjaExperimentPrototype.ReleaseAt);
        var field = recipe.Visual switch
        {
            CMUYautjaExperimentVisual.Solvent or CMUYautjaExperimentVisual.Coolant => 1,
            CMUYautjaExperimentVisual.Veil or CMUYautjaExperimentVisual.Prism or CMUYautjaExperimentVisual.Decoy => 2,
            CMUYautjaExperimentVisual.Orbit or CMUYautjaExperimentVisual.Containment or CMUYautjaExperimentVisual.Catalyst => 3,
            CMUYautjaExperimentVisual.Scan or CMUYautjaExperimentVisual.Glyphs or CMUYautjaExperimentVisual.Echo or
                CMUYautjaExperimentVisual.Radar or CMUYautjaExperimentVisual.Pulse or CMUYautjaExperimentVisual.Root => 4,
            _ => 0,
        };
        var size = new Vector2(66 + charge * 28, field == 1 ? 90 : 76 + charge * 16);
        if (ScreenTexture != null)
        {
            _experimentRefraction.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _experimentRefraction.SetParameter("phase", _experimentTime);
            _experimentRefraction.SetParameter("opacity", fade * charge * (field == 2 ? .65f : .22f));
            _experimentRefraction.SetParameter("strength", field == 2 ? 4.5f : 2f);
            _experimentRefraction.SetParameter("optical", field == 2 ? 1f : 0f);
            try
            {
                h.UseShader(_experimentRefraction);
                h.DrawRect(Box2.CenteredAround(rise, size / 32), Color.White);
            }
            finally { h.UseShader(null); }
        }
        DrawExperimentField(h, rise, size, field, recipe.Color.WithAlpha(fade * charge * .85f));
        // Three grounded spill plumes make the projection visibly originate in the recovered wrist computer.
        if (p >= CMUYautjaExperimentPrototype.ReleaseAt && recipe.Material is
            CMUYautjaExperimentMaterial.Plasma or CMUYautjaExperimentMaterial.Discharge or CMUYautjaExperimentMaterial.Cryogenic)
        {
            for (var i = -1; i <= 1; i++)
                DrawExperimentField(h, at + new Vector2(i * 17, 5) / 32,
                    new Vector2(28, 34), 1, recipe.Color.WithAlpha(fade * .4f));
        }
    }

    private void ExperimentTrace(DrawingHandleWorld h, Vector2 from, Vector2 to, Color color)
    {
        if (color.A <= .001f)
            return;
        var delta = to - from;
        var length = delta.Length();
        if (length < .005f)
            return;
        _experimentRibbon.SetParameter("phase", _experimentTime);
        _experimentRibbon.SetParameter("material", (float) _experimentRecipe.Material);
        _experimentRibbon.SetParameter("tint", color);
        _experimentRibbon.SetParameter("core", _experimentRecipe.CoreColor);
        _experimentRibbon.SetParameter("length_px", length * 32);
        // Local lengths, not world coordinates, keep the carrier signal coherent when the camera moves.
        _experimentRibbon.SetParameter("seed", length * 13.7f);
        try
        {
            h.SetTransform(Matrix3x2.CreateRotation(MathF.Atan2(delta.Y, delta.X)) *
                           Matrix3x2.CreateTranslation((from + to) * .5f));
            h.UseShader(_experimentRibbon);
            h.DrawRect(Box2.CenteredAround(Vector2.Zero, new Vector2(length, 6f / 32)), Color.White);
        }
        finally
        {
            h.SetTransform(Matrix3x2.Identity);
            h.UseShader(null);
        }
    }

    private void DrawExperimentField(DrawingHandleWorld h, Vector2 at, Vector2 pixels, float shape, Color color)
    {
        if (color.A <= .001f)
            return;
        pixels = Vector2.Max(pixels, Vector2.One);
        _experimentField.SetParameter("phase", _experimentTime);
        _experimentField.SetParameter("material", (float) _experimentRecipe.Material);
        _experimentField.SetParameter("shape", shape);
        _experimentField.SetParameter("tint", color);
        _experimentField.SetParameter("core", _experimentRecipe.CoreColor);
        _experimentField.SetParameter("extent", pixels);
        try
        {
            h.UseShader(_experimentField);
            h.DrawRect(Box2.CenteredAround(at, pixels / 32), Color.White);
        }
        finally { h.UseShader(null); }
    }

    private void ExperimentMote(DrawingHandleWorld h, Vector2 at, Vector2 size, Color color) =>
        DrawExperimentField(h, at, Vector2.Max(size * 64, new Vector2(4)), 0, color);

    private void DrawExperimentProjection(DrawingHandleWorld h, Texture texture, Vector2 at, float scale, float angle, Color color)
    {
        _experimentProjection.SetParameter("phase", _experimentTime);
        _experimentProjection.SetParameter("tint", color);
        _experimentProjection.SetParameter("core", _experimentRecipe.CoreColor);
        _experimentProjection.SetParameter("opacity", color.A);
        _experimentProjection.SetParameter("optical", _experimentRecipe.Visual == CMUYautjaExperimentVisual.Veil ? 1f : 0f);
        try
        {
            h.UseShader(_experimentProjection);
            DrawArt(h, texture, at, scale, angle, Color.White);
        }
        finally { h.UseShader(null); }
    }

    private void DisposeExperimentMaterials()
    {
        _experimentRibbon?.Dispose();
        _experimentField?.Dispose();
        _experimentProjection?.Dispose();
        _experimentRefraction?.Dispose();
    }
}
