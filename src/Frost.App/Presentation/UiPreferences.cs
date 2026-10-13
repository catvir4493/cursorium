using System.Text.Json;
using Frost.Core;

namespace Frost.App.Presentation;

// UI preferences never enter cursor/effect profiles or the applied native configuration.
public sealed class UiPreferences
{
    public int SchemaVersion {get;set;}=1;
    public bool EnableAnimations {get;set;}=true;
    public bool UseSystemTitleBar {get;set;}
    public static string FilePath=>Path.Combine(Storage.Root,"interface.json");
    public static UiPreferences Load()
    {
        if(!File.Exists(FilePath))return new();
        var value=JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(FilePath),Storage.Json)??throw new InvalidDataException("Invalid interface preferences.");
        if(value.SchemaVersion!=1)throw new InvalidDataException("Unsupported interface preference schema; original file preserved.");return value;
    }
    public void Save()=>Storage.Write(FilePath,this);
}
