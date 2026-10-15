using System.Security.Cryptography;
using System.Text;

namespace Frost.Core;
public static class BundledRights
{
    public static string ThemeDigest(CursorTheme theme)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var role in CursorRoles.All){var path=theme.Resource(role.Name);hash.AppendData(Encoding.UTF8.GetBytes(role.Name+Path.GetExtension(path)));hash.AppendData(File.ReadAllBytes(path));}
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static void VerifySteamPayload(LibraryRightsDocument document,string application)
    {
        var catalog=new ThemeCatalog(application);if(catalog.Themes.Count!=3||catalog.Warnings.Count!=0||catalog.Themes.Any(t=>t.Id is not ("system" or "frost" or "obsidian")))throw new InvalidDataException("Steam experimental payload contains an unapproved bundled theme.");
        foreach(var id in new[]{"frost","obsidian"}){
            var record=document.Resources.SingleOrDefault(r=>r.Scope=="Bundled"&&r.StableId==id);if(record==null||!record.AllowsSteam||!record.PackageSha256.Equals(ThemeDigest(catalog.Find(id)),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bundled cursor bytes differ from signed approval.");
        }
        var effects=Path.Combine(application,"Catalog","effects.json");_=EffectCatalog.Load(effects);var digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(effects)));
        foreach(var id in new[]{"comet","minimal","neon","stardust"})if(!document.Resources.Any(r=>r.Scope=="Bundled"&&r.StableId==id&&r.AllowsSteam&&r.PackageSha256.Equals(digest,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Bundled preset bytes differ from signed approval.");
    }
}
