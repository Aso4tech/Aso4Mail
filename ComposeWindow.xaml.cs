using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
namespace Aso4Mail;
public partial class ComposeWindow:Window{
 readonly MailAccount account;
 readonly List<string> attachments=new();
 const long MaxBytes=50L*1024*1024;
 public ComposeWindow(MailAccount account,string to="",string cc="",string subject="",string body=""){
  InitializeComponent();
  this.account=account;Recipient.Text=to;CcBox.Text=cc;Subject.Text=subject;Body.Text=body;
  UpdateAttachments();
 }
 void UpdateAttachments(){
  Attachments.ItemsSource=null;
  Attachments.ItemsSource=attachments.Select(x=>Path.GetFileName(x)).ToList();
  var total=attachments.Sum(x=>new FileInfo(x).Length);
  AttachmentSummary.Text=$"{attachments.Count} file(s) • {total/1048576.0:F1} MiB / 50 MiB";
 }
 void Attach_Click(object sender,RoutedEventArgs e){
  var picker=new OpenFileDialog{Multiselect=true,Title="Add file attachments"};
  if(picker.ShowDialog(this)!=true)return;
  try{
   var next=attachments.Concat(picker.FileNames).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
   long total=0;foreach(var path in next)checked{total+=new FileInfo(path).Length;}
   if(total>MaxBytes)throw new InvalidOperationException("Total attachments exceed 50 MiB. MIME encoding can make the email larger than the mail server's size limit.");
   attachments.Clear();attachments.AddRange(next);UpdateAttachments();
  }catch(Exception ex){MessageBox.Show(ex.Message,"Aso4Mail");}
 }
 void RemoveAttachment_Click(object sender,RoutedEventArgs e){
  if(Attachments.SelectedIndex is var idx && idx>=0&&idx<attachments.Count){attachments.RemoveAt(idx);UpdateAttachments();}
 }
 async void Send_Click(object sender,RoutedEventArgs e){
  SendButton.IsEnabled=false;DraftButton.IsEnabled=false;
  try{
   var result=await MailService.SendAsync(account,Recipient.Text,CcBox.Text,BccBox.Text,Subject.Text,Body.Text,attachments,CancellationToken.None);
   if(result.Warning is not null)MessageBox.Show(result.Warning,"Mail sent; Sent folder warning",MessageBoxButton.OK,MessageBoxImage.Warning);
   else MessageBox.Show("Message sent and saved to Sent.","Aso4Mail");
   Close();
  }catch(Exception ex){MessageBox.Show("Send failed: "+ex.Message,"Aso4Mail",MessageBoxButton.OK,MessageBoxImage.Warning);}
  finally{SendButton.IsEnabled=true;DraftButton.IsEnabled=true;}
 }
 async void SaveDraft_Click(object sender,RoutedEventArgs e){
  SendButton.IsEnabled=false;DraftButton.IsEnabled=false;
  try{
   await MailService.SaveDraftAsync(account,Recipient.Text,CcBox.Text,BccBox.Text,Subject.Text,Body.Text,attachments,CancellationToken.None);
   MessageBox.Show("Draft saved to the server.","Aso4Mail");
   Close();
  }catch(Exception ex){MessageBox.Show("Could not save draft: "+ex.Message,"Aso4Mail",MessageBoxButton.OK,MessageBoxImage.Warning);}
  finally{SendButton.IsEnabled=true;DraftButton.IsEnabled=true;}
 }
}