# DGScope - Digital Radar Scope Display

DGScope is a radar scope display application designed for air traffic control simulation and real-world aircraft tracking. It provides a realistic ATC radar display with support for multiple data sources.

## Features

- **Realistic Radar Display**: Full-featured ATC scope display with customizable appearance
- **Multiple Data Sources**: 
  - Dstars streaming servers
  - ADSB receivers (SBS/BaseStation format)
  - ADSBExchange integration
  - Falcon CDR playback
  - ScopeServer CDR playback
- **Full ATC Functionality**:
  - Data blocks with flight information
  - Leader lines and position symbols
  - Handoff coordination
  - Scratchpad entries
  - ATPA (conflict prediction)
- **Weather Integration**: NEXRAD weather radar display
- **Video Maps**: Support for GeoJSON and FAA video maps
- **Customizable Display**: Configurable colors, brightness, history trails

## Quick Start

### Installation

Download the latest release from the [Releases](https://github.com/yanjz124/scope/releases) page:

- **Windows Installer (Recommended)**: `DGScope-Setup-v{version}.exe`
- **MSI Installer (Enterprise)**: `DGScope-Setup-v{version}.msi`
- **Portable ZIP**: `DGScope-Portable-v{version}.zip`

See [INSTALLER-QUICKSTART.md](INSTALLER-QUICKSTART.md) for installation instructions.

### Basic Configuration

1. Launch DGScope
2. Configure your data receiver in the settings
3. Select your video map
4. Set your center point and range

For detailed configuration, see the documentation below.

## Data Sources

### Dstars Data Server

Dstars is a streaming server that provides integrated ADSB and flight plan data. This is the recommended data source for full functionality.

**📖 [Complete Dstars Documentation](DSTARS-DATA-SERVER.md)**

The documentation covers:
- How Dstars servers work
- Data structure and API endpoints
- Setting up your own Dstars server
- Integrating ADSB receivers with flight plan data
- Troubleshooting connection issues

Example configuration:
```xml
<Url>https://dstars.graiani.com/dstars/ILM/updates</Url>
```

### ADSB Receivers

DGScope can connect to various ADSB data sources:

- **SBS/BaseStation**: Direct TCP connection to dump1090/readsb
- **ADSBExchange**: Global ADSB coverage via API
- **Dstars**: Enhanced ADSB with flight plan integration (recommended)

**Note**: Basic ADSB receivers provide only position data. For full ATC features (flight plans, handoffs, etc.), use a Dstars server or similar integrated data source.

## Documentation

### For Users
- **[DSTARS-DATA-SERVER.md](DSTARS-DATA-SERVER.md)** - Complete guide to Dstars data servers
- **[INSTALLER-README.md](INSTALLER-README.md)** - Installer and build information
- **[INSTALLER-QUICKSTART.md](INSTALLER-QUICKSTART.md)** - Quick installation guide
- **[GUIDE_MultipleVideoMaps.md](GUIDE_MultipleVideoMaps.md)** - Using multiple video maps

### For Developers
- **[DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md)** - Technical guide for implementing Dstars-compatible servers
- **[RELEASE-GUIDE.md](RELEASE-GUIDE.md)** - Guide for creating releases

## Configuration Files

Configuration is stored in XML files that define:
- Receiver connections
- Display appearance and colors
- Video map selections
- Altitude filters
- ATPA settings
- And more...

Example configuration files:
- `ILM_default.xml` - Sample Wilmington ATCT configuration

## Building from Source

### Prerequisites

- Visual Studio 2017 or later
- .NET Framework 4.7.2 or later
- Windows 7 or later

### Build Steps

```bash
# Clone the repository
git clone https://github.com/yanjz124/scope.git
cd scope

# Restore NuGet packages
nuget restore scope.sln

# Build with MSBuild
msbuild scope.sln /p:Configuration=Release /p:Platform="Any CPU"
```

The compiled application will be in `scope/bin/Release/`.

### Creating Installers

See [INSTALLER-README.md](INSTALLER-README.md) for instructions on building installers.

## Project Structure

```
scope/
├── scope/                          # Main application
│   ├── RadarWindow.cs              # Main radar display
│   ├── Aircraft.cs                 # Aircraft tracking
│   └── ...
├── DGScope.Receivers.ScopeServer/  # Dstars/ScopeServer receiver
├── DGScope.Receivers.SBS/          # SBS/BaseStation receiver
├── DGScope.Receivers.ADSBX/        # ADSBExchange receiver
├── DGScope.Receivers.Falcon/       # Falcon CDR playback
├── DGScope.Installer/              # WiX installer project
└── DGScope.iss                     # Inno Setup installer script
```

## Use Cases

### Real-World Aircraft Tracking

DGScope can display real-time aircraft positions from ADSB receivers:

1. Set up an ADSB receiver (dump1090, readsb, etc.)
2. Configure DGScope to connect to your receiver
3. Or connect to a Dstars server for enhanced data

### Virtual ATC (VATSIM/IVAO)

DGScope can be adapted for virtual ATC use:

1. Connect to a compatible data source providing VATSIM/IVAO data
2. Use controller features for realistic ATC simulation

### Training and Education

- Practice ATC procedures
- Learn radar display interpretation
- Study traffic patterns and separation

## Community and Support

- **Issues**: Report bugs and request features on [GitHub Issues](https://github.com/yanjz124/scope/issues)
- **Discussions**: Ask questions in [GitHub Discussions](https://github.com/yanjz124/scope/discussions)

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

See [LICENSE](LICENSE) for license information.

## Credits

DGScope is developed by the community of air traffic control enthusiasts and developers.

Special thanks to:
- Contributors and testers
- Dstars server operators
- ADSB data providers (ADSBExchange, FlightAware, etc.)

## Frequently Asked Questions

### Why can't I see any aircraft?

- Check your receiver configuration
- Verify the server URL is correct and accessible
- Ensure your range and altitude filters include aircraft in your area
- Confirm the data source is providing data

### Why do I only see targets but no data blocks?

You may be using a basic ADSB receiver that only provides position data. For full data blocks with callsigns and flight information, use a Dstars server or similar integrated data source. See [DSTARS-DATA-SERVER.md](DSTARS-DATA-SERVER.md) for details.

### The Dstars server is down, what can I do?

The community Dstars servers may experience downtime. You can:
1. Set up your own Dstars server (see documentation)
2. Configure multiple fallback receivers
3. Use a direct ADSB receiver for basic tracking

### How do I set up my own data server?

See the comprehensive guide in [DSTARS-DATA-SERVER.md](DSTARS-DATA-SERVER.md) which covers:
- Server architecture
- Data sources (ADSB, flight plans)
- Implementation requirements
- Example code

---

**Note**: This is community-developed software. Always ensure compliance with applicable regulations when using for real-world aircraft tracking or ATC operations.
