# C# Fences (WinForms)

Modern, çok pencereli masaüstü düzenleyici. Sürüklenen öğe gizli bir depoya
(AppData) taşınır, böylece masaüstünden kaybolur; pencereden açılır,
"Masaüstüne Çıkar" ile geri alınır.

## Özellikler

- 🪟 **Birden fazla bağımsız pencere** — her biri ayrı başlık, konum, boyut
  ve öğe listesi. Tepsi simgesinden veya `＋` ile yeni pencere.
- 🎨 **Modern koyu tema** — yuvarlatılmış köşeler (Win11), yumuşak gölge,
  hover efektleri.
- 🖼️ **Gerçek Windows ikonları** — exe, excel, resim, klasör… hepsi kendi
  sistem ikonuyla (`SHGetFileInfo`).
- ↔️ **Taşınabilir + yeniden boyutlandırılabilir** — başlıktan tut-sürükle,
  kenarlardan boyutlandır.
- ⬍ **Aç/Kapat (collapse)** — başlığa çift tıkla, sadece başlık çubuğu kalır.
- ✏️ **Yeniden adlandırma** — hem pencere başlığı hem öğeler.
- 🖱️ **Öğe sağ tık:** Aç · Konumunu Aç · Yeniden Adlandır · Masaüstüne Çıkar ·
  Diskten Sil.
- 💾 **Otomatik kayıt** — pencere konumları/boyutları `config.json`'a yazılır.
- 🔔 **Tepsi (tray) simgesi** — yeni pencere, Windows ile başlat, çıkış.
- 🚀 **Windows ile başlat** — tepsi menüsünden aç/kapat.

## Çalıştırma

Visual Studio: `CSharpFences.csproj` → **F5**.
Komut satırı (.NET 8 SDK): `dotnet run` (proje klasöründe).

> WinForms yalnızca **Windows**'ta derlenir/çalışır.

## Dosyalar

| Dosya | İçerik |
|-------|--------|
| `Program.cs` | Giriş noktası — `FenceAppContext` çalıştırır |
| `Form1.cs` | Tüm mantık: `FenceAppContext` (yönetici+tray), `Form1` (pencere), `InputBox`, `Native` (Win API) |
| `app.manifest` | `asInvoker` (admin değil) + PerMonitorV2 DPI |
| `CSharpFences.csproj` | .NET 8 WinForms projesi |

## Depolama

- Ayarlar: `%AppData%\CSharpFences\config.json`
- Öğeler: `%AppData%\CSharpFences\Depo\<pencere-id>\`

Bir pencereyi kaldırırsanız içindeki öğeler **masaüstüne geri taşınır**
(kaybolmaz).

## Önemli notlar

- **Uygulamayı (ve Visual Studio'yu) yönetici olarak ÇALIŞTIRMAYIN** — aksi
  halde Explorer'dan sürükle-bırak (UIPI) engellenir.
- Farklı diske taşımada (C: → D:) `Directory.Move` hata verebilir; aynı disk
  içinde sorunsuz.
- Yuvarlak köşeler Windows 11'de görünür; Windows 10'da köşeler düz kalır
  (hata değil).
