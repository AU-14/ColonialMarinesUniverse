#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.Shared.Actions.Components;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoAcidTest
{
    private static readonly string[] CorrosiveAcidActions =
    [
        "ActionXenoAcidNormal",
        "ActionXenoAcidWeak",
        "ActionXenoAcidStrong",
        "ActionXenoAcidAcider",
        "CMU14ActionXenoPathogenAcidNormal",
        "CMU14ActionXenoPathogenAcidWeak",
        "CMU14ActionXenoPathogenAcidStrong",
        "CMU14ActionXenoPathogenAcidAcider",
    ];

    [Test]
    public async Task CorrosiveAcidActionsHaveTwoTileRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            foreach (var prototype in CorrosiveAcidActions)
            {
                var action = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);

                try
                {
                    var target = entMan.GetComponent<TargetActionComponent>(action);
                    Assert.That(target.Range, Is.EqualTo(2), prototype);
                }
                finally
                {
                    entMan.DeleteEntity(action);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
