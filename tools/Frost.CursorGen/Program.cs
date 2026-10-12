using System;
using System.IO;
using Frost.Core;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "assets/cursors");
        CursorArtwork.Generate(output,Array.IndexOf(args,"--obsidian")>=0);
        Console.WriteLine($"Generated {CursorRoles.All.Length} roles, four sizes, two 24-frame animations: {output}");
    }
}
