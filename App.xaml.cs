using System.Windows;
namespace Aso4Mail;
public partial class App : Application {
 protected override void OnStartup(StartupEventArgs e){
  MailNotifications.Initialize();
  base.OnStartup(e);
 }
}