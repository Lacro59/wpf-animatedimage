using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wpf_animatedimage
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.ShowDialog();

            if (ofd.FileName != null && ofd.FileName != string.Empty)
            {
                string fname = ofd.FileName;
                PART_AnimatedImage.Source = fname;

                var info = PART_AnimatedImage.GetInfos();
                PART_Name.Content = info.Name;
                PART_Size.Content = info.Size;
                PART_Frames.Content = info.Frames;
                PART_Delay.Content = info.Delay;
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            PART_AnimatedImage.UseBitmapImage = !PART_AnimatedImage.UseBitmapImage;
        }
    }
}
