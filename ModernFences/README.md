# Modern Fences (WPF)

Buzlu cam (blur) efektli, çok pencereli modern masaüstü düzenleyici.
Sürüklenen öğe gizli bir depoya (AppData) taşınır, masaüstünden kaybolur;
pencereden açılır, "Masaüstüne Çıkar" ile geri alınır.

## Özellikler

- 🪟 **Çoklu bağımsız pencere** — her biri ayrı başlık/konum/boyut/içerik
- 🌫️ **Buzlu cam** arka plan (`SetWindowCompositionAttribute`)
- 🖼️ **Gerçek Windows ikonları** — exe/excel/resim/klasör (`SHGetFileInfo`)
- ↔️ **Taşıma** (başlıktan) + **kenardan boyutlandırma**
- ⬍ **Aç/Kapat** (collapse)
- 🎚️ **Ayarlar** (⋯ menüsü): renk (R/G/B), şeffaflık, genişlik/yükseklik — canlı önizleme
- 🖱️ **Fare ile Aç/Kapat** — pencere kapalı durur, fare üzerine gelince içeriği açılır
- ✏️ **Yeniden adlandırma** — pencere başlığı + öğeler
- 🖱️ **Öğe menüsü:** Aç · Konumunu Aç · Yeniden Adlandır · Masaüstüne Çıkar · Diskten Sil
- ⚙️ **Başlık ⋯ menüsü:** yeni pencere · Windows ile başlat · pencereyi kaldır · çıkış
- 💾 **Otomatik kayıt** — konum/boyut/başlık `config.txt`'ye

## Bağımlılık yok

Kod **hem .NET Framework hem .NET 8** WPF projelerinde çalışacak şekilde
yazıldı. Ekstra NuGet paketi, ekstra referans veya `app.manifest` GEREKMEZ —
yalnızca standart WPF (`PresentationCore`, `PresentationFramework`,
`WindowsBase`) ve `mscorlib` kullanılır. Tepsi (tray) simgesi yerine, çıkış
başlık `⋯` menüsünden yapılır.

## Kurulum (.NET Framework projesinde)

Sadece şu **4 dosyanın içeriğini** repodakiyle değiştir:
`App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`.
Sonra **Derle → F5**. `.csproj`'a veya `app.manifest`'e dokunmana gerek yok.

## Depolama

- Ayarlar: `%AppData%\ModernFences\config.txt`
- Öğeler: `%AppData%\ModernFences\Depo\<pencere-id>\`

Bir pencere kaldırılırsa içindeki öğeler **masaüstüne geri taşınır**.
"Uygulamadan Çık" ise pencereleri kapatır ama öğeleri yerinde bırakır
(bir sonraki açılışta geri gelir).

## Notlar

- **Yönetici olarak çalıştırmayın** — Explorer'dan sürükle-bırak engellenir.
- Buzlu cam varsayılan `BLURBEHIND`. Daha modern acrylic için `App.xaml.cs`
  içindeki `AccentState`'i `ACCENT_ENABLE_ACRYLICBLURBEHIND` yapın (bazı
  sürümlerde siyah kutu yapabilir).
- Farklı diske taşımada `Directory.Move` hata verebilir; aynı disk içinde
  sorunsuz.

> Repodaki `ModernFences.csproj`/`app.manifest`, projeyi `dotnet` ile .NET 8
> olarak derlemek isteyenler içindir; .NET Framework kullanıcısı bunlara
> dokunmaz.
