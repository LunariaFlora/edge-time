using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
namespace EdgeTime;
internal static class DesktopVisibility {
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr h,ref MonitorInfo info);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder name,int count);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr h,int index,int value);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out Rect rect,int size);
 public static void ToolWindow(Window window){window.ShowInTaskbar=false;var h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;int style=GetWindowLong(h,-20);SetWindowLong(h,-20,(style|0x80)&~0x40000);SetWindowPos(h,IntPtr.Zero,0,0,0,0,0x37);}
 public static bool FullscreenOn(Window window){
  var front=GetForegroundWindow();if(front==IntPtr.Zero||IsIconic(front))return false;
  GetWindowThreadProcessId(front,out uint pid);if(pid==(uint)Environment.ProcessId)return false;
  var name=new StringBuilder(256);GetClassName(front,name,name.Capacity);if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")return false;
  return CoversMonitor(front,new WindowInteropHelper(window).Handle);
 }
 internal static bool CoversMonitor(IntPtr front,IntPtr target){
  var monitor=MonitorFromWindow(front,2);if(monitor!=MonitorFromWindow(target,2))return false;
  var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfo(monitor,ref info))return false;
  if(DwmGetWindowAttribute(front,9,out var rect,Marshal.SizeOf<Rect>())!=0&&!GetWindowRect(front,out rect))return false;
  var screen=info.Monitor;
  // Compare the full monitor, not its work area: ordinary maximized windows stay visible.
  return rect.Left<=screen.Left+2&&rect.Top<=screen.Top+2&&rect.Right>=screen.Right-2&&rect.Bottom>=screen.Bottom-2;
 }
}
public partial class MainWindow {
 bool fullscreenHidden,pendingFullscreenReminder;
 readonly List<Window> fullscreenAuxiliary=new();
 void UpdateFullscreenVisibility(){
  if(tab==null)return;
  bool hide=DesktopVisibility.FullscreenOn(tab);
  if(hide){
   fullscreenHidden=true;
   if(expanded||transitioning){expanded=false;transitioning=false;StopWatchingClicks();MorphSurface.BeginAnimation(WidthProperty,null);MorphSurface.BeginAnimation(HeightProperty,null);MorphSurface.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty,null);MorphSurface.BeginAnimation(System.Windows.Controls.Canvas.TopProperty,null);MorphSurface.BeginAnimation(OpacityProperty,null);ShellBorder.BeginAnimation(OpacityProperty,null);ShellBorder.Opacity=1;MorphSurface.Visibility=Visibility.Collapsed;mainGlass?.ResetMorph();}
   Hide();tab.Hide();
   foreach(Window window in Application.Current.Windows)if(window!=this&&window!=tab&&window.IsVisible){fullscreenAuxiliary.Add(window);window.Hide();}
  }else if(fullscreenHidden){
   fullscreenHidden=false;
   if(!transitioning){expanded=false;StopWatchingClicks();Hide();mainGlass?.ResetMorph();PlaceTab();tab.Show();}
   foreach(var window in fullscreenAuxiliary.ToArray())if(Application.Current.Windows.Cast<Window>().Contains(window))window.Show();
   fullscreenAuxiliary.Clear();
   if(pendingFullscreenReminder){pendingFullscreenReminder=false;ShowReminder();}
  }
 }
}
