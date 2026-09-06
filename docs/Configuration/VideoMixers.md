# Video mixers

Entries in `videoMixers`. An interface names one by `instance` in its `videoMixer`, and every input
number in that interface's `cameraMap` and `keys.input` is one of this mixer's inputs.

| `type` | Talks to | Needs |
| --- | --- | --- |
| `blackmagicdesign/atem` | an ATEM switcher over the network | `ip`, `mixEffectBlock` |
| `passthrough/default` | nothing | — |

## `blackmagicdesign/atem`

```json
{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "192.168.1.240", "mixEffectBlock": 0 }
```

| Key | Means |
| --- | --- |
| `ip` | the switcher's address |
| `mixEffectBlock` | which M/E to drive, counted from zero |

Preview, program, cut and auto all go to the switcher, and it is the switcher that says what is
live. The tally an interface sets on its cameras follows what the switcher reports rather than what
the operator last pressed.

## `passthrough/default`

```json
{ "type": "passthrough/default", "instance": 1 }
```

No switcher. It remembers what was selected and reports it back, so cameras, tally and the whole
interface work with nothing but cameras on the network. This is what to configure when the point is
to point cameras rather than to cut between them, and what the samples use so they run anywhere.
