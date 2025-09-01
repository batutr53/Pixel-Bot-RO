# HP/MP Percentage Monitoring System

Bu sistem, Python kodunuzdaki HP/MP kontrolü mantığını C# uygulamanıza entegre eder. Sistem, HP ve MP barlarınızın belirli yüzdelerini izleyerek renk değişimi algıladığında otomatik tıklama işlemleri gerçekleştirir.

## Özellikler

### 1. Yüzdeye Dayalı İzleme
```python
# Python kodunuzdaki mantık:
HP_region = (100, 67, 250 - 100, 1)  # X: 100-250, Y: 67
MP_region = (100, 84, 250 - 100, 1)  # X: 100-250, Y: 84

percentage_input_hp = float(input("HP kontrol %?: "))
x_hp_to_check = HP_region[0] + int((percentage_input_hp / 100) * HP_region[2])
```

C# sistemimizde aynı mantık:
```json
{
  "name": "HP_Monitor_50",
  "startX": 100,
  "endX": 250,
  "y": 67,
  "monitorPercentage": 50.0,
  "expectedColor": [16, 12, 255]  // 0x100CFF RGB değeri
}
```

### 2. Renk Değişimi Algılama
```python
# Python kodunuzdaki renk kontrol:
HP_color = (16, 12, 255)  # RGB => 0x100CFF
MP_color = (255, 77, 23)  # RGB => 0xFF4D17

pixel_color_hp = pyautogui.pixel(x_hp_to_check, HP_region[1])
if pixel_color_hp != HP_color:
    print("Renk değişti, tuş gönder")
```

C# sisteminde otomatik algılama:
```csharp
// PercentageProbe otomatik olarak renk değişimini algılar
// edge-falling, edge-rising, threshold-below, threshold-above events
```

## Kullanım

### 1. Konfigürasyon Dosyası

`hp-mp-config-example.json` dosyasını `config.json` olarak kopyalayın veya mevcut config'inize ekleyin:

```json
"percentageProbes": [
  {
    "name": "HP_Monitor_50",
    "type": "HP",
    "startX": 100,      // HP barının başlangıç X koordinatı
    "endX": 250,        // HP barının bitiş X koordinatı  
    "y": 67,            // HP barının Y koordinatı
    "monitorPercentage": 50.0,  // İzlenecek yüzde
    "expectedColor": [16, 12, 255],  // HP barının beklenen rengi
    "tolerance": 30,    // Renk toleransı
    "mode": "edge",     // Kenar algılama modu
    "debounceMs": 100   // Tekrar tetikleme koruması
  }
]
```

### 2. Event Tanımları

Belirli koşullarda otomatik tıklama:

```json
"events": [
  {
    "when": "HP_Monitor_50:threshold-below",  // HP %50'nin altına düştüğünde
    "click": { "x": 400, "y": 300 },         // Healing potion koordinatı
    "cooldownMs": 500,
    "priority": 1
  },
  {
    "when": "MP_Monitor_25:threshold-below",  // MP %25'in altına düştüğünde  
    "click": { "x": 450, "y": 350 },         // Mana potion koordinatı
    "cooldownMs": 300,
    "priority": 2
  }
]
```

### 3. Çoklu İzleme Noktaları

Farklı yüzdeler için farklı aksiyonlar:

```json
"percentageProbes": [
  {
    "name": "HP_Critical",
    "monitorPercentage": 25.0,  // Kritik HP %25
    "expectedColor": [16, 12, 255]
  },
  {
    "name": "HP_Low", 
    "monitorPercentage": 50.0,  // Düşük HP %50
    "expectedColor": [16, 12, 255]
  },
  {
    "name": "HP_Med",
    "monitorPercentage": 75.0,  // Orta HP %75
    "expectedColor": [16, 12, 255]
  }
]
```

## Event Türleri

### Threshold Events (Eşik Geçişi)
- `threshold-below`: Belirtilen yüzdenin altına düştüğünde
- `threshold-above`: Belirtilen yüzdenin üstüne çıktığında

### Color Change Events (Renk Değişimi)
- `edge-falling`: Beklenen renkten farklı renge geçiş
- `edge-rising`: Farklı renkten beklenen renge geçiş

## Çalıştırma

### CLI üzerinden:
```bash
# Yeni HP/MP profilini çalıştır
dotnet run --project src/Host.Console -- --profile HP-MP-Monitor

# Dry-run modu ile test et (tıklama yapmaz, sadece log)
dotnet run --project src/Host.Console -- --profile HP-MP-Monitor --dry-run

# Belirli capture modu ile
dotnet run --project src/Host.Console -- --profile HP-MP-Monitor --capture WGC --hz 60
```

### Overlay Tool ile:
```bash
# Görsel konfigürasyon için
dotnet run --project src/Tool.Overlay.WPF
```

## Avantajlar

1. **Yüzde Tabanlı**: Farklı pencere boyutlarında çalışır
2. **Çoklu İzleme**: Aynı anda birden fazla yüzde noktası
3. **Otomatik Kalkulasyon**: X koordinatı otomatik hesaplanır
4. **Renk Toleransı**: Lighting değişikliklerine dayanıklı
5. **Debounce Koruması**: Aşırı tetiklemeyi önler
6. **Priority Sistemi**: Önemli aksiyonlara öncelik
7. **Cooldown**: Spam koruması

## Teknik Detaylar

### PercentageProbe Sınıfı
- `StartX, EndX`: Bar koordinat aralığı
- `MonitorPercentage`: İzlenecek yüzde
- `CalculatedX`: Otomatik hesaplanan X koordinatı
- `CalculateCurrentPercentage()`: Gerçek zamanlı yüzde hesaplama

### Renk Algılama
- RGB, HSV, DeltaE metrikleri
- Configurable tolerance
- Box averaging için çevresel pixel örnekleme

### Event System Entegrasyonu
- Mevcut event sistemine tam uyumlu
- Priority ve cooldown desteği
- Batch processing için optimize