import { runtimeRequest } from "./lib/runtime-request.js";

const groupsElement = document.querySelector("#groups");
const statusElement = document.querySelector("#status");
const collapseStartup = document.querySelector("#collapse-startup");
const debugMode = document.querySelector("#debug-mode");
const manifest = chrome.runtime.getManifest();
document.querySelector("#identity").textContent = `v${manifest.version} / Protocol v1 / ${chrome.runtime.id}`;
const send = message => runtimeRequest(value => chrome.runtime.sendMessage(value), message);
let busy = false;
let savedSettings = {};
let settingsLoaded = false;
async function run(operation) {
  if (busy) return;
  busy = true;
  document.querySelectorAll("button,input").forEach(control => { control.disabled = true; });
  try { await operation(); }
  catch { statusElement.textContent = "操作に失敗しました。「更新」で状態を確認して再試行してください。"; }
  finally { busy = false; document.querySelectorAll("button,input").forEach(control => { control.disabled = control.tagName === "INPUT" && !settingsLoaded; }); }
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
  savedSettings = (await chrome.storage.local.get("settings")).settings ?? {};
  settingsLoaded = true; reflectSettings();
}
document.querySelector("#refresh").addEventListener("click", () => void run(async () => {
  if (!settingsLoaded) { try { await loadSettings(); } catch { statusElement.textContent = "設定を読み込めません。設定の変更は停止しています。"; } }
  await refresh();
}));
document.querySelector("#collapse-all").addEventListener("click", () => void run(async () => {
  const result = await send({ type: "collapse-all" });
  statusElement.textContent = result.message ?? (result.ok ? `${result.count} 個を折りたたみました。` : "折りたためませんでした。");
  await refresh();
}));
function reflectSettings() { collapseStartup.checked = savedSettings.collapseOnStartup ?? false; debugMode.checked = savedSettings.debugMode ?? false; }
async function saveSettings() {
  if (!settingsLoaded) return;
  const next = { ...savedSettings, collapseOnStartup: collapseStartup.checked, startupCollapseWindowMs: savedSettings.startupCollapseWindowMs ?? 20000, debugMode: debugMode.checked };
  try { await chrome.storage.local.set({ settings: next }); savedSettings = next; statusElement.textContent = "設定を保存しました。"; }
  catch { reflectSettings(); throw new Error("Settings storage failed"); }
}
collapseStartup.addEventListener("change", () => void run(saveSettings));
debugMode.addEventListener("change", () => void run(saveSettings));
await run(async () => {
  try { await loadSettings(); }
  catch { statusElement.textContent = "設定を読み込めませんでした。グループ一覧は更新して再試行できます。"; }
  await refresh();
});
