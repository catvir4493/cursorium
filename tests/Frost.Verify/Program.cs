using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using Frost.Core;
internal static class Program
{
    private static int checks;
    private static void Assert(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string source=Path.GetFullPath(args.FirstOrDefault(a=>!a.StartsWith("--"))??"assets/cursors");
            foreach(var role in CursorRoles.All)
            {
                using(var r=new BinaryReader(File.OpenRead(Path.Combine(source,role.Name+".cur"))))
                {
                    Assert(r.ReadUInt16()==0&&r.ReadUInt16()==2&&r.ReadUInt16()==4,role.Name+" CUR header");
                    foreach(int s in CursorArtwork.Sizes)
                    {
                        Assert(r.ReadByte()==s&&r.ReadByte()==s,role.Name+" dimensions");r.ReadUInt16();
                        Assert(r.ReadUInt16()==Math.Round(role.HotX*s/32)&&r.ReadUInt16()==Math.Round(role.HotY*s/32),role.Name+" hotspot directory");r.ReadUInt32();r.ReadUInt32();
                        var h=Native.LoadImage(IntPtr.Zero,Path.Combine(source,role.Name+".cur"),2,s,s,0x10);
                        Assert(h!=IntPtr.Zero,$"{role.Name} LoadImage {s}");
                        try{CheckHandle(h,role,s);}finally{Native.DestroyCursor(h);}
                    }
                }
                if(role.Animated)
                {
                    using var r=new BinaryReader(File.OpenRead(Path.Combine(source,role.FileName)));
                    Assert(new string(r.ReadChars(4))=="RIFF"&&r.ReadUInt32()==r.BaseStream.Length-8&&new string(r.ReadChars(4))=="ACON",role.Name+" RIFF");
                    var h=Native.LoadCursorFromFile(Path.Combine(source,role.FileName));Assert(h!=IntPtr.Zero,role.Name+" animated native load");Native.DestroyCursor(h);
                }
            }
            if(args.Contains("--install-roundtrip"))
            {
                var theme=new ThemeService();Assert(!theme.HasBackup,"Refuse to replace an existing backup during verification");var before=theme.Snapshot();
                try
                {
                    theme.Install(source);Assert(theme.IsInstalled,"Actual registry install");foreach(var role in CursorRoles.All){var shared=Native.LoadCursor(IntPtr.Zero,(IntPtr)role.SystemId);Assert(shared!=IntPtr.Zero,role.Name+" system role loaded");}
                    int engineIndex=Array.IndexOf(args,"--engine-exe");
                    if(engineIndex>=0)
                    {
                        var exe=Path.GetFullPath(args[engineIndex+1]);using var engine=Process.Start(new ProcessStartInfo(exe,"--tray"){UseShellExecute=false});System.Threading.Thread.Sleep(1200);Assert(engine!=null&&!engine.HasExited,"Release trail process starts");
                        using var shutdown=Process.Start(new ProcessStartInfo(exe,"--shutdown"){UseShellExecute=false});Assert(shutdown!=null&&shutdown.WaitForExit(6000)&&shutdown.ExitCode==0,"Shutdown IPC succeeds");Assert(engine!.WaitForExit(6000),"Trail process exits and releases windows");Assert(theme.IsInstalled,"Native theme remains installed after trail process exit");
                    }
                    Console.WriteLine("Native install succeeded; restoring immediately.");
                }
                finally{if(theme.HasBackup)theme.Restore();}
                var after=theme.Snapshot();Assert(JsonSerializer.Serialize(before.Cursors)==JsonSerializer.Serialize(after.Cursors)&&JsonSerializer.Serialize(before.Scheme)==JsonSerializer.Serialize(after.Scheme),"Exact registry restoration including value types and absent values");Assert(!theme.HasBackup,"Backup archived after restoration");
            }
            var settings=new Settings();var model=new TrailModel();model.Add(0,0,0,settings);for(int i=1;i<=100;i++)model.Add(i*10,0,i*8,settings);
            double distance=0;for(int i=1;i<model.Points.Count;i++){var a=model.Points[i-1];var b=model.Points[i];distance+=Math.Sqrt(Math.Pow(b.X-a.X,2)+Math.Pow(b.Y-a.Y,2));}
            Assert(distance<=settings.Length+.001,"Trail length cap follows actual arc length");Assert(model.Points.Count<=256,"History is bounded");
            model.Add(5000,5000,810,settings);Assert(model.Points.Count==1,"Teleport clears connecting trail");
            for(int i=0;i<50;i++)model.Click(i,i,810);Assert(model.Halos.Count==8,"Rapid click halos are bounded");model.Expire(1200,settings);Assert(!model.HasContent,"Expired trail and halos removed");
            model.Add(5,5,1300,settings);model.Add(5,5,1310,settings);Assert(model.Points.Count==1,"Stationary pointer does not accumulate history");
            settings.Length=double.NaN;settings.Opacity=double.PositiveInfinity;settings.Duration=-5;settings.Particles=999;settings.Color="invalid";settings.Normalize();Assert(settings.Length==110&&settings.Opacity==.4&&settings.Duration==80&&settings.Particles==24&&settings.Color=="#91DEFF","Corrupt configuration is normalized");
            settings.ApplyPreset("Standard");Assert(settings.Length==110&&settings.Duration==260&&settings.Opacity==.4&&settings.Particles==10,"Standard defaults");
            settings.Preset="\u6781\u7b80";settings.Length=73;settings.Normalize();Assert(settings.Preset=="Minimal"&&settings.Length==73,"Legacy preset identifiers migrate without changing custom parameters");
            if(args.Contains("--install-only"))
            {
                var theme=new ThemeService();Assert(!theme.HasBackup,"Fixture installation refuses an existing backup");theme.Install(source);Assert(theme.IsInstalled&&theme.HasBackup,"Active theme installed for subsequent uninstaller verification");
            }
            int comparison=Array.IndexOf(args,"--compare-backup");
            if(comparison>=0)
            {
                var expected=JsonSerializer.Deserialize<ThemeBackup>(File.ReadAllText(args[comparison+1]))!;var current=new ThemeService().Snapshot();
                Assert(JsonSerializer.Serialize(expected.Cursors)==JsonSerializer.Serialize(current.Cursors)&&JsonSerializer.Serialize(expected.Scheme)==JsonSerializer.Serialize(current.Scheme),"Uninstaller restored exact original cursor registry values and types");
            }
            Console.WriteLine($"PASS: {checks} checks; {Native.Monitors().Count} current monitor(s). {Environment.OSVersion}");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void CheckHandle(IntPtr h,CursorRole role,int s)
    {
        Assert(Native.GetIconInfo(h,out var info),role.Name+" GetIconInfo");
        try{Assert(info.HotX==Math.Round(role.HotX*s/32)&&info.HotY==Math.Round(role.HotY*s/32),$"{role.Name} actual hotspot {s}: {info.HotX},{info.HotY}");Assert(Native.GetObject(info.Color,Marshal.SizeOf<Native.Bitmap>(),out var bitmap)>0&&bitmap.Width==s&&bitmap.Height==s,role.Name+" actual bitmap size");}
        finally{if(info.Mask!=IntPtr.Zero)Native.DeleteObject(info.Mask);if(info.Color!=IntPtr.Zero)Native.DeleteObject(info.Color);}
    }
}
