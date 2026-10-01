import { parseDispatchQuery } from "./lib/dispatch-protocol.js";

const status = document.querySelector("#status");
const details = document.querySelector("#details");

async function closeSelf() {
  try {
    const tab = await chrome.tabs.getCurrent();
    if (tab?.id != null) await chrome.tabs.remove(tab.id);
  } catch { /* The user may close the tab first. */ }
}

async function run() {
  const parsed = parseDispatchQuery(location.search);
  if (!parsed.ok) {
    status.textContent = parsed.message;
    details.hidden = false;
    details.textContent = parsed.code;
    setTimeout(closeSelf, 1500);
    return;
  }

  const groupName = parsed.request.payload.groupName;
  status.textContent = `「${groupName}」を開いています…`;
  const response = await chrome.runtime.sendMessage(parsed.request);
  if (response?.ok) {
    status.textContent = `「${groupName}」へ移動しました。`;
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
