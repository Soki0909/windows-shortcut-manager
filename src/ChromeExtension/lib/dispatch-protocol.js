import { normalizeGroupTitle } from "./normalize.js";

export const DISPATCH_PROTOCOL_VERSION = 1;
export const FOCUS_GROUP_ACTION = "focus-group";
export const TOGGLE_GROUP_ACTION = "toggle-group";

export function parseDispatchQuery(search, extensionVersion) {
  const params = new URLSearchParams(search);
  const version = params.get("v");
  const action = params.get("action");
  const groupName = normalizeGroupTitle(params.get("group"));

  if (version !== String(DISPATCH_PROTOCOL_VERSION)) {
    return failure("invalid-protocol", `この拡張はProtocol v1対応です（要求: ${version ?? "未指定"}）。Managerと拡張の対応表を確認してください。`);
  }
  const minimum = params.get("minExtensionVersion");
  if (minimum && extensionVersion) {
    if (!/^\d+\.\d+\.\d+$/.test(minimum)) return failure("invalid-minimum-version", "要求された拡張バージョンが不正です。");
    const required = minimum.split(".").map(Number);
    const current = extensionVersion.split(".").map(Number);
    const difference = current.map((part, i) => part - required[i]).find(part => part !== 0) ?? 0;
    if (difference < 0) return failure("extension-update-required", `この要求には拡張v${minimum}以降が必要です。現在はv${extensionVersion}です。拡張を先に更新してください。`);
  }
  if (action !== FOCUS_GROUP_ACTION && action !== TOGGLE_GROUP_ACTION) {
    return failure("invalid-action", "未対応の Dispatcher action です。");
  }
  if (!groupName) {
    return failure("empty-group-name", "タブグループ名が空です。");
  }
  return {
    ok: true,
    request: {
      type: "dispatch",
      protocolVersion: DISPATCH_PROTOCOL_VERSION,
      action,
      payload: { groupName },
    },
  };
}

export function failure(code, message) {
  return { ok: false, code, message };
}

