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
using Windows.ApplicationModel;

namespace ChangeWallpaperAppUI {
    public partial class MainWindow : Window {
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private DispatcherTimer wallpaperUpdateTimer;
        private Boolean isSync = true;
        private DateTime maxDateTime;
        private DispatcherTimer wallpaperUpdateTimerPreview;
        private DateTime currentDateTime;
        private NotifyIcon notifyIcon;

        public MainWindow() {
            InitializeComponent();
            InitializeNotifyIcon();
            //LoadLatestTimeData();
            ToggleStartupTask(true);
            InitializePreviewTimers();
            SetIcon();
        }


        private async void ToggleStartupTask(bool enable)
        {
            StartupTask startupTask = await StartupTask.GetAsync("ChangeWallpaperAppStartupTask");
            if (enable)
            {
                StartupTaskState newState = await startupTask.RequestEnableAsync();
                // Handle newState if needed
            }
            else
            {
                startupTask.Disable();
            }
        }

        private async void InitializePreviewTimers() {
            wallpaperUpdateTimerPreview = new DispatcherTimer {
                Interval = TimeSpan.FromMinutes(10)
            };
            wallpaperUpdateTimerPreview.Tick += async (_, _) => await ApplyWallpaperAsync(1, true, "");
            wallpaperUpdateTimerPreview.Start();
            Console.WriteLine($"Timer Preview Start");
            await ApplyWallpaperAsync(1, true, "");
        }

        private async void InitializeTimers() {
            wallpaperUpdateTimer = new DispatcherTimer {
                Interval = TimeSpan.FromMinutes(10)
            };
            wallpaperUpdateTimer.Tick += async (_, _) => await ApplyWallpaperAsync(4, false, "");
            wallpaperUpdateTimer.Start();
            Console.WriteLine($"Timer Start");
            await ApplyWallpaperAsync(4, false, "");
        }

        private void SetIcon() {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/ChangeWallpaperAppUI;component/Styles/earth.ico", UriKind.RelativeOrAbsolute);
            bitmap.EndInit();
            icoDisplay.Source = bitmap;
        }

        // Initialize NotifyIcon
        private void InitializeNotifyIcon() {
            notifyIcon = new NotifyIcon
            {
                Icon = LoadIconFromResource("pack://application:,,,/ChangeWallpaperAppUI;component/Styles/earth.ico"),
                Visible = true,
                Text = "Earth Wallpaper",
                ContextMenuStrip = new ContextMenuStrip()
            };
            notifyIcon.ContextMenuStrip.Items.Add("Open", null, ShowWindow);
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, CloseWindow);
            notifyIcon.DoubleClick += (_, _) => ShowWindow();
            StateChanged += MainWindow_StateChanged; // Handle window state changes to minimize to tray
        }

        private Icon LoadIconFromResource(string resourcePath)
        {
            var resourceUri = new Uri(resourcePath, UriKind.RelativeOrAbsolute);
            var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);

