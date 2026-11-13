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
using Microsoft.Web.WebView2.Core;
using MimeKit;
namespace Aso4Mail;
public partial class MainWindow : Window{
 readonly List<MailAccount> accounts;
 IReadOnlyList<InboxRow> loaded=Array.Empty<InboxRow>();
 IReadOnlyList<MailFolderRow> folders=Array.Empty<MailFolderRow>();
 MessageDetail? detail;
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(30)};
 readonly UserPreferences preferences=UserPreferences.Load();
 readonly Dictionary<string,HashSet<uint>> inboxSnapshot=new(StringComparer.OrdinalIgnoreCase);
 MailTray? tray;
 bool isQuitting,checkingNewMail,preferencesReady;

 bool refreshing,refreshPending,suppressSelection;
 int generation;
 MailAccount? CurrentAccount=>AccountPicker.SelectedItem as MailAccount;
 MailFolderRow? CurrentFolder=>FolderList.SelectedItem as MailFolderRow;
 InboxRow? CurrentMessage=>Messages.SelectedItem as InboxRow;
 public MainWindow(){
  InitializeComponent();
  try{accounts=AccountStore.Load();}catch{accounts=new();MessageBox.Show("Saved account settings could not be decrypted. The old data was not overwritten.","Aso4Mail",MessageBoxButton.OK,MessageBoxImage.Warning);}
  AccountPicker.ItemsSource=accounts;
  timer.Tick+=async(_,_)=>{
   await RefreshMessagesAsync(true);
   await CheckNewMailAsync();
  };
  timer.Start();
  Closed+=(_,_)=>{
   timer.Stop();
   MailNotifications.OpenMessageRequested-=OpenAlert;
   tray?.Dispose();
  };
  MailNotifications.OpenMessageRequested+=OpenAlert;
  if(accounts.Count>0)AccountPicker.SelectedIndex=0;
  NotificationsToggle.IsChecked=preferences.NotificationsEnabled;
  TrayToggle.IsChecked=preferences.MinimizeToTrayOnClose;
  preferencesReady=true;

 }

 void Window_Loaded(object sender,RoutedEventArgs e){
  try{
   tray=new MailTray(ShowFromTray,QuitProgram);
  }catch(Exception ex){StatusText.Text="Tray icon unavailable: "+ex.Message;}
  if(MailNotifications.Pending is { } pending){
   MailNotifications.Pending=null;
   _=OpenAlertAsync(pending.Email,pending.Uid);
  }
  _=CheckNewMailAsync();
 }
 void ShowFromTray(){
  Dispatcher.BeginInvoke(new Action(()=>{
   Show();
   if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
   Activate();
   Topmost=true;Topmost=false;
   Focus();
  }));
 }
 void QuitProgram(){
  Dispatcher.BeginInvoke(new Action(()=>{
   isQuitting=true;
   tray?.Dispose();
   Close();
  }));
 }
 void Window_Closing(object? sender,System.ComponentModel.CancelEventArgs e){
  if(isQuitting||!preferences.MinimizeToTrayOnClose)return;
  e.Cancel=true;Hide();
 }
 void Quit_Click(object sender,RoutedEventArgs e)=>QuitProgram();
 void Preferences_Changed(object sender,RoutedEventArgs e){
  if(!preferencesReady)return;
  preferences.NotificationsEnabled=NotificationsToggle.IsChecked==true;
  preferences.MinimizeToTrayOnClose=TrayToggle.IsChecked==true;
  preferences.Save();
 }
 void TestNotification_Click(object sender,RoutedEventArgs e){
  try{MailNotifications.Test();StatusText.Text="Notification requested from Windows";}
  catch(Exception ex){MessageBox.Show(ex.Message,"Notification unavailable");}
 }
 async Task CheckNewMailAsync(){
  if(checkingNewMail||accounts.Count==0||!preferences.NotificationsEnabled)return;
  checkingNewMail=true;
  try{
   foreach(var account in accounts.ToArray()){
    IReadOnlyList<InboxRow> rows;
    try{rows=await MailService.ListMessagesAsync(account,"INBOX",CancellationToken.None);}
    catch{continue;}
    var ids=rows.Select(x=>x.Uid).ToHashSet();
    if(!inboxSnapshot.TryGetValue(account.Email,out var previous)){
     inboxSnapshot[account.Email]=ids;
     continue; // Existing mail is not new: prevent a notification flood.
    }
    var newlyArrived=rows.Where(x=>!previous.Contains(x.Uid)&&!x.IsRead)
     .OrderBy(x=>x.Date).TakeLast(4).ToList();
    inboxSnapshot[account.Email]=ids;
    foreach(var row in newlyArrived)MailNotifications.Show(account,row);
   }
  }finally{checkingNewMail=false;}
 }
 void OpenAlert(string email,uint uid)=>_=OpenAlertAsync(email,uid);
 async Task OpenAlertAsync(string email,uint uid){
  var account=accounts.FirstOrDefault(a=>a.Email.Equals(email,StringComparison.OrdinalIgnoreCase));
  if(account is null)return;
  ShowFromTray();
  if(!ReferenceEquals(CurrentAccount,account))AccountPicker.SelectedItem=account;
  for(int i=0;i<75&&
   (!ReferenceEquals(CurrentAccount,account)||folders.Count==0);i++){
   await Task.Delay(120);
  }
  if(!ReferenceEquals(CurrentAccount,account))return;
  var inbox=folders.FirstOrDefault(x=>x.Role=="Inbox");
  if(inbox is null)return;
  if(CurrentFolder?.FullName!=inbox.FullName)FolderList.SelectedItem=inbox;
  for(int i=0;i<75&&
   (CurrentFolder?.FullName!=inbox.FullName||!loaded.Any(r=>r.Uid==uid));i++){
   await Task.Delay(120);
   if(i==12)await RefreshMessagesAsync(true);
  }
  var message=loaded.FirstOrDefault(r=>r.Uid==uid);
  if(message is null){
   StatusText.Text="Notification message was moved or is not in the recent list.";
   return;
  }
  DrawList(uid);
  Messages.SelectedItem=Messages.Items.Cast<InboxRow>().FirstOrDefault(r=>r.Uid==uid);
  Messages.ScrollIntoView(Messages.SelectedItem);
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
   HtmlNotice.Text=string.IsNullOrWhiteSpace(msg.Html)?"No formatted HTML in this message.":"Open the Formatted HTML tab for a restricted preview."; if(BodyTabs.SelectedIndex==1)await RenderHtmlAsync(msg.Html);
  }catch(Exception ex){MessageBody.Text="Unable to read message: "+ex.Message;}
 }
 void ClearMessage(){detail=null;SubjectText.Text="Select an email";FromText.Text="";ToText.Text="";MessageBody.Text="";AttachmentPicker.ItemsSource=null;HtmlNotice.Text="Select an email to view its body."; HtmlWebView.Visibility=Visibility.Collapsed;HtmlNotice.Visibility=Visibility.Visible;}
 async void AddAccount_Click(object sender,RoutedEventArgs e){
  var w=new AccountWindow{Owner=this};
  if(w.ShowDialog()!=true)return;
  try{
   var email=AccountStore.ValidateEmail(w.Address);
   var candidate=new MailAccount{Email=email,Password=w.Secret};
   await MailService.ListFoldersAsync(candidate,CancellationToken.None);
   var previous=accounts.FindIndex(a=>a.Email.Equals(email,StringComparison.OrdinalIgnoreCase));
   if(previous>=0){
    candidate.Signature=accounts[previous].Signature;
    candidate.SignatureImage=accounts[previous].SignatureImage;
    candidate.SignatureWidth=accounts[previous].SignatureWidth;
    candidate.Contacts=accounts[previous].Contacts??new();
   }
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
  var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase){CurrentAccount.Email,row.SenderAddress};
  var copied=new List<string>();
  foreach(var field in new[]{detail.To,detail.Cc}){
   if(string.IsNullOrWhiteSpace(field))continue;
   try{
    foreach(var mb in InternetAddressList.Parse(field).Mailboxes){
     if(known.Add(mb.Address))copied.Add(mb.Address);
    }
   }catch(FormatException){}
  }
  new ComposeWindow(CurrentAccount,row.SenderAddress,string.Join("; ",copied),
   row.Subject.StartsWith("Re:",StringComparison.OrdinalIgnoreCase)?row.Subject:"Re: "+row.Subject,
   "\\n\\n------ Original message ------\\n"+detail.Text){Owner=this}.ShowDialog();
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
 async void Junk_Click(object sender,RoutedEventArgs e)=>await MoveToRoleAsync("Junk");
 async void EmptyTrash_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null||CurrentFolder?.Role!="Trash"){
   MessageBox.Show("Open the Trash folder before emptying it.","Aso4Mail");return;
  }
  if(MessageBox.Show("Permanently delete all messages in this Trash folder? This cannot be undone.","Empty Trash",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
  try{
   StatusText.Text="Emptying Trash...";
   await MailService.EmptyTrashAsync(CurrentAccount,CurrentFolder.FullName,CancellationToken.None);
   await RefreshMessagesAsync();
  }catch(Exception ex){MessageBox.Show(ex.Message,"Aso4Mail");}
 }

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
 void Contacts_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null)return;
  var w=new ContactsWindow(CurrentAccount){Owner=this};
  if(w.ShowDialog()==true)AccountStore.Save(accounts);
 }
 void Signature_Click(object sender,RoutedEventArgs e){
  if(CurrentAccount is null)return;
  var w=new SignatureWindow(CurrentAccount.Signature,CurrentAccount.SignatureImage,CurrentAccount.SignatureWidth){Owner=this};
  if(w.ShowDialog()!=true)return;
  CurrentAccount.Signature=w.Signature;CurrentAccount.SignatureImage=w.SignatureImage;CurrentAccount.SignatureWidth=w.SignatureWidth;AccountStore.Save(accounts);
 }

 async void BodyTabs_SelectionChanged(object sender,SelectionChangedEventArgs e){
  if(!IsLoaded||BodyTabs.SelectedIndex!=1||detail is null)return;
  await RenderHtmlAsync(detail.Html);
 }
 bool htmlViewerConfigured;
 async Task RenderHtmlAsync(string html){
  HtmlWebView.Visibility=Visibility.Collapsed;HtmlNotice.Visibility=Visibility.Visible;
  if(string.IsNullOrWhiteSpace(html)){HtmlNotice.Text="This email has no HTML content.";return;}
  if(html.Length>2_000_000){HtmlNotice.Text="HTML is too large for preview. Use Plain text.";return;}
  try{
   await HtmlWebView.EnsureCoreWebView2Async();
   var web=HtmlWebView.CoreWebView2;
   if(!htmlViewerConfigured){
    htmlViewerConfigured=true;
    var settings=web.Settings;
    settings.IsScriptEnabled=false;
    settings.AreDefaultContextMenusEnabled=false;
    settings.AreDevToolsEnabled=false;
    settings.IsWebMessageEnabled=false;
    settings.IsStatusBarEnabled=false;
    web.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
    web.WebResourceRequested+=(_,args)=>{
     // Do not allow any remote network or local file request from email HTML.
     args.Response=web.Environment.CreateWebResourceResponse(null,403,"Blocked by Aso4Mail","Content-Type: text/plain");
    };
    web.NewWindowRequested+=(_,args)=>args.Handled=true;
    web.NavigationStarting+=(_,args)=>{
     if(!args.Uri.StartsWith("about:blank",StringComparison.OrdinalIgnoreCase)&&!args.Uri.StartsWith("data:",StringComparison.OrdinalIgnoreCase))args.Cancel=true;
    };
   }
   const string csp="<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:; connect-src 'none'; form-action 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'\">";
   web.NavigateToString("<!doctype html><html><head><meta charset=\"utf-8\">"+csp+"<style>body{font:14px Segoe UI,Arial,sans-serif;padding:20px;background:#fff;color:#222;overflow-wrap:anywhere}img{max-width:100%;height:auto}table{max-width:100%}</style></head><body>"+html+"</body></html>");
   HtmlWebView.Visibility=Visibility.Visible;HtmlNotice.Visibility=Visibility.Collapsed;
  }catch(Exception){
   HtmlNotice.Text="Formatted preview is unavailable. Use Plain text. WebView2 Runtime may be missing.";HtmlNotice.Visibility=Visibility.Visible;
  }
 }
 void Website_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("https://aso4tech.com"){UseShellExecute=true});
}