; Inno Setup 7 script for Opsis Appearance (also compiles with 6.x).
; Build with build.ps1 (it publishes both builds into dist\ first).
;
; Two ways to install (second wizard page):
;   * Opsis plugin:  copies OpsisAppearancePlugin.dll into %AppData%\Opsis\plugins\Opsis Appearance (a folder Opsis scans at start-up).
;                    No separate app, no tray icon: the "Windows appearance" page appears in Opsis. Restart Opsis afterwards.
;   * Stand-alone:   installs OpsisAppearance.exe (tray icon + its own window), optional start with Windows.
; The wizard looks for Opsis and proposes the plugin install when it finds it.
; Silent installs: /VERYSILENT /MODE=plugin | /MODE=standalone   (and /PLUGINROOT=<folder> instead of %AppData%\Opsis\plugins).
#define MyAppName "Opsis Appearance"
#define MyAppVersion "1.2.0"
#define MyAppExe "OpsisAppearance.exe"
#define OpsisAppId "A9E8F765-1234-4567-89AB-CDEF01234567"
#define PluginFolder "Opsis Appearance"
#ifndef DistDir
  #define DistDir "..\dist"
#endif

[Setup]
AppId={{5D2F7B1C-8E43-4A9B-B6C0-71A3E9D4F528}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Opsis Team
SetupIconFile=..\src\OpsisAppearance.App\Assets\app.ico
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\Output
OutputBaseFilename=OpsisAppearance-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "greek"; MessagesFile: "Greek.isl"

[CustomMessages]
english.ModeTitle=How do you want to install Opsis Appearance?
english.ModeSub=It can live inside Opsis or run on its own.
english.ModePlugin=As a plugin of Opsis (no separate app: a "Windows appearance" page appears in Opsis)
english.ModeStandalone=Stand-alone app (tray icon and its own window)
english.StartWithWindows=Start Opsis Appearance with Windows
english.DesktopShortcut=Create a desktop shortcut
english.RestartOpsis=Restart Opsis to load the plugin.
greek.ModeTitle=Πώς θέλεις να εγκαταστήσεις το Opsis Appearance;
greek.ModeSub=Μπορεί να λειτουργεί μέσα στο Opsis ή αυτόνομα.
greek.ModePlugin=Ως plugin του Opsis (χωρίς ξεχωριστή εφαρμογή: εμφανίζεται σελίδα «Εμφάνιση Windows» στο Opsis)
greek.ModeStandalone=Αυτόνομη εφαρμογή (εικονίδιο στο tray και δικό της παράθυρο)
greek.StartWithWindows=Εκκίνηση του Opsis Appearance με τα Windows
greek.DesktopShortcut=Δημιουργία συντόμευσης στην επιφάνεια εργασίας
greek.RestartOpsis=Κάνε επανεκκίνηση του Opsis για να φορτωθεί το plugin.

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked; Check: IsStandalone
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked; Check: IsStandalone

[Files]
; Stand-alone: the app and everything it needs (the plugin DLL, the shared contract DLL)
Source: "{#DistDir}\standalone\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Check: IsStandalone
; Plugin: only the plugin DLL (the host provides the contract)
Source: "{#DistDir}\plugin\*"; DestDir: "{code:PluginDir}"; Flags: ignoreversion recursesubdirs; Check: IsPlugin
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion; Check: IsStandalone

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Check: IsStandalone
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; Check: IsStandalone

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExe}"" --minimized"; Tasks: startup; Flags: uninsdeletevalue
; Remember where the plugin went so the uninstaller removes exactly that folder
Root: HKCU; Subkey: "Software\Opsis Appearance"; ValueType: string; ValueName: "PluginDir"; ValueData: "{code:PluginDir}"; Check: IsPlugin; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Opsis Appearance"; ValueType: string; ValueName: "Mode"; ValueData: "{code:ModeName}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{#MyAppName}"; Flags: nowait postinstall skipifsilent; Check: IsStandalone

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Opsis\plugins\{#PluginFolder}"

[Code]
var
  ModePage: TInputOptionWizardPage;

function OpsisIsInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#OpsisAppId}_is1')
         or RegKeyExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#OpsisAppId}_is1')
         or DirExists(ExpandConstant('{userappdata}\Opsis'));
end;

function CommandLineMode: String;
begin
  Result := Lowercase(ExpandConstant('{param:MODE|}'));
end;

procedure InitializeWizard;
begin
  ModePage := CreateInputOptionPage(wpLicense, CustomMessage('ModeTitle'), CustomMessage('ModeSub'), '', True, False);
  ModePage.Add(CustomMessage('ModePlugin'));
  ModePage.Add(CustomMessage('ModeStandalone'));
  if (CommandLineMode = 'plugin') or ((CommandLineMode = '') and OpsisIsInstalled) then
    ModePage.Values[0] := True
  else
    ModePage.Values[1] := True;
end;

function IsPlugin: Boolean;
begin
  if CommandLineMode = 'plugin' then Result := True
  else if CommandLineMode = 'standalone' then Result := False
  else Result := ModePage.Values[0];
end;

function IsStandalone: Boolean;
begin
  Result := not IsPlugin;
end;

function ModeName(Param: String): String;
begin
  if IsPlugin then Result := 'plugin' else Result := 'standalone';
end;

function PluginDir(Param: String): String;
var
  Root: String;
begin
  Root := ExpandConstant('{param:PLUGINROOT|}');
  if Root = '' then Root := ExpandConstant('{userappdata}\Opsis\plugins');
  Result := AddBackslash(Root) + '{#PluginFolder}';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  { The folder and start-menu pages mean nothing for a plugin install }
  if IsPlugin and ((PageID = wpSelectDir) or (PageID = wpSelectProgramGroup) or (PageID = wpSelectTasks)) then Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and IsPlugin and (not WizardSilent) then
    MsgBox(CustomMessage('RestartOpsis'), mbInformation, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Dir: String;
begin
  { A plugin installed into a custom folder (/PLUGINROOT) is removed too - but only a folder that really is called "Opsis Appearance" }
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Opsis Appearance', 'PluginDir', Dir) then
      if (Dir <> '') and DirExists(Dir) and (CompareText(ExtractFileName(RemoveBackslashUnlessRoot(Dir)), '{#PluginFolder}') = 0) then
        DelTree(Dir, True, True, True);
end;
