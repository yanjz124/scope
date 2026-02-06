# Developer Guide - Dstars Server Implementation

## For Developers: Implementing a Dstars-Compatible Server

This guide provides technical details for developers who want to create their own Dstars-compatible data server.

## Server Requirements

### Core Functionality

Your server must:

1. Accept WebSocket or HTTP streaming connections
2. Send newline-delimited JSON updates
3. Support the Update data structures (Track, FlightPlan, Deletion, WeatherRadar)
4. Maintain persistent GUID identifiers for each track and flight plan
5. Send timestamps in ISO 8601 format
6. Optionally support bidirectional updates via POST endpoint

### Supported Transport Protocols

- **HTTP/HTTPS**: Long-lived streaming connection with newline-delimited JSON
- **WebSocket/WSS**: Real-time binary or text messages
- **Protocol Buffers** (optional): Binary serialization for efficiency

## Data Class Definitions

### C# Class Reference

The client expects these exact property names and types:

#### TrackUpdate

```csharp
public class TrackUpdate : Update
{
    public Altitude Altitude { get; set; }
    public int? GroundSpeed { get; set; }
    public int? GroundTrack { get; set; }
    public bool? Ident { get; set; }
    public bool? IsOnGround { get; set; }
    public string Squawk { get; set; }
    public GeoPoint Location { get; set; }
    public string Callsign { get; set; }
    public int? VerticalRate { get; set; }
    public int? ModeSCode { get; set; }
    public override UpdateType UpdateType => UpdateType.Track; // = 0
}
```

#### FlightPlanUpdate

```csharp
public class FlightPlanUpdate : Update
{
    public string Callsign { get; set; }
    public string AircraftType { get; set; }
    public string WakeCategory { get; set; }
    public string FlightRules { get; set; }
    public string Origin { get; set; }
    public string Destination { get; set; }
    public string EntryFix { get; set; }
    public string ExitFix { get; set; }
    public string Route { get; set; }
    public int? RequestedAltitude { get; set; }
    public string Scratchpad1 { get; set; }
    public string Scratchpad2 { get; set; }
    public string Runway { get; set; }
    public string Owner { get; set; }
    public string PendingHandoff { get; set; }
    public string AssignedSquawk { get; set; }
    public string EquipmentSuffix { get; set; }
    public int? LDRDirection { get; set; }
    public Guid? AssociatedTrackGuid { get; set; }
    public string FacilityID { get; set; }
    public override UpdateType UpdateType => UpdateType.Flightplan; // = 1
}
```

#### Base Update Class

```csharp
public abstract class Update
{
    public Guid Guid { get; set; }
    public DateTime TimeStamp { get; set; }
    public abstract UpdateType UpdateType { get; }
}

public enum UpdateType
{
    Track = 0,
    Flightplan = 1,
    Deletion = 2,
    WeatherRadar = 3
}
```

#### Supporting Types

```csharp
public class Altitude
{
    public int Value { get; set; }        // Altitude in feet
    public int AltitudeType { get; set; }  // 0 = Barometric, 1 = Geometric
}

public class GeoPoint
{
    public float Latitude { get; set; }
    public float Longitude { get; set; }
}

public class DeletionUpdate : Update
{
    public override UpdateType UpdateType => UpdateType.Deletion; // = 2
    // Only Guid and TimeStamp are needed
}
```

### JSON Serialization

Use `NullValueHandling.Ignore` to omit null values:

```csharp
var json = JsonConvert.SerializeObject(update, 
    new JsonSerializerSettings { 
        NullValueHandling = NullValueHandling.Ignore 
    });
```

Example serialized JSON:

```json
{"Guid":"a1b2c3d4-e5f6-7890-abcd-ef1234567890","TimeStamp":"2024-02-06T15:30:00Z","UpdateType":0,"ModeSCode":10551234,"Squawk":"1200","Location":{"Latitude":40.7128,"Longitude":-74.0060},"Altitude":{"Value":3500,"AltitudeType":0},"GroundSpeed":180,"GroundTrack":270}
```

## Implementation Example (Python)

Here's a minimal Python server implementation:

