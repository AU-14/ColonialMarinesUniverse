using System.Linq;
using System.Text;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     Dumps the live UI control tree, with positions, so a misplaced or duplicated panel can be
///     traced to its parent chain from the running client instead of guessed at from XAML.
/// </summary>
public sealed class CmuUiTreeCommand : IConsoleCommand
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public string Command => "cmu_uitree";
    public string Description => "Dumps the UI control tree from the window root.";
    public string Help => "cmu_uitree [filter]\n  No argument: the whole tree, one control per line.\n  With filter: only controls whose name or type matches, each printed with its full ancestor chain.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var root = _ui.RootControl;
        var filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;

        var sb = new StringBuilder();
        if (filter == null)
        {
            Walk(root, 0, sb);
        }
        else
        {
            foreach (var match in Collect(root).Where(c => Matches(c, filter)))
            {
                sb.AppendLine(Chain(match));
                sb.AppendLine($"    {Describe(match)}");
            }
        }

        shell.WriteLine(sb.ToString());
    }

    private static void Walk(Control control, int depth, StringBuilder sb)
    {
        sb.Append(' ', depth * 2);
        sb.AppendLine(Describe(control));

        foreach (var child in control.Children)
            Walk(child, depth + 1, sb);
    }

    private static System.Collections.Generic.IEnumerable<Control> Collect(Control control)
    {
        yield return control;

        foreach (var child in control.Children)
        {
            foreach (var descendant in Collect(child))
                yield return descendant;
        }
    }

    private static bool Matches(Control control, string filter)
    {
        return (control.Name != null && control.Name.ToLowerInvariant().Contains(filter))
            || control.GetType().Name.ToLowerInvariant().Contains(filter);
    }

    private static string Chain(Control control)
    {
        var steps = new System.Collections.Generic.List<string>();

        for (var c = control; c != null; c = c.Parent)
            steps.Add(Describe(c));

        steps.Reverse();
        return string.Join(" > ", steps);
    }

    private static string Describe(Control control)
    {
        var name = control.Name != null ? $" #{control.Name}" : string.Empty;
        var visible = control.Visible ? string.Empty : " [hidden]";
        return $"{control.GetType().Name}{name} at {control.GlobalPosition} size {control.PixelSize}{visible}";
    }
}
