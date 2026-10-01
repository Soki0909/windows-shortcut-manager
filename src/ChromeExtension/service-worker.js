import { normalizeGroupTitle } from "./lib/normalize.js";
import { selectMatchingGroup } from "./lib/group-selection.js";
import { selectTabForGroup } from "./lib/tab-selection.js";
import { DISPATCH_PROTOCOL_VERSION, FOCUS_GROUP_ACTION, failure } from "./lib/dispatch-protocol.js";

const LAST_TAB_KEY = "lastTabByGroupTitle";
const SETTINGS_KEY = "settings";
const STARTUP_DEADLINE_KEY = "startupCollapseUntil";
const DEFAULT_SETTINGS = { collapseOnStartup: false, startupCollapseWindowMs: 20000, debugMode: false };

async function getSettings() {
  const data = await chrome.storage.local.get(SETTINGS_KEY);
  return { ...DEFAULT_SETTINGS, ...(data[SETTINGS_KEY] ?? {}) };
}

async function getLastFocusedWindowId() {
  try {
    return (await chrome.windows.getLastFocused({ windowTypes: ["normal"] }))?.id ?? null;
  } catch { return null; }
}

async function readLastTabs() {
  const data = await chrome.storage.local.get(LAST_TAB_KEY);
  return data[LAST_TAB_KEY] ?? {};
}

async function rememberTab(tab) {
  if (!tab || tab.groupId === chrome.tabGroups.TAB_GROUP_ID_NONE) return;
  try {
    const group = await chrome.tabGroups.get(tab.groupId);
    const title = normalizeGroupTitle(group.title);
    if (!title) return;
    const map = await readLastTabs();
    map[title] = { tabId: tab.id, url: tab.url ?? null, savedAt: Date.now() };
    const entries = Object.entries(map).sort((a, b) => (b[1].savedAt ?? 0) - (a[1].savedAt ?? 0)).slice(0, 100);
    await chrome.storage.local.set({ [LAST_TAB_KEY]: Object.fromEntries(entries) });
  } catch { /* Tabs can disappear during event handling. */ }
}

async function notifyNotFound(groupName) {
  await chrome.notifications.create({
    type: "basic",
    iconUrl: "icons/icon128.png",
    title: "タブグループが見つかりません",
    message: `「${groupName}」は現在 Chrome 上で開かれていません。保存済みでも閉じているグループは開けません。`,
    priority: 1,
  });
}

async function focusGroup(groupName, notify = true) {
  let groups;
  try { groups = await chrome.tabGroups.query({}); }
  catch { return failure("group-query-failed", "タブグループ一覧を取得できませんでした。"); }

  const group = selectMatchingGroup(groups, groupName, await getLastFocusedWindowId());
  if (!group) {
    if (notify) await notifyNotFound(groupName);
    return failure("group-not-found", `「${groupName}」は現在開かれていません。`);
  }

  try { await chrome.tabGroups.update(group.id, { collapsed: false }); }
  catch { return failure("group-expand-failed", "タブグループを展開できませんでした。"); }
  try { await chrome.windows.update(group.windowId, { focused: true }); }
  catch { return failure("window-focus-failed", "Chrome ウィンドウを前面にできませんでした。"); }

  let tabs;
  try { tabs = await chrome.tabs.query({ groupId: group.id }); }
  catch { return failure("tab-query-failed", "グループ内のタブを取得できませんでした。"); }
  const lastTabs = await readLastTabs();
  const tab = selectTabForGroup(tabs, lastTabs[normalizeGroupTitle(group.title)]);
  if (tab?.id == null) return failure("tab-query-failed", "グループ内にタブがありません。");
  try { await chrome.tabs.update(tab.id, { active: true }); }
  catch { return failure("tab-activate-failed", "復帰先タブを選択できませんでした。"); }
  await rememberTab(tab);
  return { ok: true, code: "ok", groupTitle: group.title ?? "", groupId: group.id, windowId: group.windowId, tabId: tab.id };
}

async function collapseAll() {
  const groups = await chrome.tabGroups.query({});
  await Promise.allSettled(groups.map((group) => chrome.tabGroups.update(group.id, { collapsed: true })));
  return groups.length;
}

chrome.tabs.onActivated.addListener(({ tabId }) => void chrome.tabs.get(tabId).then(rememberTab).catch(() => {}));

chrome.runtime.onStartup.addListener(() => {
  void getSettings().then(async (settings) => {
    if (!settings.collapseOnStartup) return;
    const until = Date.now() + settings.startupCollapseWindowMs;
    await chrome.storage.local.set({ [STARTUP_DEADLINE_KEY]: until });
    await collapseAll();
  });
});

chrome.tabGroups.onCreated.addListener((group) => {
  void Promise.all([getSettings(), chrome.storage.local.get(STARTUP_DEADLINE_KEY)]).then(([settings, data]) => {
    if (settings.collapseOnStartup && Date.now() <= Number(data[STARTUP_DEADLINE_KEY] ?? 0)) {
      return chrome.tabGroups.update(group.id, { collapsed: true });
    }
  }).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "dispatch") {
    if (message.protocolVersion !== DISPATCH_PROTOCOL_VERSION) { sendResponse(failure("invalid-protocol", "Dispatcher protocol v1 が必要です。")); return false; }
    if (message.action !== FOCUS_GROUP_ACTION) { sendResponse(failure("invalid-action", "未対応の action です。")); return false; }
    const groupName = normalizeGroupTitle(message.payload?.groupName);
    if (!groupName) { sendResponse(failure("empty-group-name", "タブグループ名が空です。")); return false; }
    focusGroup(groupName).then(sendResponse).catch(() => sendResponse(failure("internal-error", "予期しないエラーが発生しました。")));
    return true;
  }
  if (message?.type === "focus-group") { focusGroup(normalizeGroupTitle(message.groupName), message.notify !== false).then(sendResponse); return true; }
  if (message?.type === "list-groups") { chrome.tabGroups.query({}).then((groups) => sendResponse({ ok: true, groups })).catch(() => sendResponse(failure("group-query-failed", "一覧を取得できませんでした。"))); return true; }
  if (message?.type === "collapse-all") { collapseAll().then((count) => sendResponse({ ok: true, count })).catch(() => sendResponse(failure("group-query-failed", "折りたためませんでした。"))); return true; }
  return false;
});

