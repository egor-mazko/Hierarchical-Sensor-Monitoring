; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!
#define AppIcon "Icon.ico"
#define AppExeFile "HSMClient.exe"
#define AppShortcutName "HSM"
#define ReleaseFilesPath "..\..\HSM Installing\publish"

[Setup]
AppId={{8FB93B9E-B78C-4B1F-9C19-4BD0663B985C}
AppName=HSMClient
AppVerName=HSMClient 0.8
DefaultDirName={pf}\HSMClient
DefaultGroupName=HSMClient
AppPublisher=Soft-FX

;InfoBeforeFile=
OutputBaseFilename=HSMClient_setup_0.9
;SetupIconFile=
;UninstallDisplayIcon=
DisableProgramGroupPage=yes
;LicenseFile=
Compression=lzma
SolidCompression=yes
;WizardImageFile=
;WizardSmallImageFile=

[Languages]
;
;

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
;Name: "{app}\Data"
;Name: "{app}\Help"
;Name: "{app}"; Attribs: system
;Name: "{app}\Config"
;Name: "{app}\Certificates"

[Files]
Source: "{#ReleaseFilesPath}\{#AppExeFile}"; DestDir: "{app}"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ReleaseFilesPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppIcon}"; DestDir: "{app}"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\DefaultCertificates\*"; DestDir: "{app}\DefaultCertificates"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\ca\*"; DestDir: "{app}\ca"; Flags: ignoreversion
;Location-dependent files
;Source: "{#ReleaseFilesPath}\cs\*"; DestDir: "{app}\cs"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\de\*"; DestDir: "{app}\de"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\es\*"; DestDir: "{app}\es"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\fr\*"; DestDir: "{app}\fr"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\it\*"; DestDir: "{app}\it"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\ja\*"; DestDir: "{app}\ja"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\ko\*"; DestDir: "{app}\ko"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\pl\*"; DestDir: "{app}\pl"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\pt-BR\*"; DestDir: "{app}\pt-BR"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\ru\*"; DestDir: "{app}\ru"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\tr\*"; DestDir: "{app}\tr"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\zh-Hans\*"; DestDir: "{app}\zh-Hans"; Flags: ignoreversion
;Source: "{#ReleaseFilesPath}\zh-Hant\*"; DestDir: "{app}\zh-Hant"; Flags: ignoreversion


[UninstallDelete]
;

[Icons]
;
;Name: "{app}\HSMClient"; Filename: "{app}\{#AppExeFile}"; IconFilename: "{app}\{#AppIcon}"; Tasks: desktopicon 
Name: "{userdesktop}\{#AppShortcutName}"; Filename: "{app}\{#AppExeFile}"; IconFilename: "{app}\{#AppIcon}"; Tasks: desktopicon 

[Run]
Filename: "{app}\{#AppExeFile}"; Description: "Launch client application"; Flags: nowait postinstall
;
 




