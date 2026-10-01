const groupsElement = document.querySelector("#groups");
const statusElement = document.querySelector("#status");
const collapseStartup = document.querySelector("#collapse-startup");
const debugMode = document.querySelector("#debug-mode");
const manifest = chrome.runtime.getManifest();
document.querySelector("#identity").textContent = `v${manifest.version} / Protocol v1 / ${chrome.runtime.id}`;

async function send(message) { return chrome.runtime.sendMessage(message); }
async function refresh() {
  const response = await send({ type: "list-groups" });
  if (!response?.ok) { groupsElement.textContent = response?.message ?? "一覧を取得できませんでした。"; return; }
  groupsElement.replaceChildren();
  const groups = [...response.groups].sort((a, b) => (a.title ?? "").localeCompare(b.title ?? "", "ja") || a.windowId - b.windowId || a.id - b.id);
  if (!groups.length) { groupsElement.textContent = "現在開いているタブグループはありません。"; return; }
  for (const group of groups) {
    const row = document.createElement("div"); row.className = "group";
    const name = document.createElement("span"); name.textContent = group.title || "(名称なし)";
    const state = document.createElement("span"); state.className = "state"; state.textContent = `Window ${group.windowId} · ${group.collapsed ? "折りたたみ" : "展開中"}`;
    const open = document.createElement("button"); open.textContent = "開く";
    open.addEventListener("click", async () => {
      const result = await send({ type: "focus-group", groupName: group.title ?? "" });
      statusElement.textContent = result.ok ? `「${group.title}」を開きました。` : result.message;
      if (result.ok) window.close();
    });
    row.append(name, state, open); groupsElement.append(row);
  }
}

document.querySelector("#refresh").addEventListener("click", refresh);
document.querySelector("#collapse-all").addEventListener("click", async () => { const result = await send({ type: "collapse-all" }); statusElement.textContent = result.ok ? `${result.count} 個を折りたたみました。` : result.message; await refresh(); });
async function saveSettings() {
  const data = await chrome.storage.local.get("settings");
  await chrome.storage.local.set({ settings: { collapseOnStartup: collapseStartup.checked, startupCollapseWindowMs: data.settings?.startupCollapseWindowMs ?? 20000, debugMode: debugMode.checked } });
}
collapseStartup.addEventListener("change", saveSettings);
debugMode.addEventListener("change", saveSettings);

const stored = await chrome.storage.local.get("settings");
collapseStartup.checked = stored.settings?.collapseOnStartup ?? false;
debugMode.checked = stored.settings?.debugMode ?? false;
await refresh();
