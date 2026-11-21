import { parseDispatchQuery, TOGGLE_GROUP_ACTION } from "./lib/dispatch-protocol.js";
import { dispatchDiagnostic, recordDispatchFailure } from "./lib/diagnostics.js";
import { runtimeRequest } from "./lib/runtime-request.js";

const status = document.querySelector("#status");
const details = document.querySelector("#details");
const actions = document.querySelector("#failure-actions");
const FAILURE_DISPLAY_MS = 15000;
document.querySelector("#close-dispatch").addEventListener("click", () => void closeSelf());

async function closeSelf() {
  try {
    const tab = await chrome.tabs.getCurrent();
    if (tab?.id != null) await chrome.tabs.remove(tab.id);
  } catch { /* The user may close the tab first. */ }
}

function showFailure(response) {
  status.textContent = response?.message ?? "タブグループを開けませんでした。";
  actions.hidden = false;
  setTimeout(closeSelf, FAILURE_DISPLAY_MS);
  void recordDispatchFailure(chrome.storage?.session, response).catch(() => {});
  void chrome.storage.local.get("settings").then(stored => {
    if (!stored.settings?.debugMode) return;
    details.hidden = false;
    details.textContent = JSON.stringify(dispatchDiagnostic(response), null, 2);
  }).catch(() => {});
}

async function run() {
  const parsed = parseDispatchQuery(location.search, chrome.runtime.getManifest().version);
  if (!parsed.ok) {
    showFailure(parsed);
    return;
  }

  const groupName = parsed.request.payload.groupName;
  status.textContent = parsed.request.action === TOGGLE_GROUP_ACTION ? `「${groupName}」を切り替えています…` : `「${groupName}」を開いています…`;
  const response = await runtimeRequest(message => chrome.runtime.sendMessage(message), parsed.request);
  if (response?.ok) {
    status.textContent = response.code === "collapsed" ? `「${groupName}」を折りたたみました。` : `「${groupName}」へ移動しました。`;
    await closeSelf();
    return;
  }

  showFailure(response);
}

await run().catch(() => showFailure({ code: "dispatcher-error" }));
