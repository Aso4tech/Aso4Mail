using System.Windows;
using Microsoft.Win32;
namespace Aso4Mail;
public partial class ComposeWindow : Window {
 private readonly MailAccount account;
 private readonly List<string> attachments=new();
 private const long MaxBytes=50L*1024*1024;
 public ComposeWindow(MailAccount account,string to="",string subject="",string body=""){
  InitializeComponent();this.account=account;Recipient.Text=to;Subject.Text=subject;Body.Text=body;
 }
 private void Attach_Click(object sender,RoutedEventArgs e){
  var d=new OpenFileDialog{Multiselect=true,Title="Choose attachments"};
  if(d.ShowDialog(this)!=true)return;
  var candidate=attachments.Concat(d.FileNames).Distinct().ToList();
  try{
   long sum=0;foreach(var f in candidate)checked{sum+=new FileInfo(f).Length;}
   if(sum>MaxBytes)throw new InvalidOperationException("Attachments exceed 50 MiB.");
   attachments.Clear();attachments.AddRange(candidate);
   AttachmentSummary.Text=$"{attachments.Count} file(s) • {sum/1048576.0:F1} MiB";
  }catch(Exception ex){MessageBox.Show(ex.Message);}
 }
 private async void Send_Click(object sender,RoutedEventArgs e){
  if(string.IsNullOrWhiteSpace(Recipient.Text)){MessageBox.Show("Recipient required.");return;}
  SendButton.IsEnabled=false;
  try{
   await MailService.SendAsync(account,Recipient.Text,Subject.Text,Body.Text,attachments,CancellationToken.None);
   MessageBox.Show("SMTP server accepted message. This MVP does not yet save a copy in Sent.");
   Close();
  }catch(Exception ex){MessageBox.Show("Send failed: "+ex.Message);SendButton.IsEnabled=true;}
 }
}