using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("AltPrtScnCapture")]
[assembly: AssemblyDescription("A lightweight Windows tray tool for capturing the active window with Alt + PrtScn.")]
[assembly: AssemblyCompany("zzzsssyyy1995")]
[assembly: AssemblyProduct("AltPrtScnCapture")]
[assembly: AssemblyCopyright("Copyright (c) 2026 zzzsssyyy1995")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AltPrtScnCapture
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly ToolStripMenuItem startItem;
        private readonly ToolStripMenuItem stopItem;
        private readonly ToolStripMenuItem mergeItem;
        private readonly HotKeyWindow hotKeyWindow;
        private readonly Icon applicationIcon;
        private readonly string settingsFile;
        private string saveDirectory;
        private bool isMonitoring;

        public TrayApplicationContext()
        {
            settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AltPrtScnCapture",
                "settings.txt");

            applicationIcon = LoadApplicationIcon();

            bool firstRun = !File.Exists(settingsFile);
            saveDirectory = LoadSaveDirectory();
            hotKeyWindow = new HotKeyWindow();
            hotKeyWindow.HotKeyPressed += OnHotKeyPressed;

            ContextMenuStrip menu = new ContextMenuStrip();
            startItem = new ToolStripMenuItem("开始检测", null, OnStartMonitoring);
            stopItem = new ToolStripMenuItem("停止检测", null, OnStopMonitoring);
            ToolStripMenuItem folderItem = new ToolStripMenuItem("设置保存目录", null, OnSelectFolder);
            mergeItem = new ToolStripMenuItem("合并为PDF", null, OnMergePdf);
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出", null, OnExit);

            menu.Items.Add(startItem);
            menu.Items.Add(stopItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(folderItem);
            menu.Items.Add(mergeItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = applicationIcon;
            trayIcon.Text = "Alt + PrtScn 截图（未检测）";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;

            UpdateMenuState();

            if (firstRun)
            {
                Timer firstRunTimer = new Timer();
                firstRunTimer.Interval = 500;
                firstRunTimer.Tick += delegate
                {
                    firstRunTimer.Stop();
                    firstRunTimer.Dispose();
                    ShowFirstRunFolderPrompt();
                };
                firstRunTimer.Start();
            }
        }

        private string LoadSaveDirectory()
        {
            string fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            try
            {
                if (File.Exists(settingsFile))
                {
                    string configured = File.ReadAllText(settingsFile, Encoding.UTF8).Trim();
                    if (!String.IsNullOrWhiteSpace(configured))
                    {
                        return configured;
                    }
                }
            }
            catch
            {
                // Invalid or unreadable settings fall back to the Pictures folder.
            }
            return fallback;
        }

        private void SaveSettings()
        {
            string parent = Path.GetDirectoryName(settingsFile);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.WriteAllText(settingsFile, saveDirectory, new UTF8Encoding(false));
        }

        private void ShowFirstRunFolderPrompt()
        {
            MessageBox.Show(
                "首次使用，请选择截图保存目录。若取消，将使用系统“图片”文件夹。\n\n程序当前未开始检测，请通过托盘菜单手动开启。",
                "Alt + PrtScn 截图",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            SelectFolder(true);
        }

        private void OnSelectFolder(object sender, EventArgs e)
        {
            SelectFolder(false);
        }

        private void SelectFolder(bool firstRun)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择截图保存目录";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(saveDirectory))
                {
                    dialog.SelectedPath = saveDirectory;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    saveDirectory = dialog.SelectedPath;
                    SaveSettings();
                    ShowBalloon("保存目录已更新", saveDirectory, ToolTipIcon.Info);
                }
                else if (firstRun)
                {
                    SaveSettings();
                }
            }
        }

        private void OnStartMonitoring(object sender, EventArgs e)
        {
            if (isMonitoring)
            {
                return;
            }

            if (!hotKeyWindow.Register())
            {
                MessageBox.Show(
                    "无法注册 Alt + PrtScn。该组合键可能已被其他程序占用。",
                    "启动检测失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            isMonitoring = true;
            UpdateMenuState();
            ShowBalloon("已开始检测", "按 Alt + PrtScn 截取当前活动窗口。", ToolTipIcon.Info);
        }

        private void OnStopMonitoring(object sender, EventArgs e)
        {
            if (!isMonitoring)
            {
                return;
            }

            hotKeyWindow.Unregister();
            isMonitoring = false;
            UpdateMenuState();
            ShowBalloon("已停止检测", "Alt + PrtScn 监听已关闭。", ToolTipIcon.Info);
        }

        private void UpdateMenuState()
        {
            startItem.Enabled = !isMonitoring;
            stopItem.Enabled = isMonitoring;
            if (trayIcon != null)
            {
                trayIcon.Text = isMonitoring
                    ? "Alt + PrtScn 截图（检测中）"
                    : "Alt + PrtScn 截图（未检测）";
            }
        }

        private void OnHotKeyPressed(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(saveDirectory);
                string outputPath = GetUniqueTimestampPath(saveDirectory, "Screenshot_", ".png");
                WindowCapture.CaptureForegroundWindow(outputPath);
                ShowBalloon("截图已保存", Path.GetFileName(outputPath), ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloon("截图失败", ex.Message, ToolTipIcon.Error);
            }
        }

        private void OnMergePdf(object sender, EventArgs e)
        {
            string directory = saveDirectory;
            string[] images;
            try
            {
                images = PdfImageMerger.GetImageFiles(directory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "读取图片失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (images.Length == 0)
            {
                MessageBox.Show(
                    "当前保存目录中没有 PNG、JPG 或 JPEG 图片。",
                    "合并为PDF",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            mergeItem.Enabled = false;
            ShowBalloon("正在合并PDF", "共 " + images.Length + " 张图片。", ToolTipIcon.Info);

            Task.Run(delegate
            {
                string outputPath = GetUniqueTimestampPath(directory, "Merged_", ".pdf");
                PdfImageMerger.Merge(images, outputPath);
                return outputPath;
            }).ContinueWith(delegate(Task<string> task)
            {
                mergeItem.Enabled = true;
                if (task.IsFaulted)
                {
                    Exception error = task.Exception.GetBaseException();
                    MessageBox.Show(error.Message, "PDF合并失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ShowBalloon("PDF合并完成", Path.GetFileName(task.Result), ToolTipIcon.Info);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private static string GetUniqueTimestampPath(string directory, string prefix, string extension)
        {
            string baseName = prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string candidate = Path.Combine(directory, baseName + extension);
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, baseName + "_" + suffix.ToString("000", CultureInfo.InvariantCulture) + extension);
                suffix++;
            }
            return candidate;
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = text;
            trayIcon.BalloonTipIcon = icon;
            trayIcon.ShowBalloonTip(2500);
        }

        private void OnExit(object sender, EventArgs e)
        {
            if (isMonitoring)
            {
                hotKeyWindow.Unregister();
            }
            trayIcon.Visible = false;
            trayIcon.Dispose();
            applicationIcon.Dispose();
            hotKeyWindow.Dispose();
            ExitThread();
        }

        private static Icon LoadApplicationIcon()
        {
            Stream iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("AltPrtScnCapture.app-icon.ico");
            if (iconStream == null)
            {
                return (Icon)SystemIcons.Application.Clone();
            }

            using (iconStream)
            using (Icon embeddedIcon = new Icon(iconStream))
            {
                return (Icon)embeddedIcon.Clone();
            }
        }
    }

    internal sealed class HotKeyWindow : NativeWindow, IDisposable
    {
        private const int HotKeyId = 0x5353;
        private const int WmHotKey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkSnapshot = 0x2C;
        private bool registered;

        public event EventHandler HotKeyPressed;

        public HotKeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        public bool Register()
        {
            if (registered)
            {
                return true;
            }
            registered = RegisterHotKey(Handle, HotKeyId, ModAlt | ModNoRepeat, VkSnapshot);
            return registered;
        }

        public void Unregister()
        {
            if (!registered)
            {
                return;
            }
            UnregisterHotKey(Handle, HotKeyId);
            registered = false;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotKey && message.WParam.ToInt32() == HotKeyId)
            {
                EventHandler handler = HotKeyPressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            Unregister();
            DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal static class WindowCapture
    {
        private const int DwmExtendedFrameBounds = 9;

        public static void CaptureForegroundWindow(string outputPath)
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                throw new InvalidOperationException("未找到当前活动窗口。");
            }

            NativeRect rect;
            int dwmResult = DwmGetWindowAttribute(
                window,
                DwmExtendedFrameBounds,
                out rect,
                Marshal.SizeOf(typeof(NativeRect)));

            if (dwmResult != 0 && !GetWindowRect(window, out rect))
            {
                throw new InvalidOperationException("无法获取活动窗口范围。");
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("活动窗口尺寸无效。");
            }

            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    rect.Left,
                    rect.Top,
                    0,
                    0,
                    new Size(width, height),
                    CopyPixelOperation.SourceCopy);
                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out NativeRect value, int size);
    }

    internal static class PdfImageMerger
    {
        private sealed class PdfImage
        {
            public int Width;
            public int Height;
            public byte[] JpegData;
        }

        public static string[] GetImageFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return new string[0];
            }

            return Directory.EnumerateFiles(directory)
                .Where(delegate(string path)
                {
                    string extension = Path.GetExtension(path);
                    return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(delegate(string path) { return Path.GetFileName(path); }, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static void Merge(string[] imageFiles, string outputPath)
        {
            List<PdfImage> images = new List<PdfImage>();
            foreach (string imageFile in imageFiles)
            {
                images.Add(LoadAsJpeg(imageFile));
            }

            WritePdf(images, outputPath);
        }

        private static PdfImage LoadAsJpeg(string path)
        {
            using (Image source = Image.FromFile(path))
            {
                ApplyExifOrientation(source);
                using (Bitmap rgb = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb))
                using (Graphics graphics = Graphics.FromImage(rgb))
                using (MemoryStream jpegStream = new MemoryStream())
                {
                    graphics.Clear(Color.White);
                    graphics.DrawImage(source, 0, 0, source.Width, source.Height);

                    ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                        .First(delegate(ImageCodecInfo codec) { return codec.FormatID == ImageFormat.Jpeg.Guid; });
                    using (EncoderParameters parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                        rgb.Save(jpegStream, encoder, parameters);
                    }

                    return new PdfImage
                    {
                        Width = rgb.Width,
                        Height = rgb.Height,
                        JpegData = jpegStream.ToArray()
                    };
                }
            }
        }

        private static void ApplyExifOrientation(Image image)
        {
            const int OrientationId = 0x0112;
            if (!image.PropertyIdList.Contains(OrientationId))
            {
                return;
            }

            int orientation = image.GetPropertyItem(OrientationId).Value[0];
            switch (orientation)
            {
                case 2: image.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: image.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: image.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: image.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }

        private static void WritePdf(IList<PdfImage> images, string outputPath)
        {
            int objectCount = 2 + images.Count * 3;
            long[] offsets = new long[objectCount + 1];

            using (FileStream stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                WriteAscii(stream, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

                StartObject(stream, offsets, 1);
                WriteAscii(stream, "<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

                StartObject(stream, offsets, 2);
                StringBuilder kids = new StringBuilder();
                for (int i = 0; i < images.Count; i++)
                {
                    kids.Append((3 + i * 3).ToString(CultureInfo.InvariantCulture));
                    kids.Append(" 0 R ");
                }
                WriteAscii(stream, "<< /Type /Pages /Count " + images.Count.ToString(CultureInfo.InvariantCulture)
                    + " /Kids [ " + kids + "] >>\nendobj\n");

                for (int i = 0; i < images.Count; i++)
                {
                    PdfImage image = images[i];
                    int pageId = 3 + i * 3;
                    int contentId = pageId + 1;
                    int imageId = pageId + 2;

                    bool landscape = image.Width >= image.Height;
                    double pageWidth = landscape ? 842.0 : 595.0;
                    double pageHeight = landscape ? 595.0 : 842.0;
                    double margin = 18.0;
                    double scale = Math.Min(
                        (pageWidth - margin * 2.0) / image.Width,
                        (pageHeight - margin * 2.0) / image.Height);
                    double drawWidth = image.Width * scale;
                    double drawHeight = image.Height * scale;
                    double x = (pageWidth - drawWidth) / 2.0;
                    double y = (pageHeight - drawHeight) / 2.0;

                    StartObject(stream, offsets, pageId);
                    WriteAscii(stream,
                        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + F(pageWidth) + " " + F(pageHeight)
                        + "] /Resources << /XObject << /Im0 " + imageId.ToString(CultureInfo.InvariantCulture)
                        + " 0 R >> >> /Contents " + contentId.ToString(CultureInfo.InvariantCulture) + " 0 R >>\nendobj\n");

                    string content = "q\n" + F(drawWidth) + " 0 0 " + F(drawHeight) + " " + F(x) + " " + F(y)
                        + " cm\n/Im0 Do\nQ\n";
                    byte[] contentBytes = Encoding.ASCII.GetBytes(content);
                    StartObject(stream, offsets, contentId);
                    WriteAscii(stream, "<< /Length " + contentBytes.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
                    stream.Write(contentBytes, 0, contentBytes.Length);
                    WriteAscii(stream, "endstream\nendobj\n");

                    StartObject(stream, offsets, imageId);
                    WriteAscii(stream,
                        "<< /Type /XObject /Subtype /Image /Width " + image.Width.ToString(CultureInfo.InvariantCulture)
                        + " /Height " + image.Height.ToString(CultureInfo.InvariantCulture)
                        + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length "
                        + image.JpegData.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
                    stream.Write(image.JpegData, 0, image.JpegData.Length);
                    WriteAscii(stream, "\nendstream\nendobj\n");
                }

                long xrefOffset = stream.Position;
                WriteAscii(stream, "xref\n0 " + (objectCount + 1).ToString(CultureInfo.InvariantCulture) + "\n");
                WriteAscii(stream, "0000000000 65535 f \n");
                for (int id = 1; id <= objectCount; id++)
                {
                    WriteAscii(stream, offsets[id].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
                }

                WriteAscii(stream,
                    "trailer\n<< /Size " + (objectCount + 1).ToString(CultureInfo.InvariantCulture)
                    + " /Root 1 0 R >>\nstartxref\n" + xrefOffset.ToString(CultureInfo.InvariantCulture)
                    + "\n%%EOF\n");
            }
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void StartObject(Stream stream, long[] offsets, int id)
        {
            offsets[id] = stream.Position;
            WriteAscii(stream, id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.GetEncoding(28591).GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
