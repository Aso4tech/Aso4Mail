
using System;
using System.IO;
using System.Text.Json;
namespace Aso4Mail;
public sealed class UserPreferences {
 public bool NotificationsEnabled {get;set;}=true;
 public bool MinimizeToTrayOnClose {get;set;}=true;
 private static string Location=>Path.Combine(
  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
  "Aso4Tech","Aso4Mail","preferences.json");
 public static UserPreferences Load(){
  try{
   if(!File.Exists(Location))return new();
   return JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Location))??new();
  }catch{return new();}
 }
 public void Save(){
  Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
  File.WriteAllText(Location,JsonSerializer.Serialize(this));
 }
}