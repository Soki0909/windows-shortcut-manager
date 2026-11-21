// Never serialize arbitrary exception/response text or the URL recovery map.
export function dispatchDiagnostic(response) {
  return { ok: response?.ok === true, code: typeof response?.code === "string" && /^[a-z-]{1,64}$/.test(response.code) ? response.code : "dispatcher-error" };
}

export const LAST_DISPATCH_FAILURE_KEY = "lastDispatchFailure";

const failureMessages = new Map([
  ["invalid-protocol", "Managerと拡張の通信バージョンを確認してください。"],
  ["invalid-action", "この種類のキー起動には対応していません。"],
  ["invalid-minimum-version", "要求された拡張バージョンの指定が不正です。"],
  ["extension-update-required", "Tab Bridgeを更新してください。"],
  ["empty-group-name", "Managerでタブグループ名を確認してください。"],
  ["group-not-found", "対象のグループが開いていません。Chromeで開いてから試してください。"],
  ["group-query-failed", "タブグループの一覧を取得できませんでした。"],
  ["group-expand-failed", "対象のグループを展開できませんでした。"],
  ["group-collapse-failed", "対象のグループを折りたためませんでした。"],
  ["tab-query-failed", "グループ内のタブを取得できませんでした。"],
  ["tab-activate-failed", "グループ内のタブを選択できませんでした。"],
  ["window-focus-failed", "対象のChromeウィンドウを前面にできませんでした。"],
  ["message-failed", "拡張との通信に失敗しました。グループの状態を確認してから試してください。"],
  ["message-timeout", "応答を確認できませんでした。グループの状態を確認してから試してください。"],
  ["invalid-response", "拡張からの応答が不正です。グループの状態を確認してください。"],
  ["internal-error", "拡張で処理に失敗しました。"],
  ["dispatcher-error", "キー起動の処理に失敗しました。"],
]);

export async function recordDispatchFailure(storage, response) {
  const { code } = dispatchDiagnostic(response);
  await storage?.set({
    [LAST_DISPATCH_FAILURE_KEY]: { code: failureMessages.has(code) ? code : "dispatcher-error", at: Date.now() },
  });
}

export async function readDispatchFailure(storage) {
  const data = await storage?.get(LAST_DISPATCH_FAILURE_KEY);
  const failure = data?.[LAST_DISPATCH_FAILURE_KEY];
  if (!failureMessages.has(failure?.code) || !Number.isSafeInteger(failure.at) || failure.at <= 0 || failure.at > Date.now()) return null;
  return { code: failure.code, at: failure.at, message: failureMessages.get(failure.code) };
}
