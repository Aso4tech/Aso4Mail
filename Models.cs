using System;
using System.Collections.Generic;
using System.Linq;
namespace Aso4Mail;
public sealed class MailAccount {
 public string Email {get;set;}="";
 public string Password {get;set;}="";
 public string Signature {get;set;}="";
 public List<MailContact> Contacts {get;set;}=new();
 public string SignatureImage {get;set;}="";
 public int SignatureWidth {get;set;}=220;
 public string Domain=>Email.Split('@').Last().ToLowerInvariant();
 public string ImapHost=>"mail."+Domain;
 public string SmtpHost=>"mail."+Domain;
 public override string ToString()=>Email;
}
public sealed class MailFolderRow {
 public string FullName {get;init;}="";
 public string Name {get;init;}="";
 public string Role {get;init;}="";
 public int Unread {get;init;}
 public int SortOrder {get;init;}=9;
 public string Icon=>Role switch {"Inbox"=>"▣","Sent"=>"➤","Drafts"=>"✎","Archive"=>"▤","Junk"=>"⚑","Trash"=>"▧",_=>"◻"};
 public string Display=>$"{Icon}   {Name}"+(Unread>0?$"   • {Unread}":"");
}
public sealed class InboxRow {
 public uint Uid {get;init;}
 public uint UidValidity {get;init;}
 public string Folder {get;init;}="";
 public string From {get;init;}="";
 public string SenderAddress {get;init;}="";
 public string To {get;init;}="";
 public string Subject {get;init;}="";
 public DateTimeOffset Date {get;init;}
 public string DisplayDate=>Date.LocalDateTime.ToString("dd MMM  yyyy  HH:mm");
 public bool IsRead {get;init;}
 public bool IsFlagged {get;init;}
 public bool HasAttachment {get;init;}
 public string Status=>(IsFlagged?"★  ":"")+(HasAttachment?"📎  ":"")+(IsRead?"":"●");
 public string Initials=>string.Join("",(From??"").Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x=>char.ToUpperInvariant(x[0])));
}
public sealed class MailAttachmentRow {
 public int Index {get;init;}
 public string Name {get;init;}="";
 public string Display=>$"📎 {Name}";
}
public sealed class MessageDetail {
 public string From {get;init;}="";
 public string To {get;init;}="";
 public string Cc {get;init;}="";
 public string Subject {get;init;}="";
 public string Text {get;init;}="";
 public string Html {get;init;}="";
 public IReadOnlyList<MailAttachmentRow> Attachments {get;init;}=Array.Empty<MailAttachmentRow>();
}
public sealed class SendResult {
 public bool SmtpAccepted {get;init;}
 public bool SavedToSent {get;init;}
 public string? Warning {get;init;}
}
public sealed class MailContact {
 public string Name {get;set;}="";
 public string Email {get;set;}="";
 public override string ToString() => string.IsNullOrWhiteSpace(Name)?Email:$"{Name} <{Email}>";
}
