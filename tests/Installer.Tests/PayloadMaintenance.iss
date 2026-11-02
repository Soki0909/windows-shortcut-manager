#ifndef PayloadTestsRoot
  #error PayloadTestsRoot is required
#endif
[Setup]
AppName=Shortcut Manager isolated payload checks
AppVersion=0.0.0
AppId=ShortcutManager.IsolatedPayloadTests
PrivilegesRequired=lowest
Uninstallable=no
CreateAppDir=no
OutputBaseFilename=payload-native-tests
Compression=none
DisableStartupPrompt=yes
[Code]
#include "..\..\installer\PayloadMaintenance.iss"

procedure Check(Condition: Boolean; Message: String);
begin
  if not Condition then RaiseException(Message);
end;

function InitializeSetup: Boolean;
var Root, App, Manifest, Receipt, BeforeHash, BackupFile: String; I: Integer; Found: TFindRec; HasBackup: Boolean;
begin
  Result := False; // This harness never enters an installation step.
  Root := '{#PayloadTestsRoot}';
  try
    for I := 0 to 11 do begin
      App := Root + '\case-' + IntToStr(I) + '\app';
      Manifest := Root + '\case-' + IntToStr(I) + '\next.ini';
      Receipt := App + '\.shortcut-manager-payload.ini';
      if FileExists(Receipt) then BeforeHash := GetSHA256OfFile(Receipt) else BeforeHash := '';
      if I = 0 then Log('Unicode fixture record path: ' + GetIniString('File1', 'Path', '', Receipt));
      if (I = 5) or (I = 11) then begin
        HasBackup := False;
        try BeginPayloadUpgrade(App, Manifest);
        except HasBackup := True; end;
        Check(HasBackup, 'Redirected installation root was accepted.');
        Check(GetSHA256OfFile(Receipt) = BeforeHash, 'Redirected receipt changed.');
        continue;
      end;
      BeginPayloadUpgrade(App, Manifest);
      if I = 6 then begin
        Check(SaveStringToFile(Receipt, 'concurrent receipt edit', False), 'Cannot create concurrent receipt fixture.');
        BeforeHash := GetSHA256OfFile(Receipt);
      end;
      try FinishPayloadUpgrade(Manifest);
      except if I <> 10 then RaiseException(GetExceptionMessage); end;
      Check(FileExists(App + '\current.txt'), 'Current payload disappeared.');
      if I = 0 then begin
        Check(not FileExists(App + '\旧ファイル🧪.txt'), 'Unchanged obsolete payload was not retired.');
        HasBackup := False;
        if FindFirst(App + '\.shortcut-manager-retired-*', Found) then begin
          repeat
            if Found.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then begin
              BackupFile := App + '\' + Found.Name + '\files\旧ファイル🧪.txt';
              if FileExists(BackupFile) then begin
                Check(GetSHA256OfFile(BackupFile) = GetSHA256OfFile(Root + '\old-file.txt'), 'Retired backup changed bytes.');
                HasBackup := True;
              end;
            end;
          until not FindNext(Found);
          FindClose(Found);
        end;
        Check(HasBackup, 'Retired payload has no verified backup.');
      end;
      if I = 1 then Check(FileExists(App + '\modified.txt'), 'Modified obsolete payload was removed.');
      if I = 2 then Check(FileExists(App + '\user-added.txt'), 'Unmanaged user file was removed.');
      if (I = 3) or (I = 5) or (I = 6) or (I = 8) or (I = 9) then
        Check(GetSHA256OfFile(Receipt) = BeforeHash, 'Invalid, redirected or changed receipt was overwritten.');
      if I = 4 then Check(FileExists(App + '\linked\old.txt'), 'Junction target changed.');
      if I = 7 then Check(FileExists(App + '\legacy-extra.txt') and FileExists(Receipt), 'Legacy install inferred file ownership or failed to establish receipt.');
      if I = 10 then Check(FileExists(App + '\locked.txt'), 'Locked obsolete payload was removed.');
      if (I = 0) or (I = 1) or (I = 2) or (I = 4) or (I = 7) then
        Check(GetIniString('Payload', 'InstallDirectory', '', Receipt) = App, 'New receipt is not bound to its installation directory.');
    end;
    Check(GetSHA256OfFile(Root + '\outside-external.txt') = GetSHA256OfFile(Root + '\old-file.txt'), 'Unmanaged outside file changed.');
    Check(SaveStringToFile(Root + '\result.json', '{"ok":true,"cases":12}', False), 'Cannot record native test result.');
  except
    SaveStringToFile(Root + '\failure.txt', GetExceptionMessage, False);
  end;
end;
