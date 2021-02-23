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
        public bool HasError = false;

        // APng
        private Dictionary<fcTL, MemoryStream> m_Apng;
        // WebP
        private WebpAnim webPAnim;

        private Stream stream;
        private BitmapSource bitmapSource;
        private BitmapImage bitmapImage;

        private DispatcherTimer Timer;
        private int DelayDefault = 50;
        private int Delay = 0;
        private int ActualFrame = 0;


        public new object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public new static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(AnimatedImage),
            new PropertyMetadata(null, SourceChanged));


        public AnimatedImage()
        {
            InitializeComponent();
        }


        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (AnimatedImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object NewSource, object OldSource)
        {
            if (NewSource?.Equals(OldSource) == true)
            {
                return;
            }

            Image_Unloaded(null, null);

            if (NewSource != null && NewSource is string str)
            {
                if (!File.Exists(str))
                {
                    base.Source = null;
                    return;
                }

                try
                {
                    base.Source = new BitmapImage(new Uri((string)NewSource));
                }
                catch (Exception ex)
                {
                    base.Source = null;
                }

                if (System.IO.Path.GetExtension(str).ToLower().IndexOf("png") > -1)
                {
                    CPng_Reader pngr;
                    try
                    {
                        pngr = new CPng_Reader();
                        m_Apng = pngr.Open(File.OpenRead(str)).SpltAPng();

                        // Animated
                        if (pngr.Chunks.Count != 0)
                        {
                            Delay = m_Apng.FirstOrDefault().Key.Delay_Den;

                            Timer = new DispatcherTimer(DispatcherPriority.Render);
                            if (Delay > 0)
                            {
                                Timer.Interval = TimeSpan.FromMilliseconds(Delay);
                            }
                            else
                            {
                                Timer.Interval = TimeSpan.FromMilliseconds(DelayDefault);
                            }
                            Timer.Tick += TimerTickAPng;
                            Timer.Start();
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
                }

                if (System.IO.Path.GetExtension(str).ToLower().IndexOf("webp") > -1)
                {
                    webPAnim = new WebpAnim();
                    webPAnim.Load(str);

                    // Animated
                    if (webPAnim.FramesDuration() != 0)
                    {
                        Delay = webPAnim.FramesDuration();

                        Timer = new DispatcherTimer(DispatcherPriority.Render);
                        Timer.Interval = TimeSpan.FromMilliseconds(webPAnim.FramesDuration());
                        Timer.Tick += TimerTickWebp;
                        Timer.Start();
                    }
                    else
                    {
                        Stream stream = webPAnim.GetStream();
                        if (stream != null)
                        {
                            BitmapImage bitmapImage = new BitmapImage();
                            bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = stream;
                            bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                            bitmapImage.EndInit();

                            base.Source = bitmapImage;
                        }
                        else
                        {
                            base.Source = null;
                        }
                    }
                }
            }
            else
            {
                base.Source = null;
            }
        }


        public AnimetedImageInfos GetInfos()
        {
            AnimetedImageInfos animetedImageInfos = new AnimetedImageInfos();

            if (Source is string FileName)
            {
                FileInfo info = new FileInfo((string)FileName);

                animetedImageInfos.Name = info.Name;
                animetedImageInfos.Size = info.Length;

                if (webPAnim != null)
                {
                    animetedImageInfos.Frames = webPAnim.FramesCount();
                    animetedImageInfos.Delay = Delay;
                }
                if (m_Apng != null)
                {
                    animetedImageInfos.Frames = m_Apng.Count;
                    animetedImageInfos.Delay = Delay;
                }
            }

            return animetedImageInfos;
        }


        #region Timer
        private void TimerTickWebp(object sender, EventArgs e)
        {
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("--------------------------");
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
#endif
                //stream = webPAnim.GetFrameStream(ActualFrame);
                //
                //System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//30
                //
                //bitmapImage = new BitmapImage();
                //bitmapImage.BeginInit();
                //bitmapImage.StreamSource = stream;
                //bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                //bitmapImage.EndInit();

                bitmapSource = webPAnim.GetFrameBitmapSource(ActualFrame);

#if DEBUG
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//3
#endif

                base.Source = bitmapSource;

#if DEBUG
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//1
#endif

                ActualFrame++; 
                if (ActualFrame >= webPAnim.FramesCount())
                {
                    ActualFrame = 0;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                Timer.Stop();
            }
        }

        private void TimerTickAPng(object sender, EventArgs e)
        {
            try
            {
                if (m_Apng != null)
                {
                    try
                    {
                        stream = m_Apng.ElementAt(ActualFrame).Value;
                        stream.Position = 0;
                        
                        bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = stream;
                        bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                        bitmapImage.EndInit();

                        base.Source = bitmapImage;
                    }
                    catch (Exception ex)
                    {
                        base.Source = null;
                    }

                    ActualFrame++;
                    if (ActualFrame >= m_Apng.Count)
                    {
                        ActualFrame = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                Timer.Stop();
            }
        }
        #endregion


        private void Image_Unloaded(object sender, RoutedEventArgs e)
        {
            HasError = false;
            Delay = 0;
            ActualFrame = 0;

            m_Apng = null;
            if (webPAnim != null)
            {
                webPAnim.Dispose();
            }
            webPAnim = null;

            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }

            bitmapImage = null;

            if (stream != null)
            {
                stream.Dispose();
            }

            GC.Collect();
        }


        #region Activate/Deactivated animation
        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Activated += Application_Activated;
            Application.Current.Deactivated += Application_Deactivated;
        }

        private void Application_Deactivated(object sender, EventArgs e)
        {
            if (Timer != null)
            {
                Timer.Stop();
            }
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            if (Timer != null && !HasError)
            {
                Timer.Start();
            }
        }
        #endregion
    }


    public class AnimetedImageInfos
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public int Frames { get; set; }
        public int Delay { get; set; }
    }
}