```python
import asyncio
import json
import uuid
from datetime import datetime
from aiohttp import web

class DstarsServer:
    def __init__(self):
        self.clients = set()
        self.tracks = {}
        
    async def handle_updates(self, request):
        """HTTP streaming endpoint"""
        response = web.StreamResponse()
        response.content_type = 'application/json'
        await response.prepare(request)
        
        self.clients.add(response)
        try:
            # Keep connection alive and wait for close
            while True:
                await asyncio.sleep(60)
        finally:
            self.clients.remove(response)
        return response
    
    async def broadcast_update(self, update_dict):
        """Send update to all connected clients"""
        json_str = json.dumps(update_dict) + '\n'
        for client in self.clients:
            try:
                await client.write(json_str.encode())
            except:
                pass
    
    async def process_adsb(self, adsb_data):
        """Process ADSB data and create TrackUpdate"""
        mode_s = adsb_data['hex']
        
        # Get or create track GUID
        if mode_s not in self.tracks:
            self.tracks[mode_s] = str(uuid.uuid4())
        
        track_update = {
            'Guid': self.tracks[mode_s],
            'TimeStamp': datetime.utcnow().isoformat() + 'Z',
            'UpdateType': 0,  # TrackUpdate
            'ModeSCode': int(mode_s, 16),
            'Location': {
                'Latitude': adsb_data['lat'],
                'Longitude': adsb_data['lon']
            },
            'Altitude': {
                'Value': adsb_data['altitude'],
                'AltitudeType': 0
            },
            'GroundSpeed': adsb_data.get('ground_speed'),
            'GroundTrack': adsb_data.get('track'),
            'Squawk': adsb_data.get('squawk'),
            'Callsign': adsb_data.get('flight', '').strip()
        }
        
        # Remove None values
        track_update = {k: v for k, v in track_update.items() if v is not None}
        
        await self.broadcast_update(track_update)

# Run server
app = web.Application()
server = DstarsServer()
app.router.add_get('/dstars/{facility}/updates', server.handle_updates)
web.run_app(app, host='0.0.0.0', port=8080)
```

## Implementation Example (Node.js)

```javascript
const express = require('express');
const { v4: uuidv4 } = require('uuid');

class DstarsServer {
    constructor() {
        this.clients = new Set();
        this.tracks = new Map();
    }
    
    handleUpdates(req, res) {
        // Set headers for streaming
        res.setHeader('Content-Type', 'application/json');
        res.setHeader('Cache-Control', 'no-cache');
        res.setHeader('Connection', 'keep-alive');
        
        // Add client
        this.clients.add(res);
        
        // Remove on disconnect
        req.on('close', () => {
            this.clients.delete(res);
        });
    }
    
    broadcastUpdate(update) {
        const json = JSON.stringify(update) + '\n';
        for (const client of this.clients) {
            try {
                client.write(json);
            } catch (e) {
                this.clients.delete(client);
            }
        }
    }
    
    processADSB(adsbData) {
        const modeS = adsbData.hex;
        
        // Get or create track GUID
        if (!this.tracks.has(modeS)) {
            this.tracks.set(modeS, uuidv4());
        }
        
        const trackUpdate = {
            Guid: this.tracks.get(modeS),
            TimeStamp: new Date().toISOString(),
            UpdateType: 0, // TrackUpdate
            ModeSCode: parseInt(modeS, 16),
            Location: {
                Latitude: adsbData.lat,
                Longitude: adsbData.lon
            },
            Altitude: {
                Value: adsbData.altitude,
                AltitudeType: 0
            }
        };
        
        // Add optional fields if present
        if (adsbData.ground_speed !== undefined) trackUpdate.GroundSpeed = adsbData.ground_speed;
        if (adsbData.track !== undefined) trackUpdate.GroundTrack = adsbData.track;
        if (adsbData.squawk) trackUpdate.Squawk = adsbData.squawk;
        if (adsbData.flight) trackUpdate.Callsign = adsbData.flight.trim();
        
        this.broadcastUpdate(trackUpdate);
    }
}

// Setup Express server
const app = express();
const server = new DstarsServer();

app.get('/dstars/:facility/updates', (req, res) => {
    server.handleUpdates(req, res);
});

app.listen(8080, () => {
    console.log('Dstars server running on port 8080');
});
```

## Connecting to ADSB Sources

### Readsb/dump1090 JSON

```python
import json
import aiohttp

async def fetch_readsb_data():
    async with aiohttp.ClientSession() as session:
        async with session.get('http://localhost:8080/data/aircraft.json') as resp:
            data = await resp.json()
            for aircraft in data.get('aircraft', []):
                if 'lat' in aircraft and 'lon' in aircraft:
                    await server.process_adsb(aircraft)
```

### Readsb Beast Format (Binary)

Connect to port 30005 and parse Beast binary format. Libraries available:
- Python: `pyModeS`
- Node.js: `mode-s-demodulator`

