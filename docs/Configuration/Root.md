# The file

The top level accepts three lists:

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

`instance` is unique within its list. Cameras and mixers may share a number.
Interfaces reference mixers through `videoMixer` and cameras through `cameraMap`.

```json
{
  "cams":        [ { "type": "viscaoverip", "instance": 7, "ip": "10.0.0.4" } ],
  "videoMixers": [ { "type": "passthrough/default", "instance": 1 } ],
  "interfaces":  [ { "type": "keyboard", "instance": 1, "videoMixer": 1,
                     "cameraMap": { "3": 7 } } ]
}
```

Mixer input 3 maps to camera 7.

## When an entry is wrong

Invalid entries are logged and skipped independently. Errors name the entry and property:

```
viscaoverip[7].port: expected a number in ]0 .. 65535], found 70000
```

Unknown properties are errors.

Loaded cameras and mixers appear in the window even if no interface references them.
