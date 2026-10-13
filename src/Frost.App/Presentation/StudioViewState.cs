using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Frost.App.Presentation;

public sealed class StudioViewState:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool canApply,canCancel,isBusy,hasProfile,invalidColor,engineActive,notificationError;
    private string stateLabel="Applied",draftMessage="Your desktop configuration",cursor="",trail="",click="",profile="No saved profile selected",applied="",notification="",runtime="Starting";
    private void Set<T>(ref T field,T value,[CallerMemberName]string name=""){if(EqualityComparer<T>.Default.Equals(field,value))return;field=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));}
    public bool CanApply {get=>canApply;set=>Set(ref canApply,value);}
    public bool CanCancel {get=>canCancel;set=>Set(ref canCancel,value);}
    public bool IsBusy {get=>isBusy;set=>Set(ref isBusy,value);}
    public bool HasProfile {get=>hasProfile;set=>Set(ref hasProfile,value);}
    public bool InvalidColor {get=>invalidColor;set=>Set(ref invalidColor,value);}
    public bool EngineActive {get=>engineActive;set=>Set(ref engineActive,value);}
    public bool NotificationError {get=>notificationError;set=>Set(ref notificationError,value);}
    public string StateLabel {get=>stateLabel;set=>Set(ref stateLabel,value);}
    public string DraftMessage {get=>draftMessage;set=>Set(ref draftMessage,value);}
    public string CursorName {get=>cursor;set=>Set(ref cursor,value);}
    public string TrailName {get=>trail;set=>Set(ref trail,value);}
    public string ClickName {get=>click;set=>Set(ref click,value);}
    public string ProfileCaption {get=>profile;set=>Set(ref profile,value);}
    public string AppliedSummary {get=>applied;set=>Set(ref applied,value);}
    public string Notification {get=>notification;set=>Set(ref notification,value);}
    public string Runtime {get=>runtime;set=>Set(ref runtime,value);}
}
