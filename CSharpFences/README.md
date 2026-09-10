# C# Fences (WinForms)

Masaüstü düzenleyici — dosya/klasör/programları bir pencerede toplar.
Sürüklenen öğe **gizli bir depoya taşınır** (AppData içinde), böylece
masaüstünden kaybolur; pencereden tıklayınca açılır, "Masaüstüne Çıkar" ile
geri alınır.

## Çalıştırma

Visual Studio ile: `CSharpFences.csproj` dosyasını açıp **F5**.

Komut satırı ile (.NET 8 SDK gerekir):

```bash
cd CSharpFences
dotnet run
```

> WinForms yalnızca **Windows**'ta derlenir/çalışır.

## Önceki koddaki hatalar ve düzeltmeler

| Sorun | Neden çalışmıyordu | Çözüm |
|------|--------------------|-------|
| `SetParent(this.Handle, Progman)` | Pencere masaüstü simgelerinin **arkasına** gidiyor, görünmüyordu | Kaldırıldı; `TopMost` + sürüklenebilir başlık |
| `Main` yok | Proje çalıştırılamaz | `Program.cs` eklendi |
| Yönetici olarak açılış | Explorer'dan sürükle-bırak (UIPI) engellenir | `app.manifest` → `asInvoker` |
| Klasör ikonu yok | `ExtractAssociatedIcon` klasörde çalışmaz | `SHGetFileInfo` — exe/excel/resim/klasör için **gerçek** ikon |
| İkon sızıntısı | `hIcon` serbest bırakılmıyordu | `DestroyIcon` çağrısı |
| Aynı ada sahip dosya | Taşıma çakışıp hata veriyordu | `BenzersizYol` ile "(1)" eki |

## Özellikler

- Sürükle-bırak: program, klasör ve tüm dosyalar
- Gerçek Windows ikonları
- Başlıktan tutup pencere taşıma
- Sağ tık: **Aç** / **Masaüstüne Çıkar** / **Diskten Sil**
- Açılışta depodaki öğeleri otomatik yükleme

## Önemli notlar

- **Uygulamayı yönetici olarak ÇALIŞTIRMAYIN.** (Visual Studio'yu da admin
  açmayın.) Aksi halde masaüstünden sürükleme engellenir.
- Öğeler `%AppData%\CSharpFences_Gizli_Depo` altında tutulur. Uygulama
  silinirse dosyalar burada kalır; oradan geri alabilirsiniz.
- Farklı bir diske (ör. C: → D:) taşımada `Directory.Move` hata verebilir;
  aynı disk içinde sorunsuz çalışır.
