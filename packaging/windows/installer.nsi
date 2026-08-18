; Camera Control installer.
;
; Driven entirely from the command line so the workflow stays the single source of truth for
; versions and paths:
;   makensis /DVERSION=1.2.3 /DARCH=x64 /DSOURCE=<publish dir> /DOUTFILE=<setup exe> installer.nsi
;
; The installer itself stays a 32 bit executable, which is the default and what runs everywhere
; including Windows on ARM under emulation. Only the payload is architecture specific, so the
; install directory and the registry are addressed with the 64 bit views explicitly.

!ifndef VERSION
  !error "VERSION is required"
!endif
!ifndef FILEVERSION
  !error "FILEVERSION is required"
!endif
!ifndef ARCH
  !error "ARCH is required"
!endif
!ifndef SOURCE
  !error "SOURCE is required"
!endif
!ifndef OUTFILE
  !error "OUTFILE is required"
!endif

!define APPNAME "Camera Control"
!define PUBLISHER "Simon Ensslen"
!define EXENAME "CgfCameraControl.exe"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\CgfCameraControl"

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

Name "${APPNAME}"
OutFile "${OUTFILE}"
Unicode true
InstallDir "$PROGRAMFILES64\${APPNAME}"
InstallDirRegKey HKLM "Software\CgfCameraControl" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma

VIProductVersion "${FILEVERSION}"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 ${PUBLISHER} and others"
VIAddVersionKey "FileDescription" "${APPNAME} setup"

!define MUI_ABORTWARNING
!define MUI_ICON "..\icon\installer.ico"
!define MUI_UNICON "..\icon\installer.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXENAME}"

!insertmacro MUI_PAGE_LICENSE "..\..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; The installer is a 32 bit process, so a 64 bit host reports its real architecture through
  ; PROCESSOR_ARCHITEW6432 and only the emulated value through PROCESSOR_ARCHITECTURE.
  ReadEnvStr $0 "PROCESSOR_ARCHITEW6432"
  ${If} $0 == ""
    ReadEnvStr $0 "PROCESSOR_ARCHITECTURE"
  ${EndIf}

  ; An ARM64 payload on anything else installs and then refuses to start. The other direction is
  ; fine: Windows on ARM runs the x64 build under emulation.
  !if "${ARCH}" == "arm64"
    ${If} $0 != "ARM64"
      MessageBox MB_ICONSTOP "This is the ARM64 build. Install the x64 build on this computer."
      Abort
    ${EndIf}
  !endif
FunctionEnd

Section "Install"
  SetRegView 64
  SetOutPath "$INSTDIR"

  ; The published output carries the native libraries the application loads at run time: SDL, Skia
  ; and HarfBuzz. Symbols are excluded because they are larger than everything else combined.
  File /r /x "*.pdb" /x "*.dbg" "${SOURCE}\*.*"

  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"

  WriteRegStr HKLM "Software\CgfCameraControl" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${EXENAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
  WriteRegStr HKLM "${UNINSTKEY}" "QuietUninstallString" "$\"$INSTDIR\uninstall.exe$\" /S"
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINSTKEY}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  SetRegView 64

  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Only what was installed. The configuration file the operator chose lives wherever they put it,
  ; and the remembered settings under AppData are deliberately left alone so a reinstall comes back
  ; up on the same desk.
  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "${UNINSTKEY}"
  DeleteRegKey HKLM "Software\CgfCameraControl"
SectionEnd
