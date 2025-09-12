# 👸 BabeMakro

> Multi-client Windows automation tool for game management with pixel monitoring and automated actions.

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/yourusername/babemakro)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://docs.microsoft.com/en-us/windows/)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

## ✨ Features

- 🎮 **8 Independent Clients** - Manage multiple game instances simultaneously
- 🎯 **HP/MP Monitoring** - Pixel-perfect health and mana tracking
- 🔥 **Auto-Healing** - Party heal system with 8 member support
- 🛡️ **Anti-Captcha** - OCR-based captcha detection and handling
- ⚔️ **Attack/Skills** - Automated skill rotation system
- 💫 **Buff/AC Management** - Automatic buff maintenance
- 🎛️ **Master Controls** - Centralized panic start/stop

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime
- MuMu Player (for game clients)

### Installation
```bash
# Clone the repository
git clone https://github.com/yourusername/babemakro.git
cd babemakro

# Build the project
dotnet build -c Release

# Run the application
dotnet run --project src/BabeMakro
```

## 📖 Documentation

- [📋 Full Specification](SPECIFICATION.md) - Complete technical documentation
- [⚙️ Configuration Guide](docs/configuration.md) - Setup and config options
- [🎮 Usage Guide](docs/usage.md) - How to use all features
- [🔧 Development Guide](docs/development.md) - Contributing guidelines

## 🏗️ Architecture

```
BabeMakro/
├── src/
│   ├── BabeMakro/          # Main WPF application
│   ├── Core/               # Core interfaces and models  
│   └── Capture.Win/        # Windows capture implementations
├── docs/                   # Documentation
├── config.json            # Configuration file
└── SPECIFICATION.md        # Technical specification
```

## 📊 Performance

- **Monitoring Rate**: 60-120 Hz
- **CPU Usage**: < 5% per client
- **Memory**: < 100MB per client
- **Response Time**: < 10ms

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with ❤️ using .NET 8 and WPF
- OCR powered by Tesseract
- Screen capture via Windows Graphics Capture API