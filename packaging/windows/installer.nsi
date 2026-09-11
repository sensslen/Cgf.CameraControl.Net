; Camera Control installer.
;
; Driven entirely from the command line so the workflow stays the single source of truth for
; versions and paths:
;   makensis /DVERSION=1.2.3 /DARCH=x64 /DSOURCE=<publish dir> /DVCREDIST=<vc_redist exe> \
;            /DOUTFILE=<setup exe> installer.nsi
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
!ifndef VCREDIST
  !error "VCREDIST is required"
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
  ; An ARM64 payload on anything else installs and then refuses to start. The other direction is
  ; fine: Windows on ARM runs the x64 build under emulation.
  ;
  ; IsNativeARM64 asks the operating system through IsWow64Process2 rather than reading
  ; PROCESSOR_ARCHITEW6432, whose value from an emulated 32 bit process is not something to stake the
  ; only ARM64 installer on.
  !if "${ARCH}" == "arm64"
    ${IfNot} ${IsNativeARM64}
      MessageBox MB_ICONSTOP "This is the ARM64 build. Install the x64 build on this computer."
      Abort
    ${EndIf}
  !endif
FunctionEnd

Section "Install"
  SetRegView 64

  ; SDL3.dll imports VCRUNTIME140.dll, which is not part of Windows. The payload is native and a 32
  ; bit installer sees SysWOW64 as System32, so the test runs with redirection off.
  InitPluginsDir
  ${DisableX64FSRedirection}
  ${IfNot} ${FileExists} "$WINDIR\System32\vcruntime140.dll"
    DetailPrint "Installing the Visual C++ runtime"
    SetOutPath "$PLUGINSDIR"
    File "/oname=vc_redist.exe" "${VCREDIST}"
    ExecWait '"$PLUGINSDIR\vc_redist.exe" /install /quiet /norestart' $0

    ; 1638 is a newer one already there, 3010 wants a restart that can wait.
    ${If} $0 != 0
    ${AndIf} $0 != 1638
    ${AndIf} $0 != 3010
      MessageBox MB_ICONSTOP "The Visual C++ runtime could not be installed (code $0). ${APPNAME} needs it to read a gamepad."
      Abort
    ${EndIf}
  ${EndIf}
  ${EnableX64FSRedirection}

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
