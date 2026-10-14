global using Content.Client.CMU14.UserInterface;

using Robust.Client.UserInterface;

namespace Content.Client.CMU14.UserInterface;

/// <summary>
/// Releases controls that still own resources or external event subscriptions through their disposal overrides.
/// </summary>
/// <remarks>
/// Removing these controls from the tree alone does not run that cleanup, particularly for unattached controls.
/// Keep the engine compatibility calls here until their disposal overrides have migrated to tree lifecycle hooks.
/// </remarks>
public static class CMUControlLifetime
{
    public static void Release(this Control control)
    {
#pragma warning disable CS0618 // The retained virtual disposal chain releases CMU GPU resources and subscriptions.
        control.Dispose();
#pragma warning restore CS0618
    }

    public static void ReleaseChildren(this Control control)
    {
#pragma warning disable CS0618 // Each child must run its disposal chain before leaving the parent.
        control.DisposeAllChildren();
#pragma warning restore CS0618
    }
}
