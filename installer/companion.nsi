Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "WordFunc.nsh"

!ifndef PAYLOAD
  !error "PAYLOAD must point to the self-contained publish directory"
!endif
!ifndef OUTPUT
  !error "OUTPUT must point to the installer executable"
!endif
!ifndef UNINSTALL_FILES
  !error "UNINSTALL_FILES must point to the generated exact-file removal list"
!endif
!ifndef APPICON
  !error "APPICON is required"
!endif
!ifndef VERSION
  !error "VERSION is required"
!endif
!define APPKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\StrigoiCompanion"

Name "Strigoi Companion"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Strigoi Companion"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
ShowInstDetails show
ShowUninstDetails show
BrandingText "Strigoi Companion · Seu Familiar de gameplay"
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1046 "ProductName" "Strigoi Companion"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalador do Strigoi Companion"
VIAddVersionKey /LANG=1046 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1046 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1046 "LegalCopyright" "Strigoi Companion"

Var TestMode
Var InstalledVersion
Var WelcomeTitle
Var InstallMessage
!define MUI_ICON "${APPICON}"
!define MUI_UNICON "${APPICON}"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "$WelcomeTitle"
!define MUI_WELCOMEPAGE_TEXT "$InstallMessage"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "O Familiar está pronto."
!define MUI_FINISHPAGE_TEXT "Abra o Strigoi Companion e depois seu jogo. O Familiar acompanha a janela ativa automaticamente.$\r$\n$\r$\nClique esquerdo no Familiar para conversar; clique direito abre controles rápidos. Ctrl + Alt + F10 abre o Control Center.$\r$\n$\r$\nVocê também pode encontrar o vampirinho na bandeja do Windows."
!define MUI_FINISHPAGE_RUN "$INSTDIR\Strigoi.Companion.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir Strigoi Companion agora"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

!macro CheckRunning PREFIX
  ${PREFIX}runningCheck:
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\Strigoi.Companion") p .r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    IfSilent ${PREFIX}runningAbort
    MessageBox MB_RETRYCANCEL|MB_ICONINFORMATION "Feche o Strigoi Companion pela bandeja do Windows e clique em Tentar novamente para continuar." IDRETRY ${PREFIX}runningCheck
    ${PREFIX}runningAbort:
    SetErrorLevel 2
    Abort
  ${EndIf}
!macroend

Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "Este instalador requer Windows de 64 bits."
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "Este aplicativo requer Windows moderno; a plataforma alvo é Windows 11."
    Abort
  ${EndIf}
  StrCpy $TestMode "0"
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/TESTINSTALL" $1
  ${IfNot} ${Errors}
    StrCpy $TestMode "1"
  ${EndIf}
  ${If} $TestMode == "0"
    ; Keep production installation inside its dedicated per-user folder.
    StrCpy $INSTDIR "$LOCALAPPDATA\Programs\Strigoi Companion"
    !insertmacro CheckRunning "install"
  ${EndIf}
  StrCpy $WelcomeTitle "Instalar Strigoi Companion ${VERSION}"
  StrCpy $InstallMessage "Seu Familiar acompanha jogos, conversa por um painel compacto e mantém memória local por run. Nenhuma imagem é enviada à internet.$\r$\n$\r$\nInstalação para o seu usuário. .NET já está incluído.$\r$\n$\r$\nPreferências e relatórios existentes serão preservados."
  ReadINIStr $0 "$INSTDIR\companion-install.ini" "Install" "Product"
  ${If} $0 == "StrigoiCompanion"
    ${GetFileVersion} "$INSTDIR\Strigoi.Companion.exe" $InstalledVersion
    ${VersionCompare} $InstalledVersion "${VERSION}" $0
    ${If} $0 == 1
      IfSilent +2
      MessageBox MB_OK|MB_ICONSTOP "Uma versão mais recente já está instalada. Use o instalador correspondente."
      SetErrorLevel 4
      Abort
    ${EndIf}
    StrCpy $WelcomeTitle "Atualizar Strigoi Companion"
    StrCpy $InstallMessage "Versão instalada: $InstalledVersion$\r$\nNova versão: ${VERSION}$\r$\n$\r$\nEste assistente atualiza o aplicativo no mesmo local, mantendo preferências, Chronicle e relatórios.$\r$\n$\r$\nNovidade: Familiar-first — detecção automática, Quick Talk e controles rápidos.$\r$\n$\r$\nFeche o pet pela bandeja antes de continuar."
  ${EndIf}
