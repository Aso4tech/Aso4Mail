using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Mail;
namespace Aso4Mail;
public static class AccountStore {
 private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aso4Tech","Aso4Mail");
 private static readonly string Pathname = Path.Combine(Folder,"accounts.dpapi");
 public static List<MailAccount> Load() {
  if (!File.Exists(Pathname)) return new();
  byte[] plaintext = ProtectedData.Unprotect(File.ReadAllBytes(Pathname),null,DataProtectionScope.CurrentUser);
  try { return JsonSerializer.Deserialize<List<MailAccount>>(plaintext) ?? new(); }
  finally { CryptographicOperations.ZeroMemory(plaintext); }
 }
 public static void Save(IReadOnlyList<MailAccount> accounts) {
  Directory.CreateDirectory(Folder);
  var plain=JsonSerializer.SerializeToUtf8Bytes(accounts);
  try {
   var encrypted=ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);
   var temp=Pathname+".tmp";
   File.WriteAllBytes(temp,encrypted);
   File.Move(temp,Pathname,true);
  } finally {CryptographicOperations.ZeroMemory(plain);}
 }
 public static string ValidateEmail(string input) {
  var email=input.Trim();
  var parsed=new MailAddress(email);
  if(!parsed.Address.Equals(email,StringComparison.OrdinalIgnoreCase) || email.Count(c=>c=='@')!=1) throw new FormatException("Invalid email address.");
  var domain=email.Split('@')[1];
  if(domain.Length>253 || !domain.Contains('.') || !System.Text.RegularExpressions.Regex.IsMatch(domain,@"^[A-Za-z0-9.-]+$") || domain.Contains("..") || domain.StartsWith('.') || domain.EndsWith('.'))
   throw new FormatException("Invalid email domain.");
  return email;
 }
}