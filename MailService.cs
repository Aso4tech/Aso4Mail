using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace Aso4Mail;
public static class MailService {
 const long MaxAttachments=50L*1024*1024;
 static readonly Regex HtmlTags=new(@"<[^>]*>",RegexOptions.Compiled|RegexOptions.Singleline);
 public static async Task<ImapClient> ConnectAsync(MailAccount account,CancellationToken ct) {
  var client=new ImapClient {Timeout=30000};
  try{
   await client.ConnectAsync(account.ImapHost,993,SecureSocketOptions.SslOnConnect,ct);
   await client.AuthenticateAsync(account.Email,account.Password,ct);
   return client;
  }catch{client.Dispose();throw;}
 }
 public static async Task<IReadOnlyList<MailFolderRow>> ListFoldersAsync(MailAccount a,CancellationToken ct) {
  using var c=await ConnectAsync(a,ct);
  var all=new Dictionary<string,MailFolderRow>(StringComparer.OrdinalIgnoreCase);
  void Add(IMailFolder folder){
   if(!folder.Exists || folder.Attributes.HasFlag(FolderAttributes.NoSelect))return;
   var role=GetRole(folder);
   var row=new MailFolderRow{FullName=folder.FullName,Name=role.Length==0?folder.Name:role,Role=role,Unread=Math.Max(0,folder.Unread),SortOrder=role switch{"Inbox"=>0,"Sent"=>1,"Drafts"=>2,"Archive"=>3,"Junk"=>4,"Trash"=>5,_=>6}};
   all.TryAdd(folder.FullName,row);
  }
  var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  async Task WalkAsync(IMailFolder folder,int depth){
   if(!visited.Add(folder.FullName))return;
   Add(folder);
   if(depth>=4||visited.Count>=300)return;
   try{
    var children=await folder.GetSubfoldersAsync(false,ct);
    foreach(var child in children)await WalkAsync(child,depth+1);
   }catch(MailKit.CommandException){}catch(FolderNotFoundException){}
  }
  await WalkAsync(c.Inbox,0);
  foreach(var ns in c.PersonalNamespaces){
   var roots=await c.GetFoldersAsync(ns,false,ct);
   foreach(var folder in roots)await WalkAsync(folder,0);
  }
  await c.DisconnectAsync(true,ct);
  return all.Values.OrderBy(x=>x.SortOrder).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
 }
 static string GetRole(IMailFolder f){
  var flags=f.Attributes;
  if(flags.HasFlag(FolderAttributes.Inbox)||f.FullName.Equals("INBOX",StringComparison.OrdinalIgnoreCase))return "Inbox";
  if(flags.HasFlag(FolderAttributes.Sent))return "Sent";
  if(flags.HasFlag(FolderAttributes.Drafts))return "Drafts";
  if(flags.HasFlag(FolderAttributes.Archive))return "Archive";
  if(flags.HasFlag(FolderAttributes.Junk))return "Junk";
  if(flags.HasFlag(FolderAttributes.Trash))return "Trash";
  var tail=f.Name.ToLowerInvariant();
  return tail switch{"sent" or "sent items"=>"Sent","drafts"=>"Drafts","archive"=>"Archive","junk" or "spam"=>"Junk","trash" or "deleted items"=>"Trash",_=>""};
 }
 public static async Task<IReadOnlyList<InboxRow>> ListMessagesAsync(MailAccount a,string folder,CancellationToken ct) {
  using var c=await ConnectAsync(a,ct);
  var f=c.GetFolder(folder,ct);
  await f.OpenAsync(FolderAccess.ReadOnly,ct);
  if(f.Count==0)return Array.Empty<InboxRow>();
  var start=Math.Max(0,f.Count-160);
  var items=await f.FetchAsync(start,f.Count-1,MessageSummaryItems.UniqueId|MessageSummaryItems.Envelope|MessageSummaryItems.Flags|MessageSummaryItems.BodyStructure,ct);
  var result=items.Select(s=>new InboxRow{
   Uid=s.UniqueId.Id,Folder=folder,
   From=s.Envelope?.From?.ToString()??"Unknown",
   SenderAddress=s.Envelope?.From?.Mailboxes?.FirstOrDefault()?.Address??"",
   To=s.Envelope?.To?.ToString()??"",
   Subject=s.Envelope?.Subject??"(No subject)",
   Date=s.Envelope?.Date??DateTimeOffset.MinValue,
   IsRead=s.Flags.HasValue&&s.Flags.Value.HasFlag(MessageFlags.Seen),
   IsFlagged=s.Flags.HasValue&&s.Flags.Value.HasFlag(MessageFlags.Flagged),
   HasAttachment=s.Attachments?.Any()??false
  }).OrderByDescending(x=>x.Date).ToList();
  await c.DisconnectAsync(true,ct);
  return result;
 }
 public static async Task<MessageDetail> ReadMessageAsync(MailAccount a,string folder,uint uid,CancellationToken ct){
  using var c=await ConnectAsync(a,ct);
  var f=c.GetFolder(folder,ct);
  await f.OpenAsync(FolderAccess.ReadOnly,ct);
  var msg=await f.GetMessageAsync(new UniqueId(uid),ct);
  var attachments=msg.Attachments.Select((att,i)=>new MailAttachmentRow{Index=i,Name=att.ContentDisposition?.FileName??att.ContentType.Name??$"Attachment {i+1}"}).ToList();
  var html=msg.HtmlBody??"";
  // Replace embedded CID images with bounded data URIs. Never fetch external image URLs.
  if(html.Length<2_000_000){
   var cumulative=0L;
   foreach(var part in msg.BodyParts.OfType<MimePart>()){
    if(string.IsNullOrEmpty(part.ContentId))continue;
    var media=part.ContentType.MimeType.ToLowerInvariant();
    if(media is not ("image/png" or "image/jpeg" or "image/gif"))continue;
    using var output=new MemoryStream();
    await part.Content.DecodeToAsync(output,ct);
    cumulative+=output.Length;
    if(output.Length>1_000_000||cumulative>4_000_000)break;
    var data="data:"+media+";base64,"+Convert.ToBase64String(output.ToArray());
    html=Regex.Replace(html,"cid:"+Regex.Escape(part.ContentId),_=>data,RegexOptions.IgnoreCase);
   }
  }
  // This HTML must only be rendered inside the isolated, script-disabled WebView.
  var text=msg.TextBody??HtmlToText(html);
  await c.DisconnectAsync(true,ct);
  return new MessageDetail{From=msg.From.ToString(),To=msg.To.ToString(),Cc=msg.Cc.ToString(),Subject=msg.Subject??"",Text=text,Html=html,Attachments=attachments};
 }
 public static string HtmlToText(string html){
  if(string.IsNullOrWhiteSpace(html))return "(Empty message)";
  var x=Regex.Replace(html,@"(?is)<(script|style|svg|iframe)\b[^>]*>.*?</\1>","");
  x=Regex.Replace(x,@"(?i)<br\s*/?>|</(?:p|div|li|tr)>","\n");
  return WebUtility.HtmlDecode(HtmlTags.Replace(x," "));
 }
 public static async Task SaveAttachmentAsync(MailAccount a,string folder,uint uid,int index,string output,CancellationToken ct){
  using var c=await ConnectAsync(a,ct);
  var f=c.GetFolder(folder,ct);
  await f.OpenAsync(FolderAccess.ReadOnly,ct);
  var msg=await f.GetMessageAsync(new UniqueId(uid),ct);
  var parts=msg.Attachments.ToList();
  if(index<0||index>=parts.Count)throw new ArgumentOutOfRangeException(nameof(index));
  using var destination=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None);
  if(parts[index] is MimePart p)await p.Content.DecodeToAsync(destination,ct);
  else if(parts[index] is MessagePart mp)await mp.Message.WriteToAsync(destination,ct);
  await c.DisconnectAsync(true,ct);
 }
 public static async Task SaveEmlAsync(MailAccount a,string folder,uint uid,string output,CancellationToken ct){
  using var c=await ConnectAsync(a,ct);
  var f=c.GetFolder(folder,ct);await f.OpenAsync(FolderAccess.ReadOnly,ct);
  var msg=await f.GetMessageAsync(new UniqueId(uid),ct);
  await using var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None);
  await msg.WriteToAsync(file,ct);
  await c.DisconnectAsync(true,ct);
 }
 public static async Task SetFlagAsync(MailAccount a,string folder,uint uid,MessageFlags flag,bool enabled,CancellationToken ct){
  using var c=await ConnectAsync(a,ct);
  var f=c.GetFolder(folder,ct);await f.OpenAsync(FolderAccess.ReadWrite,ct);
  if(enabled)await f.AddFlagsAsync(new UniqueId(uid),flag,true,ct);
  else await f.RemoveFlagsAsync(new UniqueId(uid),flag,true,ct);
  await c.DisconnectAsync(true,ct);
 }
 public static async Task MoveAsync(MailAccount a,string folder,uint uid,string destination,CancellationToken ct){
  using var c=await ConnectAsync(a,ct);
  var source=c.GetFolder(folder,ct);var target=c.GetFolder(destination,ct);
  await source.OpenAsync(FolderAccess.ReadWrite,ct);
  await source.MoveToAsync(new UniqueId(uid),target,ct);
  await c.DisconnectAsync(true,ct);
 }
 public static async Task EmptyTrashAsync(MailAccount a,string path,CancellationToken ct){
  using var c=await ConnectAsync(a,ct);
  var f=c.GetFolder(path,ct);
  if(GetRole(f)!="Trash")throw new InvalidOperationException("This folder is not verified as Trash.");
  await f.OpenAsync(FolderAccess.ReadWrite,ct);
  if(f.Count>0){
   var ids=await f.SearchAsync(MailKit.Search.SearchQuery.All,ct);
   if(ids.Count>0){
    await f.AddFlagsAsync(ids,MessageFlags.Deleted,true,ct);
    await f.ExpungeAsync(ids,ct);
   }
  }
  await c.DisconnectAsync(true,ct);
 }
 static MimeMessage BuildMessage(MailAccount account,string to,string cc,string bcc,string subject,string body,IReadOnlyList<string> paths,bool allowNoRecipient=false){
  long length=0;
  foreach(var path in paths){
   var file=new FileInfo(path);
   if(!file.Exists)throw new FileNotFoundException("Attachment not found",path);
   checked{length+=file.Length;}
   if(length>MaxAttachments)throw new InvalidOperationException("Attachments exceed 50 MiB. Your mail server may have a smaller encoded-size limit.");
  }
  var msg=new MimeMessage();
  msg.From.Add(MailboxAddress.Parse(account.Email));
  AddAddresses(msg.To,to);
  AddAddresses(msg.Cc,cc);
  AddAddresses(msg.Bcc,bcc);
  if(!allowNoRecipient && msg.To.Count+msg.Cc.Count+msg.Bcc.Count==0)throw new InvalidOperationException("Enter a recipient.");
  msg.Subject=subject.Trim();
  var signature=account.Signature??"";
  var contents=body+(signature.Length>0?"\n\n"+signature:"");
  var builder=new BodyBuilder{TextBody=contents};
  // Display outgoing content as plain text encoded to safe HTML.
  builder.HtmlBody="<html><body style=\"font:14px Segoe UI,Arial,sans-serif;white-space:pre-wrap\">"+WebUtility.HtmlEncode(contents)+"</body></html>";
  if(!string.IsNullOrEmpty(account.SignatureImage)&&File.Exists(account.SignatureImage)){
   var f=new FileInfo(account.SignatureImage);
   if(f.Length<=2*1024*1024&&new[]{".png",".jpg",".jpeg"}.Contains(f.Extension.ToLowerInvariant())){
    var image=builder.LinkedResources.Add(account.SignatureImage);
    image.ContentId=MimeUtils.GenerateMessageId();
    builder.HtmlBody="<html><body style=\"font:14px Segoe UI,Arial,sans-serif;white-space:pre-wrap\">"+WebUtility.HtmlEncode(contents)+"<p><img alt=\"Signature\" width=\""+Math.Clamp(account.SignatureWidth,50,700)+"\" src=\"cid:"+image.ContentId+"\" /></p></body></html>";
   }
  }
  foreach(var p in paths)builder.Attachments.Add(p);
  msg.Body=builder.ToMessageBody();return msg;
 }
 static void AddAddresses(InternetAddressList target,string raw){
  if(string.IsNullOrWhiteSpace(raw))return;
  var parsed=InternetAddressList.Parse(raw.Replace(';',','));
  foreach(var address in parsed)target.Add(address);
 }
 public static async Task<SendResult> SendAsync(MailAccount a,string to,string cc,string bcc,string subject,string body,IReadOnlyList<string> paths,CancellationToken ct){
  var msg=BuildMessage(a,to,cc,bcc,subject,body,paths);
  using(var smtp=new SmtpClient{Timeout=35000}){
   await smtp.ConnectAsync(a.SmtpHost,465,SecureSocketOptions.SslOnConnect,ct);
   await smtp.AuthenticateAsync(a.Email,a.Password,ct);
   await smtp.SendAsync(msg,ct);
   await smtp.DisconnectAsync(true,ct);
  }
  try{
   using var c=await ConnectAsync(a,ct);
   var sent=await FindFolderAsync(c,"Sent",ct);
   if(sent is null)return new SendResult{SmtpAccepted=true,Warning="Sent folder unavailable; server accepted the message but no copy was saved."};
   await sent.AppendAsync(msg,MessageFlags.Seen,ct);
   await c.DisconnectAsync(true,ct);
   return new SendResult{SmtpAccepted=true,SavedToSent=true};
  }catch(Exception ex){
   return new SendResult{SmtpAccepted=true,Warning="SMTP accepted the message; saving the Sent copy failed: "+ex.Message};
  }
 }
 public static async Task SaveDraftAsync(MailAccount a,string to,string cc,string bcc,string subject,string body,IReadOnlyList<string> paths,CancellationToken ct){
  var msg=BuildMessage(a,to,cc,bcc,subject,body,paths,allowNoRecipient:true);
  using var c=await ConnectAsync(a,ct);
  var drafts=await FindFolderAsync(c,"Drafts",ct);
  if(drafts is null)throw new InvalidOperationException("Drafts folder is not available on this server.");
  await drafts.AppendAsync(msg,MessageFlags.Draft,ct);
  await c.DisconnectAsync(true,ct);
 }
 static async Task<IMailFolder?> FindFolderAsync(ImapClient c,string role,CancellationToken ct){
  try {
   var special=role switch{"Sent"=>SpecialFolder.Sent,"Drafts"=>SpecialFolder.Drafts,"Trash"=>SpecialFolder.Trash,"Archive"=>SpecialFolder.Archive,"Junk"=>SpecialFolder.Junk,_=>SpecialFolder.All};
   var found=c.GetFolder(special);
   if(found is not null)return found;
  }catch(NotSupportedException){}
  var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  async Task<IMailFolder?> SearchAsync(IMailFolder folder,int depth){
   if(!visited.Add(folder.FullName))return null;
   if(GetRole(folder)==role)return folder;
   if(depth>=4||visited.Count>=300)return null;
   try{
    var children=await folder.GetSubfoldersAsync(false,ct);
    foreach(var child in children){
     var found=await SearchAsync(child,depth+1);
     if(found is not null)return found;
    }
   }catch(MailKit.CommandException){}catch(FolderNotFoundException){}
   return null;
  }
  var inbox=await SearchAsync(c.Inbox,0);if(inbox is not null)return inbox;
  foreach(var ns in c.PersonalNamespaces){
   var roots=await c.GetFoldersAsync(ns,false,ct);
   foreach(var folder in roots){
    var found=await SearchAsync(folder,0);
    if(found is not null)return found;
   }
  }
  return null;
 }
}