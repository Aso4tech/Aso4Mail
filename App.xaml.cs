using System.Windows;
namespace Aso4Mail;
public partial class App : System.Windows.Application {
 protected override void OnStartup(StartupEventArgs e){
  MailNotifications.Initialize();
  base.OnStartup(e);
 }
}