using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WpfApplication=System.Windows.Application;
namespace Aso4Mail;
public sealed class MailTray:IDisposable {
 readonly NotifyIcon icon;
 bool disposed;
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
 public MailTray(Action restore,Action quit) {
  Icon picture;
  try {
   var resource=WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/aso4mail.png"));
   if(resource is null)throw new InvalidOperationException("Missing icon");
   using var bitmap=new Bitmap(resource.Stream);
   var h=bitmap.GetHicon();
   try {picture=(Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}
  }catch {picture=(Icon)SystemIcons.Application.Clone();}
  var menu=new ContextMenuStrip();
  menu.Items.Add("Open Aso4Mail",null,(_,_)=>restore());
  menu.Items.Add("Quit Aso4Mail",null,(_,_)=>quit());
  icon=new NotifyIcon{Icon=picture,Text="Aso4Mail",Visible=true,ContextMenuStrip=menu};
  icon.DoubleClick+=(_,_)=>restore();
 }
 public void Dispose(){
  if(disposed)return;disposed=true;
  icon.Visible=false;icon.ContextMenuStrip?.Dispose();icon.Icon?.Dispose();icon.Dispose();
 }
}