
using System;
using System.Globalization;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
namespace Aso4Mail;

/// <summary>
/// Windows-native app notifications. No PowerShell, no remote push server, no autostart.
/// Handles activation on the WPF dispatcher. Windows can suppress alerts based on Settings.
/// </summary>
public static class MailNotifications {
 public static event Action<string,uint>? OpenMessageRequested;
 static bool initialized;
 public static void Initialize(){
  if(initialized)return;
  initialized=true;
  ToastNotificationManagerCompat.OnActivated+=(args)=>{
   try{
    var kv=ToastArguments.Parse(args.Argument);
    var email=kv["email"];
    var uidText=kv["uid"];
    if(string.IsNullOrWhiteSpace(email)||!uint.TryParse(uidText,NumberStyles.None,CultureInfo.InvariantCulture,out var uid))return;
    Application.Current?.Dispatcher.BeginInvoke(new Action(()=>OpenMessageRequested?.Invoke(email,uid)));
   }catch(Exception){ /* Ignore invalid or expired activation parameters. */ }
  };
 }
 public static void Test(){
  Initialize();
  new ToastContentBuilder().AddText("Aso4Mail").AddText("Windows notifications are enabled for this application.").Show();
 }
 public static void Show(MailAccount account,InboxRow message){
  if(!initialized)Initialize();
  try{
   new ToastContentBuilder()
    .AddArgument("email",account.Email)
    .AddArgument("uid",message.Uid.ToString(CultureInfo.InvariantCulture))
    .AddText("New mail • "+account.Email)
    .AddText(string.IsNullOrWhiteSpace(message.Subject)?"New message":message.Subject)
    .Show();
  }catch(Exception){ /* Notification delivery is optional; never interrupt mail sync. */ }
 }
}