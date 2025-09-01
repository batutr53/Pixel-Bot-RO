# Python-Style HP/MP Monitoring - WPF Overlay Kullanımı

Bu rehber, Python kodunuzdaki HP/MP kontrolünün C# WPF overlay tool'una nasıl entegre edildiğini ve nasıl kullanılacağını açıklar.

## 🎯 Python Kodundan C#'a Geçiş

### Python Kodunuz:
```python
HP_region = (100, 67, 250 - 100, 1)  # X: 100-250, Y: 67
MP_region = (100, 84, 250 - 100, 1)  # X: 100-250, Y: 84

HP_color = (16, 12, 255)  # RGB hali => 0x100CFF
MP_color = (255, 77, 23)  # RGB hali => 0xFF4D17

percentage_input_hp = float(input("HP kontrol %?: "))
x_hp_to_check = HP_region[0] + int((percentage_input_hp / 100) * HP_region[2])

while True:
    pixel_color_hp = pyautogui.pixel(x_hp_to_check, HP_region[1])
    if pixel_color_hp != HP_color:
        print("Renk değişti, tuş gönder")
        break
```

### C# WPF Overlay Karşılığı:
✅ **Tamamen aynı mantık**: Yüzde hesaplama, renk kontrolü ve otomatik tıklama
✅ **Görsel interface**: Koordinatları mouse ile seçebilirsiniz
✅ **Real-time monitoring**: 50ms (20Hz) hızında sürekli kontrol
✅ **Otomatik trigger**: Renk değiştiğinde potion tıklar

## 🛠️ WPF Overlay Tool Kullanımı

### 1. WPF Overlay Tool'u Başlatın
```bash
dotnet run --project src/Tool.Overlay.WPF
```

### 2. Client Card Açıklaması
Her client için ayrı bir card bulunur. Python-style HP/MP kontrolü için **🎯 Python-Style HP/MP %** bölümüne odaklanın.

### 3. HP/MP Bar Kurulumu

#### **Step 1: Window Selection**
- **🎯 Select Window** butonuna tıklayın
- Ragnarok/MuMu Player penceresini seçin
- Yeşil status indicator görmelisiniz

