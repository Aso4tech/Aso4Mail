using System;
using System.Globalization;
using Microsoft.Toolkit.Uwp.Notifications;
using WpfApplication=System.Windows.Application;
namespace Aso4Mail;
public static class MailNotifications {
 public static event Action<string,uint,uint>? OpenMessageRequested;
 public static (string Email,uint Uid,uint Validity)? Pending;
 static bool initialized;
 public static void Initialize(){
  if(initialized)return;initialized=true;
  ToastNotificationManagerCompat.OnActivated+=(args)=>{
   try {
    var data=ToastArguments.Parse(args.Argument);
    var email=data["email"];
    if(string.IsNullOrWhiteSpace(email)||!uint.TryParse(data["uid"],NumberStyles.None,CultureInfo.InvariantCulture,out var uid))return;
    if(!uint.TryParse(data["validity"],NumberStyles.None,CultureInfo.InvariantCulture,out var validity))return;
    Pending=(email,uid,validity);
    WpfApplication.Current?.Dispatcher.BeginInvoke(new Action(()=>OpenMessageRequested?.Invoke(email,uid,validity)));
   }catch { /* Ignore malformed and expired toast activation data. */ }
  };
 }
 public static void Test(){
  Initialize();
  new ToastContentBuilder().AddText("Aso4Mail — notification test")
   .AddText("Notifications have been requested. Check Windows notification settings.").Show();
 }
 public static void Show(MailAccount account,InboxRow mail){
  Initialize();
  try {
   new ToastContentBuilder()
    .AddArgument("email",account.Email)
    .AddArgument("uid",mail.Uid.ToString(CultureInfo.InvariantCulture))
    .AddArgument("validity",mail.UidValidity.ToString(CultureInfo.InvariantCulture))
    .AddText("New email • "+account.Email)
    .AddText(mail.From+" — "+(string.IsNullOrWhiteSpace(mail.Subject)?"New message":mail.Subject))
    .Show();
  }catch { /* Notification failures never interrupt mail synchronization. */ }
 }
}