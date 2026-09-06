# Cameras

Entries in `cams`. An interface reaches one through its `cameraMap`, which points a mixer input at a
camera's `instance`.

| `type` | Talks to | Needs |
| --- | --- | --- |
| `viscaoverip` | a VISCA camera over UDP | `ip` |
| `Websocket.PtzLanc` | the websocket LANC controller firmware | `ip` |
| `Signalr.PtzLanc` | the SignalR LANC controller service | `connectionUrl`, `connectionPort` |

All three take `panTiltInvert`, which flips pan and tilt for a camera mounted upside down. It never
touches zoom or focus.

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
| `panTiltInvert` | `false` | flip pan and tilt |
| `tallyMode` | `none` | which vendor's tally payload the camera understands |

Packets go out as raw VISCA with no Sony VISCA-over-IP header, so a camera that insists on the header
will not answer. Such a camera usually also listens on a raw port, often 1259, which is what to set
`port` to.

Presence is asked rather than assumed. UDP opens as readily at an empty address as at a camera, so
the version inquiry goes out every three seconds and the camera counts as connected while it is
still answering something. A camera that stops answering for ten seconds goes dark in the window.

### `tallyMode`

Nothing in the VISCA specification covers a tally lamp, so the payloads are vendor specific and only
interchangeable by accident. A camera sent the wrong vendor's payload does something unrelated
rather than nothing.

| Mode | Lamps | Bytes |
| --- | --- | --- |
| `none` | none | nothing is sent |
| `avonic` | red and green | `81 01 7E 01 0A 00 0p 0q FF` |
| `ptzoptics` | one | `81 0A 02 02 0p FF` |

`avonic` reaches Avonic and CineTreak cameras, which take the same three packets. `ptzoptics` has a
lamp rather than a colour, so preview leaves it dark. Sony's own cameras are not covered: they spend
a command per lamp and put one out that is not told to stay lit, which a single payload cannot
express. [Camera compatibility](../CameraCompatibility.md) records what has been on hardware.

## `Websocket.PtzLanc`

```json
{ "type": "Websocket.PtzLanc", "instance": 2, "ip": "192.168.1.50", "showTallyLight": true }
```

| Key | Default | Means |
| --- | --- | --- |
| `ip` | required | the controller's address |
| `panTiltInvert` | `false` | flip pan and tilt |
| `showTallyLight` | `true` | drive the controller's tally lamp |

## `Signalr.PtzLanc`

```json
{ "type": "Signalr.PtzLanc", "instance": 3, "connectionUrl": "http://10.0.0.9", "connectionPort": "5000" }
```

| Key | Default | Means |
| --- | --- | --- |
| `connectionUrl` | required | the service's address |
| `connectionPort` | required | its port, as a string |
| `panTiltInvert` | `false` | flip pan and tilt |
