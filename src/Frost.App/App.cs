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
    private Forms.ToolStripMenuItem? pauseItem;
    private StudioService studio=null!;
    private readonly ThemeService theme=new();
    private readonly CancellationTokenSource lifetime=new();
    public bool Exiting{get;private set;}
    internal MainWindow Panel=>panel!;
    internal StudioService Studio=>studio;
    internal Forms.ContextMenuStrip TrayMenu=>tray!.ContextMenuStrip!;
    internal bool PanelCreated=>panel!=null;
    private static bool isolatedTest;
    private static bool stateProbe;
    private static string InstanceId=>ProductIdentity.InstancePrefix+WindowsIdentity.GetCurrent().User!.Value+(isolatedTest?"-verification":"")+(stateProbe?"-state-probe":"");
    [STAThread] public static int Main(string[] args)
    {
        if(args.Contains("--foundation-probe")){
            int i=Array.IndexOf(args,"--test-data-root");if(!args.Contains("--isolated-test")||i<0||i+1>=args.Length)return 2;
            Storage.UseTestingRoot(args[i+1]);Storage.Write(Path.Combine(Storage.Root,"foundation-probe.json"),ProductIdentity.Describe());return 0;
        }
        isolatedTest=args.Contains("--isolated-test")&&(args.Contains("--self-test")||args.Contains("--benchmark")||args.Contains("--state-probe")||args.Contains("--ui-review")||args.Contains("--collection-review")||args.Contains("--export-library")||args.Contains("--export-covers")||args.Contains("--verify-library-public")||args.Contains("--verify-library")||args.Contains("--audit-library")||args.Contains("--prepare-library")||args.Contains("--discover-review"));
        if(args.Contains("--stability-review")){if(!args.Contains("--isolated-test"))return 2;isolatedTest=true;}
        if(args.Contains("--tray-lifecycle-review")){if(!args.Contains("--isolated-test"))return 2;isolatedTest=true;}
        stateProbe=args.Contains("--state-probe");
        if((args.Contains("--self-test")||args.Contains("--benchmark")||args.Contains("--state-probe")||args.Contains("--ui-review")||args.Contains("--collection-review")||args.Contains("--export-library")||args.Contains("--export-covers")||args.Contains("--verify-library-public")||args.Contains("--verify-library")||args.Contains("--audit-library")||args.Contains("--prepare-library")||args.Contains("--discover-review"))&&!isolatedTest)return 2;
        if(isolatedTest)
        {
            int rootIndex=Array.IndexOf(args,"--test-data-root");
            Storage.UseTestingRoot(rootIndex>=0&&rootIndex+1<args.Length?args[rootIndex+1]:Path.Combine(Path.GetTempPath(),"CursoriumVerification-"+Guid.NewGuid().ToString("N")));
        }
        if(args.Contains("--fullscreen-probe"))
        {
            var testApp=new System.Windows.Application();var w=new Window{Title="FROST Fullscreen Probe",WindowStyle=WindowStyle.None,Background=System.Windows.Media.Brushes.MidnightBlue,Topmost=true};
            w.Loaded+=(_,_)=>{var m=Native.Monitors().First(x=>(x.Flags&1)!=0).Monitor;Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(w).Handle,(IntPtr)(-1),m.Left,m.Top,m.Width,m.Height,0x40);w.Activate();};
            var timeout=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};timeout.Tick+=(_,_)=>{timeout.Stop();w.Close();};timeout.Start();testApp.Run(w);return 0;
        }
        using var mutex=new Mutex(true,@"Local\"+InstanceId,out bool first);
        if(!first)
        {
            if(!DistributionProfile.Current.UsesCommunityInstaller)return 2; // Never forward restore/show into a different channel.
            try{using var pipe=new NamedPipeClientStream(".",InstanceId,PipeDirection.Out);pipe.Connect(3000);using var w=new StreamWriter(pipe);w.WriteLine(args.Contains("--shutdown")?"shutdown":args.Contains("--restore-theme")?"restore":"show");return 0;}
            catch(Exception ex){MessageBox.Show("Cannot contact the running instance: "+ex.Message);return 1;}
        }
        try
        {
            if(args.Contains("--verify-library-public")){if(!isolatedTest)return 2;int i=Array.IndexOf(args,"--verify-library-public");LibraryPublisher.VerifyPublic(args[i+1],args[i+2]).GetAwaiter().GetResult();return 0;}
            if(args.Contains("--verify-library")){if(!isolatedTest)return 2;int i=Array.IndexOf(args,"--verify-library");LibraryPublisher.VerifyStage(args[i+1],args[i+2]);return 0;}
            if(args.Contains("--prepare-library")){int i=Array.IndexOf(args,"--prepare-library");LibraryPublisher.Prepare(args[i+1],args[i+2],args[i+3],args[i+4],args[i+5]);return 0;}
            if(args.Contains("--audit-library")){if(!args.Contains("--isolated-test"))return 2;Storage.UseTestingRoot(Path.Combine(Path.GetFullPath(args[Array.IndexOf(args,"--audit-library")+2]),"data"));int i=Array.IndexOf(args,"--audit-library");LibraryPublisher.Audit(args[i+1],args[i+2]);return 0;}
            if(args.Contains("--export-covers")){int i=Array.IndexOf(args,"--export-covers");LibraryPublisher.ExportCovers(args[i+1],args[i+2]);return 0;}
            if(args.Contains("--export-library")){int i=Array.IndexOf(args,"--export-library");LibraryPublisher.Export(args[i+1],args[i+2],args[i+3]);return 0;}
            if(args.Contains("--restore-theme")){StudioService.RestoreSavedState(new ThemeService());return 0;}
            if(args.Contains("--state-probe"))
            {
                var state=new StudioService(AppContext.BaseDirectory,new ThemeService());int reportIndex=Array.IndexOf(args,"--report");
                if(reportIndex<0||reportIndex+1>=args.Length)throw new ArgumentException("State probe needs --report.");
                Storage.Write(args[reportIndex+1],new{state=state.State,profiles=state.Profiles.Profiles,themes=state.Catalog.Themes.Select(t=>t.Id).ToArray()});return 0;
            }
            if(args.Contains("--shutdown"))return 0;
            var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Startup+=(_,_)=>app.Start(args);return app.Run();
        }
        catch(Exception ex){Storage.Log(ex.ToString());if(!args.Any(a=>a is "--verify-library-public" or "--verify-library" or "--audit-library" or "--prepare-library" or "--export-covers" or "--export-library"))MessageBox.Show("FROST could not start: "+ex.Message+"\nLog: "+Path.Combine(Storage.Root,"frost.log"));return 1;}
        finally{mutex.ReleaseMutex();}
    }
    private void Start(string[] args)
    {
        DispatcherUnhandledException+=(_,e)=>{Storage.Log(e.Exception.ToString());e.Handled=true;MessageBox.Show("An error occurred. FROST will exit safely; the installed cursor theme remains.\n"+e.Exception.Message);ExitApp();};
        studio=new StudioService(AppContext.BaseDirectory,theme);settings=studio.LiveEffects;engine=new(settings);
        if(args.Contains("--benchmark")){_ = DesktopVerification.Benchmark(this,engine,args);return;}
        if(args.Contains("--tray-lifecycle-review")){CreateTray();_ = Listen();_ = TrayLifecycleVerification.Run(this,engine,args);return;}
        if(args.Contains("--tray")&&!isolatedTest){CreateTray();_ = Listen();return;}
        panel=new(studio,engine,theme);MainWindow=panel;CreateTray();
        if(args.Contains("--stability-review")){_ = StabilityVerification.Run(this,engine,args);return;}
        if(args.Contains("--discover-review")){_ = DiscoverVerification.Run(this,args);return;}
        if(args.Contains("--collection-review")){_ = CollectionVerification.Run(this,args);return;}
        if(args.Contains("--ui-review")){_ = InterfaceVerification.CapturePages(this,args);return;}
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
                if(command=="shutdown")ExitApp();else if(command=="restore"){studio.RestoreOriginal();panel?.RefreshRestoredCursor();}else ShowPanel();
            }
            catch(OperationCanceledException){break;}
            catch(Exception ex){Storage.Log("IPC: "+ex.Message);await Task.Delay(200);}
        }
    }
    private void CreateTray()
    {
        var menu=new Forms.ContextMenuStrip();menu.Items.Add("Open CURSORIUM",null,(_,_)=>ShowPanel());
        enabledItem=new Forms.ToolStripMenuItem("Enable desktop effects"){Checked=settings.Enabled};enabledItem.Click+=(_,_)=>
        {
            try{studio.SetEffectsEnabled(!settings.Enabled);panel?.RefreshAppliedToggle();RefreshTray();}catch(Exception ex){Storage.Log(ex.ToString());MessageBox.Show("Could not save effects state: "+ex.Message);}
        };menu.Items.Add(enabledItem);
        pauseItem=new Forms.ToolStripMenuItem("Pause effects");pauseItem.Click+=(_,_)=>{engine!.PausedByUser=!engine.PausedByUser;RefreshTray();};menu.Items.Add(pauseItem);
        menu.Items.Add("Restore original cursor theme",null,(_,_)=>{ShowPanel();panel?.RestoreWithConfirmation();});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Quit",null,(_,_)=>ExitApp());
        using var iconStream=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"frost.ico"));
        tray=new Forms.NotifyIcon{Icon=new System.Drawing.Icon(iconStream),Text="CURSORIUM — Cursor & Motion Studio",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(_,_)=>ShowPanel();
    }
    public void RefreshTray(){if(enabledItem!=null)enabledItem.Checked=settings.Enabled;if(pauseItem!=null)pauseItem.Checked=engine?.PausedByUser==true;}
    private void ShowPanel(){if(panel==null){panel=new(studio,engine!,theme);MainWindow=panel;}panel.Refresh();panel.Show();panel.WindowState=WindowState.Normal;panel.Activate();}
    public void ExitApp(int exitCode=0)
    {
        if(Exiting)return;Exiting=true;lifetime.Cancel();panel?.DisposeResources();engine?.Dispose();
        if(tray!=null){tray.Visible=false;tray.Icon?.Dispose();tray.ContextMenuStrip?.Dispose();tray.Dispose();}panel?.Close();Shutdown(exitCode);
    }
}
