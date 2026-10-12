using System.Text.Json;

namespace Frost.Core;
public static class Storage
{
    public static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostWhiteComet");
    public static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static void Write<T>(string path,T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(value,Json));File.Move(temp,path,true);
    }
    public static void Log(string message)
    {
        try{Directory.CreateDirectory(Root);var path=Path.Combine(Root,"frost.log");if(File.Exists(path)&&new FileInfo(path).Length>2_000_000)File.Move(path,path+".old",true);File.AppendAllText(path,$"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");}catch(IOException){}
    }
}
