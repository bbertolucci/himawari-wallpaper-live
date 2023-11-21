using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Net;
using System.Windows.Input;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Windows.Threading;
/*
namespace ChangeWallpaperAppUI2
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer wallpaperUpdateTimerPreview;
        private DateTime currentDateTime;
        private NotifyIcon notifyIcon;
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private DispatcherTimer wallpaperUpdateTimer;
        private Boolean isSync = true;
        private DateTime maxDateTime;
        private ContextMenuStrip contextMenuStrip;
        
        private Icon LoadIconFromResource(string resourcePath)
        {
            var resourceUri = new Uri(resourcePath, UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);

            if (streamInfo != null)
            {
                using (var stream = streamInfo.Stream)
                {
                    return new Icon(stream);
                }
            }

            return null;
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadLatestTimeData(true);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/ChangeWallpaperAppUI;component/earth.ico", UriKind.Absolute);
            bitmap.EndInit();

            icoDisplay.Source = bitmap;

            DataContext = this;
            if (wallpaperUpdateTimerPreview == null) {
                wallpaperUpdateTimerPreview = new DispatcherTimer();
                wallpaperUpdateTimerPreview.Interval = TimeSpan.FromMinutes(10); // Set the interval to 10 minutes
                wallpaperUpdateTimerPreview.Tick += WallpaperUpdateTimerPreview_Tick;
                wallpaperUpdateTimerPreview.Start();
                Console.WriteLine($"Timer Preview Start");
            }

//            ApplyWallpaperAsync(1, true, "").ConfigureAwait(true);

            // Initialize NotifyIcon
            contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.Items.Add("Open", null, (sender, args) => ShowWindow());
            contextMenuStrip.Items.Add("Exit", null, (sender, args) => CloseWindow());

            notifyIcon = new NotifyIcon
            {
                Icon = LoadIconFromResource("pack://application:,,,/ChangeWallpaperAppUI;component/earth.ico"),
                Visible = true,
                Text = "Earth Wallpaper",
                ContextMenuStrip = contextMenuStrip
            };


            notifyIcon.DoubleClick += (sender, args) => ShowWindow();

            // Handle window state changes to minimize to tray
            StateChanged += MainWindow_StateChanged;

            //ApplyWallpaperAsync(4, false).ConfigureAwait(false);
        }
        private async void LoadLatestTimeData(bool loadPreview)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try {
                    string jsonResponse = await httpClient.GetStringAsync("https://himawari8.nict.go.jp/img/FULL_24h/latest.json?_=" + DateTimeOffset.Now.ToUnixTimeMilliseconds());
                    var latestData = JsonConvert.DeserializeObject<LatestImageInfo>(jsonResponse);
                    Console.WriteLine("Latest Image Date: " + latestData.Date);
                    currentDateTime = DateTime.Parse(latestData.Date);
                    maxDateTime = currentDateTime;
                    RightButton.IsEnabled = false;
                } catch (Exception ex) {
                    Console.WriteLine("Error fetching image data: " + ex.Message);
                }
            }
            UpdateDateTimeLabel();
            if(loadPreview){
                ApplyWallpaperAsync(1, true, "").ConfigureAwait(true);
            }
        }

        private void UpdateDateTimeLabel()
        {
            DateTimeLabel.Text = currentDateTime.ToLocalTime().ToString("G"); // "G" for general date/time pattern
        }
        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            // Subtract 10 minutes
            currentDateTime = currentDateTime.AddMinutes(-10);
            isSync = false;
            UpdateDateTimeLabel();
            RightButton.IsEnabled = true;
            ApplyWallpaperAsync(1, true, "").ConfigureAwait(true);
            if (wallpaperUpdateTimerPreview != null && wallpaperUpdateTimerPreview.IsEnabled) {
                wallpaperUpdateTimerPreview.Stop();
                Console.WriteLine($"Timer Preview Stop");
            }

        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentDateTime < maxDateTime) {
                // Add 10 minutes
                currentDateTime = currentDateTime.AddMinutes(10);
                isSync = false;
                UpdateDateTimeLabel();
                if (currentDateTime == maxDateTime) {
                    RightButton.IsEnabled = false;
                    isSync = true;
                    if (wallpaperUpdateTimerPreview != null && wallpaperUpdateTimerPreview.IsEnabled) {
                        wallpaperUpdateTimerPreview.Stop();
                        Console.WriteLine($"Timer Preview Stop");
                    }
                    if (wallpaperUpdateTimerPreview == null) {
                        wallpaperUpdateTimerPreview = new DispatcherTimer();
                        wallpaperUpdateTimerPreview.Interval = TimeSpan.FromMinutes(10); // Set the interval to 10 minutes
                        wallpaperUpdateTimerPreview.Tick += WallpaperUpdateTimerPreview_Tick;
                        Console.WriteLine($"Timer Preview New");

                    }
                    wallpaperUpdateTimerPreview.Start();
                    Console.WriteLine($"Timer Preview Start");
                }
                ApplyWallpaperAsync(1, true, "").ConfigureAwait(true);
            }
        }

        private void ApplyWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            if (wallpaperUpdateTimer != null && wallpaperUpdateTimer.IsEnabled) {
                wallpaperUpdateTimer.Stop();
            }

            if (isSync) {
                wallpaperUpdateTimer = new DispatcherTimer();
                wallpaperUpdateTimer.Interval = TimeSpan.FromMinutes(10); // Set the interval to 10 minutes
                wallpaperUpdateTimer.Tick += WallpaperUpdateTimer_Tick;
                wallpaperUpdateTimer.Start();
            }

            ApplyWallpaperAsync(4, false, "").ConfigureAwait(false);
        }

        private async void WallpaperUpdateTimerPreview_Tick(object sender, EventArgs e)
        {
            // Execute your methods
            await ApplyWallpaperAsync(1, true, "").ConfigureAwait(true);

        }
        private async void WallpaperUpdateTimer_Tick(object sender, EventArgs e)
        {
            // Execute your methods
            await ApplyWallpaperAsync(4, false, "").ConfigureAwait(false);

        }

        private async Task ApplyWallpaperAsync(int matrixSize, bool isPreview, string savePath)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            try
            {
                // Define your image size and matrix size
                int imageSize = 550;
                int outputImageSize = imageSize * matrixSize;
                using (Bitmap outputImage = new Bitmap(outputImageSize, outputImageSize))
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        var tasks = new List<Task>();

                        var imageDate = currentDateTime;
                        if (isSync) {
                            LoadLatestTimeData(false);
                            imageDate = currentDateTime;
                        }
                        var matrixSizeName = "thumbnail";
                        if (matrixSize > 1) {
                            matrixSizeName = matrixSize+"d";
                        }
                        string baseUrl = "https://himawari8.nict.go.jp/img/D531106/"+matrixSizeName+"/"+imageSize+"/" + imageDate.ToString("yyyy/MM/dd/HHmmss");


                        for (int row = 0; row < matrixSize; row++)
                        {
                            for (int col = 0; col < matrixSize; col++)
                            {
                                int x = col, y = row;
                                string imageUrl = baseUrl + $"_{x}_{y}.png";
                                Console.WriteLine(imageUrl);

                                tasks.Add(DownloadAndDrawImage(httpClient, imageUrl, outputImage, x, y, imageSize));

                            }
                        }

                        await Task.WhenAll(tasks);
                    }

                    if (isPreview) {
                        Dispatcher.Invoke(() =>
                        {
                            using (MemoryStream memory = new MemoryStream())
                            {
                                outputImage.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                                memory.Position = 0;
                                BitmapImage bitmapimage = new BitmapImage();
                                bitmapimage.BeginInit();
                                bitmapimage.StreamSource = memory;
                                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapimage.EndInit();

                                wallpaperPreview.Source = bitmapimage;
                            }
                        });
                    } else {
                        int size = 0;
                        foreach (var screen in Screen.AllScreens){
                            int screenWidth = screen.Bounds.Width;
                            int screenHeight = screen.Bounds.Height;
                            int s = screenWidth;
                            if(screenHeight < screenWidth) {
                                s = screenHeight;
                            }
                            if(size < s) {
                                size = s;
                            }
                        }

                        using (var resizedImage = ResizeImage(outputImage, (int)size, (int)size))
                        {
                        EncoderParameters encoderParameters = new EncoderParameters(1);
                            encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);

                            ImageCodecInfo pngEncoder = GetEncoder(ImageFormat.Png);

                            if (savePath == "") {
                                using (var memoryStream = new MemoryStream())
                                {
                                    resizedImage.Save(memoryStream, pngEncoder, encoderParameters);
                                    memoryStream.Position = 0;
                                    SetDesktopWallpaper(memoryStream);
                                }
                            } else {
                                resizedImage.Save(savePath, pngEncoder, encoderParameters);
                            }
                            //resizedImage.Save(@"C:\temp\output.png", pngEncoder, encoderParameters);
                        }
                        //SetDesktopWallpaper(@"C:\temp\output.png");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            isSync=true;
            LoadLatestTimeData(true);
            if (wallpaperUpdateTimerPreview != null && wallpaperUpdateTimerPreview.IsEnabled) {
                wallpaperUpdateTimerPreview.Stop();
                Console.WriteLine($"Timer Preview Stop");
            }
            if (wallpaperUpdateTimerPreview == null) {
                wallpaperUpdateTimerPreview = new DispatcherTimer();
                wallpaperUpdateTimerPreview.Interval = TimeSpan.FromMinutes(10); // Set the interval to 10 minutes
                wallpaperUpdateTimerPreview.Tick += WallpaperUpdateTimerPreview_Tick;
                Console.WriteLine($"Timer Preview New");

            }
            wallpaperUpdateTimerPreview.Start();
            Console.WriteLine($"Timer Preview Start");

        }
        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "PNG Image|*.png";
            saveFileDialog.Title = "Save an Image File";
            saveFileDialog.ShowDialog();

            if (!string.IsNullOrWhiteSpace(saveFileDialog.FileName))
            {
                // Assuming 'outputImage' is your image to save
                // Make sure 'outputImage' is accessible here, e.g., declared at class level
                
                ApplyWallpaperAsync(4, false, saveFileDialog.FileName).ConfigureAwait(false);

                //outputImage.Save(saveFileDialog.FileName, ImageFormat.Png); // Choose format based on file extension or user selection
            }
        }

        static async Task DownloadAndDrawImage(HttpClient httpClient, string imageUrl, Bitmap outputImage, int x, int y, int imageSize)
        {
            const int maxAttempts = 5;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                try
                {
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    using (MemoryStream ms = new MemoryStream(imageBytes))
                    using (Bitmap image = new Bitmap(ms))
                    {
                        lock (outputImage)
                        {
                            using (Graphics graphics = Graphics.FromImage(outputImage))
                            {
                                graphics.DrawImage(image, x * imageSize, y * imageSize, imageSize, imageSize);
                            }
                        }
                    }
                    return; // Success, exit the method
                }
                catch (Exception ex)
                {
                    attempts++;
                    Console.WriteLine($"Attempt {attempts} failed to download or draw image from {imageUrl}. Error: {ex.Message}");

                    if (attempts >= maxAttempts)
                    {
                        Console.WriteLine($"Max attempts reached. Giving up on {imageUrl}.");
                    }
                    else
                    {
                        Console.WriteLine($"Retrying in 5 seconds...");
                        await Task.Delay(5000); // Wait for 5 seconds before retrying
                    }
                }
            }
        }

        public static void SetDesktopWallpaper(Stream imageStream)
        {
            using (Image wallpaper = Image.FromStream(imageStream))
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "tempWallpaper.png");
                wallpaper.Save(tempPath, ImageFormat.Png);

                SetWallpaperStyle();
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
        }
        public static void SetDesktopWallpaper(string path)
        {
            SetWallpaperStyle();
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            //System.Windows.MessageBox.Show("Wallpaper changed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private static void SetWallpaperStyle()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            if (key != null)
            {
                key.SetValue(@"WallpaperStyle", "6"); // 10 for "Fill"
                key.SetValue(@"TileWallpaper", "0");
                key.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void CloseWindow()
        {
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

    }
    public class LatestImageInfo
    {
        [JsonProperty("date")]
        public string Date { get; set; }
    }

}
*/