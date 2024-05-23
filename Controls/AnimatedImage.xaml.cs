using QSoft.Apng;
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
using System.Windows.Media.Animation;
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
        public bool HasError { get; set; } = false;

        private string FileName { get; set; }

        // APng
        private Dictionary<fcTL, MemoryStream> Apng { get; set; }

        // WebP
        private WebpAnim WebPAnim { get; set; }

        private Stream Stream { get; set; }
        private BitmapSource BitmapSource { get; set; }
        private BitmapImage BitmapImage { get; set; }

        private DispatcherTimer Timer { get; set; }
        private int DelayDefault { get; set; } = 50;
        private int Delay { get; set; } = 0;
        private int ActualFrame { get; set; } = 0;

        public bool IsCharged { get; set; } = false;


        #region Properties
        public bool UseBitmapImage
        {
            get => (bool)GetValue(UseBitmapImageProperty);
            set => SetValue(UseBitmapImageProperty, value);
        }

        public static readonly DependencyProperty UseBitmapImageProperty = DependencyProperty.Register(
            nameof(UseBitmapImage),
            typeof(bool),
            typeof(AnimatedImage),
            new PropertyMetadata(true));


        public bool UseAnimated
        {
            get => (bool)GetValue(UseAnimatedProperty);
            set => SetValue(UseAnimatedProperty, value);
        }

        public static readonly DependencyProperty UseAnimatedProperty = DependencyProperty.Register(
            nameof(UseAnimated),
            typeof(bool),
            typeof(AnimatedImage),
            new PropertyMetadata(false));


        public new object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static new readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(AnimatedImage),
            new PropertyMetadata(null, SourceChanged));
        #endregion


        public AnimatedImage()
        {
            InitializeComponent();
        }


        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            AnimatedImage control = (AnimatedImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object NewSource, object OldSource)
        {
            if (NewSource?.Equals(OldSource) == true)
            {
                return;
            }

            Image_Unloaded(null, null);

            if (NewSource != null && NewSource is string)
            {
                FileName = (string)NewSource;

                if (!File.Exists(FileName))
                {
                    base.Source = null;
                    return;
                }

                try
                {
                    BitmapImage bitmapImage = await Task.Factory.StartNew(() =>
                    {
                        if (NewSource is string str)
                        {
                            using (Stream fStream = OpenReadFileStreamSafe((string)NewSource))
                            {
                                _ = fStream.Seek(0, SeekOrigin.Begin);
                                BitmapImage image = new BitmapImage();
                                image.BeginInit();
                                image.StreamSource = fStream;

                                image.CacheOption = BitmapCacheOption.OnLoad;
                                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                image.EndInit();
                                image.Freeze();

                                return image;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    });
                    base.Source = bitmapImage;
                }
                catch
                {
                    base.Source = null;
                }

                if (UseAnimated)
                {
                    if (System.IO.Path.GetExtension(FileName).ToLower().IndexOf("png") > -1)
                    {
                        Png_Reader pngr;
                        try
                        {
                            _ = Task.Run(() =>
                            {
                                pngr = new Png_Reader();
                                using (Stream fStream = OpenReadFileStreamSafe(FileName))
                                {
                                    Apng = pngr.Open(fStream).SpltAPng();
                                }

                                // Animated
                                if (Apng != null && Apng.Count != 0)
                                {
                                    Delay = Apng.FirstOrDefault().Key.Delay_Den;

                                    this.Dispatcher.BeginInvoke((Action)delegate
                                    {
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
                                    });
                                }
                                else
                                {
                                    Apng = null;
                                }

                                pngr = null;
                                IsCharged = true;
                            });
                        }
                        catch
                        {
                            pngr = null;
                            Apng = null;
                        }
                    }
                    else if (System.IO.Path.GetExtension(FileName).ToLower().IndexOf("webp") > -1)
                    {
                        _ = Task.Run(() =>
                        {
                            WebPAnim = new WebpAnim();
                            WebPAnim.Load(FileName);

                            // Animated
                            if (WebPAnim.FramesDuration() != 0)
                            {
                                Delay = WebPAnim.FramesDuration();

                                this.Dispatcher.BeginInvoke((Action)delegate
                                {
                                    Timer = new DispatcherTimer(DispatcherPriority.Render);
                                    Timer.Interval = TimeSpan.FromMilliseconds(WebPAnim.FramesDuration());
                                    Timer.Tick += TimerTickWebp;
                                    Timer.Start();
                                });
                            }

                            IsCharged = true;
                        });
                    }
                    else
                    {
                        IsCharged = true;
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
            _ = System.Threading.SpinWait.SpinUntil(() => IsCharged, -1);

            AnimetedImageInfos animetedImageInfos = new AnimetedImageInfos();

            if (FileName != null && FileName != string.Empty)
            {
                FileInfo info = new FileInfo(FileName);

                animetedImageInfos.Name = info.Name;
                animetedImageInfos.Size = info.Length;

                if (WebPAnim != null)
                {
                    animetedImageInfos.Frames = WebPAnim.FramesCount();
                    animetedImageInfos.Delay = Delay;
                }
                if (Apng != null)
                {
                    animetedImageInfos.Frames = Apng.Count;
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
                System.Diagnostics.Debug.WriteLine("-TimerTickWebp-------------------------");
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
#endif
                if (UseBitmapImage)
                {
                    Stream = WebPAnim.GetFrameStream(ActualFrame);

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//30ms
#endif

                    BitmapImage = new BitmapImage();
                    BitmapImage.BeginInit();
                    BitmapImage.StreamSource = Stream;
                    BitmapImage.EndInit();

                    base.Source = BitmapImage;
                }
                else
                {
                    BitmapSource = WebPAnim.GetFrameBitmapSource(ActualFrame);

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//3ms
#endif

                    base.Source = BitmapSource;
                }
#if DEBUG
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//1ms
                System.Diagnostics.Debug.WriteLine("-TimerTickWebp-END---------------------");
#endif

                ActualFrame++;
                if (ActualFrame >= WebPAnim.FramesCount())
                {
                    ActualFrame = 0;
                }
            }
            catch
            {
                HasError = true;
                Timer.Stop();
            }
        }

        private async void TimerTickAPng(object sender, EventArgs e)
        {
            try
            {
                if (Apng != null)
                {
                    try
                    {
                        fcTL fctl = this.Apng.ElementAt(ActualFrame).Key;
                        DrawingVisual drawingVisual = new DrawingVisual();
                        using (DrawingContext dc = drawingVisual.RenderOpen())
                        {
                            Stream = Apng.ElementAt(ActualFrame).Value;
                            Stream.Position = 0;

                            BitmapImage img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = Stream;
                            img.EndInit();
                            img.Freeze();
                            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, img.Width, img.Height));
                            dc.DrawImage(img, new Rect(fctl.X_Offset, fctl.Y_Offset, img.Width, img.Height));
                        }
                        RenderTargetBitmap rtb = new RenderTargetBitmap((int)drawingVisual.ContentBounds.Width, (int)drawingVisual.ContentBounds.Height, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(drawingVisual);
                        base.Source = rtb;
                    }
                    catch
                    {
                        base.Source = null;
                    }

                    ActualFrame++;
                    if (ActualFrame >= this.Apng.Count)
                    {
                        ActualFrame = 0;
                    }
                }
            }
            catch
            {
                HasError = true;
                Timer.Stop();
            }
        }
        #endregion


        private void Image_Unloaded(object sender, RoutedEventArgs e)
        {
            IsCharged = false;

            HasError = false;
            Delay = 0;
            ActualFrame = 0;

            Apng = null;
            if (WebPAnim != null)
            {
                WebPAnim.Dispose();
                WebPAnim = null;
            }

            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }

            BitmapImage = null;

            if (Stream != null)
            {
                Stream.Dispose();
                Stream = null;
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





        public Stream OpenReadFileStreamSafe(string path, int retryAttempts = 5)
        {
            IOException ioException = null;
            for (int i = 0; i < retryAttempts; i++)
            {
                try
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Read);
                }
                catch (IOException exc)
                {
                    ioException = exc;
                    Task.Delay(500).Wait();
                }
            }

            throw new IOException($"Failed to read {path}", ioException);
        }
    }


    public class AnimetedImageInfos
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public int Frames { get; set; }
        public int Delay { get; set; }
    }
}
