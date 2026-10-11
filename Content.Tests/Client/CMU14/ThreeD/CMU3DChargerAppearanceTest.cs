using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DChargerAppearanceTest
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void ActualIndicatorFrameAndIndependentInsertLayersSelectPartsWithoutMutation(int level)
    {
        var model = Model();
        for (var frame = 0; frame < (level == 5 ? 2 : 1); frame++)
        for (var mask = 0; mask < 4; mask++)
        {
            var light = Layer($"recharger-{level}", true, level == 5 ? 2 : 1) with { Frame = frame };
            var taser = Layer(CMU3DChargerAppearance.Taser) with { Visible = (mask & 1) != 0 };
            var baton = Layer(CMU3DChargerAppearance.Baton) with { Visible = (mask & 2) != 0 };
            Assert.That(CMU3DChargerAppearance.TryParts(model, Layer("recharger"), light, taser, baton, false,
                out var parts, out var key), Is.True);
            Assert.That(parts[0], Is.SameAs(model.ChargerAppearance!.BaseParts[0]));
            Assert.That(parts[1].Label, Is.EqualTo($"light-{level}-{frame}"));
            Assert.That(parts.Skip(2).Select(p => p.Label), Is.EqualTo(new[] { (mask & 1) != 0 ? "taser" : null,
                (mask & 2) != 0 ? "baton" : null }.Where(p => p != null)));
            Assert.That(key, Is.EqualTo($"charger:recharger-{level}:{frame}:{mask}"));
        }
        Assert.That(model.Parts.Count, Is.EqualTo(2));
        Assert.That(model.ChargerAppearance!.BaseParts.Count, Is.EqualTo(1));
        foreach (var yaw in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
            Assert.That(CMU3DSceneLayout.RenderYaw(model, yaw, false, true), Is.EqualTo(0).Within(.00001));
    }

    [Test]
    public void HiddenIndicatorAndAbsentMapperLayersKeepOnlyBody()
    {
        var model = Model();
        Assert.That(CMU3DChargerAppearance.TryParts(model, Layer("recharger"),
            Layer("recharger-0", true) with { Visible = false }, null, null, false, out var parts, out _), Is.True);
        Assert.That(parts.Select(p => p.Label), Is.EqualTo(new[] { "body" }));
    }

    [TestCase("base-hidden")] [TestCase("base-state")] [TestCase("rsi")] [TestCase("transform")]
    [TestCase("tint")] [TestCase("shader")] [TestCase("light-state")] [TestCase("frame")]
    [TestCase("frame-count")] [TestCase("insert-state")] [TestCase("insert-shader")]
    [TestCase("extra")] [TestCase("budget")] [TestCase("delay")] [TestCase("rotation")] [TestCase("geometry")]
    public void UnsupportedAppearanceFailsAtomically(string defect)
    {
        var model=Model();var basis=Layer("recharger");var light=Layer("recharger-0",true);
        CMU3DChargerLayer? taser=null;var extra=false;
        switch(defect)
        {
            case "base-hidden": basis=basis with { Visible=false }; break;
            case "base-state": basis=basis with { State="full" }; break;
            case "rsi": basis=basis with { Rsi="/Textures/other.rsi" }; break;
            case "transform": basis=basis with { IdentityTransform=false }; break;
            case "tint": light=light with { Color=Color.Red }; break;
            case "shader": light=light with { Unshaded=false }; break;
            case "light-state": light=light with { State="recharger-6" }; break;
            case "frame": light=light with { Frame=1 }; break;
            case "frame-count": light=light with { FrameCount=2 }; break;
            case "insert-state": taser=Layer("unknown"); break;
            case "insert-shader": taser=Layer(CMU3DChargerAppearance.Taser,true); break;
            case "extra": extra=true; break;
            case "budget": model.ChargerAppearance!.BaseParts.AddRange(Enumerable.Repeat(Part("extra"),128)); break;
            case "delay": model.ChargerAppearance!.LightStates["recharger-0"].Delays=[.1f]; break;
            case "rotation": model.UseEntityRotation=true; break;
            case "geometry": model.ChargerAppearance!.BaseParts[0].Min=new Vector3(float.NaN); break;
        }
        Assert.That(CMU3DChargerAppearance.TryParts(model,basis,light,taser,null,extra,out var parts,out var key),Is.False);
        Assert.That(parts,Is.Empty);Assert.That(key,Is.Empty);
    }

    private static CMU3DChargerLayer Layer(string state,bool unshaded=false,int frames=1) =>
        new("/Textures/"+CMU3DChargerAppearance.Rsi,state,0,frames,true,Color.White,true,unshaded);
    private static CMU3DModelPart Part(string label) => new()
    {
        Label=label,Min=Vector3.Zero,Max=new Vector3(.1f,.1f,.1f),
    };
    private static CMU3DModelPrototype Model()
    {
        var definition=new CMU3DChargerAppearanceDefinition { BaseParts=[Part("body")] };
        for(var level=0;level<6;level++)
            definition.LightStates[$"recharger-{level}"]=new CMU3DSpriteState
            {
                Frames=Enumerable.Range(0,level==5?2:1).Select(frame=>new CMU3DModelFrame { Parts=[Part($"light-{level}-{frame}")] }).ToList(),
                Delays=level==5?[.1f,.1f]:[1f],
            };
        definition.InsertedStates[CMU3DChargerAppearance.Taser]=new CMU3DModelFrame { Parts=[Part("taser")] };
        definition.InsertedStates[CMU3DChargerAppearance.Baton]=new CMU3DModelFrame { Parts=[Part("baton")] };
        return new CMU3DModelPrototype { ReferenceRsi=CMU3DChargerAppearance.Rsi,ReferenceState="recharger",SourceDirections=1,
            Placement="surface",ChargerAppearance=definition,Parts=[definition.BaseParts[0],definition.LightStates["recharger-0"].Frames[0].Parts[0]] };
    }
}
