using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Frost.Core;
using Forms=System.Windows.Forms;

internal static class Program
{
    private static string Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","FrostWhiteComet");
    private static string Shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"CURSORIUM.lnk");
    private static string LegacyShortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"FROST - WHITE COMET.lnk");
    private static string UninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet";
    private static readonly string ProductVersion=Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)??"1.1.0";
    private static bool silent;
    private static bool isolated;
    [STAThread]static int Main(string[] args)
    {
        silent=args.Contains("--silent");bool remove=args.Contains("--uninstall")||string.Equals(Path.GetFileName(Environment.ProcessPath),"Uninstall.exe",StringComparison.OrdinalIgnoreCase);
        if(args.Contains("--isolated-test"))
        {
            int option=Array.IndexOf(args,"--test-install-root");if(option<0||option+1>=args.Length)return 2;
            string root=Path.GetFullPath(args[option+1]),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!root.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("CursoriumUpgradeTest-",StringComparison.Ordinal)||!Directory.Exists(root))return 2;
            for(var current=root;current.Length>=temp.TrimEnd(Path.DirectorySeparatorChar).Length;current=Path.GetDirectoryName(current)!)
            {if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return 2;if(current.Equals(temp.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))break;}
            isolated=true;Target=Path.Combine(root,"Program");Shortcut=Path.Combine(root,"CURSORIUM.lnk");LegacyShortcut=Path.Combine(root,"FROST.lnk");UninstallKey+="-"+Path.GetFileName(root);Storage.UseTestingRoot(Path.Combine(root,"Data"));
        }
        else if(args.Contains("--test-install-root"))return 2;
        using var gate=new Mutex(true,@"Local\FrostWhiteComet.Setup",out bool first);
        if(!first){if(!silent)Forms.MessageBox.Show("Another installer is already running.");return 2;}
        try
        {
            if(!isolated)Target=ResolveRegisteredTarget(Target);
            if(args.Contains("--inspect-installation"))
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{target=Target,version=ProductVersion,isolated},Storage.Json));return 0;
            }
            if(!silent&&Forms.MessageBox.Show(remove?"Quit the trail engine, restore the backed-up cursor scheme, and uninstall CURSORIUM. Settings and archived backups will be retained.\n\nContinue?":$"Install CURSORIUM — Cursor & Motion Studio for the current user.\n\nLocation: {Target}\n\nNo administrator rights are required. The cursor theme and sign-in startup are not enabled automatically.",remove?"Uninstall CURSORIUM":"Install CURSORIUM",Forms.MessageBoxButtons.YesNo,Forms.MessageBoxIcon.Question)!=Forms.DialogResult.Yes)return 0;
            if(remove)Uninstall();else Install(!args.Contains("--no-launch"));
            Storage.Write(Path.Combine(Storage.Root,"setup-result.json"),new{action=remove?"uninstall":"install",success=true,at=DateTimeOffset.Now,target=Target});
            if(!silent)Forms.MessageBox.Show(remove?"The original cursor backup has been handled, and the Start menu and startup entries have been removed. Close this message to finish file cleanup. Settings and archived backups are retained.":"Installation complete. Open the control panel to install the native cursor theme.","CURSORIUM — Cursor & Motion Studio");return 0;
        }
        catch(Exception ex){if(args.Contains("--inspect-installation")){Console.Error.WriteLine(ex.Message);return 1;}Storage.Log("Setup: "+ex);Storage.Write(Path.Combine(Storage.Root,"setup-result.json"),new{action=remove?"uninstall":"install",success=false,error=ex.Message});if(!silent)Forms.MessageBox.Show("Operation failed: "+ex.Message+"\nLog: "+Path.Combine(Storage.Root,"frost.log"));return 1;}
        finally{gate.ReleaseMutex();}
    }
    private static string ResolveRegisteredTarget(string defaultTarget)
    {
        using var key=Registry.CurrentUser.OpenSubKey(UninstallKey);
        if(key?.GetValue("InstallLocation") is not string location||string.IsNullOrWhiteSpace(location))return defaultTarget;
        if(!Path.IsPathFullyQualified(location))throw new IOException("The registered installation path must be absolute.");
        var full=Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar);
        if(string.Equals(full,Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||string.Equals(full,Path.GetFullPath(Storage.Root).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||!Directory.Exists(full))throw new IOException("The registered CURSORIUM installation is unavailable or unsafe. Reconnect its drive before retrying.");
        for(var current=full;!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked installation directories are unsupported.");
        var marker=Path.Combine(full,"frost-install.marker");var executable=Path.Combine(full,"FrostWhiteComet.exe");
        if(!File.Exists(marker)||new FileInfo(marker).Length>4096||!File.ReadAllText(marker).StartsWith("CURSORIUM",StringComparison.Ordinal)||!File.Exists(executable)||!((System.Diagnostics.FileVersionInfo.GetVersionInfo(executable).ProductName??"").StartsWith("CURSORIUM",StringComparison.Ordinal)))throw new IOException("The registered directory is not a marked CURSORIUM installation.");
        return full;
    }
    private static void StopApp()
    {
        // An isolated upgrade must never send IPC to the real user's running instance.
        if(isolated)return;
        var exe=Path.Combine(Target,"FrostWhiteComet.exe");if(!File.Exists(exe))return;
        using(var request=Process.Start(new ProcessStartInfo(exe,"--shutdown"){UseShellExecute=false,CreateNoWindow=true}))
        {if(request==null||!request.WaitForExit(6000)||request.ExitCode!=0)throw new IOException("Could not request the running application to quit.");}
        for(int i=0;i<50;i++)
        {
            bool running=Process.GetProcessesByName("FrostWhiteComet").Any(p=>{using(p){try{return string.Equals(p.MainModule?.FileName,exe,StringComparison.OrdinalIgnoreCase);}catch{return true;}}});
            if(!running)return;Thread.Sleep(100);
        }
        throw new IOException("FROST is still running. Quit it from the system tray and retry.");
    }
    private static void Install(bool launch)
    {
        using var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("Frost.Payload.zip")??throw new IOException("The installer payload is missing. Build it with scripts/build-release.ps1.");
        string staging=Path.Combine(Path.GetTempPath(),"FrostSetup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        string old=Target+".previous";
        try
        {
            using(var zip=new ZipArchive(payload,ZipArchiveMode.Read))
            {
                foreach(var entry in zip.Entries)
                {
                    string path=Path.GetFullPath(Path.Combine(staging,entry.FullName));if(!path.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid path in the installer payload.");
                    if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path);
                }
            }
            if(!File.Exists(Path.Combine(staging,"FrostWhiteComet.exe")))throw new IOException("The installer payload is missing the main executable.");
            File.WriteAllText(Path.Combine(staging,"frost-install.marker"),$"CURSORIUM — Cursor & Motion Studio {ProductVersion}");
            if(Directory.Exists(Target)&&!File.Exists(Path.Combine(Target,"frost-install.marker")))throw new IOException("The target directory is not a CURSORIUM installation. Installation has been stopped.");
            if(!Directory.Exists(Target)&&File.Exists(Shortcut))throw new IOException("A Start menu shortcut with this name already exists. Preserve or rename it before retrying.");
            if(Directory.Exists(old))throw new IOException("An unfinished upgrade backup exists: "+old);
            StopApp();Directory.CreateDirectory(Path.GetDirectoryName(Target)!);bool previous=Directory.Exists(Target);if(previous)Directory.Move(Target,old);
            try
            {
                Directory.Move(staging,Target);File.Copy(Environment.ProcessPath!,Path.Combine(Target,"Uninstall.exe"),true);
                CreateShortcut();if(previous&&File.Exists(LegacyShortcut))File.Delete(LegacyShortcut);using var key=Registry.CurrentUser.CreateSubKey(UninstallKey);
                key.SetValue("DisplayName","CURSORIUM — Cursor & Motion Studio");key.SetValue("DisplayVersion",ProductVersion);key.SetValue("Publisher","CURSORIUM Project");key.SetValue("InstallLocation",Target);key.SetValue("DisplayIcon",Path.Combine(Target,"FrostWhiteComet.exe"));key.SetValue("UninstallString",$"\"{Path.Combine(Target,"Uninstall.exe")}\" --uninstall");key.SetValue("QuietUninstallString",$"\"{Path.Combine(Target,"Uninstall.exe")}\" --uninstall --silent");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
                if(!isolated&&StartupRegistration.Enabled)StartupRegistration.Set(true,Path.Combine(Target,"FrostWhiteComet.exe"));
                if(previous)DeleteOwned(old);
            }
            catch{if(Directory.Exists(Target)&&File.Exists(Path.Combine(Target,"frost-install.marker")))DeleteOwned(Target);if(previous&&Directory.Exists(old))Directory.Move(old,Target);throw;}
            if(launch&&!isolated)Process.Start(new ProcessStartInfo(Path.Combine(Target,"FrostWhiteComet.exe")){UseShellExecute=true});
        }
        finally{if(Directory.Exists(staging)){string full=Path.GetFullPath(staging);if(full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("FrostSetup-"))Directory.Delete(full,true);}}
    }
    private static void CreateShortcut()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Shortcut)!);var type=Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("Could not create the Start menu shortcut.");dynamic shell=Activator.CreateInstance(type)!;dynamic link=shell.CreateShortcut(Shortcut);
        try{link.TargetPath=Path.Combine(Target,"FrostWhiteComet.exe");link.WorkingDirectory=Target;link.Description="CURSORIUM — Cursor & Motion Studio";link.IconLocation=Path.Combine(Target,"FrostWhiteComet.exe")+",0";link.Save();}
        finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    private static void DeleteOwned(string directory)
    {
        string full=Path.GetFullPath(directory);if(!string.Equals(full,Path.GetFullPath(Target),StringComparison.OrdinalIgnoreCase)&&!string.Equals(full,Path.GetFullPath(Target+".previous"),StringComparison.OrdinalIgnoreCase))throw new IOException("Deletion outside the owned installation paths was refused.");
        if(!File.Exists(Path.Combine(full,"frost-install.marker")))throw new IOException("The installation marker is missing. Deletion was refused.");Directory.Delete(full,true);
    }
    private static string QuotePs(string value)=>"'"+value.Replace("'","''")+"'";
    private static void Uninstall()
    {
        if(!Directory.Exists(Target))return;
        if(!File.Exists(Path.Combine(Target,"frost-install.marker")))throw new IOException("The installation marker is missing. This directory will not be uninstalled.");
        StopApp();StudioService.RestoreSavedState(new ThemeService()); // abort uninstall if native or state restoration fails
        if(!isolated)StartupRegistration.Set(false,Path.Combine(Target,"FrostWhiteComet.exe"));if(File.Exists(Shortcut))File.Delete(Shortcut);if(File.Exists(LegacyShortcut))File.Delete(LegacyShortcut);Registry.CurrentUser.DeleteSubKeyTree(UninstallKey,false);
        string current=Path.GetFullPath(Environment.ProcessPath!);
        if(current.StartsWith(Target+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        {
            // Running uninstaller cannot delete itself. Hidden, native PowerShell helper only deletes this verified owned directory after we exit.
            string script=$"$frostTarget={QuotePs(Target)}; $frostExpected={QuotePs(Path.GetFullPath(Target))}; while (Get-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 200 }}; if ([IO.Path]::GetFullPath($frostTarget) -eq $frostExpected -and (Test-Path -LiteralPath (Join-Path $frostTarget 'frost-install.marker'))) {{ Remove-Item -LiteralPath $frostTarget -Recurse -Force }}";
            Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true});
        }
        else DeleteOwned(Target);
    }
}
