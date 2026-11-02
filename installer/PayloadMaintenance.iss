// File retirement only; never touch config, Start Menu HotKeys or Chrome data.
type
  TPayloadFile = record
    RelativePath: String;
    Sha256: String;
  end;
  TPayloadFiles = array of TPayloadFile;
var
  PayloadRoot, PreviousReceiptHash: String;
  PreviousReceiptExists, PreviousReceiptValid: Boolean;
  PreviousPayload, NextPayload: TPayloadFiles;

function PayloadAttributes(Name: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function PayloadLastError: Cardinal;
  external 'GetLastError@kernel32.dll stdcall';
function PayloadReplaceFile(Replaced, Replacement, Backup: String; Flags, Exclude, Reserved: Cardinal): Integer;
  external 'ReplaceFileW@kernel32.dll stdcall';

function PayloadUnsafePath(Name: String): Boolean;
var Attr, Error: Cardinal; Parent: String;
begin
  Result := True;
  Name := ExpandFileName(Name);
  while Name <> '' do begin
    Attr := PayloadAttributes(Name);
    if Attr = $FFFFFFFF then begin
      Error := PayloadLastError;
      if (Error <> 2) and (Error <> 3) then exit;
    end else if (Attr and $400) <> 0 then exit;
    Parent := ExtractFileDir(Name);
    if CompareText(Parent, Name) = 0 then break;
    Name := Parent;
  end;
  Result := False;
end;

function PayloadSafeRelative(Name: String): Boolean;
var Part, Rest: String; Sep, I: Integer;
begin
  Result := False;
  if (Name = '') or (Pos('/', Name) <> 0) or (Pos(':', Name) <> 0) or (Copy(Name, 1, 1) = '\') then exit;
  Rest := Name;
  repeat
    Sep := Pos('\', Rest);
    if Sep = 0 then Part := Rest else Part := Copy(Rest, 1, Sep - 1);
    if (Part = '') or (Part = '.') or (Part = '..') or (Part <> Trim(Part)) or (Copy(Part, Length(Part), 1) = '.') then exit;
    for I := 1 to Length(Part) do
      if (Ord(Part[I]) < 32) or (Pos(Part[I], '"|?*') <> 0) then exit;
    if Sep = 0 then break;
    Rest := Copy(Rest, Sep + 1, Length(Rest));
  until False;
  if CompareText(Copy(Name, 1, 18), '.shortcut-manager-') = 0 then exit;
  if CompareText(Copy(Name, 1, 5), 'unins') = 0 then exit;
  Result := True;
end;

function PayloadValidHash(Hash: String): Boolean;
var I: Integer;
begin
  Result := False;
  if Length(Hash) <> 64 then exit;
  for I := 1 to Length(Hash) do if Pos(Hash[I], '0123456789abcdefABCDEF') = 0 then exit;
  Result := True;
end;

function PayloadContains(const Files: TPayloadFiles; Name: String): Boolean;
var I: Integer;
begin
  Result := True;
  for I := 0 to GetArrayLength(Files) - 1 do if CompareText(Files[I].RelativePath, Name) = 0 then exit;
  Result := False;
end;

function PayloadRead(Name, BoundRoot: String; var Files: TPayloadFiles): Boolean;
var Size: Int64; Count, I, J: Integer; Section, Relative, Hash: String;
begin
  Result := False;
  SetArrayLength(Files, 0);
  if PayloadUnsafePath(Name) or not FileSize64(Name, Size) then exit;
  if (Size < 1) or (Size > 1048576) then exit;
  if GetIniString('Payload', 'Format', '', Name) <> 'ShortcutManager.InstallPayload.v1' then exit;
  if BoundRoot <> '' then
    if CompareText(GetIniString('Payload', 'InstallDirectory', '', Name), BoundRoot) <> 0 then exit;
  Count := StrToIntDef(GetIniString('Payload', 'Count', '', Name), -1);
  if (Count < 1) or (Count > 4096) then exit;
  SetArrayLength(Files, Count);
  for I := 0 to Count - 1 do begin
    Section := 'File' + IntToStr(I);
    Relative := GetIniString(Section, 'Path', '', Name);
    Hash := GetIniString(Section, 'Sha256', '', Name);
    if not PayloadSafeRelative(Relative) or not PayloadValidHash(Hash) then exit;
    for J := 0 to I - 1 do if CompareText(Files[J].RelativePath, Relative) = 0 then exit;
    Files[I].RelativePath := Relative;
    Files[I].Sha256 := LowerCase(Hash);
  end;
  Result := True;
end;

procedure BeginPayloadUpgrade(Root, Manifest: String);
var Receipt: String; I: Integer;
begin
  PayloadRoot := RemoveBackslashUnlessRoot(ExpandFileName(Root));
  PreviousReceiptHash := '';
  PreviousReceiptValid := False;
  SetArrayLength(PreviousPayload, 0);
  if not PayloadRead(Manifest, '', NextPayload) then RaiseException('Invalid embedded installer payload record.');
  Receipt := AddBackslash(PayloadRoot) + '.shortcut-manager-payload.ini';
  PreviousReceiptExists := FileExists(Receipt) or DirExists(Receipt);
  if PayloadUnsafePath(PayloadRoot) then RaiseException('Installation path is redirected or cannot be verified. No payload will be written.');
  for I := 0 to GetArrayLength(NextPayload) - 1 do
    if PayloadUnsafePath(AddBackslash(PayloadRoot) + NextPayload[I].RelativePath) then
      RaiseException('A payload destination is redirected or cannot be verified. No payload will be written.');
  if PreviousReceiptExists then begin
    PreviousReceiptValid := PayloadRead(Receipt, PayloadRoot, PreviousPayload);
    if PreviousReceiptValid then PreviousReceiptHash := GetSHA256OfFile(Receipt)
    else Log('Payload cleanup skipped: unknown or invalid prior receipt.');
  end else Log('Legacy/new install: no prior payload receipt; no files are inferred as owned.');
end;

procedure FinishPayloadUpgrade(Manifest: String);
var Receipt, BackupRoot, Stage, Source, Destination, PreviousCopy: String; I: Integer; StageCreated: Boolean;
begin
  Receipt := AddBackslash(PayloadRoot) + '.shortcut-manager-payload.ini';
  if PayloadUnsafePath(PayloadRoot) then exit;
  if PreviousReceiptExists then begin
    if not PreviousReceiptValid or PayloadUnsafePath(Receipt) or not FileExists(Receipt) then exit;
    if GetSHA256OfFile(Receipt) <> PreviousReceiptHash then begin Log('Prior payload receipt changed; cleanup stopped.'); exit; end;
  end else if FileExists(Receipt) or DirExists(Receipt) then begin Log('Payload receipt appeared; cleanup stopped.'); exit; end;
  BackupRoot := '';
  if PreviousReceiptExists then begin
    BackupRoot := AddBackslash(PayloadRoot) + '.shortcut-manager-retired-' + ExtractFileName(GenerateUniqueName(PayloadRoot, '.backup'));
    if not CreateDir(BackupRoot) then begin Log('Cannot create payload backup; cleanup stopped.'); exit; end;
    if not SaveStringToFile(AddBackslash(BackupRoot) + 'OWNER.txt', 'ShortcutManager.InstallPayload.Backup.v1', False) then exit;
    PreviousCopy := AddBackslash(BackupRoot) + 'previous-payload.ini';
    if not CopyFile(Receipt, PreviousCopy, True) or (GetSHA256OfFile(PreviousCopy) <> PreviousReceiptHash) then exit;
    for I := 0 to GetArrayLength(PreviousPayload) - 1 do begin
      if PayloadContains(NextPayload, PreviousPayload[I].RelativePath) then continue;
      Source := AddBackslash(PayloadRoot) + PreviousPayload[I].RelativePath;
      if PayloadUnsafePath(Source) or not FileExists(Source) then continue;
      if GetSHA256OfFile(Source) <> PreviousPayload[I].Sha256 then begin Log('Modified obsolete payload preserved: ' + PreviousPayload[I].RelativePath); continue; end;
      Destination := AddBackslash(BackupRoot) + 'files\' + PreviousPayload[I].RelativePath;
      if not ForceDirectories(ExtractFileDir(Destination)) or PayloadUnsafePath(Destination) then continue;
      if not CopyFile(Source, Destination, True) then continue;
      if (GetSHA256OfFile(Destination) <> PreviousPayload[I].Sha256) or (GetSHA256OfFile(Source) <> PreviousPayload[I].Sha256) or PayloadUnsafePath(Source) then continue;
      if DeleteFile(Source) then Log('Retired obsolete payload after verified backup: ' + PreviousPayload[I].RelativePath)
      else Log('Obsolete payload could not be removed; backup and source kept.');
    end;
  end;
  Stage := AddBackslash(PayloadRoot) + '.shortcut-manager-receipt-' + ExtractFileName(GenerateUniqueName(PayloadRoot, '.tmp'));
  StageCreated := False;
  try
    if not CopyFile(Manifest, Stage, True) then exit;
    StageCreated := True;
    if not SetIniString('Payload', 'InstallDirectory', PayloadRoot, Stage) then exit;
    if PreviousReceiptExists then begin
      if PayloadUnsafePath(Receipt) or (GetSHA256OfFile(Receipt) <> PreviousReceiptHash) then exit;
      if PayloadReplaceFile(Receipt, Stage, AddBackslash(BackupRoot) + 'replaced-payload.ini', 0, 0, 0) = 0 then Log('Payload receipt update failed; previous receipt and backups kept.');
    end else if not RenameFile(Stage, Receipt) then Log('Cannot commit new payload receipt; no unknown file overwritten.');
  finally
    if StageCreated and FileExists(Stage) then DeleteFile(Stage);
  end;
end;
