using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ModernFences
{
    // =====================================================================
    //  VERİ MODELİ
    // =====================================================================
    public class FenceData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "ARAÇLAR";
        public int X { get; set; } = 200;
        public int Y { get; set; } = 120;
        public int Width { get; set; } = 300;
        public int Height { get; set; } = 420;
        public bool Collapsed { get; set; } = false;
    }

    public class AppConfig
    {
        public List<FenceData> Fences { get; set; } = new List<FenceData>();
        public bool StartWithWindows { get; set; } = false;
    }

    // =====================================================================
    //  UYGULAMA (tüm pencereler + tepsi simgesi)
    // =====================================================================
    public partial class App : Application
    {
        public static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ModernFences");
        public static readonly string StoreDir = Path.Combine(BaseDir, "Depo");
        private static readonly string ConfigPath = Path.Combine(BaseDir, "config.json");

        public AppConfig Config { get; private set; }
        private readonly List<MainWindow> _windows = new List<MainWindow>();
        private Forms.NotifyIcon _tray;
        private DispatcherTimer _saveTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(StoreDir);

            Config = LoadConfig();
            if (Config.Fences.Count == 0)
                Config.Fences.Add(new FenceData());

            SetupTray();

            foreach (var d in Config.Fences.ToArray())
                OpenFence(d);

            SaveConfig();
        }

        public string StorePathFor(FenceData d)
        {
            string p = Path.Combine(StoreDir, d.Id);
            Directory.CreateDirectory(p);
            return p;
        }

        private void OpenFence(FenceData d)
        {
            var w = new MainWindow(this, d);
            _windows.Add(w);
            w.Closed += (s, e) => _windows.Remove(w);
            w.Show();
        }

        public void NewFence()
        {
            var d = new FenceData
            {
                Title = "YENİ PENCERE",
                X = 240 + _windows.Count * 32,
                Y = 140 + _windows.Count * 32
            };
            Config.Fences.Add(d);
            OpenFence(d);
            SaveConfig();
        }

        public void RemoveFence(MainWindow window, FenceData d)
        {
            try
            {
                string store = StorePathFor(d);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                foreach (var dir in Directory.GetDirectories(store))
                    SafeMove(dir, Path.Combine(desktop, Path.GetFileName(dir)), true);
                foreach (var file in Directory.GetFiles(store))
                    SafeMove(file, Path.Combine(desktop, Path.GetFileName(file)), false);
                Directory.Delete(store, true);
            }
            catch { }

            Config.Fences.Remove(d);
            window.Close();
            SaveConfig();

            if (Config.Fences.Count == 0 && _tray != null)
                _tray.ShowBalloonTip(3000, "Modern Fences",
                    "Tüm pencereler kapandı. Tepsi simgesinden yeni pencere açabilirsiniz.",
                    Forms.ToolTipIcon.Info);
        }

        // --- Ortak yardımcılar ---
        public static string Unique(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                i++;
            } while (File.Exists(candidate) || Directory.Exists(candidate));
            return candidate;
        }

        public static void SafeMove(string src, string dst, bool isDir)
        {
            dst = Unique(dst);
            if (isDir) Directory.Move(src, dst);
            else File.Move(src, dst);
        }

        // --- Yapılandırma (kaydı geciktir: sürükleme sırasında çok tetiklenir) ---
        public void RequestSave()
        {
            if (_saveTimer == null)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); SaveConfig(); };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath))
                           ?? new AppConfig();
            }
            catch { }
            return new AppConfig();
        }

        public void SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(
                    Config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // --- Tepsi simgesi ---
        private void SetupTray()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Yeni Pencere").Click += (s, e) => NewFence();

            var startItem = new Forms.ToolStripMenuItem("Windows ile Başlat")
            {
                Checked = Config.StartWithWindows,
                CheckOnClick = true
            };
            startItem.Click += (s, e) =>
            {
                Config.StartWithWindows = startItem.Checked;
                SetStartup(startItem.Checked);
                SaveConfig();
            };
            menu.Items.Add(startItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Çıkış").Click += (s, e) => ExitApp();

            _tray = new Forms.NotifyIcon
            {
                Text = "Modern Fences",
                Icon = MakeTrayIcon(),
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => NewFence();
        }

        private void ExitApp()
        {
            SaveConfig();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            base.OnExit(e);
        }

        private void SetStartup(bool on)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    if (on) key.SetValue("ModernFences", "\"" + exe + "\"");
                    else key.DeleteValue("ModernFences", false);
                }
            }
            catch { }
        }

        private static Drawing.Icon MakeTrayIcon()
        {
            var bmp = new Drawing.Bitmap(32, 32);
            using (var g = Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Drawing.Color.Transparent);
                using (var b = new Drawing.SolidBrush(Drawing.Color.FromArgb(59, 130, 246)))
                    g.FillRectangle(b, 3, 5, 26, 22);
                using (var b = new Drawing.SolidBrush(Drawing.Color.White))
                {
                    g.FillRectangle(b, 7, 10, 7, 5);
                    g.FillRectangle(b, 18, 10, 7, 5);
                    g.FillRectangle(b, 7, 18, 7, 5);
                    g.FillRectangle(b, 18, 18, 7, 5);
                }
            }
            return Drawing.Icon.FromHandle(bmp.GetHicon());
        }
    }

    // =====================================================================
    //  WINDOWS API YARDIMCILARI (buzlu cam + gerçek ikon)
    // =====================================================================
    internal static class Native
    {
        // --- Buzlu cam (acrylic/blur) ---
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private const int WCA_ACCENT_POLICY = 19;
        private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
        private const int ACCENT_ENABLE_BLURBEHIND = 3;

        public static void EnableBlur(IntPtr hwnd)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    // BLURBEHIND, AllowsTransparency ile daha uyumlu (acrylic bazı
                    // Windows sürümlerinde siyah kutu yapabiliyor). Acrylic istersen
                    // ACCENT_ENABLE_ACRYLICBLURBEHIND yap.
                    AccentState = ACCENT_ENABLE_BLURBEHIND,
                    GradientColor = unchecked((int)0x99000000)
                };
                int size = Marshal.SizeOf(accent);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    SizeOfData = size,
                    Data = ptr
                };
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(ptr);
            }
            catch { }
        }

        // --- Gerçek sistem ikonu (dosya + klasör) ---
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        public static ImageSource GetIconSource(string path)
        {
            try
            {
                var info = new SHFILEINFO();
                SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info),
                    SHGFI_ICON | SHGFI_LARGEICON);
                if (info.hIcon == IntPtr.Zero) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(info.hIcon); // handle sızıntısını önle
                src.Freeze();
                return src;
            }
            catch { return null; }
        }
    }

    // =====================================================================
    //  BASİT METİN GİRİŞ DİYALOĞU
    // =====================================================================
    internal static class PromptDialog
    {
        public static string Show(string prompt, string title, string def)
        {
            var w = new Window
            {
                Title = title,
                Width = 360,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
            var tb = new TextBox { Text = def ?? "" };
            panel.Children.Add(tb);

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var ok = new Button { Content = "Tamam", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "İptal", Width = 75, IsCancel = true };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            panel.Children.Add(btns);
            w.Content = panel;

            string result = null;
            ok.Click += (s, e) => { result = tb.Text; w.DialogResult = true; };
            w.Loaded += (s, e) => { tb.SelectAll(); tb.Focus(); };

            return w.ShowDialog() == true ? result : null;
        }
    }
}
