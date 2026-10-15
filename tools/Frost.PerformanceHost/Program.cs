using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

// The same host loads unchanged released assemblies on their exact self-contained
// runtime. No Apply, native cursor writer, real user data, or in-process input loop.
internal static class Program
{
    private const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private static readonly Stopwatch Clock=Stopwatch.StartNew();
    private static readonly Dictionary<string,double> Markers=new();
    private static string Value(string[] args,string key,string fallback=""){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    private static object? Field(object value,string name)=>value.GetType().GetField(name,Members)?.GetValue(value);
    private static double Number(object? value,string name)=>value?.GetType().GetProperty(name,Members)?.GetValue(value) is object n?Convert.ToDouble(n,CultureInfo.InvariantCulture):0;
    private static void Set(object settings,string name,object value)=>settings.GetType().GetProperty(name)!.SetValue(settings,value);
    [STAThread] private static int Main(string[] args)
    {
        // Explicit epoch: beforefieldinit can otherwise defer this field's first
        // initialization until after assembly loading. Process creation/CLR startup
        // remain separate external observations, never inferred from this clock.
        Clock.Restart();
        try
        {
            if(args.Contains("--compare-implementation"))return CompareImplementation(args);
            if(args.Contains("--drive"))return Drive(args);
            string root=Path.GetFullPath(Value(args,"--data")),report=Path.GetFullPath(Value(args,"--report")),mode=Value(args,"--mode","idle");
            int warm=int.Parse(Value(args,"--warmup","5")),seconds=int.Parse(Value(args,"--seconds","20"));
            if(warm<1||warm>120||seconds<5||seconds>259200)throw new ArgumentException("Invalid observation duration.");
            var live=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostWhiteComet");
            if(root.Equals(live,StringComparison.OrdinalIgnoreCase)||root.StartsWith(live+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||live.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetPathRoot(root)==root||Directory.Exists(root))throw new InvalidDataException("A new, isolated data root is mandatory.");
            for(string? p=root;p!=null;p=Path.GetDirectoryName(p))if(Directory.Exists(p)&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked diagnostic storage rejected.");
            using(var installation=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet"))
                if(installation?.GetValue("InstallLocation") is string installed){installed=Path.GetFullPath(installed).TrimEnd(Path.DirectorySeparatorChar);if(root.Equals(installed,StringComparison.OrdinalIgnoreCase)||root.StartsWith(installed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Diagnostic data must not be written into the installed application.");}
            Directory.CreateDirectory(root);Directory.CreateDirectory(report);
            using var mutex=new Mutex(true,@"Local\FrostWhiteComet-"+WindowsIdentity.GetCurrent().User!.Value,out bool first);
            if(!first)throw new InvalidOperationException("Close the production application before exclusive measurement.");
            AssemblyLoadContext.Default.Resolving+=(_,name)=>{var path=Path.Combine(AppContext.BaseDirectory,name.Name+".dll");return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;};
            var core=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Frost.Core.dll"));core.GetType("Frost.Core.Storage")!.GetMethod("UseTestingRoot")!.Invoke(null,new object[]{root});
            var target=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"FrostWhiteComet.dll"));Markers["mainAssemblyLoadedMs"]=Clock.Elapsed.TotalMilliseconds;
            if(mode=="state")
            {
                var native=Activator.CreateInstance(core.GetType("Frost.Core.ThemeService")!)!;
                _=Activator.CreateInstance(core.GetType("Frost.Core.StudioService")!,AppContext.BaseDirectory,native);
                Markers["stateLoadedMs"]=Clock.Elapsed.TotalMilliseconds;
                File.WriteAllText(Path.Combine(report,"state.json"),JsonSerializer.Serialize(new{markers=Markers,managedMainEpochExplicit=true,runtime=RuntimeInformation.FrameworkDescription,assembly=target.GetName().Version?.ToString()},Json));mutex.ReleaseMutex();return 0;
            }
            bool harness=mode.StartsWith("harness-",StringComparison.Ordinal);System.Windows.Application app=harness?new System.Windows.Application():(System.Windows.Application)Activator.CreateInstance(target.GetType("Frost.App.App")!)!;
            app.ShutdownMode=ShutdownMode.OnExplicitShutdown;int exit=0;
            app.Startup+=async(_,_)=>
            {
                try{await Observe(app,mode,warm,seconds,report,args);}
                catch(Exception ex){File.WriteAllText(Path.Combine(report,"failure.txt"),ex.ToString());exit=1;}
                finally{if(!harness)app.GetType().GetMethod("ExitApp")!.Invoke(app,new object[]{exit});else app.Shutdown(exit);}
            };
            app.Run();mutex.ReleaseMutex();return exit;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static async Task Observe(System.Windows.Application app,string mode,int warm,int seconds,string report,string[] args)
    {
        bool harness=mode.StartsWith("harness-",StringComparison.Ordinal);using var wakeLifetime=new CancellationTokenSource();long timerWakes=0;
        async Task WakeLoop(){while(!wakeLifetime.IsCancellationRequested){await Task.Delay(8);timerWakes++;}}
        if(mode=="harness-timer8")_ = WakeLoop();
        if(!harness)app.GetType().GetMethod("Start",Members)!.Invoke(app,new object[]{new[]{"--tray"}});
        Markers["trayStartupReturnedMs"]=Clock.Elapsed.TotalMilliseconds;
        bool trayShellLocated=!harness&&FindTrayRect(Field(app,"tray"));if(trayShellLocated)Markers["trayShellRectangleAvailableMs"]=Clock.Elapsed.TotalMilliseconds;
        var engine=harness?null:Field(app,"engine");var settings=harness?null:Field(app,"settings");
        bool initialPanel=!harness&&Field(app,"panel")!=null;
        if(settings!=null)
        {
            Set(settings,"Enabled",false);Set(settings,"TrailEnabled",true);Set(settings,"PauseFullscreen",false);
            Set(settings,"TrailEffect",mode is "minimal" or "neon" or "stardust"?mode:"comet");
            Set(settings,"Length",110d);Set(settings,"Duration",260d);Set(settings,"Opacity",.40d);Set(settings,"Particles",10);
            Set(settings,"Color","#91DEFF");Set(settings,"FrameRate",mode=="target120"?120:60);Set(settings,"PowerSaving",mode=="power30");
            Set(settings,"ClickHalo",true);Set(settings,"ClickEffect","halo");Set(settings,"HaloDuration",180d);
            Set(settings,"Enabled",mode!="disabled");
        }
        string intervention="none";
        if(mode=="comet-no-paint"&&engine!=null)
        {
            intervention="Diagnostic only: original sampling/model/render scheduling, Painter collapsed; no visual-quality acceptance claim";
            foreach(var window in (System.Collections.IEnumerable)engine.GetType().GetProperty("Windows")!.GetValue(engine)!)
                ((FrameworkElement)window!.GetType().GetProperty("Painter")!.GetValue(window)!).Visibility=Visibility.Collapsed;
        }
        if(mode=="comet-no-render"&&engine!=null)
        {
            intervention="Diagnostic only: original sampling/model, render callback detached; not a shippable optimization";
            engine.GetType().GetMethod("AttachRender",Members)!.Invoke(engine,null);
            var renderCallback=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),engine,engine.GetType().GetMethod("Render",Members)!);
            System.Windows.Media.CompositionTarget.Rendering-=renderCallback;
        }
        if(mode=="opened-closed")
        {
            double begin=Clock.Elapsed.TotalMilliseconds;app.GetType().GetMethod("ShowPanel",Members)!.Invoke(app,null);
            var panel=(Window)Field(app,"panel")!;Markers["firstShowReturnedMs"]=Clock.Elapsed.TotalMilliseconds-begin;
            var painted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);panel.ContentRendered+=(_,_)=>painted.TrySetResult();
            await painted.Task.WaitAsync(TimeSpan.FromSeconds(15));Markers["firstContentRenderedMs"]=Clock.Elapsed.TotalMilliseconds-begin;
            await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Input);Markers["firstInputDispatcherReadyMs"]=Clock.Elapsed.TotalMilliseconds-begin;
            panel.Close();
        }
        File.WriteAllText(Path.Combine(report,"ready.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,mode,runtime=RuntimeInformation.FrameworkDescription,initialPanel,managedMainEpochExplicit=true,markers=Markers},Json));
        using var process=Process.GetCurrentProcess();using var csv=new StreamWriter(Path.Combine(report,"samples.csv"));
        csv.WriteLine("phase,seconds,cpuOneCorePercent,cpuMachinePercent,machineBusyPercent,workingSet,privateBytes,gdi,user,threads,allocatedBytesPerSecond,gen0,gen1,gen2,samplerHz,renderHz,paintHz,paintWindowMeanMs,pointerAvailable,pointerChanged");
        bool paintTimingAvailable=engine?.GetType().GetProperty("Windows")?.GetValue(engine) is System.Collections.IEnumerable painters&&painters.Cast<object>().Any(w=>w.GetType().GetProperty("Painter")?.GetValue(w)?.GetType().GetProperty("PaintTicks")!=null);
        var rows=new List<object>();var timer=Stopwatch.StartNew();process.Refresh();double last=0,lastCpu=process.TotalProcessorTime.TotalSeconds;
        GetSystemTimes(out var lastIdle,out var lastKernel,out var lastUser);
        long allocation=GC.GetTotalAllocatedBytes(false);var collections=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};Dictionary<int,double>? threadBefore=null;double threadWindowStart=0;
        double sample=Number(engine,"Samples"),render=Number(engine,"RenderedFrames");var priorPaint=Paint(engine);bool priorPointAvailable=GetPhysicalCursorPos(out var point);
        while(timer.Elapsed.TotalSeconds<warm+seconds)
        {
            await Task.Delay(1000);process.Refresh();double now=timer.Elapsed.TotalSeconds,dt=now-last,cpu=process.TotalProcessorTime.TotalSeconds;
            if((GetAsyncKeyState(0x1B)&0x8000)!=0)throw new OperationCanceledException("Performance observation cancelled with Escape; no result is accepted.");
            GetSystemTimes(out var idle,out var kernel,out var userTime);double total=(kernel-lastKernel)+(userTime-lastUser);double machineBusy=total>0?(1-(idle-lastIdle)/total)*100:0;lastIdle=idle;lastKernel=kernel;lastUser=userTime;
            if(last>=warm&&threadBefore==null){threadWindowStart=timer.Elapsed.TotalSeconds;threadBefore=ThreadTimes(process);}
            long allocated=GC.GetTotalAllocatedBytes(false);var gc=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
            var paint=Paint(engine);double paintCount=paint.count-priorPaint.count;double? paintMs=paintTimingAvailable&&paintCount>0?(paint.ticks-priorPaint.ticks)*1000d/Stopwatch.Frequency/paintCount:null;
            double samples=Number(engine,"Samples"),renders=Number(engine,"RenderedFrames");bool pointerAvailable=GetPhysicalCursorPos(out var next);
            var phase=last>=warm?"stable":"warmup";var row=new{phase,seconds=now,cpuOneCorePercent=(cpu-lastCpu)/dt*100,cpuMachinePercent=(cpu-lastCpu)/dt/Environment.ProcessorCount*100,machineBusyPercent=machineBusy,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1),threads=process.Threads.Count,allocatedBytesPerSecond=(allocated-allocation)/dt,gen0=gc[0]-collections[0],gen1=gc[1]-collections[1],gen2=gc[2]-collections[2],samplerHz=(samples-sample)/dt,renderHz=(renders-render)/dt,paintHz=paintTimingAvailable?(double?)(paintCount/dt):null,paintWindowMeanMs=paintMs,pointerAvailable,pointerChanged=priorPointAvailable&&pointerAvailable&&(next.X!=point.X||next.Y!=point.Y)};
            rows.Add(row);csv.WriteLine(string.Join(',',new object?[]{row.phase,row.seconds,row.cpuOneCorePercent,row.cpuMachinePercent,row.machineBusyPercent,row.workingSet,row.privateBytes,row.gdi,row.user,row.threads,row.allocatedBytesPerSecond,row.gen0,row.gen1,row.gen2,row.samplerHz,row.renderHz,row.paintHz,row.paintWindowMeanMs,row.pointerAvailable,row.pointerChanged}.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture))));csv.Flush();
            last=now;lastCpu=cpu;allocation=allocated;collections=gc;sample=samples;render=renders;priorPaint=paint;point=next;priorPointAvailable=pointerAvailable;
        }
        wakeLifetime.Cancel();var threadAfter=ThreadTimes(process);double threadWindowSeconds=timer.Elapsed.TotalSeconds-threadWindowStart;var modules=process.Modules.Cast<ProcessModule>().Select(m=>new{start=m.BaseAddress.ToInt64(),end=m.BaseAddress.ToInt64()+m.ModuleMemorySize,name=m.ModuleName}).ToArray();
        string StartModule(int id)
        {
            IntPtr handle=OpenThread(0x0840,false,(uint)id);
            if(handle==IntPtr.Zero)return "unavailable";
            try
            {
                // ProcessThread.StartAddress can be a generic startup thunk.
                // Query the actual Win32 start address of this process's thread.
                if(NtQueryInformationThread(handle,9,out var start,IntPtr.Size,IntPtr.Zero)!=0)return "unresolved";
                long address=start.ToInt64();return modules.FirstOrDefault(m=>address>=m.start&&address<m.end)?.name??"unresolved";
            }
            finally{CloseHandle(handle);}
        }
        var threads=threadAfter.Select(p=>new{id=p.Key,uiThread=p.Key==GetCurrentThreadId(),startModule=StartModule(p.Key),cpuMilliseconds=(p.Value-(threadBefore?.GetValueOrDefault(p.Key)??p.Value))*1000}).OrderByDescending(p=>p.cpuMilliseconds).ToArray();
        File.WriteAllText(Path.Combine(report,"metrics.json"),JsonSerializer.Serialize(new{passed=true,mode,intervention,runtime=RuntimeInformation.FrameworkDescription,started=DateTimeOffset.UtcNow-timer.Elapsed,ended=DateTimeOffset.UtcNow,warmupSeconds=warm,requestedStableSeconds=seconds,initialPanel,trayShellLocated,paintTimingAvailable,managedMainEpochExplicit=true,markers=Markers,timerWakes,threadWindowSeconds,threadCpu=threads,samples=rows,notes="Production App.Start/TrailEngine, isolated writable data; same reflection host for each released assembly/runtime. Diagnostic interventions are explicitly identified and must never be used as visual-quality acceptance results. 1 Hz observation overhead remains included. Paint timings are CPU means per sampling window, not individual-frame P95 or GPU presentation; absent counters are null. No cursor Apply. External input driver measured separately. Thread snapshots omit threads that exited between snapshots and have their own recorded duration, shorter than the whole stable observation."},Json));
    }
    private static Dictionary<int,double> ThreadTimes(Process process)
    {
        var result=new Dictionary<int,double>();foreach(ProcessThread thread in process.Threads){try{result[thread.Id]=thread.TotalProcessorTime.TotalSeconds;}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}return result;
    }
    // Release commits change informational-version metadata. Compare actual IL,
    // signatures, user-string heap and embedded resources before reusing measured
    // candidate results for that final build. This is not native-code equivalence.
    private static int CompareImplementation(string[] args)
    {
        static object Read(string path)
        {
            using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();
            static string Hash(byte[] bytes)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var methods=new SortedDictionary<string,string>(StringComparer.Ordinal);
            var fields=new SortedDictionary<string,string>(StringComparer.Ordinal);
            string TypeName(TypeDefinitionHandle handle){var type=metadata.GetTypeDefinition(handle);var parent=type.GetDeclaringType();return parent.IsNil?metadata.GetString(type.Namespace)+"."+metadata.GetString(type.Name):TypeName(parent)+"+"+metadata.GetString(type.Name);}
            foreach(var handle in metadata.TypeDefinitions)
            {
                var type=metadata.GetTypeDefinition(handle);string name=TypeName(handle);
                foreach(var fieldHandle in type.GetFields()){var field=metadata.GetFieldDefinition(fieldHandle);fields.Add(name+"::"+metadata.GetString(field.Name),((int)field.Attributes).ToString(CultureInfo.InvariantCulture)+":"+Convert.ToHexString(metadata.GetBlobBytes(field.Signature)));}
                foreach(var methodHandle in type.GetMethods())
                {
                    var method=metadata.GetMethodDefinition(methodHandle);string key=name+"::"+metadata.GetString(method.Name)+":"+Convert.ToHexString(metadata.GetBlobBytes(method.Signature));
                    if(method.RelativeVirtualAddress==0){var import=method.GetImport();methods[key]="no-il:"+method.Attributes+":"+method.ImplAttributes+":"+(import.Module.IsNil?"":metadata.GetString(metadata.GetModuleReference(import.Module).Name)+":"+metadata.GetString(import.Name)+":"+import.Attributes);continue;}
                    var body=pe.GetMethodBody(method.RelativeVirtualAddress);using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);
                    writer.Write((int)method.Attributes);writer.Write((int)method.ImplAttributes);writer.Write(body.MaxStack);writer.Write(body.LocalVariablesInitialized);writer.Write(System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(body.LocalSignature));
                    var locals=body.LocalSignature.IsNil?Array.Empty<byte>():metadata.GetBlobBytes(metadata.GetStandaloneSignature(body.LocalSignature).Signature);writer.Write(locals.Length);writer.Write(locals);
                    var il=body.GetILBytes()!;writer.Write(il.Length);writer.Write(il);writer.Write(body.ExceptionRegions.Length);
                    foreach(var region in body.ExceptionRegions){writer.Write((int)region.Kind);writer.Write(region.TryOffset);writer.Write(region.TryLength);writer.Write(region.HandlerOffset);writer.Write(region.HandlerLength);writer.Write(region.FilterOffset);writer.Write(System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(region.CatchType));}
                    writer.Flush();methods.Add(key,Hash(bytes.ToArray()));
                }
            }
            // Raw #US heap also covers ldstr constants: equal IL tokens alone
            // would miss a changed literal stored at the same metadata offset.
            byte[] block=pe.GetMetadata().GetContent().ToArray();using var metadataStream=new MemoryStream(block);using var reader=new BinaryReader(metadataStream);
            if(reader.ReadUInt32()!=0x424A5342)throw new InvalidDataException("Invalid managed metadata header.");
            metadataStream.Position=12;uint versionLength=reader.ReadUInt32();metadataStream.Position=(16+versionLength+3)&~3L;
            reader.ReadUInt16();int streams=reader.ReadUInt16();string? userStrings=null;
            for(int i=0;i<streams;i++){int offset=reader.ReadInt32(),length=reader.ReadInt32();var name=new System.Text.StringBuilder();byte next;while((next=reader.ReadByte())!=0)name.Append((char)next);metadataStream.Position=(metadataStream.Position+3)&~3L;if(name.ToString()=="#US"){if(offset<0||length<0||offset>block.Length-length)throw new InvalidDataException("Invalid user-string heap.");userStrings=Hash(block.AsSpan(offset,length).ToArray());}}
            var resource=pe.PEHeaders.CorHeader!.ResourcesDirectory;
            string resourceHash=Hash(resource.Size==0?Array.Empty<byte>():pe.GetSectionData(resource.RelativeVirtualAddress).GetContent(0,resource.Size).ToArray());
            var references=metadata.AssemblyReferences.Select(h=>{var r=metadata.GetAssemblyReference(h);return new{name=metadata.GetString(r.Name),version=r.Version.ToString(),culture=metadata.GetString(r.Culture),key=Convert.ToHexString(metadata.GetBlobBytes(r.PublicKeyOrToken)),flags=(int)r.Flags};}).ToArray();
            return new{methods,fields,references,userStrings,resourceHash};
        }
        string left=Path.GetFullPath(Value(args,"--left")),right=Path.GetFullPath(Value(args,"--right")),report=Path.GetFullPath(Value(args,"--report"));
        object a=Read(left),b=Read(right);bool same=JsonSerializer.Serialize(a,Json)==JsonSerializer.Serialize(b,Json);
        File.WriteAllText(report,JsonSerializer.Serialize(new{passed=same,leftFile=Path.GetFileName(left),rightFile=Path.GetFileName(right),leftSHA256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(left))),rightSHA256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(right))),implementation=a,rightImplementation=same?null:b,notes="Managed IL, method signatures/exception regions, user-string constants and embedded resources. Informational-version metadata is intentionally excluded. Does not prove identical runtime JIT/native code or replace final release smoke/regression tests."},Json));
        return same?0:1;
    }
    private static bool FindTrayRect(object? tray)
    {
        if(tray==null)return false;var type=tray.GetType();var id=type.GetField("_id",Members)?.GetValue(tray)??type.GetField("id",Members)?.GetValue(tray);var window=type.GetField("_window",Members)?.GetValue(tray)??type.GetField("window",Members)?.GetValue(tray);
        if(id is not (int or uint)||window?.GetType().GetProperty("Handle")?.GetValue(window) is not IntPtr handle)return false;
        var identifier=new NotifyIdentifier{Size=(uint)Marshal.SizeOf<NotifyIdentifier>(),Window=handle,Id=Convert.ToUInt32(id,CultureInfo.InvariantCulture)};return Shell_NotifyIconGetRect(ref identifier,out _)==0;
    }
    private static (double count,double ticks) Paint(object? engine)
    {
        double count=0,ticks=0;if(engine?.GetType().GetProperty("Windows")?.GetValue(engine) is System.Collections.IEnumerable windows)
            foreach(var window in windows){var painter=window!.GetType().GetProperty("Painter")!.GetValue(window);count+=Number(painter,"PaintCount");ticks+=Number(painter,"PaintTicks");}return(count,ticks);
    }
    private static int Drive(string[] args)
    {
        string report=Path.GetFullPath(Value(args,"--report"));Directory.CreateDirectory(report);int duration=int.Parse(Value(args,"--seconds","30"));bool clicking=Value(args,"--input")=="fast-click";
        bool manual=Value(args,"--input")=="natural";if(!GetPhysicalCursorPos(out var original))throw new InvalidOperationException("Interactive cursor position is unavailable; no input injected.");var foreground=GetForegroundWindow();using var process=Process.GetCurrentProcess();
        var form=new System.Windows.Forms.Form{Text="CURSORIUM performance input target — Escape stops",Width=650,Height=360,StartPosition=System.Windows.Forms.FormStartPosition.Manual,Left=80,Top=80,TopMost=true};
        IntPtr inputWindow=IntPtr.Zero;Rect inputRectangle=default;int observed=0,moves=0,stopRequested=0;form.MouseDown+=(_,_)=>Interlocked.Increment(ref observed);form.MouseMove+=(_,_)=>Interlocked.Increment(ref moves);form.FormClosing+=(_,_)=>Interlocked.Exchange(ref stopRequested,1);var thread=new Thread(()=>
        {
            Thread.Sleep(300);var clock=Stopwatch.StartNew();double cpu=process.TotalProcessorTime.TotalSeconds;int events=0,inputFailures=0;bool aborted=false;
            while(clock.Elapsed.TotalSeconds<duration)
            {
                if(Volatile.Read(ref stopRequested)!=0||(GetAsyncKeyState(0x1B)&0x8000)!=0){aborted=true;break;}
                if(manual){Thread.Sleep(50);continue;}
                double t=clock.Elapsed.TotalSeconds*(clicking?3:1);int x=390+(int)(Math.Sin(t*7)*180),y=240+(int)(Math.Cos(t*10)*80);bool positioned=SetPhysicalCursorPos(x,y);if(!positioned)inputFailures++;
                if(clicking&&events%4==0)
                {
                    // Fail the workload instead of clicking another foreground or
                    // occluding topmost window. All measurements use this same driver.
                    if(!positioned||GetForegroundWindow()!=inputWindow||WindowFromPoint(new Point{X=x,Y=y})!=inputWindow){inputFailures++;}
                    else{var inputs=new[]{new Input{Type=0,Mouse=new MouseInput{Flags=2}},new Input{Type=0,Mouse=new MouseInput{Flags=4}}};if(SendInput(2,inputs,Marshal.SizeOf<Input>())!=2)inputFailures++;}
                }
                events++;Thread.Sleep(8);
            }
            bool endPointAvailable=GetPhysicalCursorPos(out var endPoint);process.Refresh();File.WriteAllText(Path.Combine(report,"driver.json"),JsonSerializer.Serialize(new{passed=!aborted&&inputFailures==0,inputFailures,seconds=clock.Elapsed.TotalSeconds,cpuOneCorePercent=(process.TotalProcessorTime.TotalSeconds-cpu)/clock.Elapsed.TotalSeconds*100,cpuMachinePercent=(process.TotalProcessorTime.TotalSeconds-cpu)/clock.Elapsed.TotalSeconds/Environment.ProcessorCount*100,events,observedMoves=Volatile.Read(ref moves),observedClicks=Volatile.Read(ref observed),inputWindowBounds=new{inputRectangle.Left,inputRectangle.Top,inputRectangle.Right,inputRectangle.Bottom},inputForegroundAtEnd=GetForegroundWindow()==inputWindow,pointAvailableAtEnd=endPointAvailable,pointOverOwnedInputAtEnd=endPointAvailable&&WindowFromPoint(endPoint)==inputWindow,input=manual?"Manual physical input; no synthetic events":clicking?"SetPhysicalCursorPos + SendInput left clicks":"SetPhysicalCursorPos",notes="Separate process; programmatic movement is not claimed to be physical HID input. Clicks require the owned foreground/hit-tested input window. Event counts cover the whole driver interval, including warmup. Geometry describes only the generated input target, never another application's title/content."},Json));try{if(!form.IsDisposed)form.BeginInvoke(new Action(()=>form.Close()));}catch(InvalidOperationException)when(Volatile.Read(ref stopRequested)!=0){}
        }){IsBackground=true};
        form.Shown+=(_,_)=>{inputWindow=form.Handle;if(!GetWindowRect(inputWindow,out inputRectangle)){File.WriteAllText(Path.Combine(report,"driver.json"),JsonSerializer.Serialize(new{passed=false,reason="Input target geometry unavailable; no events injected."},Json));form.Close();return;}thread.Start();};try{System.Windows.Forms.Application.Run(form);}finally{Interlocked.Exchange(ref stopRequested,1);if(thread.IsAlive)thread.Join();SetPhysicalCursorPos(original.X,original.Y);if(foreground!=IntPtr.Zero)SetForegroundWindow(foreground);form.Dispose();}return 0;
    }
    [StructLayout(LayoutKind.Sequential)]private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Explicit)]private struct Input{[FieldOffset(0)]public uint Type;[FieldOffset(8)]public MouseInput Mouse;}
    [DllImport("user32.dll")]private static extern bool GetPhysicalCursorPos(out Point p);
    [DllImport("user32.dll")]private static extern bool SetPhysicalCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int kind);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("kernel32.dll")]private static extern int GetCurrentThreadId();
    [DllImport("kernel32.dll")]private static extern IntPtr OpenThread(uint access,bool inherit,uint id);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr handle);
    [DllImport("ntdll.dll")]private static extern int NtQueryInformationThread(IntPtr thread,int information,out IntPtr start,int size,IntPtr returnedLength);
    [DllImport("kernel32.dll")]private static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [StructLayout(LayoutKind.Sequential)]private struct NotifyIdentifier{public uint Size;public IntPtr Window;public uint Id;public Guid Guid;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("shell32.dll")]private static extern int Shell_NotifyIconGetRect(ref NotifyIdentifier identifier,out Rect rect);
}
