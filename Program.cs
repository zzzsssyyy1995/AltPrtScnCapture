using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("AltPrtScnCapture")]
[assembly: AssemblyDescription("A lightweight Windows tray tool for capturing the active window with a configurable Print Screen hotkey.")]
[assembly: AssemblyCompany("zzzsssyyy1995")]
[assembly: AssemblyProduct("AltPrtScnCapture")]
[assembly: AssemblyCopyright("Copyright (c) 2026 zzzsssyyy1995")]
[assembly: AssemblyVersion("1.1.2.0")]
[assembly: AssemblyFileVersion("1.1.2.0")]

namespace AltPrtScnCapture
{
    internal enum HotKeyMode
    {
        PrintScreen,
        AltPrintScreen
    }

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
        private readonly ToolStripMenuItem printScreenHotKeyItem;
        private readonly ToolStripMenuItem altPrintScreenHotKeyItem;
        private readonly ToolStripMenuItem elevationItem;
        private readonly ToolStripMenuItem mergeItem;
        private readonly HotKeyWindow hotKeyWindow;
        private readonly Icon applicationIcon;
        private readonly string settingsFile;
        private readonly string hotKeySettingsFile;
        private string saveDirectory;
        private HotKeyMode hotKeyMode;
        private bool isMonitoring;
        private bool isExiting;

