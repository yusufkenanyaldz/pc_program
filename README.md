# Python Fences — Bağımsız Masaüstü Düzenleyici

Masaüstünüzdeki **programları, klasörleri ve dosyaları**, birbirinden
**bağımsız açılır pencereler** (fence) içinde düzenleyin.

## Ne değişti?

Eski sürüm, pencereye sürüklenen dosyaları `Fences_Deposu` adlı bir klasöre
**fiziksel olarak taşıyordu** (klasörleme). Bu sürümde:

- ✅ **Dosyalar TAŞINMAZ.** Her öğe sadece *referans* (kısayol mantığı) olarak
  tutulur; orijinal dosya/klasör bulunduğu yerde kalır.
- ✅ **Birden fazla bağımsız pencere** oluşturabilirsiniz. Her pencerenin kendi
  başlığı, konumu ve öğe listesi vardır.
- ✅ **Program (`.exe`), klasör ve her dosya türü** sürükle-bırak ile eklenir.

## Kullanım

```bash
pip install -r requirements.txt
python python_fences.py
```

- **Sürükle-bırak:** Program, klasör veya dosyayı pencereye bırakın.
- **Aç:** Öğeye çift tıklayın.
- **Pencereyi taşı:** Mavi başlık çubuğundan tutup sürükleyin.
- **Yeni pencere:** Alttaki `+ Yeni Pencere` düğmesi.
- **Pencere adını değiştir:** Başlığa çift tıklayın veya ✎ düğmesine basın.
- **Pencereyi kapat:** ✕ düğmesi.
- **Sağ tık menüsü:**
  - *Aç* / *Konumunu Aç*
  - *Pencereden Çıkar* (dosya silinmez, sadece referans kaldırılır)
  - *Diskten Sil…* (kalıcı siler — onay ister)

## Veri

Pencereler ve öğe listeleri `fences_data.json` dosyasına otomatik kaydedilir.
Eski `fences_data.json` biçimi (düz öğe listesi) otomatik olarak tek bir
pencereye dönüştürülür.

## Not

`os.startfile` yalnızca Windows'ta çalışır; program macOS (`open`) ve
Linux (`xdg-open`) için de uyumlu hale getirilmiştir. Sürükle-bırak için
`tkinterdnd2` gereklidir.
