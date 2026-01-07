# DGScope Installer Setup - Quick Reference

## ✅ Release via GitHub Actions (Automated)

The easiest way to create a release with installers:

```bash
# 1. Create a version tag
git tag v1.0.0

# 2. Push the tag
git push origin v1.0.0

# 3. Done! GitHub Actions builds everything automatically
```

In 5-10 minutes, your release will be live with:
- ✅ DGScope-Setup-v1.0.0.exe (Inno Setup installer)
- ✅ DGScope-Setup-v1.0.0.msi (WiX MSI installer)
- ✅ DGScope-Portable-v1.0.0.zip (All dependencies)

Check progress at: `GitHub repo → Actions tab`
Download from: `GitHub repo → Releases section`

See [RELEASE-GUIDE.md](RELEASE-GUIDE.md) for detailed instructions.

---

## 📦 What Users Get

All releases include three download options:
- ✅ DGScope executable (`scope.exe`)
- ✅ All required DLLs and dependencies
- ✅ Configuration files
- ✅ Start Menu shortcut
- ✅ Optional Desktop shortcut
- ✅ Proper uninstall support (MSI/EXE only)

---

## 🔧 Version Numbering

```bash
# Stable releases
git tag v1.0.0

# Pre-releases
git tag v0.0.1-alpha1
git tag v1.0.0-beta2
git tag v2.0.0-rc1
```

---

## 🤖 What GitHub Actions Does

When you push a tag starting with `v`:

1. ✅ Builds solution in Release mode
2. ✅ Creates portable ZIP with all files
3. ✅ Builds MSI installer (WiX)
4. ✅ Builds EXE installer (Inno Setup)
5. ✅ Generates release notes
6. ✅ Creates GitHub Release
7. ✅ Uploads all files automatically

**No local tooling needed!** Everything happens in the cloud.

---

## 🛠️ Manual Local Builds (Optional)

Only if you want to test locally before releasing:

### Build Portable ZIP
```powershell
msbuild scope.sln /p:Configuration=Release /p:Platform="Any CPU"
Compress-Archive -Path ".\build\Release\*" -DestinationPath "DGScope-Portable.zip"
```

### Build MSI (requires WiX)
```powershell
msbuild DGScope.Installer\DGScope.Installer.wixproj /p:Configuration=Release
```

### Build EXE (requires Inno Setup)
```powershell
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" DGScope.iss
```

**Note:** For releases, just use GitHub Actions instead!

---

## 📝 Customization

### Change Version Number
```bash
git tag v2.0.1
git push origin v2.0.1
```

### Change Product Info

**WiX (MSI):** Edit `DGScope.Installer/Product.wxs`
**Inno Setup (EXE):** Edit `DGScope.iss`

---

## 🆘 Troubleshooting

### Release not created
- Ensure tag starts with `v` (e.g., `v1.0.0`)
- Check GitHub → Actions tab for errors
- Verify repo has Actions enabled

### Build failed in Actions
- Check Actions logs for details
- Common: NuGet restore or compile errors
- Fix locally, commit, and re-tag

---

## 📚 More Info

- **[RELEASE-GUIDE.md](RELEASE-GUIDE.md)** - Complete release guide
- **[INSTALLER-README.md](INSTALLER-README.md)** - Technical details
- **GitHub Actions:** `.github/workflows/build-installer.yml`

---

## 🎯 Quick Reference

```bash
# Create release
git tag v1.0.0 && git push origin v1.0.0

# View tags
git tag -l

# Delete tag (if mistake)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

**That's it! GitHub handles the rest.** 🚀
