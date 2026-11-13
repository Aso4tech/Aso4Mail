using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Aso4Mail;
public partial class ContactsWindow:Window{
 readonly MailAccount account;
 readonly List<MailContact> entries;
 public ContactsWindow(MailAccount account){
  InitializeComponent();this.account=account;
  entries=(account.Contacts??new()).Select(c=>new MailContact{Name=c.Name,Email=c.Email}).ToList();
  Refresh();
 }
 void Refresh(){ContactsList.ItemsSource=null;ContactsList.ItemsSource=entries.OrderBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).ToList();}
 void ContactsList_SelectionChanged(object sender,SelectionChangedEventArgs e){
  if(ContactsList.SelectedItem is MailContact c){NameBox.Text=c.Name;EmailBox.Text=c.Email;}
 }
 void Upsert_Click(object sender,RoutedEventArgs e){
  try{
   var email=AccountStore.ValidateEmail(EmailBox.Text);
   var existing=entries.FirstOrDefault(x=>x.Email.Equals(email,StringComparison.OrdinalIgnoreCase));
   if(existing is null)entries.Add(new MailContact{Name=NameBox.Text.Trim(),Email=email});
   else existing.Name=NameBox.Text.Trim();
   Refresh();NameBox.Clear();EmailBox.Clear();
  }catch(Exception ex){MessageBox.Show(ex.Message,"Invalid contact");}
 }
 void Delete_Click(object sender,RoutedEventArgs e){
  if(ContactsList.SelectedItem is not MailContact c)return;
  entries.RemoveAll(x=>x.Email.Equals(c.Email,StringComparison.OrdinalIgnoreCase));
  Refresh();NameBox.Clear();EmailBox.Clear();
 }
 void Save_Click(object sender,RoutedEventArgs e){
  account.Contacts=entries;DialogResult=true;
 }
}