## Flight Plan Integration

### Option 1: Manual Entry
Store flight plans in a database and associate with Mode S codes:

```python
flight_plans = {
    'ABC123': {
        'callsign': 'AAL123',
        'aircraft_type': 'B738',
        'origin': 'KJFK',
        'destination': 'KLAX',
        'mode_s': 'a12345'
    }
}

def get_flight_plan(mode_s):
    for fp in flight_plans.values():
        if fp['mode_s'] == mode_s:
            return fp
    return None
```

### Option 2: FlightAware/ADSBExchange API
Query their APIs for flight details using callsign or Mode S code.

### Option 3: FAA SWIM
For authorized users, connect to FAA System Wide Information Management (SWIM) for official flight plan data.

## WebSocket Support

For WebSocket connections:

```python
from aiohttp import web

async def websocket_handler(request):
    ws = web.WebSocketResponse()
    await ws.prepare(request)
    
    server.clients.add(ws)
    try:
        async for msg in ws:
            pass  # Echo or handle client messages
    finally:
        server.clients.remove(ws)
    
    return ws

app.router.add_get('/dstars/{facility}/updates', websocket_handler)
```

## Protocol Buffers Support

For binary efficiency, implement Protocol Buffers serialization. Proto definition:

```protobuf
message Update {
    required string guid = 1;
    required int64 timestamp = 2;
    // ... add fields
}
```

Use the protobuf libraries for your language to serialize/deserialize.

## Testing Your Server

### Using DGScope

1. Edit your configuration XML
2. Set the URL to your server: `http://localhost:8080/dstars/TEST/updates`
3. Launch DGScope
4. Verify connections in server logs

### Manual Testing with curl

```bash
curl -N http://localhost:8080/dstars/TEST/updates
```

Should receive newline-delimited JSON updates.

## Performance Considerations

### Scalability
- Use async/await for I/O operations
- Implement connection pooling
- Consider Redis for distributed tracking state
- Use load balancers for multiple server instances

### Data Rate
- Typical ADSB update rate: 1-2 updates per second per aircraft
- Bandwidth: ~500 bytes per update
- 1000 aircraft = ~1 MB/s per connected client

### Update Throttling
Don't send updates faster than necessary:
- Position updates: every 1-5 seconds is sufficient
- Flight plan updates: only on changes

## Common Issues

### GUID Management
- **Problem**: Creating new GUIDs for each update
- **Solution**: Maintain a mapping of Mode S code → GUID

### Timestamp Format
- **Problem**: Incorrect timestamp format
- **Solution**: Use ISO 8601 with 'Z' timezone: `2024-02-06T15:30:00Z`

### Null Values
- **Problem**: Sending `null` for all optional fields
- **Solution**: Omit fields or use `NullValueHandling.Ignore`

### Connection Drops
- **Problem**: Clients disconnecting frequently
- **Solution**: Implement keep-alive pings, handle reconnection gracefully

## Security Considerations

### Authentication
Implement basic auth for production:

```python
from aiohttp import web
import base64

async def check_auth(request):
    auth_header = request.headers.get('Authorization')
    if not auth_header:
        raise web.HTTPUnauthorized()
    
    # Parse "Basic base64string"
    encoded = auth_header.split(' ')[1]
    decoded = base64.b64decode(encoded).decode()
    username, password = decoded.split(':')
    
    if not verify_credentials(username, password):
        raise web.HTTPUnauthorized()
```

### HTTPS/WSS
Always use TLS in production:
- Use Let's Encrypt for free certificates
- Configure reverse proxy (nginx, caddy) with TLS

### Rate Limiting
Prevent abuse:
- Limit connections per IP
- Limit update broadcast rate
- Implement token-based access

## Source Code Reference

The official DGScope client implementation:
- **Client**: `DGScope.Receivers.ScopeServer/ScopeServerClient.cs`
- **Data Classes**: `DGScope.Receivers.ScopeServer/TrackUpdate.cs`, `FlightPlanUpdate.cs`
- **Serialization**: `DGScope.Receivers.ScopeServer/Update.cs`

## Community Implementations

Known open-source Dstars-compatible servers:
- (Add community implementations here)

## Getting Help

- Review the client source code for expected behavior
- Test with curl/WebSocket tools
- Check DGScope logs for connection errors
- Ask in GitHub Discussions

## License

This implementation guide is provided to facilitate interoperability with DGScope. Implementations following this protocol are encouraged for both real-world and simulation purposes.
