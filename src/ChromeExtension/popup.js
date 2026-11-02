import { runtimeRequest } from "./lib/runtime-request.js";

const groupsElement = document.querySelector("#groups");
const statusElement = document.querySelector("#status");
const collapseStartup = document.querySelector("#collapse-startup");
const debugMode = document.querySelector("#debug-mode");
const rememberUrls = document.querySelector("#remember-urls");
const confirmClear = document.querySelector("#confirm-clear");
const clearHistory = document.querySelector("#clear-history");
const manifest = chrome.runtime.getManifest();
document.querySelector("#identity").textContent = `v${manifest.version} / Protocol v1`;
const extensionId = document.querySelector("#extension-id");
extensionId.value = chrome.runtime.id;
document.querySelector("#copy-id").addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText(chrome.runtime.id);
    document.querySelector("#copy-status").textContent = "拡張IDをコピーしました。Managerの設定へ貼り付けてください。";
  } catch {
    extensionId.focus(); extensionId.select();
    document.querySelector("#copy-status").textContent = "コピーできませんでした。選択したIDをCtrl+Cでコピーしてください。";
  }
});
const send = message => runtimeRequest(value => chrome.runtime.sendMessage(value), message);
let busy = false;
let savedSettings = {};
let settingsLoaded = false;
async function run(operation) {
  if (busy) return;
  busy = true;
  document.querySelectorAll("button,input").forEach(control => { if (control !== extensionId && control.id !== "copy-id") control.disabled = true; });
  try { await operation(); }
  catch { statusElement.textContent = "操作に失敗しました。「更新」で状態を確認して再試行してください。"; }
  finally { busy = false; document.querySelectorAll("button,input").forEach(control => { control.disabled = control !== extensionId && control.tagName === "INPUT" && !settingsLoaded; }); clearHistory.disabled = !settingsLoaded || !confirmClear.checked; }
}
async function refresh() {
  const response = await send({ type: "list-groups" });
  if (!response.ok) { groupsElement.textContent = response.message; return; }
  groupsElement.replaceChildren();
  const groups = [...response.groups].sort((a, b) => (a.title ?? "").localeCompare(b.title ?? "", "ja") || a.windowId - b.windowId || a.id - b.id);
  if (!groups.length) { groupsElement.textContent = "現在開いているタブグループはありません。"; return; }
  for (const group of groups) {
    const row = document.createElement("div"); row.className = "group";
    const name = document.createElement("span"); name.textContent = group.title || "(名称なし)";
    const state = document.createElement("span"); state.className = "state"; state.textContent = `Window ${group.windowId} · ${group.collapsed ? "折りたたみ" : "展開中"}`;
    const open = document.createElement("button"); open.textContent = "開く";
    open.addEventListener("click", () => void run(async () => {
      const result = await send({ type: "focus-group-instance", groupId: group.id, windowId: group.windowId });
      statusElement.textContent = result.ok ? `「${group.title}」を開きました。` : result.message;
      if (result.ok) window.close();
    }));
    row.append(name, state, open); groupsElement.append(row);
  }
}
async function loadSettings() {
  const result = await send({ type: "privacy-status" });
  if (!result.ok) throw new Error("Settings unavailable");
  reflectPrivacy(result);
}
function reflectPrivacy(result) {
  savedSettings = result.settings;
  settingsLoaded = true; reflectSettings();
  document.querySelector("#history-count").textContent = `保存 ${result.historyCount} グループ / URL補助 ${result.urlCount} 件（最大100グループ）`;
}
document.querySelector("#refresh").addEventListener("click", () => void run(async () => {
  try { await loadSettings(); } catch { settingsLoaded = false; statusElement.textContent = "設定を読み込めません。設定の変更は停止しています。"; }
  await refresh();
}));
document.querySelector("#collapse-all").addEventListener("click", () => void run(async () => {
  const result = await send({ type: "collapse-all" });
  statusElement.textContent = result.message ?? (result.ok ? `${result.count} 個を折りたたみました。` : "折りたためませんでした。");
  await refresh();
}));
function reflectSettings() { collapseStartup.checked = savedSettings.collapseOnStartup ?? false; debugMode.checked = savedSettings.debugMode ?? false; rememberUrls.checked = savedSettings.rememberTabUrls ?? true; }
async function saveSettings(patch) {
  if (!settingsLoaded) return;
  try {
    const result = await send({ type: "update-settings", patch });
    if (!result.ok) throw new Error("Settings storage failed");
    reflectPrivacy(result); statusElement.textContent = "設定を保存しました。";
  } catch { reflectSettings(); settingsLoaded = false; throw new Error("Settings storage failed"); }
}
collapseStartup.addEventListener("change", () => void run(() => saveSettings({ collapseOnStartup: collapseStartup.checked })));
debugMode.addEventListener("change", () => void run(() => saveSettings({ debugMode: debugMode.checked })));
rememberUrls.addEventListener("change", () => void run(() => saveSettings({ rememberTabUrls: rememberUrls.checked })));
confirmClear.addEventListener("change", () => { clearHistory.disabled = busy || !settingsLoaded || !confirmClear.checked; });
clearHistory.addEventListener("click", () => void run(async () => {
  if (!settingsLoaded || !confirmClear.checked) return;
  const result = await send({ type: "clear-tab-history" });
  if (!result.ok) { settingsLoaded = false; throw new Error("History clear failed"); }
  reflectPrivacy(result); confirmClear.checked = false;
  statusElement.textContent = "タブ復帰履歴を消去しました。開いているタブやグループは保持しています。";
}));
await run(async () => {
  try { await loadSettings(); }
  catch { statusElement.textContent = "設定を読み込めませんでした。グループ一覧は更新して再試行できます。"; }
  await refresh();
});
