# The file

The top level accepts four lists:

```json
{
  "cams": [],
  "videoMixers": [],
  "interfaces": [],
  "scenes": []
}
```

`scenes` holds stored camera framings and is written by the scene wizard. See [scenes](../Scenes.md#in-the-file).

## What every entry carries

| Key | Means |
| --- | --- |
| `type` | which builder claims the entry, and therefore which other keys it accepts; matched exactly, including case, anywhere in the entry |
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
cams[0]: viscaoverip[7].port: The field port must be between 1 and 65535.
```

The editor checks the same value rules on each edit and shows the message under the field.

Unknown properties are errors.

The editor lists an invalid entry with its error and without fields. It is saved unchanged and blocks
saving only when its `instance` repeats another entry's in the same list.

Loaded cameras and mixers appear in the window even if no interface references them.