        public TrayApplicationContext()
        {
            settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AltPrtScnCapture",
                "settings.txt");
            hotKeySettingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AltPrtScnCapture",
                "hotkey.txt");

            applicationIcon = LoadApplicationIcon();

            bool firstRun = !File.Exists(settingsFile);
            saveDirectory = LoadSaveDirectory();
            hotKeyMode = LoadHotKeyMode();
            hotKeyWindow = new HotKeyWindow();
            hotKeyWindow.HotKeyPressed += OnHotKeyPressed;

            ContextMenuStrip menu = new ContextMenuStrip();
            startItem = new ToolStripMenuItem("开始检测", null, OnStartMonitoring);
            stopItem = new ToolStripMenuItem("停止检测", null, OnStopMonitoring);
            ToolStripMenuItem hotKeyItem = new ToolStripMenuItem("截图快捷键");
            printScreenHotKeyItem = new ToolStripMenuItem("PrtScn", null, OnSelectPrintScreenHotKey);
            altPrintScreenHotKeyItem = new ToolStripMenuItem("Alt + PrtScn", null, OnSelectAltPrintScreenHotKey);
            hotKeyItem.DropDownItems.Add(printScreenHotKeyItem);
            hotKeyItem.DropDownItems.Add(altPrintScreenHotKeyItem);
            ToolStripMenuItem folderItem = new ToolStripMenuItem("设置保存目录", null, OnSelectFolder);
            mergeItem = new ToolStripMenuItem("合并为PDF", null, OnMergePdf);
            bool isAdministrator = IsRunningAsAdministrator();
            elevationItem = new ToolStripMenuItem(
                isAdministrator ? "管理员模式 ✓" : "以管理员身份重启",
                null,
                OnRestartAsAdministrator);
            elevationItem.Enabled = !isAdministrator;
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出", null, OnExit);

            menu.Items.Add(startItem);
            menu.Items.Add(stopItem);
            menu.Items.Add(hotKeyItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(folderItem);
            menu.Items.Add(mergeItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(elevationItem);
            menu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = applicationIcon;
            trayIcon.Text = "截图工具（未检测）";
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

        private HotKeyMode LoadHotKeyMode()
        {
            try
            {
                if (File.Exists(hotKeySettingsFile))
                {
                    string configured = File.ReadAllText(hotKeySettingsFile, Encoding.UTF8).Trim();
                    if (configured.Equals("AltPrintScreen", StringComparison.OrdinalIgnoreCase))
                    {
                        return HotKeyMode.AltPrintScreen;
                    }
                }
            }
            catch
            {
                // Invalid or unreadable settings fall back to the simpler Print Screen key.
            }
            return HotKeyMode.PrintScreen;
        }

        private void SaveHotKeyMode()
        {
            string parent = Path.GetDirectoryName(hotKeySettingsFile);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.WriteAllText(hotKeySettingsFile, hotKeyMode.ToString(), new UTF8Encoding(false));
        }

        private void ShowFirstRunFolderPrompt()
        {
            MessageBox.Show(
                "首次使用，请选择截图保存目录。若取消，将使用系统“图片”文件夹。\n\n程序当前未开始检测，请通过托盘菜单手动开启。",
                "活动窗口截图",
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

            if (!hotKeyWindow.Register(hotKeyMode))
            {
                if (hotKeyMode == HotKeyMode.PrintScreen)
                {
                    DialogResult fallback = MessageBox.Show(
                        "无法启动 PrtScn 按键拦截。\n\n是否切换为 Alt + PrtScn 并开始检测？",
                        "快捷键不可用",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (fallback == DialogResult.Yes && hotKeyWindow.Register(HotKeyMode.AltPrintScreen))
                    {
                        hotKeyMode = HotKeyMode.AltPrintScreen;
                        SaveHotKeyMode();
                        CompleteMonitoringStart();
                        return;
                    }

                    if (fallback == DialogResult.Yes)
                    {
                        MessageBox.Show(
                            "Alt + PrtScn 也无法注册。该快捷键可能已被其他程序占用。",
                            "启动检测失败",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    return;
                }

                MessageBox.Show(
                    "无法注册 " + GetHotKeyDisplayName(hotKeyMode) + "。该快捷键可能已被其他程序占用。",
                    "启动检测失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            CompleteMonitoringStart();
        }

        private void CompleteMonitoringStart()
        {
            isMonitoring = true;
            UpdateMenuState();
            ShowBalloon("已开始检测", "按 " + GetHotKeyDisplayName(hotKeyMode) + " 截取当前活动窗口。", ToolTipIcon.Info);
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
            ShowBalloon("已停止检测", GetHotKeyDisplayName(hotKeyMode) + " 监听已关闭。", ToolTipIcon.Info);
        }

        private void OnSelectPrintScreenHotKey(object sender, EventArgs e)
        {
            ChangeHotKeyMode(HotKeyMode.PrintScreen);
        }

        private void OnSelectAltPrintScreenHotKey(object sender, EventArgs e)
        {
            ChangeHotKeyMode(HotKeyMode.AltPrintScreen);
        }

        private void ChangeHotKeyMode(HotKeyMode newMode)
        {
            if (newMode == hotKeyMode)
            {
                return;
            }

            HotKeyMode previousMode = hotKeyMode;
            if (!isMonitoring)
            {
                hotKeyMode = newMode;
                SaveHotKeyMode();
                UpdateMenuState();
                ShowBalloon("快捷键已更新", "开始检测后使用 " + GetHotKeyDisplayName(hotKeyMode) + "。", ToolTipIcon.Info);
                return;
            }

            hotKeyWindow.Unregister();
            if (hotKeyWindow.Register(newMode))
            {
                hotKeyMode = newMode;
                SaveHotKeyMode();
                UpdateMenuState();
                ShowBalloon("快捷键已切换", "现在使用 " + GetHotKeyDisplayName(hotKeyMode) + "。", ToolTipIcon.Info);
                return;
            }

            bool restored = hotKeyWindow.Register(previousMode);
            if (!restored)
            {
                isMonitoring = false;
            }
            UpdateMenuState();

            MessageBox.Show(
                restored
                    ? "无法注册 " + GetHotKeyDisplayName(newMode) + "，已恢复使用 " + GetHotKeyDisplayName(previousMode) + "。"
                    : "无法注册 " + GetHotKeyDisplayName(newMode) + "，原快捷键也无法恢复。检测已停止。",
                "切换快捷键失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void UpdateMenuState()
        {
            startItem.Enabled = !isMonitoring;
            stopItem.Enabled = isMonitoring;
            printScreenHotKeyItem.Checked = hotKeyMode == HotKeyMode.PrintScreen;
            altPrintScreenHotKeyItem.Checked = hotKeyMode == HotKeyMode.AltPrintScreen;
            if (trayIcon != null)
            {
                trayIcon.Text = GetHotKeyDisplayName(hotKeyMode)
                    + (isMonitoring ? " 截图（检测中）" : " 截图（未检测）");
            }
        }

        private static string GetHotKeyDisplayName(HotKeyMode mode)
        {
            return mode == HotKeyMode.PrintScreen ? "PrtScn" : "Alt + PrtScn";
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

        private void OnRestartAsAdministrator(object sender, EventArgs e)
        {
            if (IsRunningAsAdministrator())
            {
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                "管理员权限可让 PrtScn 在 360 安全卫士、企业管理软件等高权限窗口中正常工作。\n\n程序将退出当前实例并请求管理员权限重新启动。重新启动后仍需手动选择“开始检测”。是否继续？",
                "以管理员身份重启",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                Process elevatedProcess = Process.Start(startInfo);
                if (elevatedProcess == null)
                {
                    throw new InvalidOperationException("Windows 未能创建管理员进程。");
                }
                elevatedProcess.Dispose();
                ShutdownApplication();
            }
            catch (Win32Exception error)
            {
                if (error.NativeErrorCode != 1223)
                {
                    MessageBox.Show(
                        "无法以管理员身份重新启动：" + error.Message,
                        "重新启动失败",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                else
                {
                    ShowBalloon("已取消", "程序继续以普通权限运行。", ToolTipIcon.Info);
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    "无法以管理员身份重新启动：" + error.Message,
                    "重新启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            ShutdownApplication();
        }

        private void ShutdownApplication()
        {
            if (isExiting)
            {
                return;
            }
            isExiting = true;
            hotKeyWindow.Unregister();
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
        private const int WmHookHotKey = 0x8001;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const int WhKeyboardLl = 13;
        private const uint ModAlt = 0x0001;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkSnapshot = 0x2C;
        private const uint VkShift = 0x10;
        private const uint VkControl = 0x11;
        private const uint VkMenu = 0x12;
        private const uint VkLWin = 0x5B;
        private const uint VkRWin = 0x5C;
        private bool registered;
        private bool registeredHotKey;
        private bool printScreenDown;
        private bool shiftDown;
        private bool controlDown;
        private bool altDown;
        private bool windowsDown;
        private IntPtr keyboardHook;
        private readonly LowLevelKeyboardProc keyboardProc;

        public event EventHandler HotKeyPressed;

        public HotKeyWindow()
        {
            keyboardProc = KeyboardHookCallback;
            CreateHandle(new CreateParams());
        }

        public bool Register(HotKeyMode mode)
        {
            if (registered)
            {
                return true;
            }
            if (mode == HotKeyMode.PrintScreen)
            {
                InitializeModifierState();
                keyboardHook = SetWindowsHookEx(
                    WhKeyboardLl,
                    keyboardProc,
                    GetModuleHandle(null),
                    0);
                registered = keyboardHook != IntPtr.Zero;
                return registered;
            }

            registeredHotKey = RegisterHotKey(
                Handle,
                HotKeyId,
                ModAlt | ModNoRepeat,
                VkSnapshot);
            registered = registeredHotKey;
            return registered;
        }

        public void Unregister()
        {
            if (!registered)
            {
                return;
            }
            if (keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
            if (registeredHotKey)
            {
                UnregisterHotKey(Handle, HotKeyId);
                registeredHotKey = false;
            }
            printScreenDown = false;
            registered = false;
        }

        protected override void WndProc(ref Message message)
        {
            if ((message.Msg == WmHotKey && message.WParam.ToInt32() == HotKeyId)
                || message.Msg == WmHookHotKey)
            {
                EventHandler handler = HotKeyPressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            base.WndProc(ref message);
        }

        private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0)
            {
                return CallNextHookEx(keyboardHook, code, wParam, lParam);
            }

            int message = wParam.ToInt32();
            bool isDown = message == WmKeyDown || message == WmSysKeyDown;
            bool isUp = message == WmKeyUp || message == WmSysKeyUp;
            KeyboardHookData data = (KeyboardHookData)Marshal.PtrToStructure(
                lParam,
                typeof(KeyboardHookData));

            UpdateModifierState(data.VirtualKey, isDown, isUp);

            if (data.VirtualKey == VkSnapshot)
            {
                if (isDown)
                {
                    if (printScreenDown)
                    {
                        return new IntPtr(1);
                    }

                    if (!shiftDown && !controlDown && !altDown && !windowsDown)
                    {
                        printScreenDown = true;
                        PostMessage(Handle, WmHookHotKey, IntPtr.Zero, IntPtr.Zero);
                        return new IntPtr(1);
                    }
                }
                else if (isUp && printScreenDown)
                {
                    printScreenDown = false;
                    return new IntPtr(1);
                }
            }

            return CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        private void InitializeModifierState()
        {
            shiftDown = IsKeyDown(VkShift);
            controlDown = IsKeyDown(VkControl);
            altDown = IsKeyDown(VkMenu);
            windowsDown = IsKeyDown(VkLWin) || IsKeyDown(VkRWin);
        }

        private static bool IsKeyDown(uint virtualKey)
        {
            return (GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;
        }

        private void UpdateModifierState(uint virtualKey, bool isDown, bool isUp)
        {
            if (!isDown && !isUp)
            {
                return;
            }

            bool state = isDown;
            if (virtualKey == VkShift || virtualKey == 0xA0 || virtualKey == 0xA1)
            {
                shiftDown = state;
            }
            else if (virtualKey == VkControl || virtualKey == 0xA2 || virtualKey == 0xA3)
            {
                controlDown = state;
            }
            else if (virtualKey == VkMenu || virtualKey == 0xA4 || virtualKey == 0xA5)
            {
                altDown = state;
            }
            else if (virtualKey == VkLWin || virtualKey == VkRWin)
            {
                windowsDown = state;
            }
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int hookType,
            LowLevelKeyboardProc callback,
            IntPtr module,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardHookData
        {
            public uint VirtualKey;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }
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
