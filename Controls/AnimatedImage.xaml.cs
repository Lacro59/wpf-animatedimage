using APNG;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
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
using System.Windows.Threading;

namespace wpf_animatedimage.Controls
{
    /// <summary>
    /// Logique d'interaction pour AnimatedImage.xaml
    /// </summary>
    public partial class AnimatedImage : Image
    {
        private Dictionary<fcTL, MemoryStream> m_Apng = new Dictionary<fcTL, MemoryStream>();
        //private Dictionary<int, BitmapImage> m_Bmps = new Dictionary<int, BitmapImage>();
        private DispatcherTimer timer;

        private WebpAnim webPAnim = new WebpAnim();

        private List<Stream> Imgs = new List<Stream>();

        private int timeSpan = 40;
        private int delay = 0;
        private int index = 0;
        private object currentSource = null;


        public new static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(AnimatedImage),
            new PropertyMetadata(null, SourceChanged));

        public new object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }


        public AnimatedImage()
        {
            InitializeComponent();
        }


        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (AnimatedImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object newSource, object oldSource)
        {
            if (newSource?.Equals(currentSource) == true)
            {
                return;
            }

            currentSource = newSource;

            m_Apng = null;
            //m_Bmps = null;
            //m_Bmps = new Dictionary<int, BitmapImage>();

            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            if (newSource != null)
            {
                if (newSource is string str)
                {
                    if (!File.Exists(str))
                    {
                        base.Source = null;
                        return;
                    }

                    //base.Source = new BitmapImage(new Uri((string)newSource));

                    if (System.IO.Path.GetExtension(str).ToLower().IndexOf("png") > -1)
                    {
                        //await Task.Run(() =>
                        //{
                            CPng_Reader pngr;
                            try
                            {
                                pngr = new CPng_Reader();
                                m_Apng = pngr.Open(File.OpenRead(str)).SpltAPng();

                                //delay = 20;

                                if (pngr.Chunks.Count != 0)
                                {
                                    //this.Dispatcher.BeginInvoke((Action)delegate
                                    //{
                                        delay = m_Apng.FirstOrDefault().Key.Delay_Num;

                                        foreach (var a in m_Apng)
                                        {
                                            Imgs.Add(a.Value);
                                        }

                                        index = 0;
                                        timer = new DispatcherTimer(DispatcherPriority.Render);

                                        if (delay > 0)
                                        {
                                            timer.Interval = TimeSpan.FromMilliseconds(delay);
                                        }
                                        else
                                        {
                                            timer.Interval = TimeSpan.FromMilliseconds(timeSpan);
                                        }
                                        timer.Tick += Timer_Tick;
                                        timer.Start();
                                    //});
                                }
                                else
                                {
                                    m_Apng = null;
                                }

                                pngr = null;
                            }
                            catch (Exception ex)
                            {
                                pngr = null;
                                m_Apng = null;
                            }
                        //});
                    }

                    if (System.IO.Path.GetExtension(str).ToLower().IndexOf("webp") > -1)
                    {
                        webPAnim = new WebpAnim();
                        webPAnim.Load(str);

                        Imgs = webPAnim.StreamImages;

                        if (webPAnim.FramesDuration() != 0)
                        {
                            timer = new DispatcherTimer(DispatcherPriority.Render);
                            timer.Interval = TimeSpan.FromMilliseconds(webPAnim.FramesDuration());
                            timer.Tick += TimerTickWebp;
                            timer.Start();
                        }
                    }
                }
            }
        }

        private void TimerTickWebp(object sender, EventArgs e)
        {
            try
            { 
                Stream stream = webPAnim.GetFrameStream(index);

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = stream;
                bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                bitmapImage.EndInit();

                base.Source = bitmapImage;

                index++; 
                if (index >= webPAnim.FramesCount())
                {
                    index = 0;
                }
            }
            catch (Exception ex)
            {

            }
        }



        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (Imgs != null)
                {
                    Stream stream = Imgs[index];
                    //if (this.m_Bmps.ContainsKey(index) == false)
                    //{
                        stream.Position = 0;
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = stream;
                        bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                        bitmapImage.EndInit();

                        //m_Bmps.Add(index, bitmapImage);
                        base.Source = bitmapImage;
                    //}

                    //base.Source = m_Bmps[index];

                    index++;
                    if (index >= Imgs.Count)
                    {
                        index = 0;
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }


        private void Image_Unloaded(object sender, RoutedEventArgs e)
        {
            m_Apng = null;
            //m_Bmps = null;

            currentSource = null;

            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            //Window window = Tools.FindParent<Window>(this);
            //window.Activated += Window_Activated;
            //window.Deactivated += Window_Deactivated;
        }


        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (timer != null)
            {
                timer.Start();
            }
        }
    }
}