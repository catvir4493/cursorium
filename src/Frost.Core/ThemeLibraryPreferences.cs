using System.Text.Json;

namespace Frost.Core;

public sealed class ThemeLibraryPreferences
{
    public int SchemaVersion {get;set;}=1;
    public string Filter {get;set;}="all";
    public HashSet<string> Favorites {get;set;}=new();
    private static string Path=>System.IO.Path.Combine(Storage.Root,"library.json");
    public static ThemeLibraryPreferences Load()
    {
        if(!File.Exists(Path))return new();
        var value=JsonSerializer.Deserialize<ThemeLibraryPreferences>(File.ReadAllText(Path),Storage.Json)??throw new InvalidDataException("Invalid theme library preferences.");
        if(value.SchemaVersion!=1||value.Favorites==null)throw new InvalidDataException("Unsupported theme library preferences.");return value;
    }
    public void Save()=>Storage.Write(Path,this);
    public bool Includes(CursorTheme theme)=>Filter switch{"favorites"=>Favorites.Contains(theme.Id),"all"=>true,_=>theme.LibraryCollection==Filter};
}
