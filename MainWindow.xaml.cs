﻿using Microsoft.Win32;
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

                PART_Name.Content = string.Empty;
                PART_Size.Content = string.Empty;
                PART_Frames.Content = string.Empty;
                PART_Delay.Content = string.Empty;

                Task.Run(() =>
                {
                    var info = PART_AnimatedImage.GetInfos();

                    this.Dispatcher.BeginInvoke((Action)delegate
                    {
                        PART_Name.Content = info.Name;
                        PART_Size.Content = info.Size;
                        PART_Frames.Content = info.Frames;
                        PART_Delay.Content = info.Delay;
                    });
                });
            }
        }
    }
}