#### **Step 2: HP Bar Configuration**
- **❤️HP** satırında alanlar:
  - **Start X**: `100` (HP barının başlangıç X koordinatı)
  - **End X**: `250` (HP barının bitiş X koordinatı)
  - **Y**: `67` (HP barının Y koordinatı)
  - **Threshold**: `50` (Tetiklenecek yüzde - örn: %50'nin altında)
  - **Tolerance**: `30` (Renk toleransı)

#### **Step 3: HP Bar Area Selection**
- **📏** butonuna tıklayın (Pick HP Bar Area)
- Mouse ile HP barının **tüm alanını** seçin (drag & drop)
- Sistem otomatik olarak StartX, EndX, Y değerlerini ayarlayacak
- HP barının tam rengini otomatik olarak algılayacak

#### **Step 4: MP Bar Configuration**
- **💙MP** satırı için aynı işlemi tekrarlayın:
  - Default değerler: Start=100, End=250, Y=84
  - **📏** buton ile MP bar alanını seçin

#### **Step 5: Potion Click Setup**
- Mevcut **❤️ HP Probe** ve **💙 MP Probe** bölümlerinde:
  - **Pota** koordinatları: HP/MP potion'larınızın bulunduğu koordinatlar
  - **📍** butonları ile potion koordinatlarını seçin

#### **Step 6: Activation**
- **🎯 Python-Style HP/MP %** bölümündeki **Enable** checkbox'ını işaretleyin
- **▶️ Start** butonuna tıklayın

## 📊 Monitor Display

### Real-time Status
- **HP Status**: `OK` (yeşil) veya `LOW` (kırmızı)  
- **MP Status**: `OK` (mavi) veya `LOW` (kırmızı)
- **Monitor Position**: Hesaplanan X koordinatları `HP: 175 (50%) MP: 175 (50%)`

### Example Setup
```
❤️HP: [100] [250] [67] [50] [30] 📏 [■]
💙MP: [100] [250] [84] [50] [30] 📏 [■]

HP Status: OK    MP Status: OK    Monitor Position: HP: 175 (50%) MP: 175 (50%)
```

## 🔧 Configuration Details

### Koordinat Sistemi (Python ile Aynı)
```python
# Python kodunuz:
x_to_check = region[0] + int((percentage / 100) * region[2])
# region = (startX, Y, width, height)

# C# karşılığı:
CalculatedX = StartX + (MonitorPercentage / 100.0) * (EndX - StartX)
```

### Renk Kontrolü (Python ile Aynı)
```python
# Python:
if pixel_color_hp != HP_color:
    print("Renk değişti, tuş gönder")

# C#:
bool colorChanged = distance > tolerance;
if (colorChanged && !isTriggered) {
    TriggerPotionClick();
}
```

## 🎮 Kullanım Senaryoları

### Senaryo 1: Temel HP/MP İzleme
1. HP barı: X(100-250), Y(67), %50 threshold
2. MP barı: X(100-250), Y(84), %50 threshold  
3. HP potion: (400, 300)
4. MP potion: (450, 350)

### Senaryo 2: Kritik Seviye İzleme
1. HP threshold: %25 (kritik seviye)
2. MP threshold: %25 (kritik seviye)
3. Düşük tolerance (10-15) hassas algılama için

### Senaryo 3: Farklı Client'lar
- Her client card ayrı ayrı konfigüre edilir
- Farklı window'lar farklı HP/MP bar konumlarına sahip olabilir
- Her client'ın kendi potion koordinatları

## 🚨 Troubleshooting

### Problem: Renk Algılanamıyor
**Çözüm**: 
- **📏** buton ile bar alanını yeniden seçin
- Tolerance değerini artırın (30-50)
- HP/MP barının tam dolu olduğu anda renk alın

### Problem: Yanlış Koordinat Hesaplama
**Çözüm**:
- StartX ve EndX değerlerini kontrol edin
- Monitor Position kısmından hesaplanan koordinatları doğrulayın
- `HP: 175 (50%)` şeklinde gösterim doğru mu kontrol edin

### Problem: Trigger Çalışmıyor
**Çözüm**:
1. **Enable** checkbox işaretli mi?
2. **▶️ Start** butonuna basıldı mı?
3. HP/MP potion koordinatları doğru mu?
4. Console'da `PYTHON-HP` veya `PYTHON-MP` logları görünüyor mu?

### Problem: Çok Hassas/Az Hassas
**Çözüm**:
- **Tolerance** değerini ayarlayın:
  - Çok hassas: Tolerance'ı artırın (40-50)
  - Az hassas: Tolerance'ı azaltın (15-25)

## 📝 Console Logs

### Başarılı Kurulum:
```
[Client 1] HP Percentage Bar: (100,67) -> (250,67) Color=RGB(16,12,255)
[Client 1] MP Percentage Bar: (100,84) -> (250,84) Color=RGB(255,77,23)
```

### Monitoring Active:
```
[Client 1] PYTHON-HP: Color changed at 175,67 (threshold 50%) - RGB(0,0,0) distance=442.7
[Client 1] PYTHON-HP: Triggering potion click at (400,300)
[Client 1] PYTHON-HP: Color restored at 175,67
```

### Debug Info:
```
[Client 1] PYTHON-HP: Current=RGB(16,12,255) Expected=RGB(16,12,255) Distance=0.0 OK
[Client 1] PYTHON-MP: Current=RGB(255,77,23) Expected=RGB(255,77,23) Distance=0.0 OK
```

## ⚡ Performance

- **20Hz Monitoring**: 50ms interval ile sürekli kontrol
- **Background Operation**: UI bloke etmeden çalışır
- **Multi-client Support**: 8+ client aynı anda
- **Low CPU Usage**: Sadece gerekli pixel'ler okunur

## 🔀 Gelişmiş Özellikler

### Multiple Threshold Support
- Her client için farklı yüzde threshold'ları
- HP: %25 kritik, %50 normal, %75 opsiyonel
- MP: %25 kritik, %50 normal

### Color Sync Between Clients
- Bir client'tan alınan renk diğerlerine senkronize edilebilir
- Tüm client'lar aynı HP/MP renk referansını kullanır

### Click Mode Integration
- Mevcut click system'i ile entegre
- PostMessage, SendMessage, Hardware click seçenekleri
- Background clicking (mouse hareket etmez)

Bu sistem Python kodunuzdaki mantığı tamamen koruyarak, görsel interface ve gelişmiş özellikler sunar. Artık HP/MP monitoring'inizi mouse ile kolayca konfigüre edebilir ve real-time olarak izleyebilirsiniz!