FunctionEnd

Section "Strigoi Companion" SEC_MAIN
  ${If} $TestMode == "0"
    !insertmacro CheckRunning "write"
  ${EndIf}
  SetOutPath "$INSTDIR"
  SetOverwrite on
  ${If} $InstalledVersion == "0.1.1.0"
    ; Obsolete readme shipped by 0.1.1, now replaced by LEIA-ME.txt.
    Delete "$INSTDIR\LEIA-ME.md"
  ${EndIf}
  File /r "${PAYLOAD}\*"
  WriteINIStr "$INSTDIR\companion-install.ini" "Install" "Product" "StrigoiCompanion"
  WriteINIStr "$INSTDIR\companion-install.ini" "Install" "TestMode" "$TestMode"
  WriteINIStr "$INSTDIR\companion-install.ini" "Install" "Version" "${VERSION}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} $TestMode == "0"
    CreateDirectory "$SMPROGRAMS\Strigoi Companion"
    CreateShortcut "$SMPROGRAMS\Strigoi Companion\Strigoi Companion.lnk" "$INSTDIR\Strigoi.Companion.exe"
    CreateShortcut "$SMPROGRAMS\Strigoi Companion\Desinstalar.lnk" "$INSTDIR\Uninstall.exe"
    CreateShortcut "$DESKTOP\Strigoi Companion.lnk" "$INSTDIR\Strigoi.Companion.exe"
    WriteRegStr HKCU "${APPKEY}" "DisplayName" "Strigoi Companion"
    WriteRegStr HKCU "${APPKEY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKCU "${APPKEY}" "Publisher" "Strigoi Companion"
    WriteRegStr HKCU "${APPKEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${APPKEY}" "DisplayIcon" "$INSTDIR\Strigoi.Companion.exe,0"
    WriteRegStr HKCU "${APPKEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "${APPKEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegDWORD HKCU "${APPKEY}" "NoModify" 1
    WriteRegDWORD HKCU "${APPKEY}" "NoRepair" 1
  ${EndIf}
SectionEnd

Function un.onInit
  SetShellVarContext current
  ReadINIStr $0 "$INSTDIR\companion-install.ini" "Install" "Product"
  ${If} $0 != "StrigoiCompanion"
    SetErrorLevel 3
    Abort
  ${EndIf}
  ReadINIStr $TestMode "$INSTDIR\companion-install.ini" "Install" "TestMode"
  ${If} $TestMode == "0"
    !insertmacro CheckRunning "uninstall"
  ${EndIf}
FunctionEnd

Section "Uninstall"
  ; Only installed payload files are removed. No recursive folder deletion.
  !include "${UNINSTALL_FILES}"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\companion-install.ini"
  RMDir "$INSTDIR"
  ${If} $TestMode == "0"
    Delete "$DESKTOP\Strigoi Companion.lnk"
    Delete "$SMPROGRAMS\Strigoi Companion\Strigoi Companion.lnk"
    Delete "$SMPROGRAMS\Strigoi Companion\Desinstalar.lnk"
    RMDir "$SMPROGRAMS\Strigoi Companion"
    DeleteRegKey HKCU "${APPKEY}"
  ${EndIf}
  ; Preferences and gameplay validation reports remain under LOCALAPPDATA.
SectionEnd
