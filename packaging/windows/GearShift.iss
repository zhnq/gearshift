#define AppName "GearShift"
#define AppVersion GetEnv("GEARSHIFT_VERSION")
#if AppVersion == ""
  #define AppVersion "0.0.0-dev"
#endif
#define SourceDir GetEnv("GEARSHIFT_PUBLISH_DIR")
#if SourceDir == ""
  #define SourceDir "publish"
#endif
#define BuildOutputDir GetEnv("GEARSHIFT_OUTPUT_DIR")
#if BuildOutputDir == ""
  #define BuildOutputDir "release"
#endif

[Setup]
AppId={{6D5CAEF3-9136-449C-8B46-F4180DABEB95}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=GearShift
DefaultDirName={autopf}\GearShift
DefaultGroupName=GearShift
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#BuildOutputDir}
OutputBaseFilename=GearShiftSetup-{#AppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=2
WizardStyle=modern
UninstallDisplayIcon={app}\GearShift.exe

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "*.pdb"

[Icons]
Name: "{group}\GearShift"; Filename: "{app}\GearShift.exe"
Name: "{autodesktop}\GearShift"; Filename: "{app}\GearShift.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Run]
Filename: "{app}\GearShift.exe"; Description: "启动 GearShift"; Flags: nowait postinstall skipifsilent

[Code]
function IsDesktopRuntimePresent: Boolean;
var
  SubKeys: TArrayOfString;
begin
  Result := RegGetSubkeyNames(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', SubKeys) and (GetArrayLength(SubKeys) > 0);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsDesktopRuntimePresent then
    MsgBox('未检测到 .NET Desktop Runtime。安装完成后请安装 .NET 10 Desktop Runtime 再启动 GearShift。', mbInformation, MB_OK);
end;
