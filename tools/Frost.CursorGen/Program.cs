using System;
using System.IO;
using Frost.Core;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "assets/cursors");
        CursorArtwork.Generate(output,Array.IndexOf(args,"--obsidian")>=0);
        // Match .gitattributes: .NET's indented JSON uses platform line endings.
        // Keep generated metadata reproducible without touching cursor/image bytes.
        foreach(var name in new[]{"manifest.json","theme.json"})
        {
            var path=Path.Combine(output,name);
            File.WriteAllText(path,File.ReadAllText(path).ReplaceLineEndings("\n"));
        }
        Console.WriteLine($"Generated {CursorRoles.All.Length} roles, four sizes, two 24-frame animations: {output}");
    }
}
