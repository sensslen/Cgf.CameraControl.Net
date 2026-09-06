# The file

A desk is one JSON file with three lists. Nothing else lives at the top level.

```json
{
  "cams": [],
  "videoMixers": [],
  "interfaces": []
}
```

## What every entry carries

| Key | Means |
| --- | --- |
| `type` | which builder claims the entry, and therefore which other keys it accepts |
| `instance` | a positive number the rest of the file refers to this entry by |

`instance` is unique within its own list, so a camera and a mixer may both be instance 1. An
interface names the mixer it drives by that number, and the cameras behind that mixer's inputs the
same way.

```json
{
  "cams":        [ { "type": "viscaoverip", "instance": 7, "ip": "10.0.0.4" } ],
  "videoMixers": [ { "type": "passthrough/default", "instance": 1 } ],
  "interfaces":  [ { "type": "keyboard", "instance": 1, "videoMixer": 1,
                     "cameraMap": { "3": 7 } } ]
}
```

That file has one camera at instance 7, reached by pressing the mixer's input 3.

## When an entry is wrong

Each entry is read on its own. One that fails is reported and skipped, and everything else still
loads, so a typo in one camera does not take the desk down with it. The message names the entry and
the property inside it:

```
viscaoverip[7].port: expected a number in ]0 .. 65535], found 70000
```

A property no schema claims is an error rather than something quietly dropped, because a binding
that will never fire looks exactly like one that is broken.

A camera or mixer that loads but that no interface reaches is still shown in the window, under
cameras and video mixers, so one that reaches nothing is visible rather than absent.
