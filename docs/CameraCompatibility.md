# Camera compatibility

Unlisted cameras are untested, not necessarily incompatible. Record only features exercised on
hardware.

## Verified on hardware

| Camera | `type` | `tallyMode` | Exercised |
| --- | --- | --- | --- |
| [CineTreak CT-PT31K](https://cinetreak.com/product/ct-pt31k/) | `viscaoverip` | `avonic` | pan, tilt, zoom, focus, tally |

## Implemented from documentation, no camera seen

Homegrown websocket and SignalR LANC controllers are outside this camera list.
The following VISCA payloads have documentation sources:

| Payload | Documented by | Notes |
| --- | --- | --- |
| `tallyMode: avonic` | [Avonic CM7x support article](https://support.avonic.com/support/solutions/articles/80001153827-visca) | the same three packets the CT-PT31K answers to |
| `tallyMode: ptzoptics` | [PTZOptics VISCA over IP commands](https://f.hubspotusercontent20.net/hubfs/418770/PTZOptics%20Documentation/Misc/PTZOptics%20VISCA%20over%20IP%20Commands.pdf) | one lamp, so preview leaves it dark |

Sony's own cameras have no `tallyMode`. Per the
[Sony VISCA command list v4](https://pro.sony/s3/2022/09/03065933/VISCA_Command_List_v4.pdf), Sony
spends a command per lamp, `8x 01 7E 01 0A 00 0p FF` for red and `8x 01 7E 04 1A 00 0p FF` for
green. Lamps expire after 15 seconds without refresh. Periodic refresh is not implemented and
Sony tally has not been verified on hardware.
