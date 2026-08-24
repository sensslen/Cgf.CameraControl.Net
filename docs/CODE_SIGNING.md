# Code signing policy

Camera Control is signed so that Windows and macOS can tell you the binary you downloaded is the one
this project built. This page says who can release it, what gets signed, and what the application
does with your data.

Free code signing is provided by [SignPath.io](https://signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

> Windows signing is applied for and not yet in place. Until it is, the Windows installer goes out
> unsigned and the release workflow says so. Delete this note once the first signed release ships.

## Who releases it

| Person | Roles |
| --- | --- |
| [Simon Ensslen](https://github.com/sensslen) | Author, Reviewer, Approver |

This is a single maintainer project, so those roles sit with one person. Two-factor authentication is
required on that GitHub account, and it is the only account with write access to the repository.

If that changes, this table changes with it before anyone else can trigger a release.

## What gets signed, and by what

Nothing is signed on a developer machine. A release exists only when a `v*` tag is pushed, and the
[release workflow](../.github/workflows/release.yml) is the only thing that can ask for a signature:

- **Windows.** The NSIS installer, `CameraControl-<version>-win-<arch>-setup.exe`. The runner uploads
  the installer it just built, SignPath signs it, and the signed file is what gets attached to the
  release and submitted to winget. No private key ever reaches the runner.
- **macOS.** The application bundle and the disk image, both signed with a Developer ID certificate
  and notarized by Apple.
- **Linux.** The Flatpak bundle is not signed. Flatpak verifies its own contents.

Every artifact is built by GitHub Actions from the tagged commit, on a hosted runner, from this
public repository. The workflow, the installer script and the bundle script are all in the
repository, so a build can be reproduced and compared against what was released.

## Privacy

The application collects nothing and sends nothing anywhere.

- No telemetry, no analytics, no crash reporting, no update check.
- The only network traffic is to the hardware you configure: your ATEM switchers and your cameras,
  on your own network, at the addresses in your `config.json`.
- Your configuration file stays wherever you put it. Two things are remembered between runs, in a
  `CgfCameraControl/settings.json` under the per-user application data directory
  (`%APPDATA%` on Windows): the path of the config file you last opened, and the interface language.
  Nothing else is written.
- There are no accounts, no licence checks and no bundled third party software.

## Uninstalling

- **Windows.** Settings → Apps → Camera Control → Uninstall, or `winget uninstall
  Sensslen.CameraControl`. The uninstaller removes the program directory, the Start Menu entry and
  the registry keys it created. Your configuration file and the remembered settings are left alone,
  so a reinstall comes back up on the same desk. Delete `%APPDATA%\CgfCameraControl` to remove those
  too.
- **macOS.** Drag Camera Control out of Applications, then delete the `CgfCameraControl` folder under
  your per-user application data directory.
- **Linux.** `flatpak uninstall io.github.sensslen.CgfCameraControl`.

## Reporting a problem with a signed build

If a signed release does something this page does not describe, open an issue at
[github.com/sensslen/Cgf.CameraControl.Net/issues](https://github.com/sensslen/Cgf.CameraControl.Net/issues).
