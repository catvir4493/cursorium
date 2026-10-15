using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frost.Core;
public static class FoundationJson
{
    public static T Read<T>(byte[] bytes,int limit,int depth=32)
    {
        if(bytes.Length>limit)throw new InvalidDataException("Document exceeds its bounded size.");
        using var json=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=depth});
        if(json.RootElement.ValueKind!=JsonValueKind.Object||!json.RootElement.EnumerateObject().Any(p=>p.Name.Equals("SchemaVersion",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("An explicit schema version is required.");
        static void Unique(JsonElement node){
            if(node.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in node.EnumerateObject()){if(!names.Add(p.Name))throw new InvalidDataException("Duplicate JSON property rejected.");Unique(p.Value);}}
            else if(node.ValueKind==JsonValueKind.Array)foreach(var child in node.EnumerateArray())Unique(child);
        }
        Unique(json.RootElement);
        return JsonSerializer.Deserialize<T>(bytes,new JsonSerializerOptions(Storage.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=depth})??throw new InvalidDataException("Empty document.");
    }
}
