using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;
using System.Text.RegularExpressions;
namespace Aso4Mail;
public static class MailService {
 private static async Task<ImapClient> ConnectAsync(MailAccount account,CancellationToken ct) {
  var client=new ImapClient {Timeout=20000};
  try {
   await client.ConnectAsync(account.ImapHost,993,SecureSocketOptions.SslOnConnect,ct);
   await client.AuthenticateAsync(account.Email,account.Password,ct);
   return client;
  } catch {client.Dispose();throw;}
 }
 public static async Task<IReadOnlyList<InboxRow>> InboxAsync(MailAccount account,CancellationToken ct) {
  using var client=await ConnectAsync(account,ct);
  var inbox=client.Inbox;
  await inbox.OpenAsync(FolderAccess.ReadOnly,ct);
  var count=inbox.Count;
  if(count==0) return Array.Empty<InboxRow>();
  var summaries=await inbox.FetchAsync(Math.Max(0,count-70),count-1,MessageSummaryItems.UniqueId|MessageSummaryItems.Envelope|MessageSummaryItems.Flags,ct);
  var rows=summaries.Select(s=>new InboxRow {
   Uid=s.UniqueId.Id,
   From=s.Envelope?.From?.ToString()??"Unknown",
   SenderAddress=s.Envelope?.From?.Mailboxes?.FirstOrDefault()?.Address??"",
   Subject=s.Envelope?.Subject??"(No subject)",
   Date=s.Envelope?.Date??DateTimeOffset.MinValue,
   IsRead=s.Flags.HasValue && s.Flags.Value.HasFlag(MessageFlags.Seen)
  }).OrderByDescending(r=>r.Date).ToList();
  await client.DisconnectAsync(true,ct);
  return rows;
 }
 public static async Task<string> ReadTextAsync(MailAccount account,uint uid,CancellationToken ct) {
  using var client=await ConnectAsync(account,ct);
  var folder=client.Inbox;
  await folder.OpenAsync(FolderAccess.ReadOnly,ct);
  var message=await folder.GetMessageAsync(new UniqueId(uid),ct);
  await client.DisconnectAsync(true,ct);
  if(!string.IsNullOrWhiteSpace(message.TextBody))return message.TextBody;
  var html=message.HtmlBody??"(Empty message)";
  return WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html,@"(?is)<(script|style)\b[^>]*>.*?</\1>",""),@"<[^>]*>"," "));
 }
 public static async Task SendAsync(MailAccount account,string to,string subject,string body,IReadOnlyList<string> paths,CancellationToken ct) {
  const long limit=50L*1024*1024;
  long total=0;
  foreach(var path in paths) {
   var file=new FileInfo(path);
   if(!file.Exists)throw new FileNotFoundException("Attachment missing",path);
   checked {total+=file.Length;}
   if(total>limit)throw new InvalidOperationException("Attachments exceed 50 MiB.");
  }
  var message=new MimeMessage();
  message.From.Add(MailboxAddress.Parse(account.Email));
  message.To.Add(MailboxAddress.Parse(to.Trim()));
  message.Subject=subject;
  var builder=new BodyBuilder {TextBody=body+(string.IsNullOrWhiteSpace(account.Signature)?"":"\n\n"+account.Signature)};
  foreach(var path in paths)builder.Attachments.Add(path);
  message.Body=builder.ToMessageBody();
  using var smtp=new SmtpClient {Timeout=30000};
  await smtp.ConnectAsync(account.SmtpHost,465,SecureSocketOptions.SslOnConnect,ct);
  await smtp.AuthenticateAsync(account.Email,account.Password,ct);
  await smtp.SendAsync(message,ct);
  await smtp.DisconnectAsync(true,ct);
 }
}