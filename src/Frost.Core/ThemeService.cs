using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text.Json;

namespace Frost.Core;

// Scheme is retained for compatibility with backups made by FROST v1.
public sealed record RegistryEntry(string Name,bool Exists,RegistryValueKind Kind,JsonElement Value);
public sealed record ThemeBackup(DateTimeOffset Created,List<RegistryEntry> Cursors,RegistryEntry Scheme,List<RegistryEntry>? Schemes=null,int SchemaVersion=1);
public sealed record ThemeTransaction(ThemeBackup Before,string? BackupBefore,string? StateBefore,int SchemaVersion=1,ThemeBackup? Intended=null,string? OwnershipBefore=null);

public sealed class ThemeService
{
    public const string ThemeName="FROST / WHITE COMET";
    public const string KeyPath=@"Control Panel\Cursors";
    public string ThemeDirectory=>Path.Combine(Storage.Root,"Theme","v1");
    public string BackupPath=>Path.Combine(Storage.Root,"original-theme.json");
    public string JournalPath=>Path.Combine(Storage.Root,"theme-transaction.json");
    private string StatePath=>Path.Combine(Storage.Root,"studio.json");
    private string OwnershipPath=>Path.Combine(Storage.Root,"theme-ownership.json");
    private static readonly string[] Names=CursorRoles.All.Select(r=>r.RegistryName).Concat(new[]{"","Scheme Source"}).ToArray();
    internal Action<int>? AfterRoleWrite {get;set;}
    public bool HasBackup=>File.Exists(BackupPath);
    public string? OriginalResource(string role)
    {
        string? path=null;var name=CursorRoles.All.Single(r=>r.Name==role).RegistryName;
        if(HasBackup)
        {
            var entry=ParseBackup(File.ReadAllText(BackupPath)).Cursors.SingleOrDefault(e=>e.Name==name);
            if(entry?.Exists==true&&entry.Value.ValueKind==JsonValueKind.String)path=entry.Value.GetString();
        }
        else {using var key=Registry.CurrentUser.OpenSubKey(KeyPath);path=key?.GetValue(name) as string;}
        if(string.IsNullOrWhiteSpace(path))return null;path=Environment.ExpandEnvironmentVariables(path);return File.Exists(path)?path:null;
    }
    public bool IsInstalled
    {
        get
        {
            using var key=Registry.CurrentUser.OpenSubKey(KeyPath);
            return key!=null&&(Equals((string?)key.GetValue(""),"CURSORIUM / frost")||CursorRoles.All.All(r=>string.Equals(key.GetValue(r.RegistryName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string,Path.Combine(ThemeDirectory,r.FileName),StringComparison.OrdinalIgnoreCase)));
        }
    }
    private static RegistryEntry Read(RegistryKey key,string name)
    {
        bool exists=key.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase);
        return new(name,exists,exists?key.GetValueKind(name):RegistryValueKind.String,JsonSerializer.SerializeToElement(exists?key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames):null));
    }
    private static void ApplyEntry(RegistryKey key,RegistryEntry entry)
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
    public ThemeBackup Snapshot(params string[] additionalSchemeNames)
    {
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath);using var schemes=key.CreateSubKey("Schemes");
        var names=schemes.GetValueNames().Where(n=>n.StartsWith("CURSORIUM / ",StringComparison.Ordinal)||n==ThemeName).Concat(additionalSchemeNames).Append(ThemeName).Distinct().OrderBy(n=>n,StringComparer.Ordinal);
        return new(DateTimeOffset.Now,Names.Select(n=>Read(key,n)).ToList(),Read(schemes,ThemeName),names.Select(n=>Read(schemes,n)).ToList());
    }
    private static void ApplyBackup(ThemeBackup backup)
    {
        foreach(var entry in backup.Cursors.Where(e=>e.Exists&&CursorRoles.All.Any(r=>r.RegistryName==e.Name)&&e.Value.ValueKind==JsonValueKind.String))
        {
            var resource=entry.Value.GetString();if(!string.IsNullOrWhiteSpace(resource)&&!File.Exists(Environment.ExpandEnvironmentVariables(resource)))throw new IOException("A cursor required for recovery is unavailable. Reconnect its drive or restore the resource before retrying; the recovery backup is retained.");
        }
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath);foreach(var entry in backup.Cursors)ApplyEntry(key,entry);
        using var schemes=key.CreateSubKey("Schemes");foreach(var entry in backup.Schemes??new(){backup.Scheme})ApplyEntry(schemes,entry);
        schemes.Flush();key.Flush();Native.ReloadCursors();
    }
    private static string? ReadText(string path)=>File.Exists(path)?File.ReadAllText(path):null;
    private static void RestoreText(string path,string? value){if(value==null){if(File.Exists(path))File.Delete(path);}else Storage.WriteText(path,value);}
    private static ThemeBackup ParseBackup(string text)
    {
        var backup=JsonSerializer.Deserialize<ThemeBackup>(text,Storage.Json)??throw new InvalidDataException("Invalid cursor backup.");
        if(backup.SchemaVersion!=1||backup.Cursors==null||backup.Scheme==null)throw new InvalidDataException("Unsupported cursor backup schema; original file preserved.");return backup;
    }
    private void RecoverInternal()
    {
        if(!File.Exists(JournalPath))return;
        var journal=JsonSerializer.Deserialize<ThemeTransaction>(File.ReadAllText(JournalPath),Storage.Json)??throw new InvalidDataException("Invalid theme transaction journal.");
        if(journal.SchemaVersion!=1||journal.Before?.SchemaVersion!=1)throw new InvalidDataException("Unsupported transaction journal schema; original file preserved.");
        EnsureRecoverable(journal);ApplyBackup(journal.Before);RestoreText(BackupPath,journal.BackupBefore);RestoreText(StatePath,journal.StateBefore);if(journal.Intended!=null)RestoreText(OwnershipPath,journal.OwnershipBefore);File.Delete(JournalPath);Storage.Log("Recovered interrupted cursor transaction.");
    }
    private void EnsureRecoverable(ThemeTransaction journal)
    {
        // Legacy journals remain recoverable. New journals record both permitted
        // states before writing; unknown external edits require explicit review.
        if(journal.Intended==null)return;
        var current=Snapshot((journal.Before.Schemes??new(){journal.Before.Scheme}).Select(e=>e.Name).ToArray());
        static bool Same(RegistryEntry a,RegistryEntry b)=>a.Name==b.Name&&a.Exists==b.Exists&&(!a.Exists||(a.Kind==b.Kind&&a.Value.GetRawText()==b.Value.GetRawText()));
        static bool Allowed(IEnumerable<RegistryEntry> actual,IEnumerable<RegistryEntry> before,IEnumerable<RegistryEntry> intended)=>actual.All(a=>before.Any(b=>Same(a,b))||intended.Any(b=>Same(a,b)));
        if(!Allowed(current.Cursors,journal.Before.Cursors,journal.Intended.Cursors)||!Allowed(current.Schemes??new(){current.Scheme},journal.Before.Schemes??new(){journal.Before.Scheme},journal.Intended.Schemes??new(){journal.Intended.Scheme}))
            throw new IOException("Windows cursors were changed outside the interrupted operation. Recovery was stopped to preserve those changes; the recovery journal and original backup are retained.");
    }
    private static Mutex EnterGate()
    {
        var gate=new Mutex(false,@"Local\FrostWhiteComet.Theme");bool entered;
        try{entered=gate.WaitOne(TimeSpan.FromSeconds(10));}catch(AbandonedMutexException){entered=true;}
        if(!entered){gate.Dispose();throw new IOException("Another cursor operation is in progress.");}return gate;
    }
    public void Recover(){using var gate=EnterGate();try{RecoverInternal();}finally{gate.ReleaseMutex();}}
    private void Transaction(string[] schemeNames,Action action,Func<ThemeBackup,ThemeBackup>? intended=null)
    {
        using var gate=EnterGate();
        try
        {
            RecoverInternal();var before=Snapshot(schemeNames);var journal=new ThemeTransaction(before,ReadText(BackupPath),ReadText(StatePath),Intended:intended?.Invoke(before),OwnershipBefore:ReadText(OwnershipPath));
            Storage.Write(JournalPath,journal);
            try{action();File.Delete(JournalPath);}
            catch(Exception failure)
            {
                try{EnsureRecoverable(journal);ApplyBackup(journal.Before);RestoreText(BackupPath,journal.BackupBefore);RestoreText(StatePath,journal.StateBefore);RestoreText(OwnershipPath,journal.OwnershipBefore);File.Delete(JournalPath);}
                catch(Exception rollback){throw new AggregateException("Apply failed and recovery is incomplete. The transaction journal is retained for the next start.",failure,rollback);}
                throw;
            }
        }
        finally{gate.ReleaseMutex();}
    }
    private Dictionary<string,string> Prepare(CursorTheme theme)
    {
        if(!RemoteResource.ValidId(theme.Id))throw new InvalidDataException("Invalid cursor theme identity.");
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var role in CursorRoles.All){var path=theme.Resource(role.Name);CursorResource.Validate(path);hash.AppendData(System.Text.Encoding.UTF8.GetBytes(role.Name+Path.GetExtension(path)));hash.AppendData(File.ReadAllBytes(path));}
        var directory=Path.Combine(Storage.Root,"Theme","installed",theme.Id,Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());DownloadManager.GuardRoot(directory);Directory.CreateDirectory(directory);
        var result=new Dictionary<string,string>();
        foreach(var role in CursorRoles.All)
        {
            var source=theme.Resource(role.Name);var target=Path.Combine(directory,role.Name+Path.GetExtension(source).ToLowerInvariant());
            DownloadManager.GuardRoot(target);
            if(!File.Exists(target))File.Copy(source,target);
            if(!SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(target))))throw new InvalidDataException("An immutable cursor resource was modified. Existing files were preserved; select or import a verified theme.");
            CursorResource.Validate(target);result.Add(role.Name,target);
        }
        return result;
    }
    public bool IsActive(CursorTheme theme)
    {
        if(theme.IsSystem)return !HasBackup;
        using var key=Registry.CurrentUser.OpenSubKey(KeyPath);if(key==null)return false;
        // Status queries must never install/copy resources or create native handles.
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var role in CursorRoles.All){var path=theme.Resource(role.Name);hash.AppendData(System.Text.Encoding.UTF8.GetBytes(role.Name+Path.GetExtension(path)));hash.AppendData(File.ReadAllBytes(path));}
        var directory=Path.Combine(Storage.Root,"Theme","installed",theme.Id,Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        return CursorRoles.All.All(r=>string.Equals(key.GetValue(r.RegistryName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string,Path.Combine(directory,r.Name+Path.GetExtension(theme.Resource(r.Name)).ToLowerInvariant()),StringComparison.OrdinalIgnoreCase));
    }
    public void ApplyTheme(CursorTheme theme,Action? commit=null)
    {
        if(theme.IsSystem){Restore(commit);return;}
        var paths=Prepare(theme);var name="CURSORIUM / "+theme.Id;
        Transaction(new[]{name},()=>
        {
            var current=Snapshot(name);var original=HasBackup?ParseBackup(File.ReadAllText(BackupPath)):current;
            var entries=(original.Schemes??new(){original.Scheme}).ToList();
            foreach(var entry in current.Schemes!)if(entries.All(e=>e.Name!=entry.Name))entries.Add(entry);
            Storage.Write(BackupPath,original with{Schemes=entries});
            using var key=Registry.CurrentUser.CreateSubKey(KeyPath);
            int index=0;foreach(var role in CursorRoles.All){key.SetValue(role.RegistryName,paths[role.Name],RegistryValueKind.String);AfterRoleWrite?.Invoke(++index);}
            key.SetValue("",name,RegistryValueKind.String);key.SetValue("Scheme Source",1,RegistryValueKind.DWord);
            using var schemes=key.CreateSubKey("Schemes");schemes.SetValue(name,string.Join(",",CursorRoles.All.Select(r=>paths[r.Name])),RegistryValueKind.String);
            schemes.Flush();key.Flush();Native.ReloadCursors();Storage.Write(OwnershipPath,Snapshot());commit?.Invoke();Storage.Log("Applied cursor theme: "+theme.Id);
        },before=>before with{Cursors=before.Cursors.Select(e=>e.Name==""?new RegistryEntry("",true,RegistryValueKind.String,JsonSerializer.SerializeToElement(name)):e.Name=="Scheme Source"?new RegistryEntry(e.Name,true,RegistryValueKind.DWord,JsonSerializer.SerializeToElement(1)):new RegistryEntry(e.Name,true,RegistryValueKind.String,JsonSerializer.SerializeToElement(paths[CursorRoles.All.Single(r=>r.RegistryName==e.Name).Name]))).ToList(),Schemes=before.Schemes!.Select(e=>e.Name==name?new RegistryEntry(name,true,RegistryValueKind.String,JsonSerializer.SerializeToElement(string.Join(",",CursorRoles.All.Select(r=>paths[r.Name])))):e).ToList()});
    }
    public void Install(string source)
    {
        var theme=new CursorTheme{Id="frost",Name=ThemeName,DirectoryPath=source,Roles=CursorRoles.All.ToDictionary(r=>r.Name,r=>r.FileName)};ApplyTheme(theme);
    }
    public void Restore(Action? commit=null)
    {
        if(!HasBackup){commit?.Invoke();return;}
        var backupText=File.ReadAllText(BackupPath);var backup=ParseBackup(backupText);
        void EnsureOwned()
        {
        var current=Snapshot();
        if(File.Exists(OwnershipPath))
        {
            var owned=ParseBackup(File.ReadAllText(OwnershipPath));
            if(JsonSerializer.Serialize(current.Cursors)!=JsonSerializer.Serialize(owned.Cursors))throw new IOException("Windows cursors were changed outside CURSORIUM. Automatic restoration was stopped; your original backup is preserved. Review the Windows cursor settings before retrying.");
        }
        else
        {
            var legacyRoot=Path.Combine(Storage.Root,"Theme")+Path.DirectorySeparatorChar;
            if(current.Cursors.Where(e=>CursorRoles.All.Any(r=>r.RegistryName==e.Name)).Any(e=>e.Value.ValueKind!=JsonValueKind.String||!(e.Value.GetString()??"").StartsWith(legacyRoot,StringComparison.OrdinalIgnoreCase)))throw new IOException("The active legacy cursor configuration is no longer owned by CURSORIUM. The original backup is preserved; automatic restoration was stopped.");
        }
        }
        Transaction((backup.Schemes??new(){backup.Scheme}).Select(s=>s.Name).ToArray(),()=>
        {
            EnsureOwned();
            if(ReadText(BackupPath)!=backupText)throw new IOException("The original backup changed during another cursor operation. It was preserved; retry restoration.");
            ApplyBackup(backup);var archive=Path.Combine(Storage.Root,"Backups",$"original-{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");Storage.Write(archive,backup);
            File.Delete(BackupPath);File.Delete(OwnershipPath);commit?.Invoke();Storage.Log("Original cursor configuration restored.");
        },_=>backup);
    }
}
