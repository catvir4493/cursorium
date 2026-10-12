using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Windows;
using Frost.Core;
using Forms=System.Windows.Forms;

namespace Frost.App;
public sealed class App:System.Windows.Application
{
    private Settings settings=null!;
    private TrailEngine? engine;
    private MainWindow? panel;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? enabledItem;
    private readonly ThemeService theme=new();
    private readonly CancellationTokenSource lifetime=new();
    public bool Exiting{get;private set;}
    internal MainWindow Panel=>panel!;
    internal Forms.ContextMenuStrip TrayMenu=>tray!.ContextMenuStrip!;
    private static bool isolatedTest;
    private static string InstanceId=>"FrostWhiteComet-"+WindowsIdentity.GetCurrent().User!.Value+(isolatedTest?"-verification":"");
    [STAThread] public static int Main(string[] args)
    {
        isolatedTest=args.Contains("--isolated-test")&&(args.Contains("--self-test")||args.Contains("--benchmark"));
        if(args.Contains("--fullscreen-probe"))
        {
            var testApp=new System.Windows.Application();var w=new Window{Title="FROST Fullscreen Probe",WindowStyle=WindowStyle.None,Background=System.Windows.Media.Brushes.MidnightBlue,Topmost=true};
            w.Loaded+=(_,_)=>{var m=Native.Monitors().First(x=>(x.Flags&1)!=0).Monitor;Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(w).Handle,(IntPtr)(-1),m.Left,m.Top,m.Width,m.Height,0x40);w.Activate();};
            var timeout=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};timeout.Tick+=(_,_)=>{timeout.Stop();w.Close();};timeout.Start();testApp.Run(w);return 0;
        }
        using var mutex=new Mutex(true,@"Local\"+InstanceId,out bool first);
        if(!first)
        {
            try{using var pipe=new NamedPipeClientStream(".",InstanceId,PipeDirection.Out);pipe.Connect(3000);using var w=new StreamWriter(pipe);w.WriteLine(args.Contains("--shutdown")?"shutdown":args.Contains("--restore-theme")?"restore":"show");return 0;}
            catch(Exception ex){MessageBox.Show("Cannot contact the running instance: "+ex.Message);return 1;}
        }
        try
        {
            if(args.Contains("--restore-theme")){var t=new ThemeService();if(t.HasBackup)t.Restore();return 0;}
            if(args.Contains("--shutdown"))return 0;
            var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Startup+=(_,_)=>app.Start(args);return app.Run();
        }
        catch(Exception ex){Storage.Log(ex.ToString());MessageBox.Show("FROST could not start: "+ex.Message+"\nLog: "+Path.Combine(Storage.Root,"frost.log"));return 1;}
        finally{mutex.ReleaseMutex();}
    }
    private void Start(string[] args)
    {
        DispatcherUnhandledException+=(_,e)=>{Storage.Log(e.Exception.ToString());e.Handled=true;MessageBox.Show("An error occurred. FROST will exit safely; the installed cursor theme remains.\n"+e.Exception.Message);ExitApp();};
        settings=Settings.Load();engine=new(settings);
        if(args.Contains("--benchmark")){_ = DesktopVerification.Benchmark(this,engine,args);return;}
        panel=new(settings,engine,theme);MainWindow=panel;CreateTray();
        if(args.Contains("--self-test")){_ = DesktopVerification.Run(this,engine,args);return;}
        if(!args.Contains("--tray"))panel.Show();_ = Listen();
    }
    private async Task Listen()
    {
        while(!lifetime.IsCancellationRequested)
        {
            try
            {
                using var pipe=new NamedPipeServerStream(InstanceId,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);using var reader=new StreamReader(pipe);var command=await reader.ReadLineAsync(lifetime.Token);
                if(command=="shutdown")ExitApp();else if(command=="restore"){if(theme.HasBackup)theme.Restore();panel?.Refresh();}else ShowPanel();
            }
            catch(OperationCanceledException){break;}
            catch(Exception ex){Storage.Log("IPC: "+ex.Message);await Task.Delay(200);}
        }
    }
    private void CreateTray()
    {
        var menu=new Forms.ContextMenuStrip();menu.Items.Add("Open settings",null,(_,_)=>ShowPanel());
        enabledItem=new Forms.ToolStripMenuItem("Enable trails"){Checked=settings.Enabled};enabledItem.Click+=(_,_)=>{settings.Enabled=!settings.Enabled;settings.Save();panel?.Refresh();RefreshTray();};menu.Items.Add(enabledItem);
        var presets=new Forms.ToolStripMenuItem("Select preset");foreach(var name in new[]{"Minimal","Standard","Glow"})presets.DropDownItems.Add(name,null,(_,_)=>{settings.ApplyPreset(name);settings.Save();panel?.Refresh();});menu.Items.Add(presets);
        menu.Items.Add("Restore original cursor theme",null,(_,_)=>{ShowPanel();panel?.RestoreWithConfirmation();});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Quit",null,(_,_)=>ExitApp());
        using var iconStream=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"frost.ico"));
        tray=new Forms.NotifyIcon{Icon=new System.Drawing.Icon(iconStream),Text="FROST / WHITE COMET",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(_,_)=>ShowPanel();
    }
    public void RefreshTray(){if(enabledItem!=null)enabledItem.Checked=settings.Enabled;}
    private void ShowPanel(){panel?.Refresh();panel?.Show();if(panel!=null){panel.WindowState=WindowState.Normal;panel.Activate();}}
    public void ExitApp(int exitCode=0)
    {
        if(Exiting)return;Exiting=true;lifetime.Cancel();try{settings?.Save();}catch(Exception ex){Storage.Log(ex.Message);}panel?.DisposeResources();engine?.Dispose();
        if(tray!=null){tray.Visible=false;tray.Icon?.Dispose();tray.ContextMenuStrip?.Dispose();tray.Dispose();}panel?.Close();Shutdown(exitCode);
    }
}
