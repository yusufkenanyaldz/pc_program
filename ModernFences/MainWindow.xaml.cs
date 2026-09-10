using System;
using System.IO;
using System.Diagnostics;
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

        private static readonly Brush CardHover = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));

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
                LoadItems();
                ApplyAppearance();
            };
        }

        // Renk/şeffaflık/boyut ve içerik durumunu uygula
        public void ApplyAppearance()
        {
            RootBorder.Background = new SolidColorBrush(
                Color.FromArgb((byte)_data.A, (byte)_data.R, (byte)_data.G, (byte)_data.B));
            Width = _data.Width;

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
            Native.EnableBlur(new WindowInteropHelper(this).Handle);
        }

        // ------------------------------------------------------------------
        //  ÖĞELER
        // ------------------------------------------------------------------
        private void LoadItems()
        {
            IkonPaneli.Children.Clear();
            string store = _app.StorePathFor(_data);

            bool any = false;
            foreach (var dir in Directory.GetDirectories(store)) { AddCard(dir); any = true; }
            foreach (var file in Directory.GetFiles(store)) { AddCard(file); any = true; }

            if (!any) ShowHint();
        }

        private void ShowHint()
        {
            IkonPaneli.Children.Add(new TextBlock
            {
                Text = "Program, klasör veya dosyaları buraya sürükleyin",
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
            var card = new Border
            {
                Width = 78,
                Height = 86,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                Tag = path
            };

            var sp = new StackPanel { Margin = new Thickness(4) };

            var img = new Image
            {
                Width = 36,
                Height = 36,
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
            if (!any) ShowHint();
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
            string store = _app.StorePathFor(_data);
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
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        }

        private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_contentVisible) return;
            Height = Math.Max(90, Height + e.VerticalChange);
        }

        private void ResizeCorner_DragDelta(object sender, DragDeltaEventArgs e)
        {
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
