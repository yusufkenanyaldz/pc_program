using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ModernFences
{
    // =====================================================================
    //  VERİ MODELİ
    // =====================================================================
    public class FenceData
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Title = "ARAÇLAR";
        public int X = 200;
        public int Y = 120;
        public int Width = 300;
        public int Height = 420;
        public bool Collapsed = false;
    }

    public class AppConfig
    {
        public List<FenceData> Fences = new List<FenceData>();
        public bool StartWithWindows = false;
    }

    // =====================================================================
    //  UYGULAMA (tüm pencereleri yönetir)
    // =====================================================================
    public partial class App : Application
    {
        public static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ModernFences");
        public static readonly string StoreDir = Path.Combine(BaseDir, "Depo");
        private static readonly string ConfigPath = Path.Combine(BaseDir, "config.txt");

        public AppConfig Config { get; private set; }
        private readonly List<MainWindow> _windows = new List<MainWindow>();
        private DispatcherTimer _saveTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(StoreDir);

            Config = LoadConfig();
            if (Config.Fences.Count == 0)
                Config.Fences.Add(new FenceData());

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
            SaveConfig();
            window.Close(); // son pencereyse OnLastWindowClose ile uygulama kapanır
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

        // --- Yapılandırma (harici kütüphane olmadan basit metin biçimi) ---
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
            var cfg = new AppConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return cfg;
                foreach (var line in File.ReadAllLines(ConfigPath))
                {
                    if (line.StartsWith("STARTWITHWINDOWS="))
                    {
                        cfg.StartWithWindows = line.EndsWith("1");
                    }
                    else if (line.StartsWith("FENCE|"))
                    {
                        var p = line.Split('|');
                        if (p.Length >= 8)
                        {
                            cfg.Fences.Add(new FenceData
                            {
                                Id = p[1],
                                Title = Uri.UnescapeDataString(p[2]),
                                X = ParseInt(p[3], 200),
                                Y = ParseInt(p[4], 120),
                                Width = ParseInt(p[5], 300),
                                Height = ParseInt(p[6], 420),
                                Collapsed = p[7] == "1"
                            });
                        }
                    }
                }
            }
            catch { }
            return cfg;
        }

        public void SaveConfig()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("STARTWITHWINDOWS=" + (Config.StartWithWindows ? "1" : "0"));
                foreach (var f in Config.Fences)
                {
                    sb.AppendLine(string.Join("|",
                        "FENCE",
                        f.Id,
                        Uri.EscapeDataString(f.Title ?? ""),
                        f.X.ToString(),
                        f.Y.ToString(),
                        f.Width.ToString(),
                        f.Height.ToString(),
                        f.Collapsed ? "1" : "0"));
                }
                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch { }
        }

        private static int ParseInt(string s, int def)
        {
            int v;
            return int.TryParse(s, out v) ? v : def;
        }

        // --- Windows ile başlat (kayıt defteri Run anahtarı) ---
        public void SetStartup(bool on)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    string exe = Process.GetCurrentProcess().MainModule.FileName;
                    if (on) key.SetValue("ModernFences", "\"" + exe + "\"");
                    else key.DeleteValue("ModernFences", false);
                }
            }
            catch { }
        }
    }

    // =====================================================================
    //  WINDOWS API YARDIMCILARI (buzlu cam + gerçek ikon) — ekstra referans yok
    // =====================================================================
    internal static class Native
    {
        // --- Buzlu cam (blur) ---
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
        private const int ACCENT_ENABLE_BLURBEHIND = 3;
        private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4; // istenirse

        public static void EnableBlur(IntPtr hwnd)
        {
            try
            {
                var accent = new AccentPolicy
                {
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
