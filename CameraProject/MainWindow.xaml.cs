using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using Emgu.CV.Structure;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Input;
using System.IO;

namespace WebCam
{
    public partial class MainWindow : Window
    {
        private FilterInfoCollection filterInfoCollection;
        private VideoCaptureDevice videoCaptureDevice;
        private VideoWriter _videoWriter;
        private bool _isRecording = false;
        private bool _isPaused = false;
        private string _videoFileName;
        private Bitmap _currentFrame;
        private DispatcherTimer _timer;
        private DateTime _startTime;
        private TimeSpan _pausedTime;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            buttonPauseRecording.Visibility = Visibility.Hidden;
            buttonStopRecording.Visibility = Visibility.Hidden;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in filterInfoCollection)
            {
                comboBoxCameras.Items.Add(device.Name);
            }

            if (comboBoxCameras.Items.Count > 0)
            {
                comboBoxCameras.SelectedIndex = 0; // Select the first camera by default
            }
            else
            {
                MessageBox.Show("No video devices found.");
            }
        }

        private void buttonStartCamera_Click(object sender, RoutedEventArgs e)
        {
            if (comboBoxCameras.SelectedIndex >= 0)
            {
                videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[comboBoxCameras.SelectedIndex].MonikerString);
                videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
                videoCaptureDevice.Start();
            }
            else
            {
                MessageBox.Show("Please select a video source from the list.");
            }
        }

        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                _currentFrame = (Bitmap)eventArgs.Frame.Clone();
                Dispatcher.Invoke(() =>
                {
                    imageDisplay.Source = BitmapToImageSource(_currentFrame);
                });

                if (_isRecording && !_isPaused)
                {
                    using (Mat mat = BitmapToMat(_currentFrame))
                    {
                        _videoWriter.Write(mat);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error capturing frame: " + ex.Message);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var elapsed = _isPaused ? _pausedTime : _pausedTime + (DateTime.Now - _startTime);
            timerTextBlock.Text = string.Format("{0:00}:{1:00}:{2:00}", elapsed.Hours, elapsed.Minutes, elapsed.Seconds);
        }

        private BitmapSource BitmapToImageSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        private static unsafe Mat BitmapToMat(Bitmap bitmap)
        {
            Mat mat = new Mat(bitmap.Height, bitmap.Width, DepthType.Cv8U, 3);
            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Mat image = new Mat(bitmapData.Height, bitmapData.Width, DepthType.Cv8U, 3, bitmapData.Scan0, bitmapData.Stride);

            CvInvoke.CvtColor(image, mat, ColorConversion.Bgr2Bgra);
            CvInvoke.CvtColor(mat, image, ColorConversion.Bgra2Bgr);

            bitmap.UnlockBits(bitmapData);
            return mat;
        }

        private void buttonCaptureImage_Click(object sender, RoutedEventArgs e)
        {
            if (videoCaptureDevice != null && videoCaptureDevice.IsRunning)
            {
                var flashAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(100),
                    AutoReverse = true
                };

                flashEffect.BeginAnimation(OpacityProperty, flashAnimation);

                string directory = "E:\\";
                string fileName = $"Picture_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg";
                string fullPath = System.IO.Path.Combine(directory, fileName);


                _currentFrame.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                imageDisplay.Effect = null;

            }
            else
            {
                MessageBox.Show("Please start the camera first.");
            }
        }

        private void buttonStartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (videoCaptureDevice != null && videoCaptureDevice.IsRunning)
            {
                string directory = "E:\\";
                string fileName = $"Video_{Guid.NewGuid().ToString().Substring(0, 8)}.mp4";
                string fullPath = System.IO.Path.Combine(directory, fileName);
                _videoFileName = fullPath;
                string fileExtension = System.IO.Path.GetExtension(_videoFileName).ToLower();
                int codec = fileExtension == ".mp4" ? FourCC.H264 : FourCC.MJPG;
                _startTime = DateTime.Now;
                _pausedTime = TimeSpan.Zero;
                _timer.Start();
                _videoWriter = new VideoWriter(_videoFileName, codec, 25, new System.Drawing.Size(_currentFrame.Width, _currentFrame.Height), true);
                _isRecording = true;
                _isPaused = false;
                timerTextBlock.Visibility = Visibility.Visible;
                buttonCaptureImage.IsEnabled = false;
                buttonPauseRecording.Visibility = Visibility.Visible;
                buttonStartRecording.Visibility = Visibility.Hidden;
                buttonStopRecording.Visibility = Visibility.Visible;

            }
            else
            {
                MessageBox.Show("Please start the camera first.");
            }
        }

        private void buttonStopRecording_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                _isRecording = false;
                _videoWriter.Dispose();
                timerTextBlock.Visibility = Visibility.Hidden;
                _timer.Stop();
                buttonCaptureImage.IsEnabled = true;
                buttonStartRecording.Visibility = Visibility.Visible;
                buttonPauseRecording.Visibility = Visibility.Hidden;
                buttonStopRecording.Visibility = Visibility.Hidden;
            }
        }

        private void buttonExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            StopVideoCaptureDevice();
        }

        private void StopVideoCaptureDevice()
        {
            if (videoCaptureDevice != null && videoCaptureDevice.IsRunning)
            {
                videoCaptureDevice.SignalToStop();
                videoCaptureDevice.WaitForStop();
            }

            if (_isRecording)
            {
                StopRecording();
            }
        }

        private void StopRecording()
        {
            if (_isRecording)
            {
                _isRecording = false;
                _videoWriter.Dispose();
            }
        }

        private System.Windows.Controls.Image CreateImage(string imagePath)
        {
            return new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
                Width = 47,
                Height = 48,
                ClipToBounds = true,
                Clip = new EllipseGeometry
                {
                    Center = new System.Windows.Point(23.5, 24),
                    RadiusX = 23.5,
                    RadiusY = 24
                }
            };
        }

        private void buttonPauseRecording_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                _isPaused = !_isPaused;
                if (_isPaused)
                {
                    buttonPauseRecording.Content = CreateImage("Resources/remuse.ico");
                    _pausedTime += DateTime.Now - _startTime;
                    _timer.Stop();
                    // Logic to pause video recording
                }
                else
                {
                    buttonPauseRecording.Content = CreateImage("Resources/pause.ico");
                    _startTime = DateTime.Now;
                    _timer.Start();
                    // Logic to resume video recording
                }
            }
        }
    }

    public static class FourCC
    {
        public const int MJPG = 1196444237; // "MJPG" FourCC
        public const int XVID = 1145656920; // "XVID" FourCC
        public const int H264 = 875967075;  // "H264" FourCC (for MP4)
        // Add other FourCC codes as needed
    }
}