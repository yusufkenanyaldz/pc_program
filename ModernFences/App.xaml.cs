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

        // Görünüm: arka plan tonu (ARGB). Varsayılan #33000000 (koyu, yarı şeffaf)
        public int A = 51;
        public int R = 0;
        public int G = 0;
        public int B = 0;

        // Fare üzerine gelince aç, çekilince kapat
        public bool AutoHide = false;
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
                            var fd = new FenceData
                            {
                                Id = p[1],
                                Title = Uri.UnescapeDataString(p[2]),
                                X = ParseInt(p[3], 200),
                                Y = ParseInt(p[4], 120),
                                Width = ParseInt(p[5], 300),
                                Height = ParseInt(p[6], 420),
                                Collapsed = p[7] == "1"
                            };
                            // Yeni alanlar (eski dosyalarda olmayabilir)
                            if (p.Length >= 13)
                            {
                                fd.A = ParseInt(p[8], 51);
                                fd.R = ParseInt(p[9], 0);
                                fd.G = ParseInt(p[10], 0);
                                fd.B = ParseInt(p[11], 0);
                                fd.AutoHide = p[12] == "1";
                            }
                            cfg.Fences.Add(fd);
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
                        f.Collapsed ? "1" : "0",
                        f.A.ToString(),
                        f.R.ToString(),
                        f.G.ToString(),
                        f.B.ToString(),
                        f.AutoHide ? "1" : "0"));
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

    // =====================================================================
    //  PENCERE AYARLARI (renk / şeffaflık / boyut / fare ile aç-kapat)
    // =====================================================================
    internal static class SettingsDialog
    {
        public static void Show(Window owner, FenceData d, Action apply)
        {
            var w = new Window
            {
                Title = "Pencere Ayarları",
                Width = 350,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Topmost = true,
                Owner = owner
            };

            var panel = new StackPanel { Margin = new Thickness(16) };

            panel.Children.Add(Section("Renk"));
            var rs = MakeSlider(0, 255, d.R);
            var gs = MakeSlider(0, 255, d.G);
            var bs = MakeSlider(0, 255, d.B);
            panel.Children.Add(Row("Kırmızı", rs));
            panel.Children.Add(Row("Yeşil", gs));
            panel.Children.Add(Row("Mavi", bs));

            panel.Children.Add(Section("Şeffaflık"));
            var a = MakeSlider(0, 255, d.A);
            panel.Children.Add(Row("Koyuluk (0=şeffaf)", a));

            panel.Children.Add(Section("Boyut"));
            var ws = MakeSlider(180, 900, d.Width);
            var hs = MakeSlider(120, 1000, d.Height);
            panel.Children.Add(Row("Genişlik", ws));
            panel.Children.Add(Row("Yükseklik", hs));

            var chk = new CheckBox
            {
                Content = "Fare üzerine gelince aç, çekilince kapat",
                IsChecked = d.AutoHide,
                Margin = new Thickness(0, 12, 0, 0)
            };
            panel.Children.Add(chk);

            var swatch = new Border
            {
                Height = 26,
                Margin = new Thickness(0, 12, 0, 0),
                CornerRadius = new CornerRadius(4),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(swatch);

            Action update = () =>
            {
                d.R = (int)rs.Value; d.G = (int)gs.Value; d.B = (int)bs.Value;
                d.A = (int)a.Value; d.Width = (int)ws.Value; d.Height = (int)hs.Value;
                d.AutoHide = chk.IsChecked == true;
                swatch.Background = new SolidColorBrush(
                    Color.FromArgb((byte)d.A, (byte)d.R, (byte)d.G, (byte)d.B));
                apply();
            };

            RoutedPropertyChangedEventHandler<double> onChange = (s, e) => update();
            rs.ValueChanged += onChange; gs.ValueChanged += onChange; bs.ValueChanged += onChange;
            a.ValueChanged += onChange; ws.ValueChanged += onChange; hs.ValueChanged += onChange;
            chk.Checked += (s, e) => update();
            chk.Unchecked += (s, e) => update();

            var close = new Button
            {
                Content = "Kapat",
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                IsCancel = true
            };
            panel.Children.Add(close);

            w.Content = new ScrollViewer { Content = panel };
            update();      // ilk uygulama + örnek renk
            w.ShowDialog();
        }

        private static TextBlock Section(string t) => new TextBlock
        {
            Text = t,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 2)
        };

        private static Slider MakeSlider(double min, double max, double val) => new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Max(min, Math.Min(max, val)),
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        private static UIElement Row(string label, Slider s)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(l, 0);
            Grid.SetColumn(s, 1);
            g.Children.Add(l);
            g.Children.Add(s);
            return g;
        }
    }
}
