# Complete Guide: Multiple Video Maps in DGScope

## Overview

DGScope supports:
- **Unlimited video maps** loaded simultaneously
- **32 DCB toggle buttons** (6 on main menu, 26 in MAPS submenu)
- **2 brightness categories** (A and B) with independent controls
- **Multiple GeoJSON files** imported and combined

---

## Quick Reference

| Feature | Access Method | Location |
|---------|---------------|----------|
| **Map Selector** | `Ctrl+F2` | Opens checklist of all maps |
| **DCB Quick Buttons** | Click on scope | MAP 1-6 on main DCB menu |
| **DCB Map Submenu** | Click "MAPS" on DCB | MAP 7-32 buttons |
| **Clear All Maps** | MAPS > CLR ALL | Hides all maps |
| **Brightness Category A** | BRITE > MPA | Adjust 5-100 |
| **Brightness Category B** | BRITE > MPB | Adjust 5-100 |

---

## Step-by-Step Setup

### Option A: Using the UI (Easiest)

#### 1. Access the Video Map Editor
- **If in Properties Window**: Find "Video Maps" property → Click `[...]` button
- **If editing XML directly**: Set `VideoMapFilename` to your target file, then open properties

#### 2. Import Your GeoJSON Files
1. In VideoMapForm, click **File > Import > From GeoJSON**
2. Select your first `.geojson` file
3. Click **Open**
4. Repeat for each additional GeoJSON file

**Important**: Each import *adds* maps to the collection. They don't replace existing maps.

#### 3. Configure Each Map
Select a map in the grid, then edit in the Property Grid (right side):

| Property | Purpose | Example |
|----------|---------|---------|
| **Number** | Unique map ID (auto-assigned) | `1`, `2`, `3`, etc. |
| **Name** | Full descriptive name | "ILM Runway Centerlines" |
| **Mnemonic** | Short code for DCB button | "RWY" (max 4-6 chars) |
| **Category** | Brightness group: `A` or `B` | `A` for primary, `B` for secondary |
| **Visible** | Initial visibility | `true` or `false` |

**Category Assignment Strategy**:
- **Category A**: Primary maps (runways, airways, boundaries)
- **Category B**: Secondary/reference maps (terrain, extended centerlines, NAVAIDs)
- Use separate categories to adjust brightness independently during operations

#### 4. Save Your Configuration
- Click **File > Save** (or `Ctrl+S`)
- This saves ALL maps to a single GeoJSON file with metadata
- The file will be referenced in your XML as `<VideoMapFilename>`

#### 5. Configure DCB Button Assignments

Edit your `ILM_default.xml` file:

```xml
<TCP>
  <DCBMapList>
    <int>1</int>   <!-- DCB Button 1 → Map #1 -->
    <int>2</int>   <!-- DCB Button 2 → Map #2 -->
    <int>3</int>   <!-- DCB Button 3 → Map #3 -->
    <int>4</int>   <!-- DCB Button 4 → Map #4 -->
    <int>5</int>   <!-- DCB Button 5 → Map #5 -->
    <int>6</int>   <!-- DCB Button 6 → Map #6 -->
    <!-- Buttons 7-32 go in MAPS submenu -->
    <int>7</int>
    <int>8</int>
    <!-- ... continue up to 32 buttons ... -->
    <int>0</int>   <!-- 0 = button not assigned -->
  </DCBMapList>
</TCP>
```

**Button Layout**:
- Buttons 1-6: Main DCB menu (quick access)
- Buttons 7-32: MAPS submenu

---

### Option B: Manual GeoJSON Creation

If you're creating GeoJSON files from scratch or editing existing ones:

