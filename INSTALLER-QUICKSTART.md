# DGScope Installer Setup - Quick Reference

## ✅ What's Been Created

I've set up **three different installer options** for DGScope:

### 1️⃣ WiX Toolset (MSI Installer)
- **Files:**
  - `DGScope.Installer/` - WiX project files
  - `build-installer.ps1` - Build script
- **Output:** `DGScope_Setup.msi`
- **Best for:** Enterprise deployment, professional distribution

### 2️⃣ Inno Setup (EXE Installer)  
- **Files:**
  - `DGScope.iss` - Inno Setup script
  - `build-installer-inno.ps1` - Build script
- **Output:** `DGScope_Setup_1.0.0.exe`
- **Best for:** Easy setup, single-file installer (RECOMMENDED)

### 3️⃣ Portable ZIP
- **Files:**
  - `build-portable.ps1` - Build script
- **Output:** `DGScope_Portable_v1.0.0.zip`
- **Best for:** No installation needed, extract and run

---

## 🚀 Quick Start

### Choose ONE method:

#### **Option A: Inno Setup (Easiest)** ⭐
```powershell
# 1. Download and install Inno Setup:
# https://jrsoftware.org/isinfo.php

# 2. Run this command:
.\build-installer-inno.ps1

# 3. Get your installer:
# installer-output\DGScope_Setup_1.0.0.exe
```

#### **Option B: WiX Toolset (Professional)**
```powershell
# 1. Download and install WiX v3.11+:
# https://github.com/wixtoolset/wix3/releases/tag/wix3112rtm

# 2. Run this command:
.\build-installer.ps1

# 3. Get your installer:
# DGScope.Installer\bin\Release\DGScope_Setup.msi
```

#### **Option C: Portable ZIP (No installer)**
```powershell
# Just run:
.\build-portable.ps1

# Get your ZIP:
# portable-output\DGScope_Portable_v1.0.0.zip
```

---

## 📦 What Users Get

All installers include:
- ✅ DGScope executable (`scope.exe`)
- ✅ All required DLLs and dependencies
- ✅ Configuration files
- ✅ Start Menu shortcut
- ✅ Optional Desktop shortcut
- ✅ Proper uninstall support (MSI/EXE only)

---

## 🔧 Customization

### Change Version Number
```powershell
# Inno Setup:
.\build-installer-inno.ps1 -Version "2.0.1"

# WiX:
.\build-installer.ps1 -Version "2.0.1"

# Portable:
.\build-portable.ps1 -Version "2.0.1"
```

### Add Your Logo/Icon
1. Place your icon file at: `scope\Resources\AppIcon.ico`
2. Uncomment the icon lines in:
   - `DGScope.Installer\Product.wxs` (for WiX)
   - `DGScope.iss` already references it (for Inno Setup)

### Include Additional Files
Edit the respective installer file:
- **WiX:** Add files in `Product.wxs` under `<ComponentGroup Id="ProductComponents">`
- **Inno Setup:** Add to `[Files]` section in `DGScope.iss`

---

## 🤖 Automated GitHub Builds

A GitHub Actions workflow has been created at:
`.github\workflows\build-installer.yml`

### How to use:
1. Push a tag: `git tag v1.0.0 && git push origin v1.0.0`
2. GitHub automatically builds both MSI and EXE installers
3. Installers are attached to the GitHub Release

Or manually trigger from GitHub Actions tab.

---

## 📝 Important Notes

### First-Time Setup
- **WiX users:** After installing WiX, restart Visual Studio
- **Inno Setup users:** Default install path is `C:\Program Files (x86)\Inno Setup 6\`

### .NET Framework Requirement
- DGScope requires .NET Framework 4.7.2 or later
- Inno Setup installer checks this automatically
- Consider including .NET installer for users who don't have it

### Build Output Location
Make sure you have a successful Release build before running installer scripts:
```powershell
msbuild scope.sln /p:Configuration=Release /p:Platform="Any CPU"
```

The release binaries should be in: `build\Release\`

---

## 🆘 Troubleshooting

### "WiX Toolset not found"
- Install from: https://github.com/wixtoolset/wix3/releases
- Make sure `%WIX%` environment variable is set
- Restart PowerShell/VS after install

### "Inno Setup not found"
- Install from: https://jrsoftware.org/isinfo.php
- Default path: `C:\Program Files (x86)\Inno Setup 6\`

### "Build failed"
- Check that solution builds successfully first
- Verify `build\Release\scope.exe` exists
- Look for NuGet package restoration issues

### Missing DLLs in installer
- Check `build\Release\` folder for all required files
- Add missing files to the installer configuration
- Ensure all project references are set to "Copy Local = True"

---

## 📚 More Information

See `INSTALLER-README.md` for detailed documentation.

## 🎯 Recommended Approach

For most users, **Inno Setup** is the best choice:
- ✅ Free and open source
- ✅ Simple to set up
- ✅ Single EXE file (easier to distribute)
- ✅ Professional looking installer
- ✅ Checks .NET Framework automatically

WiX is better if you need:
- Enterprise deployment features
- MSI format requirement
- Windows Installer database features
- Group Policy deployment
