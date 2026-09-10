# Python Fences — Bağımsız Masaüstü Düzenleyici

Masaüstünüzdeki **programları, klasörleri ve dosyaları**, birbirinden
**bağımsız açılır pencereler** (fence) içinde düzenleyin.

## Ne değişti?

Eski sürüm, pencereye sürüklenen dosyaları `Fences_Deposu` adlı bir klasöre
**fiziksel olarak taşıyordu** (klasörleme). Bu sürümde:

- ✅ **Dosyalar DİSKTE TAŞINMAZ.** Orijinal dosya/klasör yerinde kalır.
- ✅ **Gerçek Fences (Windows):** Masaüstündeki bir öğeyi pencereye
  sürüklediğinde, masaüstündeki **simgesi gizlenir** (dosya taşınmadan) ve
  öğe pencerede görünür. Pencereden çıkarınca simge masaüstüne geri gelir.
- ✅ **Birden fazla bağımsız pencere** oluşturabilirsiniz. Her pencerenin kendi
  başlığı, konumu ve öğe listesi vardır.
- ✅ **Program (`.exe`), klasör ve her dosya türü** sürükle-bırak ile eklenir.

### Masaüstü simge gizleme (gerçek Fences) — Windows

Simge gizleme `desktop_integration.py` ile yapılır (saf `ctypes`, ek paket
gerekmez). Çalışması için:

- **Windows** gerekir (64-bit Windows'ta 64-bit Python kullanın).
- Masaüstüne sağ tık → Görünüm → **"Simgeleri otomatik düzenle" KAPALI**
  olmalı (açıksa Windows simgeyi hemen geri taşır).
- Bu, Explorer'ın masaüstü liste görünümüne müdahale eden düşük seviyeli bir
  tekniktir; **deneyseldir**. Bir sorun olursa pencere başlığına sağ tıklayıp
  **"Tüm simgeleri masaüstüne geri getir"** ile hepsini kurtarabilirsiniz.
- Windows dışında veya modül yoksa program otomatik olarak **referans moduna**
  düşer: simge masaüstünde kalır, pencerede de kısayolu görünür.

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
- **Öğeye sağ tık:**
  - *Aç* / *Konumunu Aç*
  - *Pencereden Çıkar* (dosya silinmez; masaüstü simgesi gizlenmişse geri gelir)
  - *Diskten Sil…* (kalıcı siler — onay ister)
- **Başlığa sağ tık:** yeni pencere, **tüm simgeleri masaüstüne geri getir**
  (acil kurtarma), pencereyi kapat.

## Veri

Pencereler ve öğe listeleri `fences_data.json` dosyasına otomatik kaydedilir.
Eski `fences_data.json` biçimi (düz öğe listesi) otomatik olarak tek bir
pencereye dönüştürülür.

## Not

`os.startfile` yalnızca Windows'ta çalışır; program macOS (`open`) ve
Linux (`xdg-open`) için de uyumlu hale getirilmiştir. Sürükle-bırak için
`tkinterdnd2` gereklidir.