#### GeoJSON Format for Multiple Maps

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "GeometryCollection",
        "geometries": [
          {
            "type": "LineString",
            "coordinates": [
              [longitude1, latitude1],
              [longitude2, latitude2]
            ]
          }
        ]
      },
      "properties": {
        "name": "Map Display Name",
        "number": 1,
        "mnemonic": "CODE",
        "category": 0
      }
    }
  ]
}
```

**Key Fields**:
- `number`: Unique integer (1, 2, 3, ...)
- `mnemonic`: Short code for DCB button display
- `category`: `0` for Category A, `1` for Category B
- `name`: Full descriptive name

**Multiple Maps in One File**:
Add multiple Feature objects to the `features` array. Each Feature = one map.

---

## Brightness Control

### Setting Up Categories

In `ILM_default.xml`:

```xml
<CurrentPrefSet>
  <Brightness>
    <MapA>100</MapA>   <!-- Category A: 5-100 -->
    <MapB>80</MapB>    <!-- Category B: 5-100 -->
  </Brightness>
</CurrentPrefSet>
```

### In-Scope Adjustment

1. Click **BRITE** button on DCB
2. Click **MPA** to adjust Category A brightness
   - Use mouse wheel or click +/- arrows
3. Click **MPB** to adjust Category B brightness
4. Changes are immediate and saved to preferences

### Color Configuration

```xml
<!-- Category A color (ARGB format) -->
<VideoMapLineColor>-12566464</VideoMapLineColor>

<!-- Category B color (ARGB format) -->
<VideoMapLineColorB>-14671840</VideoMapLineColorB>
```

---

## Runtime Operations

### Toggling Individual Maps

**Method 1: DCB Buttons**
- Click MAP 1-6 on main menu
- Click MAPS → MAP 7-32 for submenu maps
- Button lights up when map is visible

**Method 2: Map Selector (All Maps)**
- Press `Ctrl+F2`
- Check/uncheck maps in the list
- Works for ALL maps, even those without DCB buttons

**Method 3: Clear All**
- Click MAPS → CLR ALL
- Hides all maps instantly

### Which Maps Are Active?

The `DisplayedMaps` array tracks active maps:

```xml
<CurrentPrefSet>
  <DisplayedMaps>
    <int>1</int>   <!-- Map #1 is visible -->
    <int>3</int>   <!-- Map #3 is visible -->
    <!-- Other maps are hidden -->
  </DisplayedMaps>
</CurrentPrefSet>
```

This array updates automatically as you toggle maps.

---

## Common Workflows

### Scenario 1: Import Multiple GeoJSON Files

**Problem**: You have separate GeoJSON files for runways, airways, and boundaries.

**Solution**:
1. Open VideoMapForm (properties → Video Maps → `[...]`)
2. Import first file: **File > Import > From GeoJSON** → select runways.geojson
3. Import second file: **File > Import > From GeoJSON** → select airways.geojson
4. Import third file: **File > Import > From GeoJSON** → select boundaries.geojson
5. Assign categories and mnemonics to each map
6. Save as single combined file: **File > Save As** → `ILM_AllMaps.geojson`

### Scenario 2: Assign Maps to Brightness Groups

**Problem**: Want runways bright, terrain dimmer.

**Solution**:
1. In VideoMapForm, select runway maps → set **Category** = `A`
2. Select terrain maps → set **Category** = `B`
3. Save configuration
4. In scope, use **BRITE > MPA** for runways, **BRITE > MPB** for terrain

### Scenario 3: Access Maps Beyond 32 Buttons

**Problem**: You have 50 maps but only 32 DCB buttons.

**Solution**:
1. Assign your most-used 32 maps to DCB buttons (via DCBMapList in XML)
2. Access remaining maps via `Ctrl+F2` map selector
3. Toggle them on/off in the checklist

---

## Troubleshooting

### Map Doesn't Show on DCB Button

**Check**:
1. Is the map number in `TCP.DCBMapList`?
2. Does the button index match the array position? (Button 1 = index 0, Button 2 = index 1, etc.)
3. Is the map number unique? (Duplicate numbers auto-increment)

### Map Imported But Not Visible

**Check**:
1. Is the map in `CurrentPrefSet.DisplayedMaps`?
2. Press `Ctrl+F2` → check if map is in the list but unchecked
3. Brightness set to 0 or very low? Check MPA/MPB values

### Wrong Brightness Group

**Fix**:
1. Open VideoMapForm
2. Select the map
3. Change **Category** property to `A` or `B`
4. Save

### Can't Edit Properties

The VideoMapForm might not be open. The `VideoMapCollectionEditor` provides the UI access.

---

## File Structure Reference

### ILM_default.xml Key Sections

```xml
<RadarWindow>
  <!-- Map file path (single consolidated file) -->
  <VideoMapFilename>path\to\maps.geojson</VideoMapFilename>

  <!-- Colors -->
  <VideoMapLineColor>-12566464</VideoMapLineColor>
  <VideoMapLineColorB>-14671840</VideoMapLineColorB>

  <!-- Current displayed maps -->
  <CurrentPrefSet>
    <DisplayedMaps>
      <int>1</int>
      <int>2</int>
    </DisplayedMaps>
    <Brightness>
      <MapA>100</MapA>
      <MapB>80</MapB>
    </Brightness>
  </CurrentPrefSet>

  <!-- DCB button assignments -->
  <TCP>
    <DCBMapList>
      <int>1</int>  <!-- Button 1 → Map #1 -->
      <int>2</int>  <!-- Button 2 → Map #2 -->
      <!-- ... up to 36 entries ... -->
    </DCBMapList>
  </TCP>
