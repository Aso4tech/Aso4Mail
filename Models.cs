namespace Aso4Mail;
public sealed class MailAccount {
 public string Email {get;set;} = "";
 public string Password {get;set;} = "";
 public string Signature {get;set;} = "";
 public string Domain => Email.Split('@').Last().ToLowerInvariant();
 public string ImapHost => "mail." + Domain;
 public string SmtpHost => "mail." + Domain;
 public override string ToString() => Email;
}
public sealed class InboxRow {
 public uint Uid {get;init;}
 public string From {get;init;} = "";
 public string SenderAddress {get;init;} = "";
 public string Subject {get;init;} = "";
 public DateTimeOffset Date {get;init;}
 public string DisplayDate => Date.LocalDateTime.ToString("g");
 public bool IsRead {get;init;}
}