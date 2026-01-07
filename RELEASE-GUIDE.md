# DGScope Release Guide

This guide explains how to create a new release with installers and portable builds.

## 🚀 Quick Release Process

### Method 1: Create a Release with Git Tag (Recommended)

```bash
# 1. Commit all your changes
git add .
git commit -m "Prepare for v1.0.0 release"

# 2. Create and push a version tag
git tag v1.0.0
git push origin v1.0.0

# 3. GitHub Actions automatically:
#    - Builds the solution
#    - Creates MSI installer
#    - Creates EXE installer  
#    - Creates portable ZIP
#    - Creates GitHub Release with all files attached
```

That's it! Check the "Releases" section of your GitHub repo in a few minutes.

---

## 📦 What Gets Released

When you push a tag, the workflow creates:

### 1. **DGScope-Setup-v{version}.exe** (Inno Setup)
- Single-file installer
- Recommended for most users
- ~25-50 MB

### 2. **DGScope-Setup-v{version}.msi** (WiX)
- Windows Installer package
- For enterprise deployment
- ~25-50 MB

### 3. **DGScope-Portable-v{version}.zip** 
- Portable version with all dependencies
- No installation required
- Extract and run
- ~30-60 MB

---

## 🎯 Method 2: Manual Release via GitHub UI

If you prefer not to use command line:

1. Go to your GitHub repository
2. Click on "Releases" → "Draft a new release"
3. Click "Choose a tag" and type `v1.0.0` (or your version)
4. Click "Create new tag: v1.0.0 on publish"
5. Fill in release title: "DGScope v1.0.0"
6. Click "Publish release"
7. GitHub Actions will automatically build and attach the files

---

## ⚙️ Method 3: Manual Workflow Trigger

You can also trigger a release manually without creating a tag:

1. Go to your repository on GitHub
2. Click "Actions" tab
3. Click "Build and Release DGScope" workflow
4. Click "Run workflow" dropdown
5. Enter version number (e.g., `1.0.0`)
6. Check "Create GitHub release" if you want it published
7. Click "Run workflow"

---

## 📋 Version Numbering Convention

Use semantic versioning: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes (e.g., `2.0.0`)
- **MINOR**: New features, backward compatible (e.g., `1.1.0`)
- **PATCH**: Bug fixes (e.g., `1.0.1`)

Examples:
- `v1.0.0` - First stable release
- `v1.0.1` - Bug fix release
- `v1.1.0` - New feature added
- `v2.0.0` - Major breaking changes

---

## 🔍 Monitoring the Build

After pushing a tag:

1. Go to GitHub repository
2. Click "Actions" tab
3. You'll see the workflow running
4. Click on it to see real-time progress
5. Build typically takes 5-10 minutes

### Build Steps:
1. ✅ Checkout code
2. ✅ Build solution (Release mode)
3. ✅ Create portable ZIP
4. ✅ Build MSI installer (WiX)
5. ✅ Build EXE installer (Inno Setup)
6. ✅ Create GitHub Release
7. ✅ Upload all files

---

## 📥 Download Links

After release, users can download from:

```
https://github.com/your-username/scope/releases/latest
```

Or specific version:
```
https://github.com/your-username/scope/releases/tag/v1.0.0
```

---

## 🛠️ Testing Before Release

Test locally before creating a release:

```powershell
# Build solution locally
msbuild scope.sln /p:Configuration=Release /p:Platform="Any CPU"

# Verify build output
dir .\build\Release\

# Test the executable
.\build\Release\scope.exe
```

For testing installers, you can build them manually (requires WiX/Inno Setup installed):
```powershell
# MSI (requires WiX)
msbuild DGScope.Installer\DGScope.Installer.wixproj /p:Configuration=Release

# EXE (requires Inno Setup)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" DGScope.iss
```

**Recommended:** Just push a pre-release tag and let GitHub Actions build everything!

---

## ✅ Pre-Release Checklist

Before creating a release:

- [ ] All features are complete and tested
- [ ] Version number updated in relevant places
- [ ] README.md is up to date
- [ ] CHANGELOG.md updated (if you have one)
- [ ] All tests pass
- [ ] Solution builds successfully in Release mode
- [ ] Locally built installers work correctly
- [ ] No uncommitted changes in git

---

## 🔧 Troubleshooting

### "Build failed" in GitHub Actions
- Check the Actions tab for error details
- Common issues:
  - NuGet package restore failed
  - Missing dependencies
  - Code doesn't compile

### "Release not created"
- Make sure you pushed the tag: `git push origin v1.0.0`
- Check that the tag starts with `v` (e.g., `v1.0.0` not `1.0.0`)
- Verify GitHub Actions has permission to create releases

### "Files not attached to release"
- Check the Actions log for upload errors
- Verify the build actually completed successfully
- Files might take a minute to appear after release creation

---

## 🎨 Customizing Release Notes

The workflow auto-generates release notes. To customize:

1. Edit `.github/workflows/build-installer.yml`
2. Find the "Generate release notes" step
3. Modify the `$releaseNotes` content

Or manually edit after release is created.

---

## 📊 Example Release Timeline

```
10:00 AM - Push tag: git push origin v1.0.0
10:01 AM - GitHub detects tag, starts workflow
10:02 AM - Building solution...
10:05 AM - Creating installers...
10:08 AM - Uploading artifacts...
10:09 AM - Creating GitHub Release
10:10 AM - ✅ Release published with all files!
```

---

## 🆘 Need Help?

- Check [INSTALLER-QUICKSTART.md](INSTALLER-QUICKSTART.md) for installer details
- Check [INSTALLER-README.md](INSTALLER-README.md) for comprehensive documentation
- Review GitHub Actions logs for build errors
- Test locally first with the build scripts

---

## 📝 Quick Reference Commands

```bash
# Create and push new release
git tag v1.0.0 && git push origin v1.0.0

# View all tags
git tag -l

# Delete a tag (if you made a mistake)
git tag -d v1.0.0              # Delete locally
git push origin :refs/tags/v1.0.0  # Delete remotely

# Create annotated tag with message
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

---

**Happy Releasing! 🎉**
