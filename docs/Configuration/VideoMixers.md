# Video mixers

Entries in `videoMixers`. An interface names one by `instance` in its `videoMixer`, and every input
number in that interface's `cameraMap` and `keys.input` is one of this mixer's inputs.

| `type` | Talks to | Needs |
| --- | --- | --- |
| `blackmagicdesign/atem` | an ATEM switcher over the network | `ip`, `mixEffectBlock` |
| `passthrough/default` | nothing | — |

## Tally signals

Both accept `tallyMode`, independently of the camera's tally protocol.

| Editor option | `tallyMode` | This mixer's contribution |
| --- | --- | --- |
| Program and preview | `preview-program` | preview and program, with program taking priority; the default |
| Program only | `program` | program only; off for preview cameras |
| Disabled | `none` | off for all mapped cameras |

Program-only and disabled tally initialize to off. Disabled tally sends off updates, not silence.
Neither changes preview selection or steering.

Shared cameras combine contributions after each mixer's mode is applied:
**program > preview > off**, independent of update order. A camera on program in one mixer and
preview in another shows program, then preview when the first mixer selects another camera.
It turns off when all contributions are off. Removing an interface withdraws only its contribution,
including when interfaces share a mixer.

Set **Tally signals** in the mixer editor. The camera also needs a supported
[tally protocol](Cameras.md#tallymode); no camera protocol means no tally commands.

Invalid values leave the editor selection empty and block saving. Valid values are case-insensitive
and unchanged on opening.

![Mixer editor configured for program-only tally](../images/mixer-tally.png)

## `blackmagicdesign/atem`

```json
{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "192.168.1.240", "mixEffectBlock": 0 }
```

| Key | Means |
| --- | --- |
| `ip` | the switcher's address |
| `mixEffectBlock` | which M/E to drive, counted from zero |

Preview, program, cut and auto use switcher state; tally follows its reports, not button presses.

Mixers and interfaces appear while offline. The **Mixers** drawer shows each configured M/E:
yellow is disconnected or initializing, green is connected. M/Es at one address share a connection.
Background connection attempts reconnect automatically and stop on configuration unload.
The **Atem** log records attempts, failures and connection changes.

## `passthrough/default`

```json
{ "type": "passthrough/default", "instance": 1 }
```

Stores and reports selections without a switcher. Interfaces, camera steering and tally work with
network cameras alone; samples use it to avoid requiring ATEM hardware.
