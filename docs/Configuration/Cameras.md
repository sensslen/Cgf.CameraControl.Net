# Cameras

Entries in `cams`. An interface reaches one through its `cameraMap`, which points a mixer input at a
camera's `instance`.

| `type` | Talks to | Needs |
| --- | --- | --- |
| `viscaoverip` | a VISCA camera over UDP | `ip` |
| `Websocket.PtzLanc` | the websocket LANC controller firmware | `ip` |
| `Signalr.PtzLanc` | the SignalR LANC controller service | `connectionUrl`, `connectionPort` |

## Shared settings

| Key | Default | Means |
| --- | --- | --- |
| `panTiltInvert` | `false` | reverse pan and tilt for mounting or controller wiring |
| `panTiltSpeedPercent` | `100` | whole percentage from `1` to `100`; scales pan/tilt speed |

Neither affects zoom or focus. Speed scaling preserves the response curve; `50` halves requested
speed, subject to hardware steps. VISCA cannot move below speed 1.
Edit the percentage through the linked slider and text box, not the operating controls.

## `viscaoverip`

```json
{
  "type": "viscaoverip",
  "instance": 1,
  "ip": "192.168.1.87",
  "port": 1259,
  "panTiltInvert": false,
  "tallyMode": "avonic"
}
```

| Key | Default | Means |
| --- | --- | --- |
| `ip` | required | the camera's address |
| `port` | `52381` | the UDP port it listens on |
| `tallyMode` | `none` | which vendor's tally payload the camera understands |

Packets use raw VISCA without Sony VISCA-over-IP headers. Header-only ports do not work; use a
raw port if available, often `1259`.

A version inquiry runs every three seconds. Connection requires replies, not just an open UDP
socket; ten seconds without a reply marks the camera disconnected.

### `tallyMode`

VISCA tally is vendor-specific. A wrong payload can execute an unrelated command.

| Mode | Lamps | Bytes |
| --- | --- | --- |
| `none` | none | nothing is sent |
| [`avonic`](https://support.avonic.com/support/solutions/articles/80001153827-visca) | red and green | `81 01 7E 01 0A 00 0p 0q FF` |
| [`ptzoptics`](https://f.hubspotusercontent20.net/hubfs/418770/PTZOptics%20Documentation/Misc/PTZOptics%20VISCA%20over%20IP%20Commands.pdf) | one | `81 0A 02 02 0p FF` |

`avonic` supports Avonic and CineTreak. `ptzoptics` lights only on program, not preview.
Sony tally is unsupported. See [camera compatibility](../CameraCompatibility.md) for tested cameras.

## `Websocket.PtzLanc`

```json
{ "type": "Websocket.PtzLanc", "instance": 2, "ip": "192.168.1.50", "showTallyLight": true }
```

| Key | Default | Means |
| --- | --- | --- |
| `ip` | required | the controller's address |
| `showTallyLight` | `true` | drive the controller's tally lamp |

## `Signalr.PtzLanc`

```json
{ "type": "Signalr.PtzLanc", "instance": 3, "connectionUrl": "http://10.0.0.9", "connectionPort": "COM6" }
```

| Key | Default | Means |
| --- | --- | --- |
| `connectionUrl` | required | the service's address |
| `connectionPort` | required | the serial port the service drives the LANC adapter on, such as `COM6` |

Shutdown and reload send zero movement, retry rejected stops for up to one second, and wait for
acknowledgement before closing. Missing acknowledgement logs an error; an unreachable camera's
stop cannot be guaranteed.
