import { normalizeGroupTitle } from "./lib/normalize.js";
import { selectMatchingGroup } from "./lib/group-selection.js";
import { selectTabForGroup } from "./lib/tab-selection.js";
import { DISPATCH_PROTOCOL_VERSION, FOCUS_GROUP_ACTION, TOGGLE_GROUP_ACTION, failure } from "./lib/dispatch-protocol.js";
import { shouldCollapse } from "./lib/group-operation.js";

// Serialize complete dispatches, including repeated HotKeys, without a timer-based guess.
let dispatchQueue = Promise.resolve();
function queueDispatch(operation) {
  const result = dispatchQueue.then(operation);
  dispatchQueue = result.catch(() => {});
  return result;
}

const LAST_TAB_KEY = "lastTabByGroupTitle";
const SETTINGS_KEY = "settings";
const STARTUP_DEADLINE_KEY = "startupCollapseUntil";
const EXPLICIT_GROUPS_KEY = "startupExplicitGroupIds";
let historyQueue = Promise.resolve();
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

function rememberTab(tab) {
  const result = historyQueue.then(() => rememberTabCore(tab));
  historyQueue = result.catch(() => {});
  return result;
}

async function rememberTabCore(tab) {
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

async function explicitGroups() {
  const store = chrome.storage.session ?? chrome.storage.local;
  return (await store.get(EXPLICIT_GROUPS_KEY))[EXPLICIT_GROUPS_KEY] ?? [];
}

async function protectExplicitGroup(id) {
  const store = chrome.storage.session ?? chrome.storage.local;
  await store.set({ [EXPLICIT_GROUPS_KEY]: [...new Set([...(await explicitGroups()), id])].slice(-1000) });
}

async function focusGroup(groupName, notify = true, action = FOCUS_GROUP_ACTION, dispatcherTabId = null, selected = null) {
  let groups;
  try { groups = selected ? [await chrome.tabGroups.get(selected.groupId)] : await chrome.tabGroups.query({}); }
  catch { return selected ? failure("selection-unavailable", "選択したグループを取得できません。消滅や権限の問題が考えられます。一覧を更新してください。") : failure("group-query-failed", "タブグループ一覧を取得できませんでした。"); }

  const group = selected ? groups.find(group => group.id === selected.groupId && group.windowId === selected.windowId) : selectMatchingGroup(groups, groupName, await getLastFocusedWindowId());
  if (!group) {
    if (notify) await notifyNotFound(groupName);
    return selected ? failure("selection-changed", "選択したグループのウィンドウが変わりました。一覧を更新してください。") : failure("group-not-found", `「${groupName}」は現在開かれていません。`);
  }
  await protectExplicitGroup(group.id);

  if (shouldCollapse(action, group.collapsed)) {
    try {
      await chrome.windows.update(group.windowId, { focused: true });
      // Closing the active Dispatcher can otherwise reactivate a member and undo collapse.
      const outside = (await chrome.tabs.query({ windowId: group.windowId }))
        .filter((tab) => tab.id !== dispatcherTabId && tab.groupId !== group.id && !tab.url?.startsWith(`chrome-extension://${chrome.runtime.id}/dispatch.html`))
        .sort((left, right) => Number(right.active) - Number(left.active) || left.index - right.index)[0];
      if (outside) await chrome.tabs.update(outside.id, { active: true });
      else await chrome.tabs.create({ windowId: group.windowId, url: "about:blank", active: true });
      await chrome.tabGroups.update(group.id, { collapsed: true });
      // Never activate a member tab here: that would immediately expand the group again.
      return { ok: true, code: "collapsed", groupId: group.id, windowId: group.windowId };
    } catch { return failure("group-collapse-failed", "タブグループを折りたためませんでした。"); }
  }

  try { await chrome.tabGroups.update(group.id, { collapsed: false }); }
  catch { return failure("group-expand-failed", "タブグループを展開できませんでした。"); }
  try { await chrome.windows.update(group.windowId, { focused: true }); }
  catch { return failure("window-focus-failed", "Chrome ウィンドウを前面にできませんでした。"); }

  let tabs;
  try { tabs = await chrome.tabs.query({ groupId: group.id }); }
  catch { return failure("tab-query-failed", "グループ内のタブを取得できませんでした。"); }
  await historyQueue;
  const lastTabs = await readLastTabs();
  const tab = selectTabForGroup(tabs, lastTabs[normalizeGroupTitle(group.title)]);
  if (tab?.id == null) return failure("tab-query-failed", "グループ内にタブがありません。");
  try { await chrome.tabs.update(tab.id, { active: true }); }
  catch { return failure("tab-activate-failed", "復帰先タブを選択できませんでした。"); }
  // Tab activation also records history. Do not delay visible success on the
  // redundant persistence write; the next history read waits for this queue.
  void rememberTab(tab);
  return { ok: true, code: "ok", groupTitle: group.title ?? "", groupId: group.id, windowId: group.windowId, tabId: tab.id };
}

async function collapseAll(automatic = false) {
  const groups = await chrome.tabGroups.query({});
  const protectedIds = automatic ? await explicitGroups() : [];
  const targets = groups.filter(group => !protectedIds.includes(group.id));
  const results = await Promise.allSettled(targets.map(async group => {
    try { await chrome.tabGroups.update(group.id, { collapsed: true }); return "succeeded"; }
    catch {
      try { return (await chrome.tabGroups.query({})).some(current => current.id === group.id) ? "failed" : "disappeared"; }
      catch { return "failed"; } // A failed read is not proof of disappearance.
    }
  }));
  const values = results.map(result => result.status === "fulfilled" ? result.value : "failed");
  const succeeded = values.filter(value => value === "succeeded").length;
  const failed = values.filter(value => value === "failed").length;
  const disappeared = values.filter(value => value === "disappeared").length;
  return { ok: failed === 0 && disappeared === 0, code: failed || disappeared ? "partial-collapse" : "ok", count: succeeded, succeeded, failed, disappeared, skipped: groups.length - targets.length, total: groups.length,
    message: `${succeeded} 個を折りたたみました。失敗 ${failed} 個、消滅 ${disappeared} 個。` };
}

chrome.tabs.onActivated.addListener(({ tabId }) => void chrome.tabs.get(tabId).then(rememberTab).catch(() => {}));

chrome.runtime.onStartup.addListener(() => {
  void queueDispatch(async () => {
    const settings = await getSettings();
    if (!settings.collapseOnStartup) return;
    const until = Date.now() + settings.startupCollapseWindowMs;
    await chrome.storage.local.set({ [STARTUP_DEADLINE_KEY]: until });
    await collapseAll(true);
  }).catch(() => {});
});

chrome.tabGroups.onCreated.addListener((group) => {
  void queueDispatch(async () => {
    const [settings, data] = await Promise.all([getSettings(), chrome.storage.local.get(STARTUP_DEADLINE_KEY)]);
    if (settings.collapseOnStartup && Date.now() <= Number(data[STARTUP_DEADLINE_KEY] ?? 0)) {
      if ((await explicitGroups()).includes(group.id)) return;
      return chrome.tabGroups.update(group.id, { collapsed: true });
    }
  }).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "dispatch") {
    if (message.protocolVersion !== DISPATCH_PROTOCOL_VERSION) { sendResponse(failure("invalid-protocol", "Dispatcher protocol v1 が必要です。")); return false; }
    if (![FOCUS_GROUP_ACTION, TOGGLE_GROUP_ACTION].includes(message.action)) { sendResponse(failure("invalid-action", "未対応の action です。")); return false; }
    const groupName = normalizeGroupTitle(message.payload?.groupName);
    if (!groupName) { sendResponse(failure("empty-group-name", "タブグループ名が空です。")); return false; }
    queueDispatch(() => focusGroup(groupName, true, message.action, _sender.tab?.id)).then(sendResponse).catch(() => sendResponse(failure("internal-error", "予期しないエラーが発生しました。")));
    return true;
  }
  if (message?.type === "focus-group") { queueDispatch(() => focusGroup(normalizeGroupTitle(message.groupName), message.notify !== false)).then(sendResponse).catch(() => sendResponse(failure("internal-error", "グループを開けませんでした。更新して再試行してください。"))); return true; }
  if (message?.type === "focus-group-instance") {
    if (!Number.isInteger(message.groupId) || message.groupId < 0 || !Number.isInteger(message.windowId) || message.windowId < 0) { sendResponse(failure("invalid-selection", "一覧の選択が不正です。更新してください。")); return false; }
    queueDispatch(() => focusGroup("", false, FOCUS_GROUP_ACTION, null, message)).then(sendResponse).catch(() => sendResponse(failure("internal-error", "選択したグループを開けませんでした。更新して再試行してください。")));
    return true;
  }
  if (message?.type === "list-groups") { chrome.tabGroups.query({}).then((groups) => sendResponse({ ok: true, groups })).catch(() => sendResponse(failure("group-query-failed", "一覧を取得できませんでした。"))); return true; }
  if (message?.type === "collapse-all") { queueDispatch(() => collapseAll()).then(sendResponse).catch(() => sendResponse(failure("group-query-failed", "折りたためませんでした。更新して再試行してください。"))); return true; }
  return false;
});

