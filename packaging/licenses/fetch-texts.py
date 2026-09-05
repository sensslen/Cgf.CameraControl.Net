"""Fetch the text of every licence the application ships under.

The report says which licence each package is under; this fetches the text of each one it names,
from the SPDX licence list, which is what those identifiers identify. The texts are committed and
compiled into the binary by the licence source generator, so a licence with no text here fails the
build rather than showing an empty page.

    python packaging/licenses/fetch-texts.py
"""

import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
REPORT = ROOT / "src/Cgf.CameraControl.App/Licenses/third-party-licenses.json"
TEXTS = ROOT / "src/Cgf.CameraControl.App/Licenses/texts"
SOURCE = "https://raw.githubusercontent.com/spdx/license-list-data/main/text/{identifier}.txt"

TEXTS.mkdir(parents=True, exist_ok=True)
report = json.loads(REPORT.read_text(encoding="utf-8"))
wanted = sorted({package["License"] for package in report if package.get("License")})

for identifier in wanted:
    with urllib.request.urlopen(SOURCE.format(identifier=identifier)) as response:
        text = response.read().decode("utf-8")
    # Committed as LF whatever the machine, and trailing blank lines only pad the window.
    (TEXTS / f"{identifier}.txt").write_text(text.replace("\r\n", "\n").rstrip() + "\n", encoding="utf-8", newline="")
    print(f"{identifier}: {len(text)} characters")

for stale in sorted(TEXTS.glob("*.txt")):
    if stale.stem not in wanted:
        stale.unlink()
        print(f"{stale.stem}: no longer used, removed")
