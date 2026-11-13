using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace Aso4Mail;
public partial class MainWindow : Window {
 private readonly List<MailAccount> accounts;
 private IReadOnlyList<InboxRow> loaded=Array.Empty<InboxRow>();
 private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(30)};
 private bool busy, rebuilding, refreshQueued;
 private int generation;
 private MailAccount? CurrentAccount=>AccountPicker.SelectedItem as MailAccount;
 public MainWindow() {
  InitializeComponent();
  try {accounts=AccountStore.Load();}
  catch {accounts=new();MessageBox.Show("Saved accounts could not be decrypted.","Aso4Mail");}
  AccountPicker.ItemsSource=accounts;
  if(accounts.Count>0)AccountPicker.SelectedIndex=0;
  timer.Tick+=async (_,_)=>await RefreshInboxAsync(true);
  timer.Start();
  Closed+=(_,_)=>timer.Stop();
 }
 private async Task RefreshInboxAsync(bool silent=false) {
  if(busy||CurrentAccount is null)return;
  busy=true;
  var account=CurrentAccount;var gen=generation;
  if(!silent)StatusText.Text="Loading inbox…";
  try {
   var rows=await MailService.InboxAsync(account,CancellationToken.None);
   if(gen!=generation||!ReferenceEquals(account,CurrentAccount))return;
   bool unchanged=loaded.Count==rows.Count&&loaded.Zip(rows).All(p=>p.First.Uid==p.Second.Uid&&p.First.IsRead==p.Second.IsRead&&p.First.Subject==p.Second.Subject);
   if(unchanged){if(!silent)StatusText.Text="Up to date";return;}
   var selected=(Messages.SelectedItem as InboxRow)?.Uid;
   loaded=rows;DrawList(selected);
   StatusText.Text=$"{rows.Count} messages loaded";
  } catch(Exception ex) {
   StatusText.Text="Connection failed. Check account or TLS settings.";
   if(!silent)MessageBox.Show(ex.Message,"Connection error",MessageBoxButton.OK,MessageBoxImage.Warning);
  } finally {
   busy=false;
   if(refreshQueued){refreshQueued=false;_=RefreshInboxAsync();}
  }
 }
 private void DrawList(uint? selectedUid=null) {
  rebuilding=true;
  try {
   var q=SearchBox.Text.Trim();
   var rows=loaded.Where(r=>q.Length==0||r.Subject.Contains(q,StringComparison.OrdinalIgnoreCase)||r.From.Contains(q,StringComparison.OrdinalIgnoreCase)).ToList();
   Messages.ItemsSource=rows;
   if(selectedUid is uint uid)Messages.SelectedItem=rows.FirstOrDefault(r=>r.Uid==uid);
  } finally {rebuilding=false;}
 }
 private void AccountPicker_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  generation++;loaded=Array.Empty<InboxRow>();Messages.ItemsSource=null;
  MessageBody.Text="";FromText.Text="";SubjectText.Text="Select an email";
  if(busy)refreshQueued=true;else _=RefreshInboxAsync();
 }
 private async void AddAccount_Click(object sender,RoutedEventArgs e) {
  var dialog=new AccountWindow{Owner=this};
  if(dialog.ShowDialog()!=true)return;
  try {
   string email=AccountStore.ValidateEmail(dialog.Address);
   var candidate=new MailAccount{Email=email,Password=dialog.Secret};
   await MailService.InboxAsync(candidate,CancellationToken.None);
   int index=accounts.FindIndex(a=>a.Email.Equals(email,StringComparison.OrdinalIgnoreCase));
   if(index>=0)accounts[index]=candidate;else accounts.Add(candidate);
   AccountStore.Save(accounts);AccountPicker.Items.Refresh();AccountPicker.SelectedItem=candidate;
   await RefreshInboxAsync();
  } catch(Exception ex){MessageBox.Show("Account setup failed: "+ex.Message,"Aso4Mail");}
 }
 private async void Refresh_Click(object sender,RoutedEventArgs e)=>await RefreshInboxAsync();
 private void SearchBox_TextChanged(object sender,TextChangedEventArgs e){if(IsLoaded)DrawList();}
 private async void Messages_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(rebuilding||Messages.SelectedItem is not InboxRow row||CurrentAccount is null)return;
  var account=CurrentAccount;int gen=generation;
  SubjectText.Text=row.Subject;FromText.Text=row.From;MessageBody.Text="Reading…";
  try {
   var body=await MailService.ReadTextAsync(account,row.Uid,CancellationToken.None);
   if(gen==generation&&ReferenceEquals(account,CurrentAccount)&&(Messages.SelectedItem as InboxRow)?.Uid==row.Uid)MessageBody.Text=body;
  }catch{MessageBody.Text="Unable to read message.";}
 }
 private void Compose_Click(object sender,RoutedEventArgs e) {
  if(CurrentAccount is null){MessageBox.Show("Add an account first.");return;}
  new ComposeWindow(CurrentAccount){Owner=this}.ShowDialog();
 }
 private void Reply_Click(object sender,RoutedEventArgs e) {
  if(CurrentAccount is null||Messages.SelectedItem is not InboxRow row)return;
  new ComposeWindow(CurrentAccount,row.SenderAddress,row.Subject.StartsWith("Re:",StringComparison.OrdinalIgnoreCase)?row.Subject:"Re: "+row.Subject,"\n\n---- Original message ----\n"+MessageBody.Text){Owner=this}.ShowDialog();
 }
 private void Signature_Click(object sender,RoutedEventArgs e) {
  if(CurrentAccount is null)return;
  var dialog=new SignatureWindow(CurrentAccount.Signature){Owner=this};
  if(dialog.ShowDialog()!=true)return;
  CurrentAccount.Signature=dialog.Signature;AccountStore.Save(accounts);
 }
}