</RadarWindow>
```

---

## Advanced Tips

### Map Numbering Best Practices
- Use sequential numbers: 1, 2, 3, 4...
- Reserve gaps for future maps (e.g., 1-10 for runways, 11-20 for airways)
- Document your numbering scheme in map names

### Mnemonic Guidelines
- Keep to 4-6 characters max
- Use abbreviations: RWY, BDRY, TERR, NAV, FIX
- Match real-world STARS conventions for familiarity

### Performance Considerations
- No limit on active maps, but many complex maps may impact frame rate
- Consider using Category B for less-critical maps and dimming them
- Simplify geometry where possible (fewer vertices per line)

### Real-World STARS Alignment
This implementation mirrors real STARS/CRC behavior:
- **DCB buttons**: Limited quick-access slots
- **MAPS list** (`Ctrl+F2`): Access all maps beyond DCB capacity
- **Brightness categories**: Independent control like MPA/MPB in real STARS
- **Toggle persistence**: Map states save to preferences

---

## Summary Checklist

- [ ] Import all GeoJSON files via VideoMapForm
- [ ] Assign unique numbers to each map
- [ ] Set meaningful mnemonics (4-6 chars)
- [ ] Assign maps to Category A or B
- [ ] Configure DCBMapList in XML (button assignments)
- [ ] Set initial DisplayedMaps array
- [ ] Configure MapA and MapB brightness levels
- [ ] Test map visibility via DCB buttons
- [ ] Test map selector (`Ctrl+F2`)
- [ ] Adjust brightness in-scope to verify categories work

---

## Code References

- Map rendering: [RadarWindow.cs:5075-5101](scope/RadarWindow.cs#L5075-L5101)
- DCB button handler: [RadarWindow.cs:3645-3659](scope/RadarWindow.cs#L3645-L3659)
- Map selector (Ctrl+F2): [RadarWindow.cs:3111](scope/RadarWindow.cs#L3111)
- GeoJSON import: [MapGeoJSON.cs:82-155](scope/MapGeoJSON.cs#L82-L155)
- Brightness adjustment: [RadarWindow.cs:4046-4085](scope/RadarWindow.cs#L4046-L4085)

---

**Need Help?** Check the example files:
- `EXAMPLE_MultiMap.geojson` - Sample multi-map GeoJSON structure
- `EXAMPLE_MultiMap_Config.xml` - Sample XML configuration

**Real-World Reference**: See [Vice Documentation](https://pharr.org/vice/) for STARS/vSTARS behavior alignment.
