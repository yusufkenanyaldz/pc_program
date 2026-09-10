using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CSharpFences
{
    // =====================================================================
    //  VERİ MODELİ
    // =====================================================================
    public class FenceData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Araçlar";
        public int X { get; set; } = 200;
        public int Y { get; set; } = 120;
        public int Width { get; set; } = 300;
        public int Height { get; set; } = 380;
        public bool Collapsed { get; set; } = false;
    }

    public class AppConfig
    {
        public List<FenceData> Fences { get; set; } = new List<FenceData>();
        public bool StartWithWindows { get; set; } = false;
    }

    // =====================================================================
    //  UYGULAMA YÖNETİCİSİ (tüm pencereler + tepsi simgesi)
    // =====================================================================
    public class FenceAppContext : ApplicationContext
    {
        public static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CSharpFences");
        public static readonly string StoreDir = Path.Combine(BaseDir, "Depo");
        private static readonly string ConfigPath = Path.Combine(BaseDir, "config.json");

        public AppConfig Config { get; private set; }
        private readonly List<Form1> _forms = new List<Form1>();
        private NotifyIcon _tray;

        public FenceAppContext()
        {
            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(StoreDir);

            Config = LoadConfig();
            if (Config.Fences.Count == 0)
                Config.Fences.Add(new FenceData());

            SetupTray();

            foreach (var fd in Config.Fences.ToArray())
                OpenFence(fd);

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
            var f = new Form1(this, d);
            _forms.Add(f);
            f.FormClosed += (s, e) => _forms.Remove(f);
            f.Show();
        }

        public void NewFence()
        {
            var d = new FenceData
            {
                Title = "Yeni Pencere",
                X = 240 + _forms.Count * 32,
                Y = 140 + _forms.Count * 32
            };
            Config.Fences.Add(d);
            OpenFence(d);
            SaveConfig();
        }

        public void RemoveFence(Form1 form, FenceData d)
        {
            // Pencere kaldırılırken içindekiler kaybolmasın -> masaüstüne taşı
            try
            {
                string store = StorePathFor(d);
                string desktop = Environment.GetFolderPath(
                    Environment.SpecialFolder.Desktop);
                foreach (var dir in Directory.GetDirectories(store))
                    SafeMove(dir, Path.Combine(desktop, Path.GetFileName(dir)), true);
                foreach (var file in Directory.GetFiles(store))
                    SafeMove(file, Path.Combine(desktop, Path.GetFileName(file)), false);
                Directory.Delete(store, true);
            }
            catch { }

            Config.Fences.Remove(d);
            form.Close();
            SaveConfig();

            if (Config.Fences.Count == 0 && _tray != null)
                _tray.ShowBalloonTip(3000, "C# Fences",
                    "Tüm pencereler kapandı. Tepsi simgesinden yeni pencere açabilirsiniz.",
                    ToolTipIcon.Info);
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

        // --- Yapılandırma ---
        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<AppConfig>(
                        File.ReadAllText(ConfigPath)) ?? new AppConfig();
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
            var menu = new ContextMenuStrip();
            menu.Items.Add("Yeni Pencere").Click += (s, e) => NewFence();

            var startItem = new ToolStripMenuItem("Windows ile Başlat")
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

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Çıkış").Click += (s, e) => ExitApp();

            _tray = new NotifyIcon
            {
                Text = "C# Fences",
                Icon = Native.MakeTrayIcon(),
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => NewFence();
        }

        private void ExitApp()
        {
            SaveConfig();
            if (_tray != null) _tray.Visible = false;
            foreach (var f in _forms.ToArray())
                f.Close();
            ExitThread();
        }

        private void SetStartup(bool on)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (on) key.SetValue("CSharpFences", "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue("CSharpFences", false);
                }
            }
            catch { }
        }
    }

    // =====================================================================
    //  TEK BİR FENCE PENCERESİ
    // =====================================================================
    public partial class Form1 : Form
    {
        // Renk paleti (modern koyu tema)
        private static readonly Color COL_BG = Color.FromArgb(32, 33, 36);
        private static readonly Color COL_HEADER = Color.FromArgb(59, 130, 246);
        private static readonly Color COL_HEADER_HOVER = Color.FromArgb(37, 99, 235);
        private static readonly Color COL_CARD = Color.FromArgb(48, 49, 54);
        private static readonly Color COL_CARD_HOVER = Color.FromArgb(64, 66, 73);
        private static readonly Color COL_TEXT = Color.FromArgb(235, 235, 235);
        private static readonly Color COL_HINT = Color.FromArgb(140, 140, 140);

        private readonly FenceAppContext _app;
        private readonly FenceData _data;

        private Panel _header;
        private Label _title;
        private Button _btnCollapse;
        private FlowLayoutPanel _grid;

        private bool _dragging;
        private Point _dragStart;

        public Form1(FenceAppContext app, FenceData data)
        {
            _app = app;
            _data = data;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(180, 34);
            BackColor = COL_BG;
            ForeColor = COL_TEXT;
            Font = new Font("Segoe UI", 8.5f);
            AllowDrop = true;
            DoubleBuffered = true;

            Bounds = new Rectangle(data.X, data.Y, data.Width, data.Height);

            BuildHeader();
            BuildGrid();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            ResizeEnd += (s, e) => PersistBounds();

            Load += (s, e) =>
            {
                Native.EnableRoundedCorners(Handle);
                LoadItems();
                if (_data.Collapsed) SetCollapsed(true, false);
            };
        }

        // Borderless pencereye yumuşak gölge ver (CS_DROPSHADOW)
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        // ------------------------------------------------------------------
        //  ARAYÜZ
        // ------------------------------------------------------------------
        private void BuildHeader()
        {
            _header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = COL_HEADER };

            _title = new Label
            {
                Text = _data.Title,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = COL_HEADER,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var btnNew = HeaderButton("➕", "Yeni pencere");   // ➕
            btnNew.Click += (s, e) => _app.NewFence();
            var btnMenu = HeaderButton("⋯", "Menü");          // ⋯
            btnMenu.Click += (s, e) => ShowHeaderMenu(btnMenu);
            _btnCollapse = HeaderButton("—", "Aç / Kapat");   // —
            _btnCollapse.Click += (s, e) => ToggleCollapse();
            var btnClose = HeaderButton("✕", "Pencereyi kaldır"); // ✕
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(231, 76, 60);
            btnClose.Click += (s, e) => ConfirmRemove();

            buttons.Controls.Add(btnNew);
            buttons.Controls.Add(btnMenu);
            buttons.Controls.Add(_btnCollapse);
            buttons.Controls.Add(btnClose);

            _header.Controls.Add(_title);
            _header.Controls.Add(buttons);
            _title.BringToFront();

            // Başlıktan tutup taşıma + çift tık ile aç/kapat
            foreach (Control c in new Control[] { _header, _title })
            {
                c.MouseDown += Header_MouseDown;
                c.MouseMove += Header_MouseMove;
                c.MouseUp += Header_MouseUp;
                c.MouseDoubleClick += (s, e) => ToggleCollapse();
            }

            Controls.Add(_header);
        }

        private Button HeaderButton(string text, string tip)
        {
            var b = new Button
            {
                Text = text,
                Width = 30,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = COL_HEADER,
                Font = new Font("Segoe UI", 9),
                TabStop = false,
                Margin = new Padding(0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = COL_HEADER_HOVER;
            var tt = new ToolTip();
            tt.SetToolTip(b, tip);
            return b;
        }

        private void BuildGrid()
        {
            _grid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = COL_BG,
                AllowDrop = true
            };
            _grid.DragEnter += OnDragEnter;
            _grid.DragDrop += OnDragDrop;

            Controls.Add(_grid);
            _grid.BringToFront(); // Fill, header'ın ALTINI doldursun (docking sırası)
        }

        // ------------------------------------------------------------------
        //  ÖĞELER
        // ------------------------------------------------------------------
        private void LoadItems()
        {
            _grid.Controls.Clear();
            string store = _app.StorePathFor(_data);

            bool any = false;
            foreach (var dir in Directory.GetDirectories(store)) { AddCard(dir); any = true; }
            foreach (var file in Directory.GetFiles(store)) { AddCard(file); any = true; }

            if (!any) ShowHint();
        }

        private void ShowHint()
        {
            var l = new Label
            {
                AutoSize = true,
                Margin = new Padding(10, 24, 10, 10),
                MaximumSize = new Size(Math.Max(120, _data.Width - 44), 0),
                ForeColor = COL_HINT,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Text = "Program, klasör veya dosyaları\nburaya sürükleyin"
            };
            _grid.Controls.Add(l);
        }

        private void AddCard(string path)
        {
            var card = new Button
            {
                Width = 92,
                Height = 92,
                FlatStyle = FlatStyle.Flat,
                BackColor = COL_CARD,
                ForeColor = COL_TEXT,
                Font = new Font("Segoe UI", 7.5f),
                Text = ShortName(Path.GetFileName(path)),
                TextAlign = ContentAlignment.BottomCenter,
                ImageAlign = ContentAlignment.TopCenter,
                Margin = new Padding(6),
                Tag = path,
                TabStop = false
            };
            card.FlatAppearance.BorderSize = 0;
            card.FlatAppearance.MouseOverBackColor = COL_CARD_HOVER;

            var img = Native.GetIcon(path);
            if (img != null) card.Image = img;

            var tt = new ToolTip();
            tt.SetToolTip(card, Path.GetFileName(path));

            card.Click += (s, e) => Open(path);
            card.ContextMenuStrip = BuildItemMenu(path, card);

            _grid.Controls.Add(card);
        }

        private static string ShortName(string name)
        {
            return name.Length > 22 ? name.Substring(0, 20) + "…" : name;
        }

        private ContextMenuStrip BuildItemMenu(string path, Button card)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Aç").Click += (s, e) => Open(path);
            menu.Items.Add("Konumunu Aç").Click += (s, e) => Reveal(path);
            menu.Items.Add("Yeniden Adlandır").Click += (s, e) => RenameItem(path);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Masaüstüne Çıkar").Click += (s, e) => MoveToDesktop(path, card);
            menu.Items.Add("Diskten Sil").Click += (s, e) => DeleteFromDisk(path, card);
            return menu;
        }

        private void Open(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Açılamadı: " + ex.Message);
            }
        }

        private void Reveal(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                else
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Konum açılamadı: " + ex.Message);
            }
        }

        private void RenameItem(string path)
        {
            bool isDir = Directory.Exists(path);
            string current = isDir
                ? Path.GetFileName(path)
                : Path.GetFileNameWithoutExtension(path);
            string ext = isDir ? "" : Path.GetExtension(path);

            string input = InputBox.Show("Yeni ad:", "Yeniden Adlandır", current);
            if (string.IsNullOrWhiteSpace(input) || input == current) return;

            try
            {
                string dst = FenceAppContext.Unique(
                    Path.Combine(Path.GetDirectoryName(path), input.Trim() + ext));
                if (isDir) Directory.Move(path, dst);
                else File.Move(path, dst);
                LoadItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yeniden adlandırılamadı: " + ex.Message);
            }
        }

        private void MoveToDesktop(string path, Button card)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            try
            {
                FenceAppContext.SafeMove(path,
                    Path.Combine(desktop, Path.GetFileName(path)),
                    Directory.Exists(path));
                RemoveCard(card);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Masaüstüne taşınamadı: " + ex.Message);
            }
        }

        private void DeleteFromDisk(string path, Button card)
        {
            if (MessageBox.Show(
                    $"'{Path.GetFileName(path)}' KALICI olarak silinsin mi?\n\nBu işlem geri alınamaz!",
                    "Diskten Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes)
                return;
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                RemoveCard(card);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Silinemedi: " + ex.Message);
            }
        }

        private void RemoveCard(Button card)
        {
            card.Image?.Dispose();
            _grid.Controls.Remove(card);
            card.Dispose();
            bool any = false;
            foreach (Control c in _grid.Controls)
                if (c is Button) { any = true; break; }
            if (!any) ShowHint();
        }

        // ------------------------------------------------------------------
        //  SÜRÜKLE-BIRAK
        // ------------------------------------------------------------------
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Move;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string store = _app.StorePathFor(_data);
            foreach (var src in files)
            {
                try
                {
                    string name = Path.GetFileName(src.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string dst = FenceAppContext.Unique(Path.Combine(store, name));
                    if (Directory.Exists(src)) Directory.Move(src, dst);
                    else File.Move(src, dst);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Taşınamadı. Dosya açık olabilir ya da farklı bir diskte olabilir.\n\n" +
                        ex.Message, "Taşıma Hatası");
                }
            }
            LoadItems();
        }

        // ------------------------------------------------------------------
        //  PENCERE TAŞIMA / AÇ-KAPAT / MENÜ
        // ------------------------------------------------------------------
        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
            }
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Left += e.X - _dragStart.X;
                Top += e.Y - _dragStart.Y;
            }
        }

        private void Header_MouseUp(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                PersistBounds();
            }
        }

        private void ToggleCollapse()
        {
            SetCollapsed(!_data.Collapsed, true);
        }

        private void SetCollapsed(bool collapsed, bool persist)
        {
            if (collapsed)
            {
                if (!_data.Collapsed) _data.Height = Height; // açık yüksekliği hatırla
                _grid.Visible = false;
                Height = _header.Height;
                _btnCollapse.Text = "▢"; // ▢
            }
            else
            {
                _grid.Visible = true;
                Height = _data.Height > _header.Height ? _data.Height : 380;
                _btnCollapse.Text = "—"; // —
            }
            _data.Collapsed = collapsed;
            if (persist) { PersistBounds(); _app.SaveConfig(); }
        }

        private void ShowHeaderMenu(Control anchor)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Pencere Adını Değiştir").Click += (s, e) => RenameFence();
            menu.Items.Add("Yeni Pencere").Click += (s, e) => _app.NewFence();
            menu.Items.Add(_data.Collapsed ? "Genişlet" : "Daralt").Click += (s, e) => ToggleCollapse();
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Pencereyi Kaldır").Click += (s, e) => ConfirmRemove();
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void RenameFence()
        {
            string input = InputBox.Show("Pencere başlığı:", "Yeniden Adlandır", _data.Title);
            if (string.IsNullOrWhiteSpace(input)) return;
            _data.Title = input.Trim();
            _title.Text = _data.Title;
            _app.SaveConfig();
        }

        private void ConfirmRemove()
        {
            if (MessageBox.Show(
                    "Bu pencere kaldırılsın mı?\n\nİçindeki öğeler masaüstüne geri taşınacak.",
                    "Pencereyi Kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes)
                return;
            _app.RemoveFence(this, _data);
        }

        private void PersistBounds()
        {
            _data.X = Left;
            _data.Y = Top;
            _data.Width = Width;
            if (!_data.Collapsed) _data.Height = Height;
            _app.SaveConfig();
        }

        // Kenarlardan yeniden boyutlandırma (borderless olsa da)
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCHITTEST && !_data.Collapsed)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1) // HTCLIENT
                {
                    long lp = m.LParam.ToInt64();
                    int sx = (short)(lp & 0xFFFF);
                    int sy = (short)((lp >> 16) & 0xFFFF);
                    Point p = PointToClient(new Point(sx, sy));
                    int g = 6;
                    bool left = p.X <= g, right = p.X >= ClientSize.Width - g;
                    bool top = p.Y <= g, bottom = p.Y >= ClientSize.Height - g;
                    if (bottom && right) m.Result = (IntPtr)17;
                    else if (bottom && left) m.Result = (IntPtr)16;
                    else if (top && right) m.Result = (IntPtr)14;
                    else if (top && left) m.Result = (IntPtr)13;
                    else if (right) m.Result = (IntPtr)11;
                    else if (left) m.Result = (IntPtr)10;
                    else if (bottom) m.Result = (IntPtr)15;
                    else if (top) m.Result = (IntPtr)12;
                }
                return;
            }
            base.WndProc(ref m);
        }
    }

    // =====================================================================
    //  BASİT METİN GİRİŞ KUTUSU (WinForms'ta hazır yok)
    // =====================================================================
    internal static class InputBox
    {
        public static string Show(string prompt, string title, string def)
        {
            using (var f = new Form
            {
                Width = 380,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var lbl = new Label { Left = 14, Top = 14, Width = 340, Text = prompt };
                var txt = new TextBox { Left = 14, Top = 40, Width = 340, Text = def ?? "" };
                var ok = new Button { Text = "Tamam", Left = 196, Width = 75, Top = 78, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "İptal", Left = 279, Width = 75, Top = 78, DialogResult = DialogResult.Cancel };
                f.Controls.Add(lbl);
                f.Controls.Add(txt);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                txt.SelectAll();
                return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
            }
        }
    }

    // =====================================================================
    //  WINDOWS API YARDIMCILARI (ikon çekme, yuvarlak köşe, tepsi ikonu)
    // =====================================================================
    internal static class Native
    {
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

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public static void EnableRoundedCorners(IntPtr hwnd)
        {
            try
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }

        public static Image GetIcon(string path)
        {
            try
            {
                var info = new SHFILEINFO();
                SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info),
                    SHGFI_ICON | SHGFI_LARGEICON);
                if (info.hIcon == IntPtr.Zero) return null;
                using (var ico = Icon.FromHandle(info.hIcon))
                {
                    var bmp = new Bitmap(ico.ToBitmap(), new Size(32, 32));
                    DestroyIcon(info.hIcon);
                    return bmp;
                }
            }
            catch { return null; }
        }

        public static Icon MakeTrayIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(59, 130, 246)))
                    g.FillRectangle(b, 3, 5, 26, 22);
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillRectangle(b, 7, 10, 7, 5);
                    g.FillRectangle(b, 18, 10, 7, 5);
                    g.FillRectangle(b, 7, 18, 7, 5);
                    g.FillRectangle(b, 18, 18, 7, 5);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}
