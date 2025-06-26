using QSoft.Apng;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Image = System.Windows.Controls.Image;

namespace wpf_animatedimage.Controls
{
    public partial class AnimatedImage : Image
    {
        // Public state flags
        public bool HasError { get; private set; } = false;
        public bool IsLoadedCompletely { get; private set; } = false;

        // Private fields
        private string _filePath;
        private Dictionary<fcTL, MemoryStream> _apngFrames;
        private WebpAnim _webpAnim;
        private DispatcherTimer _frameTimer;
        private int _frameIndex;
        private int _frameDelayMs = 50; // Default frame delay

        // Dependency Properties
        public static readonly DependencyProperty UseBitmapImageProperty = DependencyProperty.Register(
            nameof(UseBitmapImage), typeof(bool), typeof(AnimatedImage), new PropertyMetadata(true));

        public static readonly DependencyProperty UseAnimatedProperty = DependencyProperty.Register(
            nameof(UseAnimated), typeof(bool), typeof(AnimatedImage), new PropertyMetadata(false));

        public static new readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source), typeof(object), typeof(AnimatedImage), new PropertyMetadata(null, OnSourceChanged));

        public bool UseBitmapImage
        {
            get => (bool)GetValue(UseBitmapImageProperty);
            set => SetValue(UseBitmapImageProperty, value);
        }

        public bool UseAnimated
        {
            get => (bool)GetValue(UseAnimatedProperty);
            set => SetValue(UseAnimatedProperty, value);
        }

        public new object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public AnimatedImage()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (AnimatedImage)d;
            control.LoadImageAsync(e.NewValue as string).ConfigureAwait(false);
        }

        private async Task LoadImageAsync(string path)
        {
            Reset();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                base.Source = null;
                return;
            }

            _filePath = path;

            try
            {
                if (UseAnimated)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext.Contains("png"))
                    {
                        await LoadApngAsync();
                    }
                    else if (ext.Contains("webp"))
                    {
                        await LoadWebpAsync();
                    }
                    else
                    {
                        await LoadStaticImageAsync();
                    }
                }
                else
                {
                    await LoadStaticImageAsync();
                }
            }
            catch
            {
                HasError = true;
            }

            IsLoadedCompletely = true;
        }

        private async Task LoadStaticImageAsync()
        {
            await Task.Run(() =>
            {
                using (var fs = OpenSafeStream(_filePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.StreamSource = fs;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Dispatcher.Invoke(() => base.Source = bitmap);
                }
            });
        }

        private async Task LoadApngAsync()
        {
            await Task.Run(() =>
            {
                var reader = new Png_Reader();
                using (var stream = OpenSafeStream(_filePath))
                {
                    _apngFrames = reader.Open(stream).SpltAPng();
                }

                if (_apngFrames?.Count > 0)
                {
                    _frameDelayMs = _apngFrames.First().Key.Delay_Den;
                    SetupTimer(TimerTickApng);
                }
            });
        }

        private async Task LoadWebpAsync()
        {
            await Task.Run(() =>
            {
                _webpAnim = new WebpAnim();
                _webpAnim.Load(_filePath);

                _frameDelayMs = _webpAnim.FramesDuration();
                if (_frameDelayMs > 0)
                {
                    SetupTimer(TimerTickWebp);
                }
            });
        }

        private void SetupTimer(EventHandler tickHandler)
        {
            Dispatcher.Invoke(() =>
            {
                _frameTimer = new DispatcherTimer(DispatcherPriority.Render);
                _frameTimer.Interval = TimeSpan.FromMilliseconds(_frameDelayMs);
                _frameTimer.Tick += tickHandler;
                _frameTimer.Start();
            });
        }

        private void TimerTickApng(object sender, EventArgs e)
        {
            try
            {
                if (_apngFrames == null || _apngFrames.Count == 0)
                {
                    return;
                }

                var frame = _apngFrames.ElementAt(_frameIndex);
                using (var stream = frame.Value)
                {
                    stream.Position = 0;

                    var img = new BitmapImage();
                    img.BeginInit();
                    img.StreamSource = stream;
                    img.EndInit();
                    img.Freeze();

                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, img.Width, img.Height));
                        dc.DrawImage(img, new Rect(frame.Key.X_Offset, frame.Key.Y_Offset, img.Width, img.Height));
                    }

                    var rtb = new RenderTargetBitmap((int)img.Width, (int)img.Height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(visual);
                    Dispatcher.Invoke(() => base.Source = rtb);
                }

                _frameIndex = (_frameIndex + 1) % _apngFrames.Count;
            }
            catch
            {
                StopTimer();
                HasError = true;
            }
        }

        private void TimerTickWebp(object sender, EventArgs e)
        {
            try
            {
                if (_webpAnim == null)
                {
                    return;
                }

                BitmapSource frame = UseBitmapImage ? _webpAnim.GetFrameBitmapSource(_frameIndex) : GetWebpFrameImage();
                Dispatcher.Invoke(() => base.Source = _webpAnim.GetFrameBitmapSource(_frameIndex));
                _frameIndex = (_frameIndex + 1) % _webpAnim.FramesCount();
            }
            catch
            {
                StopTimer();
                HasError = true;
            }
        }

        private BitmapImage GetWebpFrameImage()
        {
            using (var stream = _webpAnim.GetFrameStream(_frameIndex))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = stream;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }

        private void Reset()
        {
            StopTimer();
            _apngFrames = null;
            _webpAnim?.Dispose();
            _webpAnim = null;
            _frameIndex = 0;
            HasError = false;
            IsLoadedCompletely = false;
        }

        private void StopTimer()
        {
            if (_frameTimer != null)
            {
                _frameTimer.Stop();
                _frameTimer.Tick -= TimerTickApng;
                _frameTimer.Tick -= TimerTickWebp;
                _frameTimer = null;
            }
        }

        private Stream OpenSafeStream(string path, int retries = 5)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (IOException)
                {
                    Task.Delay(200).Wait();
                }
            }
            throw new IOException($"Unable to open file stream for {path}");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Activated += OnAppActivated;
            Application.Current.Deactivated += OnAppDeactivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Activated -= OnAppActivated;
            Application.Current.Deactivated -= OnAppDeactivated;
            Reset();
        }

        private void OnAppActivated(object sender, EventArgs e)
        {
            if (!HasError)
            {
                _frameTimer?.Start();
            }
        }

        private void OnAppDeactivated(object sender, EventArgs e)
        {
            _frameTimer?.Stop();
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