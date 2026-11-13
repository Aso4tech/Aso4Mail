
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
namespace Aso4Mail;
public sealed class MailTray:IDisposable{
 readonly NotifyIcon tray;
 bool disposed;
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
 public MailTray(Action restore,Action exit){
  Icon ico;
  try{
   var res=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/aso4mail.png"));
   if(res is null)throw new InvalidOperationException("Missing logo");
   using var bmp=new Bitmap(res.Stream);
   IntPtr h=bmp.GetHicon();
   try{ico=(Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}
  }catch{ico=(Icon)SystemIcons.Application.Clone();}
  var menu=new ContextMenuStrip();
  menu.Items.Add("Open Aso4Mail",null,(_,_)=>restore());
  menu.Items.Add("Quit Aso4Mail",null,(_,_)=>exit());
  tray=new NotifyIcon{Icon=ico,Text="Aso4Mail",Visible=true,ContextMenuStrip=menu};
  tray.DoubleClick+=(_,_)=>restore();
 }
 public void Dispose(){
  if(disposed)return;disposed=true;
  tray.Visible=false;
  tray.ContextMenuStrip?.Dispose();
  tray.Icon?.Dispose();
  tray.Dispose();
 }
}