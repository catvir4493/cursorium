using System.Text.Json;

namespace Frost.Core;
public static class Storage
{
    // Keep the original location so existing settings and cursor backups survive an upgrade.
    private static string? testingRoot;
    public static string Root=>testingRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostWhiteComet");
    public static void UseTestingRoot(string path)
    {
        var full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var live=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostWhiteComet");
        if(full.Length==0||string.Equals(full,Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||string.Equals(full,live,StringComparison.OrdinalIgnoreCase)||full.StartsWith(live+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||live.StartsWith(full+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Diagnostic data must be isolated from the real user data and volume roots.");
        using(var registration=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet"))
            if(registration?.GetValue("InstallLocation") is string location)
            {var installed=Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar);if(full.Equals(installed,StringComparison.OrdinalIgnoreCase)||full.StartsWith(installed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Diagnostic data must not be stored in the installed program directory.");}
        DownloadManager.GuardRoot(full);testingRoot=full;
    }
    public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
    private static readonly System.Text.RegularExpressions.Regex localPaths=new("(?:[A-Za-z]:[\\\\/]|\\\\\\\\)[^\\r\\n\"<>|]*",System.Text.RegularExpressions.RegexOptions.NonBacktracking);
    private static readonly System.Text.RegularExpressions.Regex secrets=new("(Bearer\\s+|(?:token|password|secret)\\s*[:=]\\s*)[^\\s;,]+",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.NonBacktracking);
    internal static string SanitizeDiagnostic(string message)=>secrets.Replace(localPaths.Replace(message.Length>8192?message[..8192]:message,"[local-path]"),"$1[redacted]");
    public static void Write<T>(string path,T value)
    {
        WriteText(path,JsonSerializer.Serialize(value,Json));
    }
    public static void WriteText(string path,string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            { using(var writer=new StreamWriter(file,leaveOpen:true)){writer.Write(value);writer.Flush();}file.Flush(true); }
            File.Move(temp,path,true);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static void Log(string message)
    {
        try{Directory.CreateDirectory(Root);var path=Path.Combine(Root,"frost.log");if(File.Exists(path)&&new FileInfo(path).Length>2_000_000)File.Move(path,path+".old",true);File.AppendAllText(path,$"{DateTimeOffset.Now:O} {SanitizeDiagnostic(message)}{Environment.NewLine}");}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){}
    }
}
