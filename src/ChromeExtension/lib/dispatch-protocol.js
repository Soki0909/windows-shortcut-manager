import { normalizeGroupTitle } from "./normalize.js";

export const DISPATCH_PROTOCOL_VERSION = 1;
export const FOCUS_GROUP_ACTION = "focus-group";
export const TOGGLE_GROUP_ACTION = "toggle-group";

export function parseDispatchQuery(search) {
  const params = new URLSearchParams(search);
  const version = params.get("v");
  const action = params.get("action");
  const groupName = normalizeGroupTitle(params.get("group"));

  if (version !== String(DISPATCH_PROTOCOL_VERSION)) {
    return failure("invalid-protocol", "Dispatcher protocol v1 が必要です。");
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

