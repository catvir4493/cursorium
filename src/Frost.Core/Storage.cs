using System.Text.Json;

namespace Frost.Core;
public static class Storage
{
    // Keep the original location so existing settings and cursor backups survive an upgrade.
    private static string? testingRoot;
    public static string Root=>testingRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostWhiteComet");
    public static void UseTestingRoot(string path)=>testingRoot=Path.GetFullPath(path);
    public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
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
        try{Directory.CreateDirectory(Root);var path=Path.Combine(Root,"frost.log");if(File.Exists(path)&&new FileInfo(path).Length>2_000_000)File.Move(path,path+".old",true);File.AppendAllText(path,$"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");}catch(IOException){}
    }
}
