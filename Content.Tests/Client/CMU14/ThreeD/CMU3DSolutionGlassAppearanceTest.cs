using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSolutionGlassAppearanceTest
{
    private const string Clear = "Objects/Consumable/Drinks/glass_clear.rsi";
    private const string Coffee = "Objects/Consumable/Drinks/coffeeglass.rsi";

    [Test]
    public void ActualMetamorphicResourceAndVisibilitySelectPhysicalPartsWithoutMutation()
    {
        var model = Model();
        var fill = Layer(Coffee,"fill-2") with { Color = new Color(.5f,.25f,.75f,.4f) };
        var layers = new[] { Layer(Coffee,"icon_empty"),fill,Layer(Clear,"icon-front") with { Visible=false } };
        Assert.That(CMU3DSolutionGlassAppearance.TryParts(model,layers,out var parts,out var key),Is.True);
        Assert.That(parts.Select(p=>p.Label),Is.EqualTo(new[] {"coffee bowl","coffee liquid"}));
        Assert.That(parts[1].Color,Is.EqualTo(fill.Color));
        Assert.That(model.SolutionAppearance!.Layers[3].Parts[0].Color,Is.EqualTo(Color.White));
        Assert.That(key,Does.Contain(Coffee));
        layers[1]=fill with { Visible=false };
        Assert.That(CMU3DSolutionGlassAppearance.TryParts(model,layers,out parts,out var hiddenKey),Is.True);
        Assert.That(parts.Select(p=>p.Label),Is.EqualTo(new[] {"coffee bowl"}));
        Assert.That(hiddenKey,Is.Not.EqualTo(key));
    }

    [TestCase("unknown-rsi")] [TestCase("unknown-state")] [TestCase("frame")]
    [TestCase("transform")] [TestCase("base-hidden")] [TestCase("duplicate")]
    [TestCase("budget")] [TestCase("geometry")] [TestCase("color")] [TestCase("layers")]
    [TestCase("foam")]
    public void UnsupportedSourceOrGeometryFallsBackAtomically(string defect)
    {
        var model=Model();var layers=new[] {Layer(Coffee,"icon_empty"),Layer(Coffee,"fill-2"),Layer(Clear,"icon-front")};
        switch(defect)
        {
            case "unknown-rsi": layers[1]=layers[1] with {Rsi="/Textures/other.rsi"};break;
            case "unknown-state": layers[1]=layers[1] with {State="unknown"};break;
            case "frame": layers[1]=layers[1] with {Frame=1};break;
            case "transform": layers[1]=layers[1] with {IdentityTransform=false};break;
            case "base-hidden": layers[0]=layers[0] with {Visible=false};break;
            case "duplicate": model.SolutionAppearance!.Layers.Add(model.SolutionAppearance.Layers[0]);break;
            case "budget": model.SolutionAppearance!.Layers[3].Parts.AddRange(Enumerable.Repeat(Part("extra"),128));break;
            case "geometry": model.SolutionAppearance!.Layers[3].Parts[0].Min=new Vector3(float.NaN);break;
            case "color": layers[1]=layers[1] with {Color=new Color(float.NaN,1,1,1)};break;
            case "layers": layers=layers.Take(2).ToArray();break;
            case "foam": model.FoamAppearance=new CMU3DFoamAppearanceDefinition();break;
        }
        Assert.That(CMU3DSolutionGlassAppearance.TryParts(model,layers,out var parts,out var key),Is.False);
        Assert.That(parts,Is.Empty);Assert.That(key,Is.Empty);
    }

    private static CMU3DButtonLayer Layer(string rsi,string state) => new("/Textures/"+rsi,state,0,true,Color.White,true);
    private static CMU3DModelPart Part(string label) => new() {Label=label,Min=Vector3.Zero,Max=new Vector3(.1f)};
    private static CMU3DSolutionLayerGeometry Geometry(string role,string rsi,string state,string label) =>
        new() {Role=role,Rsi=rsi,State=state,Parts=[Part(label)]};
    private static CMU3DModelPrototype Model() => new()
    {
        ReferenceRsi=Clear,ReferenceState="icon",SourceDirections=1,UseEntityRotation=true,Placement="surface",
        SolutionAppearance=new CMU3DSolutionAppearanceDefinition
        {
            Layers=[Geometry("Base",Clear,"icon","clear bowl"),Geometry("Fill",Clear,"fill-1","clear liquid"),
                Geometry("Base",Coffee,"icon_empty","coffee bowl"),Geometry("Fill",Coffee,"fill-2","coffee liquid"),
                Geometry("Overlay",Clear,"icon-front","clear front")],
        },
    };
}
