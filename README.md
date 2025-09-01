# Pixel Automation Tool

Çoklu pencere (HWND) üzerinde gerçek zamanlı piksel/probe izlemesi yapıp kurallara göre tıklama üreten, performanslı ve modüler Windows otomasyon aracı.

## Özellikler

- **Çoklu Backend**: Windows Graphics Capture (WGC), PrintWindow, GetPixel
- **Esnek Tıklama**: Message modu (imleç zıplamaz) veya Cursor modu (SendInput)
- **Gerçek Zamanlı İzleme**: 60-120 Hz piksel analizi
- **Eşzamanlı İşleme**: Her pencere için ayrı worker thread'i
- **DPI Uyumlu**: Per-MonitorV2 DPI farkındalığı
- **Çoklu Monitör**: Tam ekran çoklu monitör desteği
- **İnteraktif Overlay**: Sürükle-bırak probe editörü
- **Profil Yönetimi**: Farklı senaryolar için preset'ler

## Gereksinimler

- Windows 10 v2004+ (build 19041+) WGC için
- .NET 8.0 Runtime
- Visual Studio 2022 veya .NET 8 SDK

## Kurulum

```bash
# Projeyi klonla
git clone <repo-url>
cd PixelAutomation

# Paketleri geri yükle
dotnet restore

# Projeyi derle
dotnet build -c Release

# CLI çalıştır
dotnet run --project src/Host.Console

# Overlay aracını çalıştır
dotnet run --project src/Tool.Overlay.WPF
```

## CLI Kullanımı

### Temel Kullanım
```bash
# Varsayılan config ile başlat
PixelAutomation.exe

# Belirli profil ile başlat
PixelAutomation.exe --profile TwoMonitor-8Clients

# Capture modunu belirle
PixelAutomation.exe --capture WGC --click message --hz 80

# Dry run modu (tıklamaları logla, çalıştırma)
PixelAutomation.exe --dry-run
```

### Komut Satırı Seçenekleri
- `--config/-c <path>`: Config dosyası yolu (varsayılan: config.json)
- `--profile/-p <name>`: Kullanılacak profil adı
- `--capture <mode>`: Capture modu (WGC/PRINT/GPIXEL)
- `--click <mode>`: Click modu (message/cursor-jump/cursor-return)
- `--hz <freq>`: Hedef yakalama frekansı
- `--dry-run/-d`: Dry run modu
- `--telemetry/-t`: Telemetri çıktısını etkinleştir

## Overlay Aracı Kullanımı

### Başlatma
```bash
dotnet run --project src/Tool.Overlay.WPF
```

### Temel İşlemler
1. **F2**: Hedef pencereyi seç (Window Picker)
2. **P**: Point probe ekle
3. **R**: Rectangle probe ekle
4. **F3**: Grid snap aç/kapat
5. **F4**: Magnifier aç/kapat
6. **Ctrl+S**: Config kaydet
7. **Del**: Seçili shape'i sil

### Shape Düzenleme
- **Sürükle-bırak**: Shape'leri taşı
- **Resize handles**: Rectangle probe'ları yeniden boyutlandır
- **Shift+Sürükle**: Eksen kilidi (sadece X veya Y)
- **Ctrl+Sürükle**: Kopyala
- **Grid snap**: 1px/5px/10px seçenekleri

### Profil Yönetimi
- **Ctrl+Alt+→**: Sonraki profil
- **Ctrl+Alt+←**: Önceki profil
- **Ctrl+O**: Config dosyası aç
- **Export**: Mevcut ayarları dışa aktar

## Konfigürasyon

### Probe Türleri
- **Point**: Tek piksel noktası izleme
- **Rect**: Dikdörtgen alan ortalama renk

### Modlar
- **Level**: Renk seviyesi testi (örn. siyah ise tetikle)
- **Edge**: Renk geçişi (örn. kırmızı→siyah)

### Click Modları
- **Message**: PostMessage ile tıkla (imleç hareket etmez)
- **Cursor-jump**: SendInput ile tıkla (imleç yeni yerde kalır)  
- **Cursor-return**: SendInput ile tıkla sonra eski konuma dön

### Örnek Probe Konfigürasyonu
```json
{
  "name": "RedButton",
  "kind": "point",
  "x": 320,
  "y": 420,
  "box": 5,
  "mode": "edge",
  "metric": "rgb",
  "refColor": [220, 40, 40],
  "toColor": [0, 0, 0],
  "tolerance": 30,
  "debounceMs": 30
}
```

## Performans

### Hedef Metrikler
- 8 worker @ 80 Hz: CPU %15-25
- Y periyodik tıklama sapması: ±15 ms
- Olay bazlı gecikme: 30-50 ms

### Optimizasyon İpuçları
1. **ROI Kullanımı**: Büyük pencereler için Region of Interest tanımla
2. **Backend Seçimi**: WGC > PrintWindow > GetPixel
3. **Debounce**: Yanlış tetikleri azaltmak için debounce süresi ekle
4. **Rate Limiting**: maxBurstPerSec ile aşırı tıklamayı önle

## Hotkey Setleri

### Default Set
- `Ctrl+~`: Overlay aç/kapat
- `F2`: Pencere seç
- `F3`: Snap aç/kapat
- `F4`: Magnifier
- `P`: Point probe ekle
- `R`: Rectangle probe ekle
- `Del`: Shape sil
- `Ctrl+S`: Kaydet
- `Pause`: Panic stop

### Streamer Set
Çakışma riski düşük tuş kombinasyonları:
- `Alt+` `: Overlay toggle
- `Ctrl+F2`: Pencere seç
- `Ctrl+Break`: Panic stop

## Sorun Giderme

### WGC Çalışmıyor
- Windows 10 v2004+ gerekli
- Minimized pencereler desteklenmiyor
- Bazı uygulamalar WGC engelleyebilir → PrintWindow fallback

### Message Click Çalışmıyor
- Bazı uygulamalar PostMessage'ı kabul etmez
- Auto-fallback cursor-return moduna geçer
- UAC korumalı uygulamalar için yükseltilmiş ayrıcalık gerekebilir

### DPI Sorunları
- Per-MonitorV2 farkındalık aktif
- Koordinatlar client alanına göre
- Pencere taşıma sonrası DPI otomatik güncellenir

### Performans Sorunları
- ROI boyutunu küçült
- Capture frekansını azalt (--hz 60)
- Debounce sürelerini artır
- Gereksiz probe'ları kapat

## Dosya Yapısı

```
PixelAutomation/
├── src/
│   ├── Core/                    # Temel arayüzler ve modeller
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Services/
│   ├── Capture.Win/             # Windows yakalama backend'leri
│   │   ├── Backends/
│   │   └── Providers/
│   ├── Host.Console/            # CLI uygulaması
│   │   └── Services/
│   └── Tool.Overlay.WPF/        # İnteraktif overlay
│       ├── Services/
│       ├── ViewModels/
│       └── Models/
├── config.json                 # Ana konfigürasyon
└── logs/                       # Log dosyaları
```

## Lisans

Bu proje MIT lisansı altında lisanslanmıştır.