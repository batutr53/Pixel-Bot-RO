# BabeMakro Performance Tests

Bu dizin BabeMakro'nun performance testlerini içerir. Testler non-invasive olarak tasarlanmıştır ve mevcut kodu değiştirmez.

## 📁 Test Dosyaları

### Core Framework
- **`BasePerformanceTest.cs`** - Tüm testler için base class
- **`PerformanceTests.csproj`** - Test projesi konfigürasyonu

### Test Kategorileri

#### 1. Memory Tests (`MemoryLeakTests.cs`)
- ✅ Kısa vadeli memory stability (5 dakika)
- ✅ Orta vadeli memory leak testi (30 dakika)
- ✅ Uzun vadeli 24 saat endurance test (manuel aktif edilmeli)
- ✅ Handle leak detection
- ✅ GC pressure analizi

#### 2. CPU Tests (`CpuUsageTests.cs`)
- ✅ Normal yük altında CPU kullanımı
- ✅ Timer system CPU impact
- ✅ Yüksek frekanslı operasyon efficiency
- ✅ Multi-monitor simulation
- ✅ Thread efficiency

#### 3. Stress Tests (`StressTests.cs`)
- ✅ Single client endurance (1 saat)
- ✅ Rapid start/stop stress test
- ✅ Memory pressure simulation
- ✅ High DPI multi-monitor simulation
- ✅ Maximum load stress test (manuel)

#### 4. Timer Tests (`TimerPrecisionTests.cs`)
- ✅ 120Hz timer precision
- ✅ Frequency comparison (60/80/120 Hz)
- ✅ MasterTimer consolidation benefits
- ✅ Timer stability under load
- ✅ BenchmarkDotNet precision benchmarks

#### 5. Reporting (`PerformanceReportGenerator.cs`)
- ✅ Comprehensive performance assessment
- ✅ HTML, JSON, Markdown reports
- ✅ Automated scoring system
- ✅ Performance recommendations

## 🚀 Testleri Çalıştırma

### Prerequisite
BabeMakro uygulamasının çalışır durumda olması gerekir. Testler external monitoring yapar.

### Komutlar

```bash
# Tüm testleri çalıştır
dotnet test

# Specific test kategorisi
dotnet test --filter "Category=Memory"
dotnet test --filter "Category=CPU"

# Comprehensive report oluştur
dotnet test --filter "Generate_Comprehensive_Performance_Report"

# Benchmark testleri (BenchmarkDotNet)
dotnet run -c Release --filter "*Benchmark*"
```

### Manual Test Activation

Bazı testler uzun sürdüğü için manuel aktif edilmelidir:

```csharp
// 24 saat memory leak test
[Fact(Skip = "Long running test - enable manually for 24h testing")]

// Maximum load stress test
[Fact(Skip = "Resource intensive test - enable manually")]
```

Bu testleri aktif etmek için `Skip` attribute'unu kaldırın.

## 📊 Performance Kriterleri

### Memory
- ✅ Ortalama kullanım < 500MB
- ✅ 24 saat memory growth < %15
- ✅ Handle leak %5'in altında

### CPU
- ✅ 8 client ile ortalama < %10
- ✅ CPU spikes < %25
- ✅ Timer jitter < %15

### Timer Precision
- ✅ 120Hz target için jitter < %15
- ✅ Accuracy deviation < %10
- ✅ Standard deviation < 2ms

### Stability
- ✅ Handle count değişimi < %5
- ✅ Thread count değişimi < %10
- ✅ Process responsive durumda

## 📁 Report Outputs

Test raporları `E:\ro\performance-reports\` dizininde saklanır:

- **JSON**: Machine-readable detaylı metrics
- **HTML**: Web browser'da görüntüleme
- **Markdown**: Documentation ve GitHub integration

## 🔧 Troubleshooting

### "BabeMakro process not found"
- BabeMakro uygulamasını başlatın
- Process name'in "BabeMakro" olduğundan emin olun

### Permission Errors
- Performance counter access için admin yetkisi gerekebilir
- Visual Studio'yu "Run as Administrator" ile çalıştırın

### Test Timeout
- Uzun testler için timeout ayarlarını artırın
- `xunit.runner.json` dosyasında timeout konfigürasyonu

## 📈 Continuous Integration

Bu testler CI/CD pipeline'a entegre edilebilir:

```yaml
# Azure DevOps / GitHub Actions example
- name: Run Performance Tests
  run: |
    # Start BabeMakro in background
    start /b dotnet run --project src/BabeMakro

    # Wait for startup
    timeout /t 10

    # Run performance tests
    dotnet test tests/PerformanceTests --logger trx

    # Upload performance reports
    # (implementation specific)
```

## ⚠️ Important Notes

1. **Non-Invasive**: Testler mevcut BabeMakro kodunu değiştirmez
2. **External Monitoring**: Process ve system metrics'i external olarak okur
3. **Resource Intensive**: Bazı testler sistem kaynağı kullanır
4. **Production Safe**: Production ortamında güvenle çalıştırılabilir

## 🎯 Gelecek İyileştirmeler

- [ ] Real-time dashboard integration
- [ ] Automated performance regression detection
- [ ] Multi-machine distributed testing
- [ ] Performance baseline tracking
- [ ] Alert system integration