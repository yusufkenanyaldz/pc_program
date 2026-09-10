using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CSharpFences
{
    public partial class Form1 : Form
    {
        // --- Windows API: dosya/klasör için GERÇEK sistem ikonunu çeker ---
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

        // Arayüz ve depolama
        private FlowLayoutPanel gridPanel;
        private string gizliDepoYolu;

        // Pencere sürükleme durumu
        private bool _surukleniyor;
        private Point _fareBaslangic;

        public Form1()
        {
            // 1. Ana pencere ayarları
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(41, 128, 185); // Fences mavisi
            this.Width = 350;
            this.Height = 500;
            this.AllowDrop = true;
            this.TopMost = true;                 // Her zaman üstte
            this.ShowInTaskbar = true;           // Görev çubuğunda görünsün
            this.StartPosition = FormStartPosition.Manual;
            this.Text = "C# Fences";

            // Ekranın sağ üstüne yakın konumlandır
            var alan = Screen.PrimaryScreen.WorkingArea;
            this.Left = alan.Right - this.Width - 40;
            this.Top = alan.Top + 40;

            // 2. Gizli depo klasörü (AppData içinde)
            gizliDepoYolu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CSharpFences_Gizli_Depo");
            Directory.CreateDirectory(gizliDepoYolu);

            // 3. Başlık (buradan tutup pencere taşınır)
            Label lblBaslik = new Label
            {
                Text = "ARAÇLAR",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 40
            };
            lblBaslik.MouseDown += Baslik_MouseDown;
            lblBaslik.MouseMove += Baslik_MouseMove;
            lblBaslik.MouseUp += Baslik_MouseUp;
            lblBaslik.MouseDoubleClick += (s, e) => this.WindowState =
                this.WindowState == FormWindowState.Minimized
                    ? FormWindowState.Normal : this.WindowState;

            // 4. Kapatma butonu (altta)
            Button btnKapat = new Button
            {
                Text = "Uygulamadan Çık",
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 40
            };
            btnKapat.Click += (s, e) => this.Close();

            // 5. İkon ızgarası (ortayı doldurur)
            gridPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(52, 152, 219),
                AllowDrop = true
            };
            gridPanel.DragEnter += DosyaSuruklendiginde;
            gridPanel.DragDrop += DosyaBirakildiginda;

            // Kontrolleri ekle. Dock=Fill panelin diğerlerini örtmemesi için
            // en ÖNE alınması gerekir (BringToFront) — WinForms docking sırası.
            this.Controls.Add(lblBaslik);
            this.Controls.Add(btnKapat);
            this.Controls.Add(gridPanel);
            gridPanel.BringToFront();

            // Forma bırakma da çalışsın (panelin dışına denk gelirse)
            this.DragEnter += DosyaSuruklendiginde;
            this.DragDrop += DosyaBirakildiginda;

            this.Load += (s, e) => MevcutDosyalariYukle();
        }

        // NOT: Eski koddaki SetParent(Progman) numarası KALDIRILDI.
        // Pencereyi masaüstü simgelerinin ARKASINA gönderdiği için görünmüyordu.
        // Bunun yerine TopMost + sürüklenebilir başlık kullanıyoruz.

        // --- Pencere sürükleme ---
        private void Baslik_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _surukleniyor = true;
                _fareBaslangic = e.Location;
            }
        }

        private void Baslik_MouseMove(object sender, MouseEventArgs e)
        {
            if (_surukleniyor)
            {
                this.Left += e.X - _fareBaslangic.X;
                this.Top += e.Y - _fareBaslangic.Y;
            }
        }

        private void Baslik_MouseUp(object sender, MouseEventArgs e)
        {
            _surukleniyor = false;
        }

        // --- Sürükle-bırak ---
        private void DosyaSuruklendiginde(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Move;
        }

        private void DosyaBirakildiginda(object sender, DragEventArgs e)
        {
            string[] dosyalar = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string kaynakYol in dosyalar)
            {
                try
                {
                    string dosyaAdi = Path.GetFileName(kaynakYol.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string hedefYol = Path.Combine(gizliDepoYolu, dosyaAdi);

                    if (string.Equals(kaynakYol, hedefYol,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ArayuzeIkonEkle(hedefYol);
                        continue;
                    }

                    // Aynı isim zaten varsa üzerine yazma; sıralı yeni ad ver
                    hedefYol = BenzersizYol(hedefYol);

                    // Dosyayı/klasörü gizli depoya TAŞI (masaüstünden kaybolur)
                    if (Directory.Exists(kaynakYol))
                        Directory.Move(kaynakYol, hedefYol);
                    else
                        File.Move(kaynakYol, hedefYol);

                    ArayuzeIkonEkle(hedefYol);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Taşınamadı. Dosya başka bir programda açık olabilir " +
                        "ya da farklı bir diskte olabilir.\n\nDetay: " + ex.Message,
                        "Taşıma Hatası");
                }
            }
        }

        private static string BenzersizYol(string yol)
        {
            if (!File.Exists(yol) && !Directory.Exists(yol))
                return yol;
            string dizin = Path.GetDirectoryName(yol);
            string ad = Path.GetFileNameWithoutExtension(yol);
            string uzanti = Path.GetExtension(yol);
            int i = 1;
            string yeni;
            do
            {
                yeni = Path.Combine(dizin, $"{ad} ({i}){uzanti}");
                i++;
            } while (File.Exists(yeni) || Directory.Exists(yeni));
            return yeni;
        }

        // --- Arayüz ---
        private void MevcutDosyalariYukle()
        {
            gridPanel.Controls.Clear();
            foreach (string dir in Directory.GetDirectories(gizliDepoYolu))
                ArayuzeIkonEkle(dir);
            foreach (string file in Directory.GetFiles(gizliDepoYolu))
                ArayuzeIkonEkle(file);
        }

        private void ArayuzeIkonEkle(string dosyaYolu)
        {
            Button ogeButonu = new Button
            {
                Text = Path.GetFileName(dosyaYolu),
                Width = 95,
                Height = 90,
                BackColor = Color.FromArgb(236, 240, 241),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(6),
                TextAlign = ContentAlignment.BottomCenter,
                ImageAlign = ContentAlignment.TopCenter,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Tag = dosyaYolu
            };

            // Gerçek Windows ikonunu çek (exe, excel, resim, klasör vb.)
            Image ikon = SistemIkonuAl(dosyaYolu);
            if (ikon != null)
                ogeButonu.Image = ikon;

            // Sol tık: aç
            ogeButonu.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = dosyaYolu,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Açılamadı: " + ex.Message);
                }
            };

            // Sağ tık menüsü
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Aç").Click += (s, e) => ogeButonu.PerformClick();
            menu.Items.Add("Masaüstüne Çıkar").Click += (s, e) =>
                MasaustuneCikar(dosyaYolu, ogeButonu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Diskten Sil").Click += (s, e) =>
                DisktenSil(dosyaYolu, ogeButonu);
            ogeButonu.ContextMenuStrip = menu;

            gridPanel.Controls.Add(ogeButonu);
        }

        private static Image SistemIkonuAl(string yol)
        {
            try
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(yol, 0, ref shinfo,
                    (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                if (shinfo.hIcon == IntPtr.Zero)
                    return null;
                // İkonu bitmap'e kopyala, sonra handle'ı serbest bırak (sızıntı olmasın)
                using (Icon ico = Icon.FromHandle(shinfo.hIcon))
                {
                    Bitmap bmp = ico.ToBitmap();
                    DestroyIcon(shinfo.hIcon);
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private void MasaustuneCikar(string dosyaYolu, Button oge)
        {
            string masaustu = Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop);
            string hedef = BenzersizYol(
                Path.Combine(masaustu, Path.GetFileName(dosyaYolu)));
            try
            {
                if (Directory.Exists(dosyaYolu))
                    Directory.Move(dosyaYolu, hedef);
                else
                    File.Move(dosyaYolu, hedef);

                oge.Image?.Dispose();
                gridPanel.Controls.Remove(oge);
                oge.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Masaüstüne taşınamadı: " + ex.Message);
            }
        }

        private void DisktenSil(string dosyaYolu, Button oge)
        {
            if (MessageBox.Show(
                    $"'{Path.GetFileName(dosyaYolu)}' KALICI olarak silinsin mi?\n\n" +
                    "Bu işlem geri alınamaz!",
                    "Diskten Sil", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                if (Directory.Exists(dosyaYolu))
                    Directory.Delete(dosyaYolu, true);
                else if (File.Exists(dosyaYolu))
                    File.Delete(dosyaYolu);

                oge.Image?.Dispose();
                gridPanel.Controls.Remove(oge);
                oge.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Silinemedi: " + ex.Message);
            }
        }
    }
}
