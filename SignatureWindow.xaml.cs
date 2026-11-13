using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
namespace Aso4Mail;
public partial class SignatureWindow:Window{
 public SignatureWindow(string signature,string image="",int width=220){
  InitializeComponent();Editor.Text=signature;ImagePath.Text=image;WidthSlider.Value=Math.Clamp(width,50,700);
 }
 public string Signature=>Editor.Text;
 public string SignatureImage=>ImagePath.Text;
 public int SignatureWidth=>(int)WidthSlider.Value;
 void ChooseImage_Click(object sender,RoutedEventArgs e){
  var picker=new OpenFileDialog{Title="Select signature image",Filter="Images|*.png;*.jpg;*.jpeg"};
  if(picker.ShowDialog(this)!=true)return;
  if(new FileInfo(picker.FileName).Length>2*1024*1024){MessageBox.Show("Signature image must be smaller than 2 MiB.");return;}
  ImagePath.Text=picker.FileName;
 }
 void RemoveImage_Click(object sender,RoutedEventArgs e)=>ImagePath.Text="";
 void Save_Click(object sender,RoutedEventArgs e)=>DialogResult=true;
}