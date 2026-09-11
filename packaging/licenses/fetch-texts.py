"""Regenerate the third party licence report and the licence texts that go with it.

    dotnet tool restore
    python packaging/licenses/fetch-texts.py

nuget-license writes the report and refuses a licence the project has not accepted. It also downloads
what each package's licence URL serves, which is the package's own licence text for some and a
reference page for the rest; only the former is kept. Every licence the report names also gets the
text of its SPDX identifier, which is what a package with no text of its own is shown under.

The texts are committed. The report is not: it names the exact versions restored, which every
dependency bump changes, so a build generates it first and the licence source generator compiles it
along with the texts beside it. A licence with no text at all fails the build rather than showing an
empty page.

    python packaging/licenses/fetch-texts.py --report-only

is what a build runs, leaving the committed texts alone.
"""

import argparse
import json
import pathlib
import re
import subprocess
import tempfile
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
LICENSES = ROOT / "src/Cgf.CameraControl.App/Licenses"
REPORT = LICENSES / "third-party-licenses.json"
PACKAGE_TEXTS = LICENSES / "texts/packages"
SPDX_TEXTS = LICENSES / "texts/spdx"
SPDX_SOURCE = "https://raw.githubusercontent.com/spdx/license-list-data/main/text/{identifier}.txt"

PROJECT = "src/Cgf.CameraControl.App/Cgf.CameraControl.App.csproj"
# What builds the binary rather than travelling in it. The diagnostics support package ships in Debug
# alone, and the compiler and the trimmer carry the host's runtime identifier in their package id, so
# a report naming them would differ between a developer machine and CI.
IGNORED = ";".join(
    [
        "AvaloniaUI.DiagnosticsSupport",
        "Microsoft.DotNet.ILCompiler",
        "runtime.*.Microsoft.DotNet.ILCompiler",
        "Microsoft.NET.ILLink.Tasks",
    ]
)
ALLOWED = "MIT;Zlib;BSD-3-Clause"


def nuget_license(*arguments: str) -> None:
    subprocess.run(
        ["dotnet", "nuget-license", "-i", PROJECT, "-t", "-ignore", IGNORED, "-a", ALLOWED, *arguments],
        cwd=ROOT,
        check=True,
    )


def written(directory: pathlib.Path, name: str, text: str) -> pathlib.Path:
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{name}.txt"
    # Committed as LF whatever the machine, and trailing blank lines only pad the window.
    path.write_text(text.replace("\r\n", "\n").rstrip() + "\n", encoding="utf-8", newline="")
    return path


def prune(directory: pathlib.Path, keep: set[pathlib.Path]) -> None:
    for stale in sorted(directory.glob("*.txt")) if directory.exists() else []:
        if stale not in keep:
            stale.unlink()
            print(f"{stale.relative_to(LICENSES)}: no longer used, removed")


arguments = argparse.ArgumentParser(description=__doc__)
arguments.add_argument("--markdown", help="also write the report as markdown, for a build to keep")
arguments.add_argument(
    "--report-only", action="store_true", help="write the report and stop, leaving the committed texts alone"
)
options = arguments.parse_args()

# nuget-license reads the restored package graph rather than the project file, and a checkout that
# has not been built yet has none.
subprocess.run(["dotnet", "restore", PROJECT], cwd=ROOT, check=True)

nuget_license("-o", "JsonPretty", "-fo", str(REPORT))
if options.markdown:
    nuget_license("-o", "Markdown", "-fo", options.markdown)

report = json.loads(REPORT.read_text(encoding="utf-8"))
packages = {entry["PackageId"] for entry in report}
print(f"{len(packages)} packages")

# A build compiles in the texts as committed. Fetching them there would ship whatever the run
# happened to download, which nobody reviewed.
if options.report_only:
    raise SystemExit(0)

with tempfile.TemporaryDirectory() as downloads:
    nuget_license("-o", "Table", "-d", downloads)

    # A licence URL that served plain text is the package's own licence. One that served a page is
    # the reference for an identifier, and the identifier's text is fetched below instead.
    kept = set()
    for downloaded in sorted(pathlib.Path(downloads).glob("*.txt")):
        package = re.sub(r"__[^_]*$", "", downloaded.stem)
        if package in packages:
            kept.add(written(PACKAGE_TEXTS, package, downloaded.read_text(encoding="utf-8")))
            print(f"{package}: its own licence text")
    prune(PACKAGE_TEXTS, kept)

# The artwork report beside it is written by hand and names its own licences, which need their text too.
reports = [json.loads(path.read_text(encoding="utf-8")) for path in sorted(LICENSES.glob("*.json"))]
identifiers = sorted({entry["License"] for entries in reports for entry in entries if entry.get("License")})
kept = set()
for identifier in identifiers:
    with urllib.request.urlopen(SPDX_SOURCE.format(identifier=identifier)) as response:
        kept.add(written(SPDX_TEXTS, identifier, response.read().decode("utf-8")))
    print(f"{identifier}: SPDX text")
prune(SPDX_TEXTS, kept)
