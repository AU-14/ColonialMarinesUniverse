using System.Numerics;
using Content.Shared.Body;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    // CMU14: CM-SS13 stores the Yautja body as six PRE-COLOURED variants per part (tan_head_m, green_head_m, ...), each
    // with its own marking colours. Multiplying a single greyscaled base by the skin colour could never reproduce that -
    // it produced washed-out grey/silver hunters. The variants are ported verbatim as "<part>_<colour>" states and the
    // layer state is switched to match the profile's skin colour, with no greyscale shader and no colour multiply.
    private static readonly (YautjaSkinColor Colour, string Suffix)[] YautjaSkinVariants =
    [
        (YautjaSkinColor.Tan, "tan"),
        (YautjaSkinColor.Green, "green"),
        (YautjaSkinColor.Purple, "purple"),
        (YautjaSkinColor.Blue, "blue"),
        (YautjaSkinColor.Red, "red"),
        (YautjaSkinColor.Black, "black"),
    ];

    // Only the yautja body sheet uses these part state names; human part sheets use their own, so this keeps the
    // variant switch off any other mob that happens to run with the Yautja species prototype.
    private static readonly HashSet<string> YautjaBodyParts =
    [
        "full",
        "head_m", "head_f", "torso_m", "torso_f",
        "l_arm", "r_arm", "l_hand", "r_hand",
        "l_leg", "r_leg", "l_foot", "r_foot",
    ];

    private static readonly ProtoId<SpeciesPrototype> YautjaSpecies = "Yautja";

    private bool IsYautjaBodyRsi(string? organRsi, EntityUid body)
    {
        if (YautjaBodyVariants.IsBodyRsi(organRsi))
            return true;

        return TryComp<SpriteComponent>(body, out var sprite) &&
               YautjaBodyVariants.IsBodyRsi(sprite.BaseRSI?.Path.ToString());
    }

    /// <summary>
    /// Whether this layer should carry the Yautja skin treatment: the body is a Yautja and the layer draws the Yautja
    /// body sheet. Both halves matter - the sheet alone is not enough, because a preview dummy keeps its organs when it
    /// is reused for another species, and the tint must not leak onto that body.
    /// </summary>
    private bool IsYautjaSkinLayer(EntityUid body, string? organSheet)
    {
        if (!TryComp<HumanoidProfileComponent>(body, out var humanoid) || humanoid.Species != YautjaSpecies)
            return false;

        return IsYautjaBodyRsi(organSheet, body);
    }

    private static string YautjaSkinSuffix(Color skin)
    {
        var best = "tan";
        var bestDistance = double.MaxValue;

        foreach (var (colour, suffix) in YautjaSkinVariants)
        {
            var candidate = YautjaCharacterProfile.GetSkinColorColor(colour);
            var dr = candidate.R - skin.R;
            var dg = candidate.G - skin.G;
            var db = candidate.B - skin.B;
            var distance = dr * dr + dg * dg + db * db;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = suffix;
        }

        return best;
    }

    private static string YautjaStripSuffix(string state)
    {
        foreach (var (_, suffix) in YautjaSkinVariants)
        {
            if (state.EndsWith($"_{suffix}", StringComparison.Ordinal))
                return state[..^(suffix.Length + 1)];
        }

        return state;
    }

    /// <summary>
    /// The typed layer for an index; SpriteComponent's indexer does not expose the RSI state name, AllLayers does.
    /// </summary>
    private static ISpriteLayer? YautjaLayer(SpriteComponent sprite, int index)
    {
        var i = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (i++ == index)
                return layer;
        }

        return null;
    }

    /// <summary>
    /// The pre-coloured state for a Yautja body organ, chosen from the organ's own layer identity and the skin colour in
    /// its profile. Returns null for anything that is not a Yautja body part, so other species and layers are untouched.
    /// The state names come from the CM-SS13 sheet ported into body.rsi ("<part>_<colour>").
    /// </summary>
    private static string? YautjaVariantStateFor(Entity<VisualOrganComponent> organ, Sex sex)
    {
        var suffix = YautjaSkinSuffix(organ.Comp.Profile.SkinColor);
        var male = sex != Sex.Female;
        return organ.Comp.Layer switch
        {
            HumanoidVisualLayers.Head => $"head_{(male ? "m" : "f")}_{suffix}",
            HumanoidVisualLayers.Chest => $"torso_{(male ? "m" : "f")}_{suffix}",
            HumanoidVisualLayers.LArm => $"l_arm_{suffix}",
            HumanoidVisualLayers.RArm => $"r_arm_{suffix}",
            HumanoidVisualLayers.LHand => $"l_hand_{suffix}",
            HumanoidVisualLayers.RHand => $"r_hand_{suffix}",
            HumanoidVisualLayers.LLeg => $"l_leg_{suffix}",
            HumanoidVisualLayers.RLeg => $"r_leg_{suffix}",
            HumanoidVisualLayers.LFoot => $"l_foot_{suffix}",
            HumanoidVisualLayers.RFoot => $"r_foot_{suffix}",
            _ => null,
        };
    }

    /// <summary>
    /// The pre-coloured state for this organ, or null when the organ is not a Yautja body part on the Yautja sheet.
    /// Used by ApplyVisual, which is the path that renders replicated organ visuals.
    /// </summary>
    private static string? YautjaVariantState(Entity<VisualOrganComponent> organ, SpriteComponent sprite, int index)
    {
        if (YautjaLayer(sprite, index) is not { } layer)
            return null;

        var baseState = YautjaStripSuffix(layer.RsiState.Name ?? string.Empty);
        if (!YautjaBodyParts.Contains(baseState))
            return null;

        var rsiPath = sprite.BaseRSI?.Path.ToString() ?? string.Empty;
        if (!rsiPath.Contains("CMU14/Yautja/body.rsi", StringComparison.OrdinalIgnoreCase))
            return null;

        return $"{baseState}_{YautjaSkinSuffix(organ.Comp.Profile.SkinColor)}";
    }

    private void ApplyYautjaSkinShader(EntityUid body, int index, Color skin, Color color, bool eyes = false,
        string? organRsi = null)
    {
        if (!TryComp<SpriteComponent>(body, out var sprite) || YautjaLayer(sprite, index) is not { } layer)
        {
            return;
        }
        if (eyes)
        {
            // CMU14: Yautja bake their eyes into the head art, so the shared humanoid eyes layer is switched off for
            // them. This flag is not species-specific - the shared appearance path passes it whenever the organ is the
            // eyes layer, for any species - so the decision has to be made on the species, and false must be undone for
            // everyone else or their eye colour stops showing.
            var yautja = TryComp<HumanoidProfileComponent>(body, out var bodyProfile) &&
                         bodyProfile.Species.Id == "Yautja";
            sprite.LayerSetVisible(index, !yautja);
        }

        // Identify a Yautja skin layer from the sheet the organ draws from plus the body's species - not from
        // YautjaComponent (the lobby preview doll has none) and not from the layer colour (each organ is tinted with its
        // own). The part-name and sheet guards below do the rest.
        var isSkin = !eyes && IsYautjaSkinLayer(body, organRsi);

        if (!isSkin)
        {
            // clear any greyscale we applied earlier for a different profile state
            sprite.LayerSetShader(index, null, null);
            return;
        }

        // Pre-coloured CM art: pick the nearest baked variant and let its own pixels show.
        // The profile skin colour drives the choice: ApplyVisual forces the organ layer colour to white so the imported
        // art is not tinted, which makes the organ value useless as a selector.
        var selection = skin;
        var baseState = YautjaStripSuffix(layer.RsiState.Name ?? string.Empty);
        if (!YautjaBodyParts.Contains(baseState))
        {
            return;
        }

        // The variants live in the yautja body sheet only; some mobs carry the Yautja components while rendering from the
        // human part sheet, which has no "<part>_<colour>" states.
        var sheet = organRsi ?? sprite.BaseRSI?.Path.ToString();
        if (!YautjaBodyVariants.IsBodyRsi(sheet))
        {
            return;
        }

        var variant = $"{baseState}_{YautjaSkinSuffix(selection)}";
        if ((layer.RsiState.Name ?? string.Empty) != variant)
            sprite.LayerSetState(index, variant);
        sprite.LayerSetShader(index, null, null);
        sprite.LayerSetColor(index, Color.White);
    }

    private void ApplyYautjaMarkingShader(Entity<SpriteComponent?> body, int index, EntityUid organ, HumanoidVisualLayers layer)
    {
        if (!Resolve(body, ref body.Comp) || layer != HumanoidVisualLayers.Hair || !TryComp<VisualOrganComponent>(organ, out var visual))
            return;

        ApplyYautjaSkinShader(body.Owner, index, visual.Profile.SkinColor, body.Comp[index].Color);
    }
}
