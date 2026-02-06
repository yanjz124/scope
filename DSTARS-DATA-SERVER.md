# Dstars Data Server Documentation

## Overview

The Dstars data server (e.g., `https://dstars.graiani.com/dstars/`) is a streaming server that provides real-time aircraft tracking data to DGScope. This document explains how the server works, the data structure, and how to set up your own server.

## What is Dstars?

Dstars is a **data streaming server** that aggregates aircraft tracking information from multiple sources and streams it to connected DGScope clients. The data includes:

- **ADSB-sourced data**: Position, altitude, speed, track, transponder codes
- **Flight plan data**: Callsign, aircraft type, origin/destination, route information
- **ATC-specific data**: Controller ownership, handoffs, scratchpad entries, leader line directions

## Data Architecture

### Update Types

The server streams three types of updates:

1. **TrackUpdate (Type 0)**: Real-time position and flight data from ADSB
2. **FlightPlanUpdate (Type 1)**: Flight plan and ATC coordination data
3. **DeletionUpdate (Type 2)**: Notification when a track or flight plan is removed
4. **WeatherRadarUpdate (Type 3)**: Weather radar data

### Data Structure

All updates share a common structure with a unique GUID identifier and timestamp:

```json
{
  "Guid": "12345678-1234-1234-1234-123456789abc",
  "TimeStamp": "2024-01-01T12:00:00Z",
  "UpdateType": 0
}
```

#### TrackUpdate Fields (UpdateType = 0)

Real-time aircraft position and flight parameters:

```json
{
  "Guid": "...",
  "TimeStamp": "...",
  "UpdateType": 0,
  "ModeSCode": 12345678,
  "Squawk": "1200",
  "Callsign": "AAL123",
  "Location": {
    "Latitude": 40.7128,
    "Longitude": -74.0060
  },
  "Altitude": {
    "Value": 35000,
    "AltitudeType": 0
  },
  "GroundSpeed": 450,
  "GroundTrack": 270,
  "VerticalRate": 0,
  "Ident": false,
  "IsOnGround": false
}
```

**Field Descriptions:**
- `ModeSCode`: Mode S transponder code (24-bit ICAO address)
- `Squawk`: 4-digit transponder squawk code
- `Callsign`: Aircraft callsign from ADSB
- `Location`: GPS coordinates (latitude/longitude)
- `Altitude`: Flight altitude with type indicator
- `GroundSpeed`: Speed in knots
- `GroundTrack`: Direction of travel in degrees (0-360)
- `VerticalRate`: Rate of climb/descent in feet per minute
- `Ident`: Whether IDENT button is pressed
- `IsOnGround`: Ground/airborne status

#### FlightPlanUpdate Fields (UpdateType = 1)

Flight plan and ATC coordination information:

```json
{
  "Guid": "...",
  "TimeStamp": "...",
  "UpdateType": 1,
  "Callsign": "AAL123",
  "AircraftType": "B738",
  "WakeCategory": "H",
  "FlightRules": "IFR",
  "Origin": "KJFK",
  "Destination": "KLAX",
  "EntryFix": "CAMRN",
  "ExitFix": "ROBUC",
  "Route": "...",
  "RequestedAltitude": 35000,
  "Scratchpad1": "",
  "Scratchpad2": "",
  "Owner": "N90",
  "PendingHandoff": "ZNY",
  "EquipmentSuffix": "G",
  "LDRDirection": 2,
  "AssignedSquawk": "1234",
  "AssociatedTrackGuid": "12345678-1234-1234-1234-123456789abc",
  "FacilityID": "N90"
}
```

