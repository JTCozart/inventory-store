; Inventory Store - Inno Setup script
; Build: ISCC /DAppVersion="20260606.1430" /DAppSemVer="1.0606.1430" setup.iss

#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif
#ifndef AppSemVer
  #define AppSemVer "0.0.0"
#endif

[Setup]
AppId={{E4A1B2C3-D4E5-4F6A-B7C8-D9E0F1A2B3C4}
AppName=Inventory Store
AppVersion={#AppVersion}
AppVerName=Inventory Store {#AppVersion}
AppPublisher=Jake Cozart
AppPublisherURL=https://github.com/JTCozart/inventory-store
AppSupportURL=https://github.com/JTCozart/inventory-store/issues
AppUpdatesURL=https://github.com/JTCozart/inventory-store/releases
DefaultDirName={autopf}\InventoryStore
DefaultGroupName=Inventory Store
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=InventoryStore-Setup-{#AppVersion}
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\InventoryStore.Tray.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
VersionInfoVersion={#AppSemVer}
VersionInfoCompany=Jake Cozart
VersionInfoDescription=Inventory Store Installer
VersionInfoProductName=Inventory Store
VersionInfoProductVersion={#AppSemVer}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Service / web server (single-file exe + web assets)
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Tray companion (single-file exe)
Source: "..\publish\tray\InventoryStore.Tray.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Inventory Store"; Filename: "{app}\InventoryStore.Tray.exe"; Comment: "Open Inventory Store"
Name: "{group}\Uninstall Inventory Store"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Inventory Store"; Filename: "{app}\InventoryStore.Tray.exe"; Tasks: desktopicon

[Registry]
; Start the tray companion at Windows login (current user)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "InventoryStoreTray"; \
  ValueData: """{app}\InventoryStore.Tray.exe"""; \
  Flags: uninsdeletevalue

[Run]
; Install the Windows service
Filename: "{sys}\sc.exe"; \
  Parameters: "create InventoryStore binPath= ""{app}\InventoryStore.App.exe"" start= auto DisplayName= ""Inventory Store"""; \
  Flags: runhidden waituntilterminated; StatusMsg: "Installing service..."

Filename: "{sys}\sc.exe"; \
  Parameters: "description InventoryStore ""Inventory Store web server - accessible at http://localhost:5050"""; \
  Flags: runhidden waituntilterminated

; Stamp the build version into appsettings.json so the app knows its own version
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
  Parameters: "-NoProfile -Command ""$f='{app}\appsettings.json'; $j=Get-Content $f -Raw | ConvertFrom-Json; $j.AppVersion='{#AppVersion}'; $j | ConvertTo-Json -Depth 10 | Set-Content $f"""; \
  Flags: runhidden waituntilterminated; StatusMsg: "Configuring..."

; Start the service immediately
Filename: "{sys}\net.exe"; Parameters: "start InventoryStore"; \
  Flags: runhidden waituntilterminated; StatusMsg: "Starting service..."

; Launch tray companion (no-wait so installer can close)
Filename: "{app}\InventoryStore.Tray.exe"; \
  Description: "Launch Inventory Store tray"; \
  Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{sys}\net.exe"; Parameters: "stop InventoryStore"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete InventoryStore"; Flags: runhidden waituntilterminated

[UninstallDelete]
; Remove the data directory only if the user confirms (via Code section)
Type: dirifempty; Name: "{localappdata}\InventoryStore"

[Code]
// Stop the service and tray before installing new files
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec(ExpandConstant('{sys}\net.exe'), 'stop InventoryStore', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/F /IM InventoryStore.Tray.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    // Brief pause to ensure handles are released before file copy
    Sleep(1500);
  end;
end;

// Kill any running tray companion before uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec('taskkill.exe', '/F /IM InventoryStore.Tray.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

// Offer to remove user data on uninstall
procedure CurUninstallStepChanged2(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Do you want to remove all Inventory Store data (database and settings)?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\InventoryStore'), True, True, True);
    end;
  end;
end;
