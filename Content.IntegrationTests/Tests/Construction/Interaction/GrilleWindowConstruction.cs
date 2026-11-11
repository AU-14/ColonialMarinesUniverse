using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Construction.Interaction;

/// <summary>
///     Check that we can build grilles on top of windows, but not the other way around.
/// </summary>
public sealed class GrilleWindowConstruction : InteractionTest
{
    private const string Grille = "Grille";
    private const string Window = "Window";

    [Test]
    public async Task WindowOnGrille()
    {
        // Construct Grille
        await StartConstruction(Grille);
        await InteractUsing(Rod, 10); // CMU14: graphs still consume the upstream MetalRod stack
        ClientAssertPrototype(Grille, Target);
        var grille = Target;

        // Construct Window
        await StartConstruction(Window);
        await InteractUsing(Glass, 10); // CMU14: graphs still consume the upstream Glass stack
        ClientAssertPrototype(Window, Target);

        // Deconstruct Window
        await Interact(Screw, Wrench);
        AssertDeleted();

        // Deconstruct Grille
        Target = grille;
        await InteractUsing(Cut);
        AssertDeleted();
    }

}