**Field Descriptions:**
- `Callsign`: Flight plan callsign
- `AircraftType`: ICAO aircraft type code
- `WakeCategory`: Wake turbulence category (L/M/H/J)
- `FlightRules`: IFR/VFR/DVFR/SVFR
- `Origin`: Departure airport ICAO code
- `Destination`: Arrival airport ICAO code
- `EntryFix`: Airspace entry fix
- `ExitFix`: Airspace exit fix
- `Route`: Filed route
- `RequestedAltitude`: Requested cruise altitude
- `Scratchpad1`: Primary ATC scratchpad
- `Scratchpad2`: Secondary ATC scratchpad
- `Owner`: Controlling position/facility
- `PendingHandoff`: Position aircraft is being handed off to
- `EquipmentSuffix`: Equipment code suffix
- `LDRDirection`: Leader line direction (1-9, numeric keypad layout)
- `AssignedSquawk`: Controller-assigned squawk code
- `AssociatedTrackGuid`: GUID linking to the associated TrackUpdate
- `FacilityID`: Facility identifier

## API Endpoints

The Dstars server provides the following endpoints:

### HTTP/HTTPS Streaming (JSON)

```
GET https://dstars.graiani.com/dstars/{FACILITY}/updates
```

- Streams newline-delimited JSON updates
- Each line contains one complete update object
- Connection stays open for continuous streaming
- Example facilities: `N90`, `ILM`, `ZDC`, etc.

### WebSocket Streaming (JSON)

```
ws://dstars.graiani.com/dstars/{FACILITY}/updates
wss://dstars.graiani.com/dstars/{FACILITY}/updates
```

- Real-time WebSocket connection
- Each message contains one complete update object
- More efficient than HTTP streaming

### Protocol Buffers Streaming

```
GET https://dstars.graiani.com/dstars/{FACILITY}/proto
ws://dstars.graiani.com/dstars/{FACILITY}/proto
```

- Binary Protocol Buffers format for efficiency
- Requires Protocol Buffers deserialization
- Smaller payload size, faster transmission

### Update Endpoint (Client to Server)

```
POST https://dstars.graiani.com/dstars/{FACILITY}/update
Content-Type: application/json
```

- Allows clients to send updates back to the server
- Used for controller inputs (scratchpads, handoffs, etc.)
- Requires authentication (username/password)

## Configuring DGScope to Use a Data Server

Edit your DGScope configuration XML file (e.g., `ILM_default.xml`) and add a receiver:

```xml
<Receivers>
  <Receiver AssemblyQualifiedName="DGScope.Receivers.ScopeServer.ScopeServerClient, DGScope.Receivers.ScopeServer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null">
    <ScopeServerClient>
      <Name>Your Facility Name</Name>
      <Enabled>true</Enabled>
      <Location>
        <Latitude>34.2719616</Latitude>
        <Longitude>-77.9024448</Longitude>
      </Location>
      <Range>250</Range>
      <CreateNewAircraft>true</CreateNewAircraft>
      <Url>https://dstars.graiani.com/dstars/ILM/updates</Url>
      <!-- Optional authentication -->
      <Username>your_username</Username>
      <Password>your_password</Password>
    </ScopeServerClient>
  </Receiver>
</Receivers>
```

## Setting Up Your Own Dstars Server

### Data Sources

To create your own Dstars-compatible server, you need:

1. **ADSB Data Source**
   - **Readsb/dump1090**: Local ADSB receiver (for local coverage)
   - **ADSBExchange**: Global ADSB data via API
   - **FlightAware**: ADSB data with additional enrichment
   - **OpenSky Network**: Research-oriented ADSB data

2. **Flight Plan Data Source**
   - **FAA SWIM**: Official FAA flight data (requires authorization)
   - **VATSIM/IVAO**: For virtual ATC simulation
   - **FlightAware**: Provides route and flight plan information
   - **Manual entry**: Controller-entered flight strips

3. **ATC Coordination Data**
   - Usually built into the server for controller coordination
   - Stored in a database with the server
   - Synchronized across all connected clients

### Server Implementation Requirements

Your server needs to:

1. **Aggregate Data**: Combine ADSB position updates with flight plan data
2. **Track Association**: Link ADSB tracks (by Mode S code) with flight plans
3. **Streaming**: Maintain long-lived connections to push updates to clients
4. **Update Management**: Handle both client subscriptions and bidirectional updates
5. **Data Retention**: Keep track history for continuity

### Example Server Architecture

