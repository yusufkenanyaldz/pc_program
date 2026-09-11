using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ModernFences
{
    public partial class MainWindow : Window
    {
        private readonly App _app;
        private readonly FenceData _data;
        private bool _contentVisible = true;
        private bool _peek;
        private System.IO.FileSystemWatcher _portalWatcher;

        private static readonly Brush CardHover = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));

        public FenceData Data { get { return _data; } }
        public void ReloadItems() { LoadItems(); }

        private bool IsPortal { get { return !string.IsNullOrEmpty(_data.PortalPath); } }

        public MainWindow(App app, FenceData data)
        {
            InitializeComponent();
            _app = app;
            _data = data;

            Width = data.Width;
            Height = data.Height;
            Left = data.X;
            Top = data.Y;
            BaslikYazi.Text = data.Title;

            // Fare ile aç/kapat (auto-hide)
            MouseEnter += (s, e) => { if (_data.AutoHide && !_contentVisible) SetContentVisible(true); };
            MouseLeave += (s, e) => { if (_data.AutoHide && _contentVisible) SetContentVisible(false); };

            Loaded += (s, e) =>
            {
                ApplyAppearance();
                StartPortalWatcher();
            };
            Closed += (s, e) => StopPortalWatcher();
        }

        // Peek: geçici olarak öne getir (App çağırır)
        public void Peek(bool on)
        {
            _peek = on;
            Topmost = on;
            if (on)
            {
                if (!_contentVisible) SetContentVisible(true); // gizli/daralmış çiti aç
            }
            else
            {
                bool show = _data.AutoHide ? IsMouseOver : !_data.Collapsed;
                SetContentVisible(show);
            }
        }

        // Renk/şeffaflık/boyut/köşe + içerik durumunu uygula
        public void ApplyAppearance()
        {
            RootBorder.Background = new SolidColorBrush(
                Color.FromArgb((byte)_data.A, (byte)_data.R, (byte)_data.G, (byte)_data.B));
            RootBorder.CornerRadius = new CornerRadius(_data.Corner);
            Width = _data.Width;

            LoadItems();

            bool show;
            if (_data.AutoHide) show = IsMouseOver;      // fare üstündeyse açık
            else show = !_data.Collapsed;                // değilse manuel duruma göre
            SetContentVisible(show);
        }

        private void SetContentVisible(bool visible)
        {
            _contentVisible = visible;
            Icerik.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            Height = visible ? (_data.Height > 60 ? _data.Height : 420) : 40;
            CollapseBtn.Content = visible ? "—" : "▢";
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Native.MakeDesktopWidget(hwnd);       // masaüstüne yapıştır (hep altta)
            Native.EnableBlur(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        }

        // Pencere z-sırasını değiştirmeye çalıştığında onu en ALTA zorla
        // (diğer programların üstüne çıkmasın, masaüstünde kalsın).
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int SWP_NOZORDER = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x, y, cx, cy;
            public int flags;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_WINDOWPOSCHANGING && !_peek)
            {
                var wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                wp.hwndInsertAfter = HWND_BOTTOM;
                wp.flags &= ~SWP_NOZORDER;
                Marshal.StructureToPtr(wp, lParam, false);
            }
            return IntPtr.Zero;
        }

        // ------------------------------------------------------------------
        //  ÖĞELER
        // ------------------------------------------------------------------
        private void LoadItems()
        {
            IkonPaneli.Children.Clear();

            string source;
            if (IsPortal)
            {
                if (!Directory.Exists(_data.PortalPath))
                {
                    ShowHint("Portal klasörü bulunamadı:\n" + _data.PortalPath);
                    return;
                }
                source = _data.PortalPath;
            }
            else source = _app.StorePathFor(_data);

            var dirs = Directory.GetDirectories(source);
            var files = Directory.GetFiles(source);
            Array.Sort(dirs, CompareEntries);
            Array.Sort(files, CompareEntries);

            bool any = false;
            foreach (var dir in dirs) { AddCard(dir); any = true; }
            foreach (var file in files) { AddCard(file); any = true; }

            if (!any) ShowHint("Program, klasör veya dosyaları buraya sürükleyin");
        }

        private int CompareEntries(string a, string b)
        {
            if (_data.Sort == 1) // türe göre
            {
                int c = string.Compare(Path.GetExtension(a), Path.GetExtension(b),
                    StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
            }
            return string.Compare(Path.GetFileName(a), Path.GetFileName(b),
                StringComparison.OrdinalIgnoreCase);
        }

        private void ShowHint(string text)
        {
            IkonPaneli.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Width = 220,
                Margin = new Thickness(8, 24, 8, 8)
            });
        }

        private void AddCard(string path)
        {
            int isz = _data.IconSize < 16 ? 36 : _data.IconSize;
            var card = new Border
            {
                Width = isz + 44,
                Height = isz + 52,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                Tag = path
            };

            var sp = new StackPanel { Margin = new Thickness(4) };

            var img = new Image
            {
                Width = isz,
                Height = isz,
                Margin = new Thickness(0, 4, 0, 6),
                Source = Native.GetIconSource(path)
            };

            var txt = new TextBlock
            {
                Text = Path.GetFileName(path),
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 32,
                FontSize = 10
            };

            sp.Children.Add(img);
            sp.Children.Add(txt);
            card.Child = sp;
            card.ToolTip = Path.GetFileName(path);

            card.MouseEnter += (s, e) => card.Background = CardHover;
            card.MouseLeave += (s, e) => card.Background = Brushes.Transparent;
            card.MouseLeftButtonUp += (s, e) => Open(path);
            card.ContextMenu = BuildItemMenu(path, card);

            IkonPaneli.Children.Add(card);
        }

        private ContextMenu BuildItemMenu(string path, Border card)
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem2("Aç", () => Open(path)));
            menu.Items.Add(MenuItem2("Konumunu Aç", () => Reveal(path)));
            menu.Items.Add(MenuItem2("Yeniden Adlandır", () => RenameItem(path)));
            menu.Items.Add(new Separator());
            if (!IsPortal) // portal canlı klasör görünümüdür; öğe koparılmaz
                menu.Items.Add(MenuItem2("Masaüstüne Çıkar", () => MoveToDesktop(path, card)));
            menu.Items.Add(MenuItem2("Diskten Sil", () => DeleteFromDisk(path, card)));
            return menu;
        }

        private static MenuItem MenuItem2(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            return item;
        }

        private void Open(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Açılamadı: " + ex.Message); }
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
            catch (Exception ex) { MessageBox.Show("Konum açılamadı: " + ex.Message); }
        }

        private void RenameItem(string path)
        {
            bool isDir = Directory.Exists(path);
            string current = isDir ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
            string ext = isDir ? "" : Path.GetExtension(path);

            string input = PromptDialog.Show("Yeni ad:", "Yeniden Adlandır", current);
            if (string.IsNullOrWhiteSpace(input) || input == current) return;

            try
            {
                string dst = App.Unique(Path.Combine(Path.GetDirectoryName(path), input.Trim() + ext));
                if (isDir) Directory.Move(path, dst);
                else File.Move(path, dst);
                LoadItems();
            }
            catch (Exception ex) { MessageBox.Show("Yeniden adlandırılamadı: " + ex.Message); }
        }

        private void MoveToDesktop(string path, Border card)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            try
            {
                App.SafeMove(path, Path.Combine(desktop, Path.GetFileName(path)), Directory.Exists(path));
                RemoveCard(card);
            }
            catch (Exception ex) { MessageBox.Show("Masaüstüne taşınamadı: " + ex.Message); }
        }

        private void DeleteFromDisk(string path, Border card)
        {
            if (MessageBox.Show(
                    $"'{Path.GetFileName(path)}' KALICI olarak silinsin mi?\n\nBu işlem geri alınamaz!",
                    "Diskten Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                RemoveCard(card);
            }
            catch (Exception ex) { MessageBox.Show("Silinemedi: " + ex.Message); }
        }

        private void RemoveCard(Border card)
        {
            IkonPaneli.Children.Remove(card);
            bool any = false;
            foreach (var c in IkonPaneli.Children)
                if (c is Border) { any = true; break; }
            if (!any) ShowHint("Program, klasör veya dosyaları buraya sürükleyin");
        }

        // ------------------------------------------------------------------
        //  SÜRÜKLE-BIRAK
        // ------------------------------------------------------------------
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            // Portal ise gerçek klasöre, değilse gizli depoya taşı
            string store = IsPortal ? _data.PortalPath : _app.StorePathFor(_data);
            if (IsPortal && !Directory.Exists(store)) return;
            foreach (string src in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                try
                {
                    string name = Path.GetFileName(src.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string dst = App.Unique(Path.Combine(store, name));
                    if (Directory.Exists(src)) Directory.Move(src, dst);
                    else File.Move(src, dst);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Taşınamadı. Dosya açık olabilir ya da farklı bir diskte olabilir.\n\n" + ex.Message,
                        "Taşıma Hatası");
                }
            }
            LoadItems();
        }

        // ------------------------------------------------------------------
        //  PENCERE: taşıma / boyutlandırma / aç-kapat / menü
        // ------------------------------------------------------------------
        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_data.Locked) return;
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_data.Locked) return;
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        }

        private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_data.Locked || !_contentVisible) return;
            Height = Math.Max(90, Height + e.VerticalChange);
        }

        private void ResizeCorner_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_data.Locked) return;
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
            if (_contentVisible)
                Height = Math.Max(90, Height + e.VerticalChange);
        }

        private void Yeni_Click(object sender, RoutedEventArgs e) => _app.NewFence();

        private void Collapse_Click(object sender, RoutedEventArgs e) => ToggleCollapse();

        private void ToggleCollapse()
        {
            if (_data.AutoHide) _data.AutoHide = false; // manuel kontrol -> auto-hide kapansın
            _data.Collapsed = !_data.Collapsed;
            SetContentVisible(!_data.Collapsed);
            _app.RequestSave();
        }

        private void Kapat_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Bu pencere kaldırılsın mı?\n\nİçindeki öğeler masaüstüne geri taşınacak.",
                    "Pencereyi Kaldır", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _app.RemoveFence(this, _data);
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem2("Ayarlar… (renk, şeffaflık, boyut)", OpenSettings));
            menu.Items.Add(MenuItem2("Pencere Adını Değiştir", RenameFence));
            menu.Items.Add(MenuItem2("Yeni Pencere", () => _app.NewFence()));
            menu.Items.Add(MenuItem2(_contentVisible ? "Daralt" : "Genişlet", ToggleCollapse));

            var autoHideItem = new MenuItem
            {
                Header = "Fare ile Aç/Kapat",
                IsCheckable = true,
                IsChecked = _data.AutoHide
            };
            autoHideItem.Click += (s, ev) =>
            {
                _data.AutoHide = autoHideItem.IsChecked;
                if (!_data.AutoHide) _data.Collapsed = false;
                ApplyAppearance();
                _app.RequestSave();
            };
            menu.Items.Add(autoHideItem);

            var lockItem = new MenuItem
            {
                Header = "Kilitle (taşıma/boyut kapalı)",
                IsCheckable = true,
                IsChecked = _data.Locked
            };
            lockItem.Click += (s, ev) => { _data.Locked = lockItem.IsChecked; _app.RequestSave(); };
            menu.Items.Add(lockItem);

            menu.Items.Add(new Separator());
            if (!IsPortal)
                menu.Items.Add(MenuItem2("Klasör Portalı Yap…", MakePortal));
            else
                menu.Items.Add(MenuItem2("Portalı Kaldır (normal çit)", RemovePortal));
            menu.Items.Add(MenuItem2("Otomatik Kurallar…", () => RulesDialog.Show(this, _app)));

            var startItem = new MenuItem
            {
                Header = "Windows ile Başlat",
                IsCheckable = true,
                IsChecked = _app.Config.StartWithWindows
            };
            startItem.Click += (s, ev) =>
            {
                _app.Config.StartWithWindows = startItem.IsChecked;
                _app.SetStartup(startItem.IsChecked);
                _app.SaveConfig();
            };
            menu.Items.Add(startItem);

            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem2("Pencereyi Kaldır", () => Kapat_Click(null, null)));
            menu.Items.Add(MenuItem2("Uygulamadan Çık", () => Application.Current.Shutdown()));
            menu.PlacementTarget = (UIElement)sender;
            menu.IsOpen = true;
        }

        private void RenameFence()
        {
            string input = PromptDialog.Show("Pencere başlığı:", "Yeniden Adlandır", _data.Title);
            if (string.IsNullOrWhiteSpace(input)) return;
            _data.Title = input.Trim();
            BaslikYazi.Text = _data.Title;
            _app.RequestSave();
        }

        private void OpenSettings()
        {
            SettingsDialog.Show(this, _data, () =>
            {
                ApplyAppearance();
                _app.RequestSave();
            });
        }

        // ------------------------------------------------------------------
        //  FOLDER PORTAL
        // ------------------------------------------------------------------
        private void MakePortal()
        {
            string start = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = PromptDialog.Show(
                "Yansıtılacak klasörün tam yolu:\n(Explorer adres çubuğundan kopyalayabilirsin)",
                "Klasör Portalı", start);
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim().Trim('"');
            if (!Directory.Exists(path))
            {
                MessageBox.Show("Klasör bulunamadı:\n" + path);
                return;
            }
            _data.PortalPath = path;
            string folderName = Path.GetFileName(path.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            _data.Title = string.IsNullOrEmpty(folderName) ? path : folderName;
            BaslikYazi.Text = _data.Title;
            _app.SaveConfig();
            StartPortalWatcher();
            LoadItems();
        }

        private void RemovePortal()
        {
            _data.PortalPath = "";
            StopPortalWatcher();
            _app.SaveConfig();
            LoadItems();
        }

        private void StartPortalWatcher()
        {
            StopPortalWatcher();
            if (!IsPortal || !Directory.Exists(_data.PortalPath)) return;
            try
            {
                _portalWatcher = new System.IO.FileSystemWatcher(_data.PortalPath)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };
                System.IO.FileSystemEventHandler h =
                    (s, e) => Dispatcher.BeginInvoke(new Action(LoadItems));
                _portalWatcher.Created += h;
                _portalWatcher.Deleted += h;
                _portalWatcher.Renamed += (s, e) => Dispatcher.BeginInvoke(new Action(LoadItems));
            }
            catch { }
        }

        private void StopPortalWatcher()
        {
            try
            {
                if (_portalWatcher != null)
                {
                    _portalWatcher.Dispose();
                    _portalWatcher = null;
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------
        //  KONUM/BOYUT KAYDI
        // ------------------------------------------------------------------
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            UpdateBounds();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateBounds();
        }

        private void UpdateBounds()
        {
            if (_data == null) return;
            if (!double.IsNaN(Left)) _data.X = (int)Left;
            if (!double.IsNaN(Top)) _data.Y = (int)Top;
            _data.Width = (int)Width;
            if (_contentVisible) _data.Height = (int)Height; // sadece açıkken sakla
            _app?.RequestSave();
        }
    }
}
