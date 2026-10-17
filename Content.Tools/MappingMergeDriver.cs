using System;

namespace Content.Tools;

internal static class MappingMergeDriver
{
    /// %A: Our file
    /// %O: Origin (common, base) file
    /// %B: Other file
    /// %P: Actual filename of the resulting file
    public static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "check")
            return Check(args[1..]);

        if (args.Length < 3)
        {
            Console.WriteLine("usage: <ours> <base> <other> [result]  |  check <map.yml>...");
            return 2;
        }

        var ours = new Map(args[0]);
        var based = new Map(args[1]);
        var other = new Map(args[2]);

        if (!new Merger(ours, based, other).Merge())
        {
            Console.WriteLine("unable to merge!");
            return 1;
        }

        ours.Save();
        return 0;
    }

    /// Parse-validate map files without merging anything.
    private static int Check(string[] paths)
    {
        var failed = 0;
        foreach (var path in paths)
        {
            try
            {
                var map = new Map(path);
                Console.WriteLine($"ok {path}: {map.Entities.Count} entities, {map.GridIds.Count} grids");
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"FAIL {path}: {e.Message}");
            }
        }

        Console.WriteLine($"{paths.Length - failed}/{paths.Length} maps parsed");
        return failed == 0 ? 0 : 1;
    }
}
