using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MailKit;
using Microsoft.Win32;
namespace Aso4Mail;
public partial class MainWindow : Window{
 readonly List<MailAccount> accounts;
 IReadOnlyList<InboxRow> loaded=Array.Empty<InboxRow>();
 IReadOnlyList<MailFolderRow> folders=Array.Empty<MailFolderRow>();
 MessageDetail? detail;
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(30)};
 bool refreshing,refreshPending,suppressSelection;
 int generation;
 MailAccount? CurrentAccount=>AccountPicker.SelectedItem as MailAccount;
 MailFolderRow? CurrentFolder=>FolderList.SelectedItem as MailFolderRow;
 InboxRow? CurrentMessage=>Messages.SelectedItem as InboxRow;
 public MainWindow(){
  InitializeComponent();
  try{accounts=AccountStore.Load();}catch{accounts=new();MessageBox.Show("Saved account settings could not be decrypted. The old data was not overwritten.","Aso4Mail",MessageBoxButton.OK,MessageBoxImage.Warning);}
  AccountPicker.ItemsSource=accounts;
  timer.Tick+=async(_,_)=>await RefreshMessagesAsync(true);
  timer.Start();
  Closed+=(_,_)=>timer.Stop();
  if(accounts.Count>0)AccountPicker.SelectedIndex=0;
 }
 async Task LoadFoldersAsync(){
  if(CurrentAccount is null)return;
  var account=CurrentAccount;int gen=generation;
  StatusText.Text="Loading folders...";
  try{
   var rows=await MailService.ListFoldersAsync(account,CancellationToken.None);
   if(gen!=generation||!ReferenceEquals(account,CurrentAccount))return;
   folders=rows;
   suppressSelection=true;
   FolderList.ItemsSource=rows;
   FolderList.SelectedItem=rows.FirstOrDefault(x=>x.Role=="Inbox")??rows.FirstOrDefault();
   suppressSelection=false;
   if(CurrentFolder is not null)await RefreshMessagesAsync();
   else StatusText.Text="No folders available";
  }catch(Exception e){StatusText.Text="Could not load folders: "+e.Message;}
 }
 async Task RefreshMessagesAsync(bool silent=false){
  var account=CurrentAccount;var folder=CurrentFolder;
  if(account is null||folder is null)return;
  if(refreshing){refreshPending=true;return;}
  refreshing=true;int gen=generation;
  if(!silent)StatusText.Text="Synchronizing "+folder.Name+"...";
  try{
   var rows=await MailService.ListMessagesAsync(account,folder.FullName,CancellationToken.None);
   if(gen!=generation||!ReferenceEquals(account,CurrentAccount)||CurrentFolder?.FullName!=folder.FullName)return;
   bool unchanged=loaded.Count==rows.Count&&loaded.Zip(rows).All(x=>x.First.Uid==x.Second.Uid&&x.First.Folder==x.Second.Folder&&x.First.IsRead==x.Second.IsRead&&x.First.IsFlagged==x.Second.IsFlagged&&x.First.Subject==x.Second.Subject);
   if(!unchanged){
    var uid=CurrentMessage?.Uid;
    loaded=rows;DrawList(uid);
   }
   FolderCount.Text=$"{rows.Count} recent messages";
   StatusText.Text="Last synchronized "+DateTime.Now.ToString("HH:mm:ss");
  }catch(Exception ex){
   StatusText.Text="Sync failed — "+ex.Message;
   if(!silent)MessageBox.Show(ex.Message,"IMAP error",MessageBoxButton.OK,MessageBoxImage.Warning);
  }finally{
   refreshing=false;
   if(refreshPending){refreshPending=false;_=RefreshMessagesAsync(true);}
  }
 }
 void DrawList(uint? selected=null){
  suppressSelection=true;
  try{
   var q=SearchBox.Text.Trim();
   var result=loaded.Where(r=>(UnreadFilter.IsChecked!=true||!r.IsRead)&&(q.Length==0||r.From.Contains(q,StringComparison.OrdinalIgnoreCase)||r.Subject.Contains(q,StringComparison.OrdinalIgnoreCase)||r.To.Contains(q,StringComparison.OrdinalIgnoreCase))).ToList();
   Messages.ItemsSource=result;
   if(selected is uint uid)Messages.SelectedItem=result.FirstOrDefault(x=>x.Uid==uid);
  }finally{suppressSelection=false;}
 }
 async void AccountPicker_SelectionChanged(object sender,SelectionChangedEventArgs e){
  generation++;folders=Array.Empty<MailFolderRow>();loaded=Array.Empty<InboxRow>();
  FolderList.ItemsSource=null;Messages.ItemsSource=null;ClearMessage();
  if(CurrentAccount is not null)await LoadFoldersAsync();
 }
 async void FolderList_SelectionChanged(object sender,SelectionChangedEventArgs e){
  if(suppressSelection)return;
  generation++;loaded=Array.Empty<InboxRow>();Messages.ItemsSource=null;ClearMessage();
  FolderTitle.Text=CurrentFolder?.Name??"Inbox";
  FolderCount.Text="";
  await RefreshMessagesAsync();
 }
 void Filter_Changed(object sender,RoutedEventArgs e){if(IsLoaded)DrawList(CurrentMessage?.Uid);}
 void SearchBox_TextChanged(object sender,TextChangedEventArgs e){if(IsLoaded)DrawList(CurrentMessage?.Uid);}
 async void Refresh_Click(object sender,RoutedEventArgs e)=>await RefreshMessagesAsync();
 async void Messages_SelectionChanged(object sender,SelectionChangedEventArgs e){
  if(suppressSelection||CurrentMessage is null||CurrentAccount is null||CurrentFolder is null)return;
  var account=CurrentAccount;var folder=CurrentFolder;var row=CurrentMessage;int gen=generation;
  ClearMessage();SubjectText.Text=row.Subject;FromText.Text=row.From;ToText.Text="To: "+row.To;
  MessageBody.Text="Loading message...";
  try{
   var msg=await MailService.ReadMessageAsync(account,folder.FullName,row.Uid,CancellationToken.None);
   if(gen!=generation||!ReferenceEquals(account,CurrentAccount)||CurrentMessage?.Uid!=row.Uid||CurrentFolder?.FullName!=folder.FullName)return;
   detail=msg;MessageBody.Text=msg.Text;AttachmentPicker.ItemsSource=msg.Attachments;
   if(msg.Attachments.Count>0)AttachmentPicker.SelectedIndex=0;
   HtmlNotice.Text=string.IsNullOrWhiteSpace(msg.Html)?"No HTML body; view the plain-text message.":"HTML is available, but active HTML rendering remains disabled until its security review is completed. Use Plain text.";
  }catch(Exception ex){MessageBody.Text="Unable to read message: "+ex.Message;}
 }
 void ClearMessage(){detail=null;SubjectText.Text="Select an email";FromText.Text="";ToText.Text="";MessageBody.Text="";AttachmentPicker.ItemsSource=null;HtmlNotice.Text="Select an email to view its body.";}
 async void AddAccount_Click(object sender,RoutedEventArgs e){
  var w=new AccountWindow{Owner=this};
  if(w.ShowDialog()!=true)return;
  try{
   var email=AccountStore.ValidateEmail(w.Address);
   var candidate=new MailAccount{Email=email,Password=w.Secret};
   await MailService.ListFoldersAsync(candidate,CancellationToken.None);
   var previous=accounts.FindIndex(a=>a.Email.Equals(email,StringComparison.OrdinalIgnoreCase));
   if(previous>=0)candidate.Signature=accounts[previous].Signature;
   if(previous>=0)accounts[previous]=candidate;else accounts.Add(candidate);
   AccountStore.Save(accounts);AccountPicker.Items.Refresh();AccountPicker.SelectedItem=candidate;
  }catch(Exception ex){MessageBox.Show("Unable to add account: "+ex.Message,"Aso4Mail",MessageBoxButton.OK,MessageBoxImage.Warning);}
 }
 void Compose_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null){MessageBox.Show("Add an email account first.");return;}
  new ComposeWindow(CurrentAccount){Owner=this}.ShowDialog();
 }
 void Reply_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null||CurrentMessage is null||detail is null)return;
  var row=CurrentMessage;
  new ComposeWindow(CurrentAccount,row.SenderAddress,"",row.Subject.StartsWith("Re:",StringComparison.OrdinalIgnoreCase)?row.Subject:"Re: "+row.Subject,"\n\n------ Original message ------\n"+detail.Text){Owner=this}.ShowDialog();
 }
 void ReplyAll_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null||CurrentMessage is null||detail is null)return;
  var row=CurrentMessage;
  var to=row.SenderAddress;
  var cc=detail.To; // keep original distribution, avoid duplicate current account
  new ComposeWindow(CurrentAccount,to,cc,row.Subject.StartsWith("Re:",StringComparison.OrdinalIgnoreCase)?row.Subject:"Re: "+row.Subject,"\n\n------ Original message ------\n"+detail.Text){Owner=this}.ShowDialog();
 }
 void Forward_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null||CurrentMessage is null||detail is null)return;
  var row=CurrentMessage;
  new ComposeWindow(CurrentAccount,"","",row.Subject.StartsWith("Fwd:",StringComparison.OrdinalIgnoreCase)?row.Subject:"Fwd: "+row.Subject,"\n\n------ Forwarded message ------\nFrom: "+detail.From+"\nTo: "+detail.To+"\n\n"+detail.Text){Owner=this}.ShowDialog();
 }
 async Task WithMessageAsync(Func<MailAccount,MailFolderRow,InboxRow,Task> action){
  if(CurrentAccount is null||CurrentFolder is null||CurrentMessage is null)return;
  var a=CurrentAccount;var f=CurrentFolder;var r=CurrentMessage;
  try{await action(a,f,r);await RefreshMessagesAsync();}catch(Exception ex){MessageBox.Show(ex.Message,"Aso4Mail",MessageBoxButton.OK,MessageBoxImage.Warning);}
 }
 async void Archive_Click(object sender,RoutedEventArgs e)=>await MoveToRoleAsync("Archive");
 async void Delete_Click(object sender,RoutedEventArgs e)=>await MoveToRoleAsync("Trash");
 async Task MoveToRoleAsync(string role){
  var dest=folders.FirstOrDefault(f=>f.Role==role);
  if(dest is null){MessageBox.Show(role+" folder was not found on this server.");return;}
  if(CurrentFolder?.FullName==dest.FullName)return;
  await WithMessageAsync((a,f,r)=>MailService.MoveAsync(a,f.FullName,r.Uid,dest.FullName,CancellationToken.None));
 }
 async void ReadToggle_Click(object sender,RoutedEventArgs e)=>await WithMessageAsync((a,f,r)=>MailService.SetFlagAsync(a,f.FullName,r.Uid,MessageFlags.Seen,!r.IsRead,CancellationToken.None));
 async void Flag_Click(object sender,RoutedEventArgs e)=>await WithMessageAsync((a,f,r)=>MailService.SetFlagAsync(a,f.FullName,r.Uid,MessageFlags.Flagged,!r.IsFlagged,CancellationToken.None));
 async void SaveEml_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null||CurrentFolder is null||CurrentMessage is null)return;
  var dialog=new SaveFileDialog{Title="Save original email",Filter="Email files|*.eml",FileName="message.eml"};
  if(dialog.ShowDialog(this)!=true)return;
  if(File.Exists(dialog.FileName)){MessageBox.Show("Choose a new file name to avoid overwriting existing files.");return;}
  try{await MailService.SaveEmlAsync(CurrentAccount,CurrentFolder.FullName,CurrentMessage.Uid,dialog.FileName,CancellationToken.None);StatusText.Text="Email saved.";}catch(Exception ex){MessageBox.Show(ex.Message);}
 }
 async void SaveAttachment_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null||CurrentFolder is null||CurrentMessage is null||AttachmentPicker.SelectedItem is not MailAttachmentRow part)return;
  var suggested=Path.GetFileName(part.Name);
  var dialog=new SaveFileDialog{Title="Save attachment",FileName=suggested};
  if(dialog.ShowDialog(this)!=true)return;
  if(File.Exists(dialog.FileName)){MessageBox.Show("Choose a new file name to avoid overwriting existing files.");return;}
  try{await MailService.SaveAttachmentAsync(CurrentAccount,CurrentFolder.FullName,CurrentMessage.Uid,part.Index,dialog.FileName,CancellationToken.None);StatusText.Text="Attachment saved.";}catch(Exception ex){MessageBox.Show(ex.Message);}
 }
 void Signature_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null)return;
  var w=new SignatureWindow(CurrentAccount.Signature){Owner=this};
  if(w.ShowDialog()!=true)return;
  CurrentAccount.Signature=w.Signature;AccountStore.Save(accounts);
 }
 void Website_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("https://aso4tech.com"){UseShellExecute=true});
}