```
┌─────────────────┐
│  ADSB Source    │ (dump1090/readsb/ADSBExchange)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Data Processor │ (correlate tracks with flight plans)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Dstars Server  │ (HTTP/WS streaming server)
│  - Track DB     │
│  - Flight Plans │
│  - Updates API  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  DGScope Client │
└─────────────────┘
```

### Minimal Server Implementation

A basic server needs:

```csharp
// 1. Receive ADSB data and create TrackUpdates
var trackUpdate = new TrackUpdate {
    Guid = trackGuid,
    TimeStamp = DateTime.UtcNow,
    ModeSCode = modeS,
    Location = new GeoPoint(lat, lon),
    Altitude = altitude,
    GroundSpeed = speed,
    GroundTrack = track,
    // ... other fields
};

// 2. Serialize to JSON
var json = JsonConvert.SerializeObject(trackUpdate, 
    new JsonSerializerSettings { 
        NullValueHandling = NullValueHandling.Ignore 
    });

// 3. Stream to all connected clients
await SendToAllClients(json + "\n");
```

### Using Your ADSB Setup

If you have **Readsb + Tar1090** already running:

1. Connect to your Readsb JSON output (typically port 30047)
2. Parse the JSON aircraft data
3. Create TrackUpdate objects for each aircraft
4. Stream them to DGScope

Example Readsb data format:
```json
{
  "hex": "abc123",
  "flight": "AAL123",
  "lat": 40.7128,
  "lon": -74.0060,
  "altitude": 35000,
  "ground_speed": 450,
  "track": 270
}
```

### Why Standard ADSB Doesn't Track in DGScope

The issue you experienced is that **ADSB data alone is insufficient** for DGScope's full functionality:

- **ADSB receivers** (like Readsb/Tar1090) only provide position updates
- **DGScope** expects integrated flight plan data for features like:
  - Data blocks with callsigns, destinations
  - Controller ownership and handoffs
  - Scratchpad entries
  - Entry/exit fixes
  
The Dstars server bridges this gap by **combining ADSB with flight plan data**.

## Troubleshooting

### Server Goes Down

The Dstars server at `https://dstars.graiani.com` is a community-operated service and may experience downtime. Options:

1. **Set up your own server** (see above)
2. **Use multiple data sources** in your configuration
3. **Add fallback receivers** in your XML configuration

### No Targets Visible

Check:
- Server URL is correct in configuration
- Facility code matches your area (e.g., ILM, N90, ZDC)
- Range setting covers your area of interest
- Altitude filters aren't excluding all aircraft

### Can't Track Aircraft

If you see targets but can't interact with them:
- Ensure `CreateNewAircraft` is set to `true`
- Check that you're using the ScopeServer receiver (not SBS or other basic ADSB)
- Verify the server provides FlightPlanUpdates, not just TrackUpdates

## Data Flow Summary

```
ADSB Receiver → Mode S data → Dstars Server → TrackUpdate (JSON/Protobuf)
                                    ↓
Flight Plan System → Route/Fix data → FlightPlanUpdate (JSON/Protobuf)
                                    ↓
                              DGScope Client
                                    ↓
Controller Actions → Scratchpad/Handoff → Server (update endpoint)
```

## Additional Resources

- **Source Code**: See `DGScope.Receivers.ScopeServer/ScopeServerClient.cs` for client implementation
- **Data Classes**: 
  - `TrackUpdate.cs` - Position update structure
  - `FlightPlanUpdate.cs` - Flight plan structure
  - `Update.cs` - Base update class
- **Example Config**: `ILM_default.xml` - Sample receiver configuration

## Community Servers

Known Dstars servers (community-operated):
- `https://dstars.graiani.com/dstars/{FACILITY}/updates`

Replace `{FACILITY}` with codes like:
- `N90` - New York TRACON
- `ILM` - Wilmington ATCT
- `ZDC` - Washington ARTCC
- (Other facilities as available)

## License

This documentation describes the data protocol used by DGScope. Implementation of servers following this protocol is encouraged for both real-world ADSB tracking and virtual ATC simulation purposes.
