using System.Windows;
namespace Aso4Mail;
public partial class AccountWindow : Window {
 public AccountWindow(){InitializeComponent();}
 public string Address=>Email.Text.Trim();
 public string Secret=>Password.Password;
 private void Connect_Click(object sender,RoutedEventArgs e){
  if(string.IsNullOrWhiteSpace(Address)||string.IsNullOrEmpty(Secret)){MessageBox.Show("Email and password required.");return;}
  DialogResult=true;
 }
}