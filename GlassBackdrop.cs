using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace EdgeTime;
// A click-through native composition window lives directly behind each WPF window.
// The compositor samples the real desktop and the native effect applies Gaussian blur.
internal sealed class GlassBackdrop : IDisposable {
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct QueueOptions {public int Size,Thread,Apartment;}
 [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(QueueOptions options,out IntPtr controller);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowExW(int ex,string cls,string title,int style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr inst,IntPtr param);
 [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int x1,int y1,int x2,int y2,int w,int h);
 [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
 [DllImport("GlassEffect.dll")] static extern int CreateBlur(IntPtr compositor,out IntPtr result);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateTarget(IntPtr instance,IntPtr hwnd,int topmost,out IntPtr target);
 static IntPtr queue;
 readonly Window window; readonly IntPtr front,back;
 readonly Compositor compositor; readonly DesktopWindowTarget target; readonly SpriteVisual visual; readonly CompositionEffectBrush brush; readonly CompositionRoundedRectangleGeometry clipShape; readonly CompositionGeometricClip clip;
 int oldWidth,oldHeight; bool disposed,morphing;
 public GlassBackdrop(Window w){
  window=w; front=new WindowInteropHelper(w).Handle;
  if(queue==IntPtr.Zero){var options=new QueueOptions{Size=12,Thread=2,Apartment=2};int result=CreateDispatcherQueueController(options,out queue);if(result<0)Marshal.ThrowExceptionForHR(result);}
  back=CreateWindowExW(0x00200000|0x00000080|0x08000000|0x00000020|0x00000008,"Static","",unchecked((int)0x80000000),0,0,1,1,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
  if(back==IntPtr.Zero)throw new InvalidOperationException("无法创建磨砂背景窗口。");
  try {
   compositor=new Compositor(); var raw=MarshalInspectable<Compositor>.FromManaged(compositor); IntPtr interop=IntPtr.Zero,targetPtr=IntPtr.Zero,brushPtr=IntPtr.Zero;
   try{var iid=new Guid("29E691FA-4567-4DCA-B319-D0F207EB6807");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(raw,in iid,out interop));var vtable=Marshal.ReadIntPtr(interop);var create=Marshal.GetDelegateForFunctionPointer<CreateTarget>(Marshal.ReadIntPtr(vtable,3*IntPtr.Size));Marshal.ThrowExceptionForHR(create(interop,back,1,out targetPtr));target=MarshalInterface<DesktopWindowTarget>.FromAbi(targetPtr);Marshal.ThrowExceptionForHR(CreateBlur(raw,out brushPtr));brush=MarshalInterface<CompositionEffectBrush>.FromAbi(brushPtr);}
   finally{if(brushPtr!=IntPtr.Zero)Marshal.Release(brushPtr);if(targetPtr!=IntPtr.Zero)Marshal.Release(targetPtr);if(interop!=IntPtr.Zero)Marshal.Release(interop);if(raw!=IntPtr.Zero)Marshal.Release(raw);}
   visual=compositor.CreateSpriteVisual(); visual.Brush=brush;clipShape=compositor.CreateRoundedRectangleGeometry();clip=compositor.CreateGeometricClip(clipShape);visual.Clip=clip;target.Root=visual;
   window.LocationChanged+=Changed;window.SizeChanged+=Changed;window.IsVisibleChanged+=VisibilityChanged;window.Closed+=Closed;
   Sync();
  } catch {DestroyWindow(back);throw;}
 }
 void Closed(object? s,EventArgs e)=>Dispose();
 void Changed(object? s,EventArgs e)=>Sync();
 void VisibilityChanged(object s,DependencyPropertyChangedEventArgs e)=>Sync();
 void Sync(){if(disposed)return;if(!window.IsVisible){ShowWindow(back,0);return;}if(!GetWindowRect(front,out var r))return;int w=Math.Max(1,r.Right-r.Left),h=Math.Max(1,r.Bottom-r.Top);if(!morphing)visual.Size=new Vector2(w,h);if(w!=oldWidth||h!=oldHeight){int radius=(int)((window.Width<100?28:32)*GetDpiForWindow(front)/96.0);if(!morphing)clipShape.Size=new Vector2(w,h);clipShape.CornerRadius=new Vector2(radius/2f);SetWindowRgn(back,CreateRoundRectRgn(0,0,w+1,h+1,radius,radius),true);oldWidth=w;oldHeight=h;}SetWindowPos(back,front,r.Left,r.Top,w,h,0x0010|0x0040);}
 public void PrepareMorph(double width,double height,double x,double y){if(disposed)return;float dpi=GetDpiForWindow(front)/96f;morphing=true;visual.Size=new Vector2((float)width*dpi,(float)height*dpi);clipShape.Size=visual.Size;visual.Offset=new Vector3((float)x*dpi,(float)y*dpi,0);}
 // Animate only compositor visuals; the native and WPF windows remain at their final bounds.
 public void Morph(double startWidth,double startHeight,double startX,double startY,bool expand,int milliseconds){
  if(disposed||!GetWindowRect(front,out var r))return;float dpi=GetDpiForWindow(front)/96f;var full=new Vector2(r.Right-r.Left,r.Bottom-r.Top);var small=new Vector2((float)startWidth*dpi,(float)startHeight*dpi);var offset=new Vector3((float)startX*dpi,(float)startY*dpi,0);var fromSize=expand?small:full;var toSize=expand?full:small;var fromOffset=expand?offset:Vector3.Zero;var toOffset=expand?Vector3.Zero:offset;
  morphing=true;var easing=compositor.CreateCubicBezierEasingFunction(new Vector2(1f/3f,1),new Vector2(2f/3f,1));var size=compositor.CreateVector2KeyFrameAnimation();size.Duration=TimeSpan.FromMilliseconds(milliseconds);size.InsertKeyFrame(0,fromSize);size.InsertKeyFrame(1,toSize,easing);var position=compositor.CreateVector3KeyFrameAnimation();position.Duration=size.Duration;position.InsertKeyFrame(0,fromOffset);position.InsertKeyFrame(1,toOffset,easing);var radius=compositor.CreateVector2KeyFrameAnimation();radius.Duration=size.Duration;radius.InsertKeyFrame(0,new Vector2((expand?14:16)*dpi));radius.InsertKeyFrame(1,new Vector2((expand?16:14)*dpi),easing);visual.StartAnimation("Size",size);clipShape.StartAnimation("Size",size);clipShape.StartAnimation("CornerRadius",radius);visual.StartAnimation("Offset",position);
 }
 public void ResetMorph(){if(disposed)return;morphing=false;visual.StopAnimation("Size");visual.StopAnimation("Offset");clipShape.StopAnimation("Size");clipShape.StopAnimation("CornerRadius");visual.Offset=Vector3.Zero;oldWidth=0;oldHeight=0;Sync();}
 public void Dispose(){if(disposed)return;disposed=true;window.LocationChanged-=Changed;window.SizeChanged-=Changed;window.IsVisibleChanged-=VisibilityChanged;window.Closed-=Closed;target.Root=null;brush.Dispose();visual.Dispose();clip.Dispose();clipShape.Dispose();target.Dispose();compositor.Dispose();DestroyWindow(back);}
}
