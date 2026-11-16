; Скрипт установщика Inno Setup для FileSignatureChecker.
; Собирается в GitHub Actions (.github/workflows/publish.yml), но можно и локально:
;   dotnet publish FileSignatureChecker.csproj -c Release -r win-x64 --self-contained -o publish
;   iscc /DAppVersion=1.0.0 /DSourceDir=..\publish installer\FileSignatureChecker.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\installer-output"
#endif

#define AppName "Проверка файлов и подписей"
#define AppExeName "FileSignatureChecker.exe"
#define AppPublisher "13cyberpunk02"
#define AppUrl "https://github.com/13cyberpunk02/FileSignatureChecker"

[Setup]
; AppId не менять: по нему установщик находит уже установленную версию для обновления
AppId={{EBD0BB94-4E07-4EE7-8BA2-C33D4BE6F27C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\FileSignatureChecker
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\icons8-puzzle-matching-50.ico
OutputDir={#OutputDir}
OutputBaseFilename=FileSignatureChecker-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Без прав администратора ставится в профиль пользователя, с правами — в Program Files
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
