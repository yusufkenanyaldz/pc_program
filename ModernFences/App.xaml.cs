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

        // Ekstra görünüm
        public int IconSize = 36;   // simge boyutu (px)
        public int Corner = 10;     // köşe yuvarlaklığı
        public bool Locked = false; // taşıma/boyutlandırma kilidi
        public int Sort = 0;        // 0=ada göre, 1=türe göre

        // Folder Portal: doluysa bu çit gerçek bir klasörü canlı yansıtır
        public string PortalPath = "";
    }

    public class RuleData
    {
        public string Ext = "";       // örn ".png"
        public string TargetId = "";  // hedef çit Id
    }

    public class AppConfig
    {
        public List<FenceData> Fences = new List<FenceData>();
        public List<RuleData> Rules = new List<RuleData>();
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
        private bool _allHidden;

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
            SetupHotkeys();
            StartDesktopWatcher();
        }

        public IEnumerable<FenceData> AllFences { get { return Config.Fences; } }

        public void RefreshFence(string id)
        {
            foreach (var w in _windows)
                if (w.Data.Id == id) { w.ReloadItems(); break; }
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
            // Portal ise gerçek klasöre DOKUNMA; sadece çiti kaldır.
            if (string.IsNullOrEmpty(d.PortalPath))
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
            }

            // Bu çiti hedefleyen kuralları da temizle
            Config.Rules.RemoveAll(r => r.TargetId == d.Id);

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
                    else if (line.StartsWith("RULE|"))
                    {
                        var p = line.Split('|');
                        if (p.Length >= 3)
                            cfg.Rules.Add(new RuleData
                            {
                                Ext = Uri.UnescapeDataString(p[1]),
                                TargetId = p[2]
                            });
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
                            if (p.Length >= 18)
                            {
                                fd.IconSize = ParseInt(p[13], 36);
                                fd.Corner = ParseInt(p[14], 10);
                                fd.Locked = p[15] == "1";
                                fd.Sort = ParseInt(p[16], 0);
                                fd.PortalPath = Uri.UnescapeDataString(p[17]);
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
                        f.AutoHide ? "1" : "0",
                        f.IconSize.ToString(),
                        f.Corner.ToString(),
                        f.Locked ? "1" : "0",
                        f.Sort.ToString(),
                        Uri.EscapeDataString(f.PortalPath ?? "")));
                }
                foreach (var r in Config.Rules)
                {
                    sb.AppendLine(string.Join("|",
                        "RULE", Uri.EscapeDataString(r.Ext ?? ""), r.TargetId ?? ""));
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

        // ================= GLOBAL KISAYOL TUŞLARI (Peek / Hızlı gizle) =========
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 1, MOD_CONTROL = 2;
        private const int WM_HOTKEY = 0x0312;
        private const int HK_HIDE = 1, HK_PEEK = 2;
        private HwndSource _hotkeySource;

        private void SetupHotkeys()
        {
            try
            {
                var parms = new HwndSourceParameters("MF_Hotkeys")
                {
                    Width = 0,
                    Height = 0,
                    ParentWindow = new IntPtr(-3), // HWND_MESSAGE (yalnızca mesaj penceresi)
                    WindowStyle = 0
                };
                _hotkeySource = new HwndSource(parms);
                _hotkeySource.AddHook(HotkeyHook);
                // Ctrl+Alt+H = tümünü gizle/göster,  Ctrl+Alt+F = öne getir (peek)
                RegisterHotKey(_hotkeySource.Handle, HK_HIDE, MOD_CONTROL | MOD_ALT, 0x48);
                RegisterHotKey(_hotkeySource.Handle, HK_PEEK, MOD_CONTROL | MOD_ALT, 0x46);
            }
            catch { }
        }

        private IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HK_HIDE) { ToggleHideAll(); handled = true; }
                else if (id == HK_PEEK) { PeekAll(); handled = true; }
            }
            return IntPtr.Zero;
        }

        public void ToggleHideAll()
        {
            _allHidden = !_allHidden;
            foreach (var w in _windows)
            {
                if (_allHidden) w.Hide();
                else w.Show();
            }
        }

        public void PeekAll()
        {
            foreach (var w in _windows) w.Peek(true);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                foreach (var w in _windows) w.Peek(false);
            };
            t.Start();
        }

        // ================= OTOMATİK KURALLAR (masaüstü izleyici) ===============
        private FileSystemWatcher _desktopWatcher;

        public void StartDesktopWatcher()
        {
            try
            {
                if (Config.Rules.Count == 0) return; // kural yoksa izleme
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                _desktopWatcher = new FileSystemWatcher(desktop)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };
                _desktopWatcher.Created += (s, e) => OnDesktopNew(e.FullPath);
                _desktopWatcher.Renamed += (s, e) => OnDesktopNew(e.FullPath);
            }
            catch { }
        }

        public void RestartDesktopWatcher()
        {
            try { if (_desktopWatcher != null) { _desktopWatcher.Dispose(); _desktopWatcher = null; } }
            catch { }
            StartDesktopWatcher();
        }

        private void OnDesktopNew(string path)
        {
            // Watcher arka plan iş parçacığında çalışır.
            try
            {
                string ext = Directory.Exists(path) ? "" : Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext)) return;

                RuleData rule = null;
                foreach (var r in Config.Rules)
                    if (string.Equals(r.Ext, ext, StringComparison.OrdinalIgnoreCase)) { rule = r; break; }
                if (rule == null) return;

                FenceData target = null;
                foreach (var f in Config.Fences)
                    if (f.Id == rule.TargetId && string.IsNullOrEmpty(f.PortalPath)) { target = f; break; }
                if (target == null) return;

                string store = StorePathFor(target);
                string dst = Unique(Path.Combine(store, Path.GetFileName(path)));

                // Dosya yeni oluşmuş olabilir, kilitliyse birkaç kez dene
                for (int i = 0; i < 6; i++)
                {
                    try { File.Move(path, dst); break; }
                    catch { System.Threading.Thread.Sleep(300); }
                }

                Dispatcher.Invoke(new Action(() => RefreshFence(target.Id)));
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_hotkeySource != null)
                {
                    UnregisterHotKey(_hotkeySource.Handle, HK_HIDE);
                    UnregisterHotKey(_hotkeySource.Handle, HK_PEEK);
                    _hotkeySource.Dispose();
                }
                if (_desktopWatcher != null) _desktopWatcher.Dispose();
            }
            catch { }
            base.OnExit(e);
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

        // --- Masaüstü widget'ı: pencereyi diğer programların ALTINDA tut ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;  // Alt+Tab'da görünmesin
        private const int WS_EX_NOACTIVATE = 0x08000000;  // öne gelip odağı çalmasın
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        public static void MakeDesktopWidget(IntPtr hwnd)
        {
            try
            {
                // TOOLWINDOW: Alt+Tab'da görünmez. (NOACTIVATE eklemiyoruz;
                // sürükleme/tıklama sorunsuz kalsın. Alta itmeyi WM_WINDOWPOSCHANGING yapıyor.)
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
                SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }
        }

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

            panel.Children.Add(Section("Simgeler ve Çerçeve"));
            var ico = MakeSlider(16, 64, d.IconSize);
            var cor = MakeSlider(0, 24, d.Corner);
            panel.Children.Add(Row("Simge boyutu", ico));
            panel.Children.Add(Row("Köşe yuvarlaklığı", cor));

            var sortCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 0) };
            sortCombo.Items.Add("Ada göre sırala");
            sortCombo.Items.Add("Türe göre sırala");
            sortCombo.SelectedIndex = d.Sort == 1 ? 1 : 0;
            panel.Children.Add(sortCombo);

            var lockChk = new CheckBox
            {
                Content = "Kilitle (taşıma/boyutlandırma kapalı)",
                IsChecked = d.Locked,
                Margin = new Thickness(0, 10, 0, 0)
            };
            panel.Children.Add(lockChk);

            var chk = new CheckBox
            {
                Content = "Fare üzerine gelince aç, çekilince kapat",
                IsChecked = d.AutoHide,
                Margin = new Thickness(0, 8, 0, 0)
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
                d.IconSize = (int)ico.Value; d.Corner = (int)cor.Value;
                d.Sort = sortCombo.SelectedIndex == 1 ? 1 : 0;
                d.Locked = lockChk.IsChecked == true;
                d.AutoHide = chk.IsChecked == true;
                swatch.Background = new SolidColorBrush(
                    Color.FromArgb((byte)d.A, (byte)d.R, (byte)d.G, (byte)d.B));
                apply();
            };

            RoutedPropertyChangedEventHandler<double> onChange = (s, e) => update();
            rs.ValueChanged += onChange; gs.ValueChanged += onChange; bs.ValueChanged += onChange;
            a.ValueChanged += onChange; ws.ValueChanged += onChange; hs.ValueChanged += onChange;
            ico.ValueChanged += onChange; cor.ValueChanged += onChange;
            sortCombo.SelectionChanged += (s, e) => update();
            lockChk.Checked += (s, e) => update();
            lockChk.Unchecked += (s, e) => update();
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

    // =====================================================================
    //  OTOMATİK KURALLAR DİYALOĞU
    // =====================================================================
    internal static class RulesDialog
    {
        public static void Show(Window owner, App app)
        {
            var w = new Window
            {
                Title = "Otomatik Kurallar",
                Width = 430,
                Height = 430,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Topmost = true,
                Owner = owner
            };

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock
            {
                Text = "Masaüstüne YENİ düşen dosyalar, uzantısına göre seçtiğin çite otomatik taşınır.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var list = new ListBox { Height = 170 };
            panel.Children.Add(list);

            Func<string, string> titleOf = id =>
            {
                foreach (var f in app.Config.Fences) if (f.Id == id) return f.Title;
                return "(silinmiş çit)";
            };
            Action refresh = () =>
            {
                list.Items.Clear();
                foreach (var r in app.Config.Rules)
                    list.Items.Add(r.Ext + "  →  " + titleOf(r.TargetId));
            };
            refresh();

            var remove = new Button
            {
                Content = "Seçili kuralı sil",
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 150
            };
            remove.Click += (s, e) =>
            {
                int i = list.SelectedIndex;
                if (i >= 0 && i < app.Config.Rules.Count)
                {
                    app.Config.Rules.RemoveAt(i);
                    app.SaveConfig();
                    app.RestartDesktopWatcher();
                    refresh();
                }
            };
            panel.Children.Add(remove);

            panel.Children.Add(new TextBlock
            {
                Text = "Yeni kural:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 14, 0, 4)
            });

            var addRow = new StackPanel { Orientation = Orientation.Horizontal };
            var extBox = new TextBox { Width = 90, Text = ".png", VerticalAlignment = VerticalAlignment.Center };
            var fenceCombo = new ComboBox { Width = 190, Margin = new Thickness(8, 0, 0, 0) };
            foreach (var f in app.Config.Fences)
                if (string.IsNullOrEmpty(f.PortalPath))
                    fenceCombo.Items.Add(new ComboBoxItem { Content = f.Title, Tag = f.Id });
            if (fenceCombo.Items.Count > 0) fenceCombo.SelectedIndex = 0;

            var add = new Button { Content = "Ekle", Width = 70, Margin = new Thickness(8, 0, 0, 0) };
            add.Click += (s, e) =>
            {
                string ext = extBox.Text.Trim();
                if (string.IsNullOrEmpty(ext)) return;
                if (!ext.StartsWith(".")) ext = "." + ext;
                var item = fenceCombo.SelectedItem as ComboBoxItem;
                if (item == null) return;
                app.Config.Rules.Add(new RuleData { Ext = ext, TargetId = (string)item.Tag });
                app.SaveConfig();
                app.RestartDesktopWatcher();
                refresh();
            };
            addRow.Children.Add(extBox);
            addRow.Children.Add(fenceCombo);
            addRow.Children.Add(add);
            panel.Children.Add(addRow);

            var close = new Button
            {
                Content = "Kapat",
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                IsCancel = true
            };
            panel.Children.Add(close);

            w.Content = panel;
            w.ShowDialog();
        }
    }
}
