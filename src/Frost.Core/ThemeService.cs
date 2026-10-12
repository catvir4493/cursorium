using Microsoft.Win32;
using System.Text.Json;

namespace Frost.Core;
public sealed record RegistryEntry(string Name, bool Exists, RegistryValueKind Kind, JsonElement Value);
public sealed record ThemeBackup(DateTimeOffset Created,List<RegistryEntry> Cursors,RegistryEntry Scheme);
public sealed class ThemeService
{
    public const string ThemeName="FROST / WHITE COMET";
    public const string KeyPath=@"Control Panel\Cursors";
    public string ThemeDirectory=>Path.Combine(Storage.Root,"Theme","v1");
    public string BackupPath=>Path.Combine(Storage.Root,"original-theme.json");
    private static readonly string[] Names=CursorRoles.All.Select(r=>r.RegistryName).Concat(new[]{"","Scheme Source"}).ToArray();
    public bool HasBackup=>File.Exists(BackupPath);
    public bool IsInstalled
    {
        get{using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return key!=null&&CursorRoles.All.All(r=>string.Equals(key.GetValue(r.RegistryName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string,Path.Combine(ThemeDirectory,r.FileName),StringComparison.OrdinalIgnoreCase));}
    }
    private static RegistryEntry Read(RegistryKey key,string name)
    {
        bool exists=key.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase);
        return new(name,exists,exists?key.GetValueKind(name):RegistryValueKind.String,JsonSerializer.SerializeToElement(exists?key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames):null));
    }
    private static void Apply(RegistryKey key,RegistryEntry entry)
    {
        if(!entry.Exists){key.DeleteValue(entry.Name,false);return;}
        object value=entry.Kind switch
        {
            RegistryValueKind.DWord=>entry.Value.GetInt32(),RegistryValueKind.QWord=>entry.Value.GetInt64(),
            RegistryValueKind.Binary or RegistryValueKind.None=>entry.Value.GetBytesFromBase64(),
            RegistryValueKind.MultiString=>entry.Value.Deserialize<string[]>()!,_=>entry.Value.GetString()!
        };
        key.SetValue(entry.Name,value,entry.Kind);
    }
    public ThemeBackup Snapshot()
    {
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath);using var schemes=key.CreateSubKey("Schemes");
        return new(DateTimeOffset.Now,Names.Select(n=>Read(key,n)).ToList(),Read(schemes,ThemeName));
    }
    private static void ApplyBackup(ThemeBackup backup)
    {
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath);foreach(var value in backup.Cursors)Apply(key,value);
        using var schemes=key.CreateSubKey("Schemes");Apply(schemes,backup.Scheme);key.Flush();Native.ReloadCursors();
    }
    public void Install(string source)
    {
        using var gate=new Mutex(false,@"Local\FrostWhiteComet.Theme");if(!gate.WaitOne(TimeSpan.FromSeconds(10)))throw new IOException("Another cursor theme operation is in progress.");
        try
        {
            if(HasBackup&&!IsInstalled)throw new InvalidOperationException("An unrestored cursor backup already exists. Restore it first to avoid replacing your backup.");
            Directory.CreateDirectory(ThemeDirectory);
            foreach(var role in CursorRoles.All)
            {
                var path=Path.Combine(source,role.FileName);var handle=Native.LoadCursorFromFile(path);
                if(handle==IntPtr.Zero)throw new IOException($"Windows could not load {path}");
                Native.DestroyCursor(handle); // Owned handle. Never destroy LoadCursor's shared system handles.
                File.Copy(path,Path.Combine(ThemeDirectory,role.FileName),true);
            }
            var before=Snapshot();if(!HasBackup)Storage.Write(BackupPath,before); // durable journal BEFORE modifying HKCU
            try
            {
                using var key=Registry.CurrentUser.CreateSubKey(KeyPath);
                foreach(var role in CursorRoles.All)key.SetValue(role.RegistryName,Path.Combine(ThemeDirectory,role.FileName),RegistryValueKind.String);
                key.SetValue("",ThemeName,RegistryValueKind.String);key.SetValue("Scheme Source",1,RegistryValueKind.DWord);
                using var schemes=key.CreateSubKey("Schemes");schemes.SetValue(ThemeName,string.Join(",",CursorRoles.All.Select(r=>Path.Combine(ThemeDirectory,r.FileName))),RegistryValueKind.String);
                key.Flush();Native.ReloadCursors();Storage.Log("Theme installed");
            }
            catch{ApplyBackup(before);throw;}
        }
        finally{gate.ReleaseMutex();}
    }
    public void Restore()
    {
        using var gate=new Mutex(false,@"Local\FrostWhiteComet.Theme");if(!gate.WaitOne(TimeSpan.FromSeconds(10)))throw new IOException("Another cursor theme operation is in progress.");
        try
        {
            if(!HasBackup)throw new InvalidOperationException("No original cursor scheme backup is available.");
            var backup=JsonSerializer.Deserialize<ThemeBackup>(File.ReadAllText(BackupPath),Storage.Json)??throw new IOException("The cursor scheme backup is invalid.");
            ApplyBackup(backup);
            var archive=Path.Combine(Storage.Root,"Backups",$"original-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");Directory.CreateDirectory(Path.GetDirectoryName(archive)!);File.Move(BackupPath,archive);
            Storage.Log("Original theme restored");
        }
        finally{gate.ReleaseMutex();}
    }
}
