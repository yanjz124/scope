# DGScope Installer

This project uses automated GitHub Actions to build and release installers.

## 🚀 Creating a Release

Simply create and push a git tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will automatically:
- ✅ Build DGScope in Release mode
- ✅ Create an EXE installer (Inno Setup)
- ✅ Create an MSI installer (WiX Toolset)
- ✅ Create a portable ZIP with all dependencies
- ✅ Publish a GitHub Release with all files attached

See [RELEASE-GUIDE.md](RELEASE-GUIDE.md) for complete release instructions.

---

## 📦 What Gets Released

Each release includes three download options:

### 1️⃣ DGScope-Setup-v{version}.exe (Recommended)
- Single-file installer built with Inno Setup
- Best for end users
- Includes installation wizard
- Creates shortcuts automatically
- ~25-50 MB

### 2️⃣ DGScope-Setup-v{version}.msi
- Windows Installer package built with WiX Toolset
- Best for enterprise/IT deployment
- Group Policy compatible
- MSI format features
- ~25-50 MB

### 3️⃣ DGScope-Portable-v{version}.zip
- No installation required
- Extract and run `scope.exe`
- Perfect for USB drives or testing
- Includes all dependencies
- ~30-60 MB

---

## 🔧 Manual Local Builds (Optional)

If you need to test installers locally before releasing:

### Prerequisites
### Prerequisites

For manual local builds only (not needed for GitHub releases):
- [WiX Toolset v3.11+](https://github.com/wixtoolset/wix3/releases) for MSI
- [Inno Setup](https://jrsoftware.org/isinfo.php) for EXE
- Visual Studio 2017+ or MSBuild

### Build Manually

```powershell
# Build solution
msbuild scope.sln /p:Configuration=Release /p:Platform="Any CPU"

# Build WiX MSI
msbuild DGScope.Installer\DGScope.Installer.wixproj /p:Configuration=Release

# Build Inno Setup EXE (modify version in DGScope.iss first)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" DGScope.iss

# Create Portable ZIP
Compress-Archive -Path ".\build\Release\*" -DestinationPath "DGScope-Portable.zip"
```

---

## 📁 Project Structure

```
DGScope.Installer/
├── DGScope.Installer.wixproj   # WiX project file
└── Product.wxs                  # WiX installer definition

DGScope.iss                      # Inno Setup script

.github/workflows/
└── build-installer.yml          # Automated build workflow
```

---

## 🎨 Customization

### Change Product Information

Edit [DGScope.Installer/Product.wxs](DGScope.Installer/Product.wxs):
- `ProductName` - Application name
- `Manufacturer` - Company/developer name
- **DO NOT CHANGE** `UpgradeCode` after first release

Edit [DGScope.iss](DGScope.iss):
- `MyAppName` - Application name
- `MyAppPublisher` - Company/developer name
- `MyAppURL` - Website/repository URL

### Add Application Icon

1. Add icon file: `scope\Resources\AppIcon.ico`
2. Uncomment icon lines in:
   - `Product.wxs` (WiX)
   - `DGScope.iss` (Inno Setup)

### Include Additional Files

**WiX:** Edit `Product.wxs`, add to `ProductComponents` group
**Inno Setup:** Edit `DGScope.iss`, add to `[Files]` section

---

## 🤖 GitHub Actions Workflow

The workflow in [.github/workflows/build-installer.yml](.github/workflows/build-installer.yml):

**Triggers:**
- Any tag starting with `v` (e.g., `v1.0.0`, `v2.1.3-beta`)
- Manual dispatch from Actions tab

**What it does:**
1. Checks out code
2. Restores NuGet packages
3. Builds solution in Release mode
4. Creates portable ZIP with all dependencies
5. Installs WiX Toolset
6. Builds MSI installer
7. Installs Inno Setup  
8. Builds EXE installer
9. Generates release notes
10. Creates GitHub Release with all files attached

**Runtime:** ~5-10 minutes

---

## 📊 Release Versioning

Use semantic versioning: `MAJOR.MINOR.PATCH[-PRERELEASE]`

Examples:
- `v0.0.1-alpha1` - Alpha release
- `v0.1.0-beta1` - Beta release
- `v1.0.0` - First stable release
- `v1.0.1` - Bug fix
- `v1.1.0` - New features
- `v2.0.0` - Breaking changes

---

## 🆘 Troubleshooting

### Release not created on GitHub
- Ensure tag starts with `v` (e.g., `v1.0.0`)
- Check Actions tab for build errors
- Verify GitHub Actions is enabled for your repo

### Build fails in GitHub Actions
- Check Actions logs for specific error
- Common issues:
  - NuGet package restore failure
  - Compilation errors
  - Missing dependencies

### Missing files in installers
- Ensure all dependencies are in `build\Release\`
- Update installer configurations to include new files
- Check that project references have "Copy Local = True"

---

## 📚 Additional Documentation

- **[RELEASE-GUIDE.md](RELEASE-GUIDE.md)** - Complete guide to creating releases
- **[INSTALLER-QUICKSTART.md](INSTALLER-QUICKSTART.md)** - Quick reference

---

## ✅ Installation Features

All installers include:
- Complete application with all DLLs
- Configuration files
- Start Menu shortcuts
- Optional Desktop shortcut
- Proper uninstall support (MSI/EXE)
- Upgrade support (newer versions replace older)
- .NET Framework 4.7.2 requirement check (Inno Setup)

---

## 🎯 Recommended for Users

**Most users:** Download the `.exe` installer (Inno Setup)
**IT/Enterprise:** Use the `.msi` installer (WiX)
**Portable/Testing:** Use the `.zip` package

---

For questions or issues, see the [GitHub repository](https://github.com/yanjz124/scope).
