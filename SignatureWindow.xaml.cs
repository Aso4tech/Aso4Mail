using System.Windows;
namespace Aso4Mail;
public partial class SignatureWindow : Window {
 public SignatureWindow(string signature){InitializeComponent();Editor.Text=signature;}
 public string Signature=>Editor.Text;
 private void Save_Click(object sender,RoutedEventArgs e)=>DialogResult=true;
}