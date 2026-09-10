# Modern Fences (WPF)

Buzlu cam (blur) efektli, çok pencereli modern masaüstü düzenleyici.
Sürüklenen öğe gizli bir depoya (AppData) taşınır, masaüstünden kaybolur;
pencereden açılır, "Masaüstüne Çıkar" ile geri alınır.

## Özellikler

- 🪟 **Çoklu bağımsız pencere** — her biri ayrı başlık/konum/boyut/içerik
- 🌫️ **Buzlu cam** arka plan (`SetWindowCompositionAttribute`)
- 🖼️ **Gerçek Windows ikonları** — exe/excel/resim/klasör (`SHGetFileInfo`)
- ↔️ **Taşıma** (başlıktan) + **kenardan boyutlandırma** (WPF Thumb)
- ⬍ **Aç/Kapat** (collapse)
- ✏️ **Yeniden adlandırma** — pencere başlığı + öğeler
- 🖱️ **Öğe menüsü:** Aç · Konumunu Aç · Yeniden Adlandır · Masaüstüne Çıkar · Diskten Sil
- 💾 **Otomatik kayıt** — konum/boyut/başlık `config.json`'a
- 🔔 **Tepsi simgesi** — yeni pencere · Windows ile başlat · çıkış

## Çalıştırma

Visual Studio: `ModernFences.csproj` → **F5**.
Komut satırı (.NET 8 SDK): proje klasöründe `dotnet run`.

> Yalnızca **Windows**'ta derlenir/çalışır (WPF).

## Önceki koddaki hatalar ve düzeltmeler

| Sorun | Neden | Çözüm |
|------|-------|-------|
| Tüm pencerede `DragMove` | İkon tıklamalarını bloke ediyordu | Sürükleme yalnızca başlıkta |
| Aynı isimli dosya | `File.Move` çakışıp hata veriyordu | `App.Unique` → "(1)" eki |
| Klasör ikonu yok + handle sızıntısı | `ExtractAssociatedIcon` klasörde çalışmaz, `DestroyIcon` yok | `SHGetFileInfo` + `DestroyIcon` + `Freeze()` |
| Admin çalıştırma | Sürükle-bırak (UIPI) engelli | `app.manifest` → `asInvoker` |
| Tek pencere, kayıt yok | — | `App` yöneticisi + `config.json` + tepsi |

## Depolama

- Ayarlar: `%AppData%\ModernFences\config.json`
- Öğeler: `%AppData%\ModernFences\Depo\<pencere-id>\`

Bir pencere kaldırılırsa içindeki öğeler **masaüstüne geri taşınır**.

## Notlar

- **Yönetici olarak çalıştırmayın** (VS dahil) — sürükle-bırak engellenir.
- Buzlu cam varsayılan olarak `BLURBEHIND`. Daha modern acrylic için
  `App.xaml.cs` içindeki `AccentState`'i `ACCENT_ENABLE_ACRYLICBLURBEHIND`
  yapın (bazı Windows sürümlerinde siyah kutu yapabilir).
- Farklı diske taşımada `Directory.Move` hata verebilir; aynı disk içinde
  sorunsuz.
