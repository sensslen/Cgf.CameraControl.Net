# Camera compatibility

Which cameras have been driven by this application, and how far the driving went. A camera absent
from this page is not known to be broken, only untested. Add a row when you have one in front of
you, and say what you actually exercised rather than what you expect to work.

## Verified on hardware

| Camera | `type` | `tallyMode` | Exercised |
| --- | --- | --- | --- |
| [CineTreak CT-PT31K](https://cinetreak.com/product/ct-pt31k/) | `viscaoverip` | `avonic` | pan, tilt, zoom, focus, tally |

## Implemented from documentation, no camera seen

The `Websocket.PtzLanc` and `Signalr.PtzLanc` types drive homegrown LANC hardware rather than a
camera off a shelf, so they are out of scope here. What is left is the VISCA tally payloads.

| Payload | Documented by | Notes |
| --- | --- | --- |
| `tallyMode: avonic` | [Avonic CM7x support article](https://support.avonic.com/support/solutions/articles/80001153827-visca) | the same three packets the CT-PT31K answers to |
| `tallyMode: ptzoptics` | [PTZOptics VISCA over IP commands](https://f.hubspotusercontent20.net/hubfs/418770/PTZOptics%20Documentation/Misc/PTZOptics%20VISCA%20over%20IP%20Commands.pdf) | one lamp, so preview leaves it dark |

Sony's own cameras have no `tallyMode`. Per the
[Sony VISCA command list v4](https://pro.sony/s3/2022/09/03065933/VISCA_Command_List_v4.pdf), Sony
spends a command per lamp, `8x 01 7E 01 0A 00 0p FF` for red and `8x 01 7E 04 1A 00 0p FF` for
green, and puts out a lamp that is not told to stay lit every 15 seconds. The re-send that would keep
it lit was not built without a camera to check it against.
