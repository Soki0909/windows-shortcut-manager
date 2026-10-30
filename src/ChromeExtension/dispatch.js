import { parseDispatchQuery, TOGGLE_GROUP_ACTION } from "./lib/dispatch-protocol.js";

const status = document.querySelector("#status");
const details = document.querySelector("#details");

async function closeSelf() {
  try {
    const tab = await chrome.tabs.getCurrent();
    if (tab?.id != null) await chrome.tabs.remove(tab.id);
  } catch { /* The user may close the tab first. */ }
}

async function run() {
  const parsed = parseDispatchQuery(location.search, chrome.runtime.getManifest().version);
  if (!parsed.ok) {
    status.textContent = parsed.message;
    details.hidden = false;
    details.textContent = parsed.code;
    setTimeout(closeSelf, 1500);
    return;
  }

  const groupName = parsed.request.payload.groupName;
  status.textContent = parsed.request.action === TOGGLE_GROUP_ACTION ? `「${groupName}」を切り替えています…` : `「${groupName}」を開いています…`;
  const response = await chrome.runtime.sendMessage(parsed.request);
  if (response?.ok) {
    status.textContent = response.code === "collapsed" ? `「${groupName}」を折りたたみました。` : `「${groupName}」へ移動しました。`;
    await closeSelf();
    return;
  }

  status.textContent = response?.message ?? "タブグループを開けませんでした。";
  const stored = await chrome.storage.local.get("settings");
  if (stored.settings?.debugMode) {
    details.hidden = false;
    details.textContent = JSON.stringify(response, null, 2);
  } else {
    setTimeout(closeSelf, 1500);
  }
}

run().catch((error) => {
  status.textContent = "Dispatcher でエラーが発生しました。";
  details.hidden = false;
  details.textContent = String(error);
  setTimeout(closeSelf, 1500);
});
