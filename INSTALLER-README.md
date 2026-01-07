# DGScope Installer

This folder contains multiple installer options for creating Windows installers for DGScope.

## Quick Start - Choose Your Method

### Method 1: Inno Setup (⭐ RECOMMENDED - Easiest)
**Best for:** Quick setup, simple requirements, single EXE installer
```powershell
# 1. Install Inno Setup: https://jrsoftware.org/isinfo.php
# 2. Run the build script:
.\build-installer-inno.ps1
# Output: installer-output\DGScope_Setup_1.0.0.exe
```

### Method 2: WiX Toolset (Professional MSI)
**Best for:** Enterprise deployment, Group Policy, Windows Installer features
```powershell
# 1. Install WiX Toolset: https://github.com/wixtoolset/wix3/releases
# 2. Run the build script:
.\build-installer.ps1
# Output: DGScope.Installer\bin\Release\DGScope_Setup.msi
```

### Method 3: Portable ZIP (No installer needed)
**Best for:** Quick distribution, no installation required, portable use
```powershell
.\build-portable.ps1
# Output: portable-output\DGScope_Portable_v1.0.0.zip
```

---

## Detailed Instructions

## Prerequisites

### Option 1: WiX Toolset (Recommended)
1. Download and install [WiX Toolset v3.11.2](https://github.com/wixtoolset/wix3/releases/tag/wix3112rtm) or later
2. Install Visual Studio 2017 or later with .NET desktop development workload
3. Optionally install the [WiX Toolset Visual Studio Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2019Extension)

### Option 2: Advanced Installer (Commercial Alternative)
If you prefer a GUI-based approach, you can use [Advanced Installer](https://www.advancedinstaller.com/) which has a free edition.

### Option 3: Inno Setup (Free Alternative)
[Inno Setup](https://jrsoftware.org/isinfo.php) is another free option that's simpler but still professional.

## Building the Installer

### Using PowerShell Script (Easiest)
```powershell
.\build-installer.ps1
```

This will:
1. Build the solution in Release mode
2. Create the MSI installer
3. Output to `DGScope.Installer\bin\Release\DGScope_Setup.msi`

### Custom Version Number
```powershell
.\build-installer.ps1 -Version "1.2.3.4"
```

### Manual Build
```batch
msbuild scope.sln /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild
msbuild DGScope.Installer\DGScope.Installer.wixproj /p:Configuration=Release
```

## Installer Features

The generated installer includes:
- ✅ Complete application with all dependencies
- ✅ Start Menu shortcut
- ✅ Desktop shortcut
- ✅ Proper uninstall support
- ✅ Upgrade support (newer versions replace older ones)
- ✅ Optional "Launch after install"
- ✅ Professional MSI package format

## Customization

### Change Install Location
Edit `Product.wxs` and modify the `INSTALLFOLDER` directory name.

### Add Application Icon
1. Add your icon file (e.g., `AppIcon.ico`)
2. Uncomment the icon lines in `Product.wxs`:
   ```xml
   <Icon Id="icon.ico" SourceFile="$(var.SolutionDir)\scope\Resources\AppIcon.ico"/>
   <Property Id="ARPPRODUCTICON" Value="icon.ico" />
   ```

### Include Additional Files
Edit the `ProductComponents` ComponentGroup in `Product.wxs` to add more files or folders.

### Modify Product Information
Edit the PropertyGroup defines at the top of `Product.wxs`:
- `ProductVersion` - Version number
- `ProductName` - Display name
- `Manufacturer` - Company/developer name
- `UpgradeCode` - **DO NOT CHANGE** after first release (ensures upgrades work)

## Distribution

The generated `.msi` file can be:
1. Distributed directly to users
2. Uploaded to a website for download
3. Included in a GitHub Release
4. Deployed via Group Policy or SCCM in enterprise environments

## Automated Builds with GitHub Actions

To automatically build installers on every release:

1. Add the WiX binaries to your repository, OR
2. Use the GitHub Actions workflow below:

```yaml
name: Build Installer

on:
  push:
    tags:
      - 'v*'
  release:
    types: [published]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '4.7.2'
    
    - name: Setup MSBuild
      uses: microsoft/setup-msbuild@v1
    
    - name: Install WiX Toolset
      run: |
        Invoke-WebRequest -Uri 'https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311.exe' -OutFile wix311.exe
        .\wix311.exe /install /quiet /norestart
    
    - name: Build Installer
      run: .\build-installer.ps1 -Version ${{ github.ref_name }}
    
    - name: Upload Installer
      uses: actions/upload-artifact@v3
      with:
        name: DGScope-Installer
        path: DGScope.Installer\bin\Release\*.msi
```

## Alternative: Inno Setup Script

If you prefer Inno Setup (no WiX dependency), create `DGScope.iss`:

```pascal
[Setup]
AppName=DGScope
AppVersion=1.0.0
DefaultDirName={pf}\DGScope
DefaultGroupName=DGScope
OutputDir=output
OutputBaseFilename=DGScope_Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "build\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\DGScope"; Filename: "{app}\scope.exe"
Name: "{autodesktop}\DGScope"; Filename: "{app}\scope.exe"

[Run]
Filename: "{app}\scope.exe"; Description: "Launch DGScope"; Flags: nowait postinstall skipifsilent
```

Then compile with:
```batch
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" DGScope.iss
```

## Troubleshooting

### "WiX Toolset not found"
Install WiX from https://github.com/wixtoolset/wix3/releases

### "Build failed" errors
1. Make sure the solution builds successfully first
2. Check that Release build output is in `build\Release\`
3. Verify all file paths in `Product.wxs` are correct

### Missing DLLs in installer
Check the `build\Release\` folder and add any missing files to the `Dependencies` component in `Product.wxs`.

## Support

For issues specific to the installer, check:
- [WiX Toolset Documentation](https://wixtoolset.org/documentation/)
- [WiX Tutorial](https://www.firegiant.com/wix/tutorial/)
