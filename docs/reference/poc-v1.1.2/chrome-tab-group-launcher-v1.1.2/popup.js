const groupsEl = document.querySelector("#groups");
const statusEl = document.querySelector("#status");
const extensionIdEl = document.querySelector("#extension-id");

function setStatus(text) {
  statusEl.textContent = text;
}

async function send(message) {
  return chrome.runtime.sendMessage(message);
}

async function refreshGroups() {
  const response = await send({ type: "list-groups" });
  if (!response?.ok) {
    groupsEl.textContent = "グループ一覧を取得できませんでした。";
    return;
  }

  const groups = response.groups ?? [];
  if (groups.length === 0) {
    groupsEl.textContent = "現在開いているタブグループはありません。";
    return;
  }

  groupsEl.textContent = "";
  for (const group of groups) {
    const row = document.createElement("div");
    row.className = "group";

    const name = document.createElement("span");
    name.textContent = group.title || "(名称なし)";

    const state = document.createElement("span");
    state.className = "state";
    state.textContent = group.collapsed ? "折りたたみ" : "展開中";

    row.append(name, state);
    groupsEl.append(row);
  }
}

document.querySelectorAll("button[data-title]").forEach((button) => {
  button.addEventListener("click", async () => {
    const title = button.dataset.title;
    const response = await send({ type: "focus-group", title });
    if (response?.ok) {
      setStatus(`「${title}」を開きました。`);
      window.close();
    } else {
      setStatus(`「${title}」が現在開かれていません。`);
    }
  });
});

document.querySelector("#collapse-all").addEventListener("click", async () => {
  const response = await send({ type: "collapse-all" });
  if (response?.ok) {
    setStatus(`${response.count} 個のグループを折りたたみました。`);
    await refreshGroups();
  } else {
    setStatus("折りたたみに失敗しました。");
  }
});

extensionIdEl.textContent = `Extension ID: ${chrome.runtime.id}`;
void refreshGroups();
