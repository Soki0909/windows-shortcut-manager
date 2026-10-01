const STARTUP_COLLAPSE_WINDOW_MS = 20_000;
const STARTUP_COLLAPSE_UNTIL_KEY = "startupCollapseUntil";
const LAST_TAB_KEY = "lastTabByGroupTitle";

function normalizeTitle(value) {
  return (value ?? "").trim().normalize("NFKC");
}

async function getLastFocusedNormalWindowId() {
  try {
    const win = await chrome.windows.getLastFocused({ windowTypes: ["normal"] });
    return win?.id ?? null;
  } catch {
    return null;
  }
}

async function findTargetGroup(title) {
  const target = normalizeTitle(title);
  const groups = await chrome.tabGroups.query({});
  const matches = groups.filter(
    (group) => normalizeTitle(group.title) === target,
  );

  if (matches.length === 0) return null;
  if (matches.length === 1) return matches[0];

  const lastWindowId = await getLastFocusedNormalWindowId();
  return matches.find((group) => group.windowId === lastWindowId) ?? matches[0];
}

async function readLastTabMap() {
  const data = await chrome.storage.local.get(LAST_TAB_KEY);
  return data[LAST_TAB_KEY] ?? {};
}

async function rememberTabForGroup(tab) {
  if (!tab || tab.groupId === chrome.tabGroups.TAB_GROUP_ID_NONE) return;

  try {
    const group = await chrome.tabGroups.get(tab.groupId);
    const title = normalizeTitle(group.title);
    if (!title) return;

    const map = await readLastTabMap();
    map[title] = {
      tabId: tab.id,
      url: tab.url ?? null,
      savedAt: Date.now(),
    };
    await chrome.storage.local.set({ [LAST_TAB_KEY]: map });
  } catch {}
}

async function pickTabForGroup(group, title) {
  const tabs = await chrome.tabs.query({ groupId: group.id });
  if (tabs.length === 0) return null;

  const map = await readLastTabMap();
  const remembered = map[normalizeTitle(title)];

  if (remembered?.tabId != null) {
    const byId = tabs.find((tab) => tab.id === remembered.tabId);
    if (byId) return byId;
  }

  if (remembered?.url) {
    const byUrl = tabs.find((tab) => tab.url === remembered.url);
    if (byUrl) return byUrl;
  }

  const currentlyActive = tabs.find((tab) => tab.active);
  return currentlyActive ?? tabs[0];
}

async function notifyGroupNotFound(title) {
  await chrome.notifications.create({
    type: "basic",
    iconUrl: "icons/icon128.png",
    title: "タブグループが見つかりません",
    message: `「${title}」は現在Chrome上で開かれていません。`,
    priority: 1,
  });
}

async function focusGroupByTitle(title, { notify = true } = {}) {
  const group = await findTargetGroup(title);
  if (!group) {
    if (notify) await notifyGroupNotFound(title);
    return { ok: false, reason: "not-found", title };
  }

  await chrome.tabGroups.update(group.id, { collapsed: false });
  await chrome.windows.update(group.windowId, { focused: true });

  const tab = await pickTabForGroup(group, title);
  if (tab?.id != null) {
    await chrome.tabs.update(tab.id, { active: true });
    await rememberTabForGroup(tab);
  }

  return {
    ok: true,
    title,
    groupId: group.id,
    windowId: group.windowId,
    tabId: tab?.id ?? null,
  };
}

async function collapseAllGroups() {
  const groups = await chrome.tabGroups.query({});
  await Promise.allSettled(
    groups.map((group) => chrome.tabGroups.update(group.id, { collapsed: true })),
  );
  return groups.length;
}

async function beginStartupCollapseWindow() {
  const until = Date.now() + STARTUP_COLLAPSE_WINDOW_MS;
  await chrome.storage.local.set({ [STARTUP_COLLAPSE_UNTIL_KEY]: until });
  await collapseAllGroups();
}

chrome.tabs.onActivated.addListener(async ({ tabId }) => {
  try {
    const tab = await chrome.tabs.get(tabId);
    await rememberTabForGroup(tab);
  } catch {}
});

chrome.runtime.onStartup.addListener(() => {
  void beginStartupCollapseWindow();
});

chrome.tabGroups.onCreated.addListener(async (group) => {
  try {
    const data = await chrome.storage.local.get(STARTUP_COLLAPSE_UNTIL_KEY);
    const until = Number(data[STARTUP_COLLAPSE_UNTIL_KEY] ?? 0);
    if (Date.now() <= until) {
      await chrome.tabGroups.update(group.id, { collapsed: true });
    }
  } catch {}
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "focus-group" && typeof message.title === "string") {
    focusGroupByTitle(message.title, { notify: message.notify !== false })
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, reason: String(error) }));
    return true;
  }

  if (message?.type === "collapse-all") {
    collapseAllGroups()
      .then((count) => sendResponse({ ok: true, count }))
      .catch((error) => sendResponse({ ok: false, reason: String(error) }));
    return true;
  }

  if (message?.type === "list-groups") {
    chrome.tabGroups.query({})
      .then((groups) => sendResponse({
        ok: true,
        groups: groups.map((group) => ({
          id: group.id,
          title: group.title ?? "",
          collapsed: group.collapsed,
          color: group.color,
          windowId: group.windowId,
        })),
      }))
      .catch((error) => sendResponse({ ok: false, reason: String(error) }));
    return true;
  }

  return false;
});
