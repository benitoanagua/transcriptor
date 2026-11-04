using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
namespace Transcriptor.Desktop;
internal sealed record AvaloniaMiniModeSnapshot(bool IsRecording,bool IsTranscribing,bool IsError,TimeSpan Elapsed,float MicrophoneLevel,float SystemLevel,bool IsPaused,string Status,string SessionTitle,bool IsIdle,bool HasMicrophone,bool HasClient,bool IsPlaying,bool HasPlayback,TimeSpan PlaybackPosition,TimeSpan PlaybackDuration,string PlaybackTrack);
public partial class MiniModeWindow : Window
{
 private readonly MainWindow _main; private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(200)}; private readonly DispatcherTimer _boundsSaveTimer=new(){Interval=TimeSpan.FromMilliseconds(350)}; private bool _allowClose; private bool _updatingSlider; private PixelPoint? _restoredPosition;
 public MiniModeWindow(MainWindow main){_main=main;InitializeComponent();Topmost=main.MiniModeAlwaysOnTop;TopmostToggle.IsChecked=Topmost;Width=Math.Max(MinWidth,main.MiniModeWidth??420);Height=Math.Max(MinHeight,main.MiniModeHeight??150);if(main.MiniModeLeft is double left&&main.MiniModeTop is double top)_restoredPosition=new PixelPoint((int)left,(int)top);_timer.Tick+=(_,_)=>RefreshSnapshot();_boundsSaveTimer.Tick+=(_,_)=>{_boundsSaveTimer.Stop();SaveBounds();};Opened+=(_,_)=>{EnsureVisiblePosition(_restoredPosition);_restoredPosition=null;Screens.Changed+=ScreensChanged;_timer.Start();RefreshSnapshot();};Closed+=(_,_)=>Screens.Changed-=ScreensChanged;PositionChanged+=(_,_)=>DebounceBoundsSave();SizeChanged+=(_,_)=>DebounceBoundsSave();}
 internal void EnsureVisiblePosition(PixelPoint? preferredPosition=null)
 {
  var screens=Screens.All;
  var primary=Screens.Primary?.WorkingArea;
  var scale=RenderScaling;
  var width=Math.Max(1,(int)Math.Ceiling(Width*scale));
  var height=Math.Max(1,(int)Math.Ceiling(Height*scale));
  var requested=preferredPosition??Position;
  Position=MiniModePlacement.GetVisiblePosition(requested,width,height,screens.Select(x=>x.WorkingArea).ToArray(),primary);
 }
 private void ScreensChanged(object? sender,EventArgs e)
 {
  Dispatcher.UIThread.Post(()=>{if(IsVisible)EnsureVisiblePosition();});
 }
 private void InitializeComponent()=>Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
 private void DebounceBoundsSave(){_boundsSaveTimer.Stop();_boundsSaveTimer.Start();}
 private void Root_PointerPressed(object? s,PointerPressedEventArgs e){if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&e.Source is not Button)BeginMoveDrag(e);}
 private void RefreshSnapshot(){AvaloniaMiniModeSnapshot? s;try{s=_main.GetMiniModeSnapshot();}catch(Exception ex){StateText.Text="⚠ ERROR";SessionText.Text="No se pudo actualizar el estado; reintentando…";System.Diagnostics.Debug.WriteLine(ex);return;}if(s is null)return;if(s.IsTranscribing){StateText.Text="⏳ TRANSCRIBIENDO";StateText.Foreground=Avalonia.Media.Brushes.Orange;ElapsedText.Text=string.Empty;RecordingPanel.IsVisible=false;RecordingControls.IsVisible=false;IdlePanel.IsVisible=false;TranscriptionPanel.IsVisible=true;TranscriptionText.Text=s.Status;CancelTranscriptionButton.IsEnabled=!_main.IsMiniModeTranscriptionCancellationRequested;return;}TranscriptionPanel.IsVisible=false;IdlePanel.IsVisible=s.IsIdle;RecordingPanel.IsVisible=!s.IsIdle;RecordingControls.IsVisible=!s.IsIdle;if(s.IsIdle){StateText.Text="● LISTO";StateText.Foreground=Avalonia.Media.Brushes.LightGreen;ElapsedText.Text=string.Empty;IdleSessionText.Text=s.SessionTitle;PlayMicrophoneButton.IsEnabled=s.HasMicrophone;PlayClientButton.IsEnabled=s.HasClient;StopPlaybackButton.IsEnabled=s.IsPlaying;PlaybackSeekSlider.Maximum=Math.Max(1,s.PlaybackDuration.TotalSeconds);PlaybackSeekSlider.IsEnabled=s.HasPlayback&&s.PlaybackDuration>TimeSpan.Zero;if(!PlaybackSeekSlider.IsPointerOver&&!PlaybackSeekSlider.IsFocused){_updatingSlider=true;PlaybackSeekSlider.Value=Math.Clamp(s.PlaybackPosition.TotalSeconds,0,PlaybackSeekSlider.Maximum);_updatingSlider=false;}PlaybackCurrentText.Text=FormatTime(s.PlaybackPosition);PlaybackDurationText.Text=FormatTime(s.PlaybackDuration);PlaybackTrackText.Text=s.HasPlayback?"Reproduciendo: "+s.PlaybackTrack:"Sin reproducción";return;}StateText.Text=s.IsError?"⚠ ERROR":s.IsPaused?"Ⅱ PAUSADO":"● REC";StateText.Foreground=s.IsError?Avalonia.Media.Brushes.OrangeRed:s.IsPaused?Avalonia.Media.Brushes.Orange:Avalonia.Media.Brushes.LightCoral;long sec=Math.Max(0,(long)s.Elapsed.TotalSeconds);ElapsedText.Text=$"{sec/3600:00}:{sec/60%60:00}:{sec%60:00}";MicLevel.Value=Math.Clamp(s.MicrophoneLevel*100,0,100);SystemLevel.Value=Math.Clamp(s.SystemLevel*100,0,100);MicLevelText.Text=$"{MicLevel.Value:0}%";SystemLevelText.Text=$"{SystemLevel.Value:0}%";PauseButton.Content=s.IsPaused?"▶ Reanudar":"Ⅱ Pausar";PauseButton.IsEnabled=!s.IsError;SessionText.Text=s.SessionTitle;}
 private static string FormatTime(TimeSpan t){long s=Math.Max(0,(long)t.TotalSeconds);return $"{s/60:00}:{s%60:00}";}
 private void Pause_Click(object? sender,RoutedEventArgs e){try{if(_main.GetMiniModeSnapshot()?.IsPaused==true)_main.ResumeRecordingFromMiniMode();else _main.PauseRecordingFromMiniMode();}catch(Exception ex){_main.ReportMiniModeError("No se pudo pausar o reanudar: "+ex.Message);}RefreshSnapshot();}
 private async void Stop_Click(object? sender,RoutedEventArgs e){if(!await _main.ConfirmMiniModeStopAsync(this))return;PauseButton.IsEnabled=false;StopButton.IsEnabled=false;try{await _main.StopRecordingFromMiniModeAsync();}catch(Exception ex){_main.ReportMiniModeError("No se pudo detener la grabación: "+ex.Message);}finally{RefreshSnapshot();}}
 private void PlayMicrophone_Click(object? s,RoutedEventArgs e){_main.PlayMicrophoneFromMiniMode();RefreshSnapshot();} private void PlayClient_Click(object? s,RoutedEventArgs e){_main.PlayClientFromMiniMode();RefreshSnapshot();} private void StopPlayback_Click(object? s,RoutedEventArgs e){_main.StopPlaybackFromMiniMode();RefreshSnapshot();}
 private void PlaybackSeekSlider_ValueChanged(object? s,RangeBaseValueChangedEventArgs e){if(!_updatingSlider&&(PlaybackSeekSlider.IsPointerOver||PlaybackSeekSlider.IsFocused)&&_main.GetMiniModeSnapshot()?.HasPlayback==true)_main.SeekPlaybackFromMiniMode(TimeSpan.FromSeconds(PlaybackSeekSlider.Value));}
 private void CancelTranscription_Click(object? s,RoutedEventArgs e){CancelTranscriptionButton.IsEnabled=false;TranscriptionText.Text="Cancelando transcripción…";_main.CancelTranscriptionFromMiniMode();}
 private void Topmost_Click(object? s,RoutedEventArgs e){Topmost=TopmostToggle.IsChecked==true;_main.UpdateMiniModePreferences(Topmost,Position.X,Position.Y,Width,Height);}
 private void Expand_Click(object? s,RoutedEventArgs e)=>_main.ExpandFromMiniMode(this); private void CloseMini_Click(object? s,RoutedEventArgs e)=>_main.CloseMiniModeFromWindow(this);
 private void MiniWindow_KeyDown(object? s,KeyEventArgs e){if(e.KeyModifiers==KeyModifiers.None&&e.Key==Key.Space&&_main.GetMiniModeSnapshot()?.IsRecording==true){Pause_Click(s,new RoutedEventArgs());e.Handled=true;}else if(e.KeyModifiers==KeyModifiers.Control&&e.Key==Key.M){Expand_Click(s,new RoutedEventArgs());e.Handled=true;}else if(e.KeyModifiers==(KeyModifiers.Control|KeyModifiers.Shift)&&e.Key==Key.S&&_main.GetMiniModeSnapshot()?.IsRecording==true){Stop_Click(s,new RoutedEventArgs());e.Handled=true;}}
 private void MiniWindow_Closed(object? s,EventArgs e){_timer.Stop();_boundsSaveTimer.Stop();if(!_allowClose)_main.MiniModeWindowClosed(this);} internal void CloseFromMain(){_allowClose=true;_timer.Stop();_boundsSaveTimer.Stop();SaveBounds();Close();}
 internal void SaveBounds()=>_main.UpdateMiniModePreferences(Topmost,Position.X,Position.Y,Width,Height);
}