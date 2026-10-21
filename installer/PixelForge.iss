; PixelForge Inno Setup Script
; High-performance LZMA2 ultra-compressed installer

#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef SourceDir
#define SourceDir "..\publish\staging"
#endif

#ifndef OutputDir
#define OutputDir "..\publish\win-x64"
#endif

[Setup]
AppId={{5E28B5BA-2CE7-4D29-A71C-078C3D2B52C9}
AppName=PixelForge
AppVersion={#AppVersion}
AppVerName=PixelForge v{#AppVersion}
AppPublisher=widlily-corp
AppPublisherURL=https://github.com/widlily-corp/PixelForge
AppSupportURL=https://github.com/widlily-corp/PixelForge/issues
AppUpdatesURL=https://github.com/widlily-corp/PixelForge/releases
DefaultDirName={autopf}\PixelForge
DefaultGroupName=PixelForge
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename=PixelForge_{#AppVersion}_x64-setup
SetupIconFile=..\src\PixelForge.App\Assets\icon.ico
UninstallDisplayIcon={app}\PixelForge.App.exe
PrivilegesRequiredOverridesAllowed=commandline dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4
DisableDirPage=no
DisableProgramGroupPage=no
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PixelForge"; Filename: "{app}\PixelForge.App.exe"
Name: "{group}\{cm:UninstallProgram,PixelForge}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PixelForge"; Filename: "{app}\PixelForge.App.exe"; Tasks: desktopicon

[Registry]
; Register .pxproj file extension and association
Root: HKA; Subkey: "Software\Classes\.pxproj"; ValueType: string; ValueName: ""; ValueData: "PixelForge.Project"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.pxproj\Content Type"; ValueType: string; ValueName: ""; ValueData: "application/x-pixelforge"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.pxproj\OpenWithProgids"; ValueType: string; ValueName: "PixelForge.Project"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\PixelForge.Project"; ValueType: string; ValueName: ""; ValueData: "PixelForge Project"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PixelForge.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PixelForge.App.exe,0"
Root: HKA; Subkey: "Software\Classes\PixelForge.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\PixelForge.App.exe"" ""%1"""
; Register Application for Open With and Windows Default Apps
Root: HKA; Subkey: "Software\Classes\Applications\PixelForge.App.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "PixelForge"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\PixelForge.App.exe\SupportedTypes"; ValueType: string; ValueName: ".pxproj"; ValueData: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\PixelForge.App.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\PixelForge.App.exe"" ""%1"""

[Run]
Filename: "{app}\PixelForge.App.exe"; Description: "{cm:LaunchProgram,PixelForge}"; Flags: nowait postinstall skipifsilent
