# Configuration

One JSON file describes a desk. It has three lists, and every entry in them carries a `type` that
picks what is built and an `instance` that everything else refers to it by.

```json
{
  "cams": [ ... ],
  "videoMixers": [ ... ],
  "interfaces": [ ... ]
}
```

- **[The file](Root.md)** — the three lists, `type` and `instance`, and what happens when an entry is wrong.
- **[Cameras](Cameras.md)** — `cams`, one entry per camera, and the tally payloads.
- **[Video mixers](VideoMixers.md)** — `videoMixers`, the ATEM and the passthrough.
- **[Interfaces](Interfaces.md)** — `interfaces`, the gamepad and the keyboard, their functions and every key name.

A camera that is known to work is listed in [camera compatibility](../CameraCompatibility.md).

An entry that will not load is reported against that entry alone, so the rest of the desk still
comes up. The message names the entry and the property, which is the fastest way back to the line
that is wrong.