            if (streamInfo != null) {
                using (var stream = streamInfo.Stream) {
                    return new Icon(stream);
                }
            }
            return null;
        }
        private void MainWindow_StateChanged(object sender, EventArgs e) {
            if (WindowState == WindowState.Minimized) {
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void ShowWindow(object sender = null, EventArgs e = null) {
            Show();
            WindowState = WindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void CloseWindow(object sender, EventArgs e) {
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private async Task LoadLatestTimeData() {
            using (var httpClient = new HttpClient()) {
                try {
                    Console.WriteLine("Update time");
                    string jsonResponse = await httpClient.GetStringAsync("https://himawari8.nict.go.jp/img/FULL_24h/latest.json");
                    var latestData = JsonConvert.DeserializeObject<LatestImageInfo>(jsonResponse);
                    currentDateTime = DateTime.Parse(latestData.Date);
                    DateTimeLabel.Text = currentDateTime.ToLocalTime().ToString("G");
                    maxDateTime = currentDateTime;
                    RightButton.IsEnabled = false;
                } catch (Exception ex) {
                    System.Windows.MessageBox.Show("Error fetching latest image data: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }   

        private async Task ApplyWallpaperAsync(int matrixSize, bool isPreview, string savePath){
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            int imageSize = 550;
            int outputImageSize = imageSize * matrixSize;
            using (var outputImage = new Bitmap(outputImageSize, outputImageSize)) {
                using (var httpClient = new HttpClient()) {
                    string matrixSizeName = matrixSize > 1 ? $"{matrixSize}d" : "thumbnail";
                    var imageDate = currentDateTime;
                    if (isSync) {
                        await LoadLatestTimeData();
                        imageDate = currentDateTime;
                    }
                    string baseUrl = $"https://himawari8.nict.go.jp/img/D531106/{matrixSizeName}/{imageSize}/{imageDate:yyyy/MM/dd/HHmmss}";
                    var tasks = new List<Task>();
                    for (int row = 0; row < matrixSize; row++) {
                        for (int col = 0; col < matrixSize; col++) {
                            string imageUrl = $"{baseUrl}_{col}_{row}.png";
                            tasks.Add(DownloadAndDrawImage(httpClient, imageUrl, outputImage, col, row, imageSize));
                        }
                    }
                    Console.WriteLine($"Draw earth size {matrixSize}");
                    await Task.WhenAll(tasks);
                }
                if (isPreview) {
                    UpdatePreview(outputImage);
                } else {
                    ProcessForWallpaper(outputImage, savePath);
                }
            }
        }

        private async Task DownloadAndDrawImage(HttpClient httpClient, string imageUrl, Bitmap outputImage, int x, int y, int imageSize) {
            const int maxAttempts = 5;
            int attempts = 0;
            while (attempts < maxAttempts) {
                try {
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    using (var ms = new MemoryStream(imageBytes))
                    using (var image = new Bitmap(ms)) {
                        lock (outputImage) {
                            using (var graphics = Graphics.FromImage(outputImage)) {
                                graphics.DrawImage(image, x * imageSize, y * imageSize, imageSize, imageSize);
                            }
                        }
                    }
                    return;
                }
                catch (Exception ex) {
                    if (++attempts >= maxAttempts) {
                        System.Windows.MessageBox.Show($"Failed to download or draw image from {imageUrl}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    await Task.Delay(5000);
                }
            }
        }

        private void UpdatePreview(Bitmap outputImage) {
            Dispatcher.Invoke(() => {
                using (var memory = new MemoryStream()) {
                    outputImage.Save(memory, ImageFormat.Png);
                    memory.Position = 0;
                    var bitmapimage = new BitmapImage();
                    bitmapimage.BeginInit();
                    bitmapimage.StreamSource = memory;
                    bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapimage.EndInit();
                    wallpaperPreview.Source = bitmapimage;
                }
            });
        }

        private void ProcessForWallpaper(Bitmap outputImage, string savePath) {
            int size = 0;
            foreach (var screen in Screen.AllScreens) {
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

            using (var resizedImage = ResizeImage(outputImage, size, size)) {
                if (string.IsNullOrEmpty(savePath)) {
                    using (var memoryStream = new MemoryStream()) {
                        resizedImage.Save(memoryStream, ImageFormat.Png);
                        memoryStream.Position = 0;
                        SetDesktopWallpaper(memoryStream);
                    }
                } else {
                    resizedImage.Save(savePath, ImageFormat.Png);
                }
            }
        }

        private static Bitmap ResizeImage(Image image, int width, int height) {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage)) {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes()) {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        public static void SetDesktopWallpaper(Stream imageStream)
        {
            using (var wallpaper = Image.FromStream(imageStream))
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "tempWallpaper.png");
                wallpaper.Save(tempPath, ImageFormat.Png);
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key != null) {
                    key.SetValue(@"WallpaperStyle", "6"); // 10 for "Fill"
                    key.SetValue(@"TileWallpaper", "0");
                    key.Close();
                }
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            AdjustDateTime(-10);
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            AdjustDateTime(10);
        }

        private void AdjustDateTime(int minutes)
        {
            currentDateTime = currentDateTime.AddMinutes(minutes);
            if (wallpaperUpdateTimerPreview != null && wallpaperUpdateTimerPreview.IsEnabled) {
                wallpaperUpdateTimerPreview.Stop();
                Console.WriteLine($"Timer Preview Stop");
            }
            if (currentDateTime < maxDateTime) {
                isSync = false;
                RightButton.IsEnabled = true;
            } else {
                currentDateTime = maxDateTime;
                isSync = true;
                RightButton.IsEnabled = false;
                InitializePreviewTimers();

            }
            DateTimeLabel.Text = currentDateTime.ToLocalTime().ToString("G");
            ApplyWallpaperAsync(1, true, "").ConfigureAwait(false);
        }

        private void ApplyWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            if (wallpaperUpdateTimer != null && wallpaperUpdateTimer.IsEnabled) {
                wallpaperUpdateTimer.Stop();
            }

            if (isSync) {
                InitializeTimers();
            }
            ApplyWallpaperAsync(4, false, "").ConfigureAwait(false);
        }

        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Save an Image File"
            };
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ApplyWallpaperAsync(4, false, saveFileDialog.FileName).ConfigureAwait(false);
            }
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

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            isSync=true;
            //LoadLatestTimeData();
            if (wallpaperUpdateTimerPreview != null && wallpaperUpdateTimerPreview.IsEnabled) {
                wallpaperUpdateTimerPreview.Stop();
                Console.WriteLine($"Timer Preview Stop");
            }

            InitializePreviewTimers();

        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
    public class LatestImageInfo
    {
        [JsonProperty("date")]
        public string Date { get; set; }
    }

}
