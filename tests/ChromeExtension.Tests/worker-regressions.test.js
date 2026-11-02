import test from "node:test";
import assert from "node:assert/strict";
import { runtimeRequest } from "../../src/ChromeExtension/lib/runtime-request.js";
import { dispatchDiagnostic } from "../../src/ChromeExtension/lib/diagnostics.js";

let serial = 0;
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function fixture() {
  const hooks = {}; const local = {}; const session = {};
  const groups = [{ id: 1, windowId: 10, title: "same", collapsed: true }, { id: 2, windowId: 20, title: "same", collapsed: true }];
  const tabs = [{ id: 11, groupId: 1, windowId: 10, url: "https://example.com/1", index: 0 }, { id: 22, groupId: 2, windowId: 20, url: "https://example.com/2", index: 0 }];
  const api = {
    runtime: { id: "fixture", onMessage: { addListener(fn) { hooks.message = fn; } }, onStartup: { addListener(fn) { hooks.startup = fn; } } },
    storage: {
      local: { async get(key) { return { [key]: structuredClone(local[key]) }; }, async set(value) { Object.assign(local, structuredClone(value)); } },
      session: { async get(key) { return { [key]: structuredClone(session[key]) }; }, async set(value) { Object.assign(session, structuredClone(value)); } },
    },
    tabGroups: {
      TAB_GROUP_ID_NONE: -1,
      async query() { return groups.map(group => ({ ...group })); },
      async get(id) { const group = groups.find(group => group.id === id); if (!group) throw new Error("gone"); return { ...group }; },
      async update(id, update) { const group = groups.find(group => group.id === id); if (!group) throw new Error("gone"); Object.assign(group, update); return { ...group }; },
      onCreated: { addListener(fn) { hooks.created = fn; } },
    },
    windows: { async getLastFocused() { return { id: 10 }; }, async update(id) { hooks.focused = id; } },
    tabs: {
      onActivated: { addListener(fn) { hooks.activated = fn; } },
      async get(id) { return tabs.find(tab => tab.id === id); },
      async query(query) { return tabs.filter(tab => query.groupId != null ? tab.groupId === query.groupId : tab.windowId === query.windowId); },
      async update(id) { hooks.active = id; }, async create() { return {}; },
    },
    notifications: { async create() {} },
  };
  globalThis.chrome = api;
  await import(`../../src/ChromeExtension/service-worker.js?regression=${++serial}`);
  const send = message => new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("Worker did not respond")), 1000);
    hooks.message(message, {}, value => { clearTimeout(timer); resolve(value); });
  });
  return { api, hooks, local, session, groups, tabs, send, async close() { await wait(20); delete globalThis.chrome; } };
}

test("privacy defaults preserve URL assistance, while status and diagnostics never return stored URLs", async () => {
  const f = await fixture();
  try {
    f.hooks.activated({ tabId: 11 });
    const status = await f.send({ type: "privacy-status" });
    assert.equal(status.settings.rememberTabUrls, true);
    assert.equal(status.historyCount, 1); assert.equal(status.urlCount, 1);
    assert.equal(f.local.lastTabByGroupTitle.same.url, "https://example.com/1");
    assert.equal(JSON.stringify(status).includes("example.com"), false);
    assert.deepEqual(dispatchDiagnostic({ ok: false, code: "tab-query-failed", url: "https://private.invalid", message: "private", groupTitle: "private" }), { ok: false, code: "tab-query-failed" });
    assert.equal(JSON.stringify(dispatchDiagnostic({ code: "https://private.invalid" })).includes("private"), false);
  } finally { await f.close(); }
});

test("URL opt-out waits for earlier writes, removes prior URLs and survives Worker reload", async () => {
  const f = await fixture();
  let release;
  try {
    f.local.settings = { debugMode: true, retainedOption: "keep" };
    const set = f.api.storage.local.set;
    let started;
    const firstWrite = new Promise(resolve => { started = resolve; });
    f.api.storage.local.set = async value => {
      if (release === undefined) { const blocked = new Promise(resolve => { release = resolve; }); started(); await blocked; }
      return set(value);
    };
    f.hooks.activated({ tabId: 11 }); await firstWrite;
    const disabled = f.send({ type: "update-settings", patch: { rememberTabUrls: false } });
    release();
    assert.equal((await disabled).ok, true);
    assert.equal(f.local.lastTabByGroupTitle.same.url, undefined);
    assert.equal(f.local.settings.retainedOption, "keep"); assert.equal(f.local.settings.debugMode, true);
    await import(`../../src/ChromeExtension/service-worker.js?privacy-restart=${++serial}`);
    f.hooks.activated({ tabId: 22 }); await f.send({ type: "privacy-status" });
    assert.equal(JSON.stringify(f.local.lastTabByGroupTitle).includes("url"), false);
    assert.equal((await f.send({ type: "focus-group-instance", groupId: 1, windowId: 10 })).ok, true);
  } finally { release?.(); await f.close(); }
});

test("history clear is ordered after activation, keeps settings/tabs and preserves basic switching", async () => {
  const f = await fixture();
  try {
    f.local.settings = { rememberTabUrls: false, collapseOnStartup: true };
    f.hooks.activated({ tabId: 11 });
    const result = await f.send({ type: "clear-tab-history" });
    assert.equal(result.ok, true); assert.equal(result.historyCount, 0);
    assert.deepEqual(f.local.lastTabByGroupTitle, {});
    assert.equal(f.local.settings.collapseOnStartup, true); assert.equal(f.tabs.length, 2); assert.equal(f.groups.length, 2);
    assert.equal((await f.send({ type: "focus-group-instance", groupId: 2, windowId: 20 })).ok, true);
    await f.send({ type: "privacy-status" });
    assert.equal(f.local.lastTabByGroupTitle.same.tabId, 22); assert.equal(f.local.lastTabByGroupTitle.same.url, undefined);
  } finally { await f.close(); }
});

test("privacy storage failures are visible, invalid patches do not overwrite settings or history", async () => {
  const f = await fixture();
  try {
    f.hooks.activated({ tabId: 11 }); await f.send({ type: "privacy-status" });
    const before = structuredClone(f.local);
    for (const patch of [{ rememberTabUrls: "false" }, { unknown: true }, null, []]) assert.equal((await f.send({ type: "update-settings", patch })).code, "invalid-settings");
    assert.deepEqual(f.local, before);
    f.api.storage.local.set = async () => { throw new Error("quota"); };
    assert.equal((await f.send({ type: "update-settings", patch: { rememberTabUrls: false } })).code, "history-storage-failed");
    assert.equal((await f.send({ type: "clear-tab-history" })).code, "history-storage-failed");
    assert.deepEqual(f.local, before);
  } finally { await f.close(); }
});

test("special group title stays an own history key and URL assistance can be re-enabled", async () => {
  const f = await fixture();
  try {
    f.groups[0].title = "__proto__";
    await f.send({ type: "update-settings", patch: { rememberTabUrls: false } });
    f.hooks.activated({ tabId: 11 }); await f.send({ type: "privacy-status" });
    assert.equal(Object.hasOwn(f.local.lastTabByGroupTitle, "__proto__"), true);
    assert.equal(f.local.lastTabByGroupTitle.__proto__.url, undefined);
    await f.send({ type: "update-settings", patch: { rememberTabUrls: true } });
    f.hooks.activated({ tabId: 11 }); await f.send({ type: "privacy-status" });
    assert.equal(f.local.lastTabByGroupTitle.__proto__.url, "https://example.com/1");
  } finally { await f.close(); }
});

test("runtime rejection, missing response and timeout remain actionable without automatic replay", async () => {
  assert.equal((await runtimeRequest(() => Promise.reject(new Error("closed")), {})).code, "message-failed");
  assert.equal((await runtimeRequest(() => undefined, {})).code, "invalid-response");
  let calls = 0;
  assert.equal((await runtimeRequest(() => { calls++; return new Promise(() => {}); }, {}, 5)).code, "message-timeout");
  assert.equal(calls, 1);
});

test("Popup requires explicit clear confirmation, sends a URL-only patch and disables settings after a failed write", async () => {
  const controls = new Map(); const calls = [];
  for (const id of ["groups", "status", "identity", "collapse-startup", "debug-mode", "remember-urls", "confirm-clear", "clear-history", "history-count", "refresh", "collapse-all"]) {
    controls.set(id, { tagName: ["collapse-startup", "debug-mode", "remember-urls", "confirm-clear"].includes(id) ? "INPUT" : ["clear-history", "refresh", "collapse-all"].includes(id) ? "BUTTON" : "DIV",
      checked: false, disabled: false, textContent: "", listeners: {}, addEventListener(event, fn) { this.listeners[event] = fn; }, replaceChildren() {} });
  }
  const settings = { collapseOnStartup: false, debugMode: false, rememberTabUrls: true };
  let historyCount = 1; let failed = false;
  globalThis.document = { querySelector(selector) { return controls.get(selector.slice(1)); }, querySelectorAll() { return [...controls.values()].filter(node => ["INPUT", "BUTTON"].includes(node.tagName)); } };
  globalThis.chrome = { runtime: { id: "fixture", getManifest: () => ({ version: "0.3.1" }), async sendMessage(message) {
    calls.push(message);
    if (message.type === "list-groups") return { ok: true, groups: [] };
    if (message.type === "update-settings") { if (failed) return { ok: false }; Object.assign(settings, message.patch); }
    if (message.type === "clear-tab-history") historyCount = 0;
    return { ok: true, settings: { ...settings }, historyCount, urlCount: settings.rememberTabUrls ? historyCount : 0 };
  } } };
  const trigger = async (id, event) => { controls.get(id).listeners[event](); await wait(0); };
  try {
    await import(`../../src/ChromeExtension/popup.js?privacy-ui=${++serial}`);
    assert.equal(controls.get("clear-history").disabled, true);
    await trigger("clear-history", "click");
    assert.equal(calls.some(call => call.type === "clear-tab-history"), false);
    controls.get("confirm-clear").checked = true; await trigger("confirm-clear", "change");
    assert.equal(controls.get("clear-history").disabled, false);
    await trigger("clear-history", "click");
    assert.equal(calls.filter(call => call.type === "clear-tab-history").length, 1);
    assert.equal(controls.get("confirm-clear").checked, false); assert.equal(controls.get("clear-history").disabled, true);
    controls.get("remember-urls").checked = false; await trigger("remember-urls", "change");
    assert.deepEqual(calls.find(call => call.type === "update-settings").patch, { rememberTabUrls: false });
    assert.match(controls.get("history-count").textContent, /URL補助 0/);
    failed = true; controls.get("debug-mode").checked = true; await trigger("debug-mode", "change");
    assert.equal(controls.get("debug-mode").checked, false); assert.equal(controls.get("remember-urls").disabled, true);
    assert.match(controls.get("status").textContent, /更新/);
    await trigger("refresh", "click"); assert.equal(controls.get("remember-urls").disabled, false);
  } finally { delete globalThis.chrome; delete globalThis.document; }
});

test("Popup instance selection opens each same-name group and rejects vanished/moved selection", async () => {
  const f = await fixture();
  try {
    for (const [groupId, windowId, tabId] of [[2, 20, 22], [1, 10, 11]]) {
      assert.equal((await f.send({ type: "focus-group-instance", groupId, windowId })).ok, true);
      assert.equal(f.hooks.focused, windowId); assert.equal(f.hooks.active, tabId);
    }
    assert.equal((await f.send({ type: "focus-group-instance", groupId: 2, windowId: 10 })).ok, false);
    f.groups.splice(1, 1);
    assert.equal((await f.send({ type: "focus-group-instance", groupId: 2, windowId: 20 })).ok, false);
    assert.equal((await f.send({ type: "focus-group-instance", groupId: "2", windowId: 20 })).code, "invalid-selection");
  } finally { await f.close(); }
});

test("legacy focus and list return failures for storage, query and notification errors", async () => {
  const f = await fixture();
  try {
    f.api.storage.local.get = async () => { throw new Error("storage unavailable"); };
    assert.equal((await f.send({ type: "focus-group", groupName: "same" })).ok, false);
    f.api.tabGroups.query = async () => { throw new Error("query denied"); };
    assert.equal((await f.send({ type: "list-groups" })).code, "group-query-failed");
    f.api.tabGroups.query = async () => [];
    f.api.notifications.create = async () => { throw new Error("notifications denied"); };
    assert.equal((await f.send({ type: "focus-group", groupName: "missing" })).ok, false);
  } finally { await f.close(); }
});

test("collapse-all reports successful, failed and disappeared groups separately", async () => {
  const f = await fixture();
  try {
    f.groups.push({ id: 3, windowId: 30, title: "gone" });
    const update = f.api.tabGroups.update;
    f.api.tabGroups.update = async (id, value) => {
      if (id === 2) throw new Error("denied");
      if (id === 3) { f.groups.splice(f.groups.findIndex(group => group.id === 3), 1); throw new Error("gone"); }
      return update(id, value);
    };
    const response = await f.send({ type: "collapse-all" });
    assert.equal(response.ok, false); assert.equal(response.count, 1);
    assert.equal(response.succeeded, 1); assert.equal(response.failed, 1); assert.equal(response.disappeared, 1);
    let queryCalls = 0;
    f.api.tabGroups.query = async () => { if (++queryCalls > 1) throw new Error("recheck denied"); return [{ id: 2 }]; };
    const uncertain = await f.send({ type: "collapse-all" });
    assert.equal(uncertain.failed, 1); assert.equal(uncertain.disappeared, 0);
  } finally { await f.close(); }
});

test("overlapping activation history preserves both groups and the 100-title bound", async () => {
  const f = await fixture();
  try {
    f.groups[1].title = "other";
    f.local.lastTabByGroupTitle = Object.fromEntries(Array.from({ length: 100 }, (_, i) => [`old-${i}`, { savedAt: i }]));
    const get = f.api.storage.local.get;
    f.api.storage.local.get = async key => { const result = await get(key); await wait(3); return result; };
    f.hooks.activated({ tabId: 11 }); f.hooks.activated({ tabId: 22 });
    // A foreground operation waits for earlier history mutations before reading.
    await f.send({ type: "focus-group-instance", groupId: 1, windowId: 10 });
    assert.equal(f.local.lastTabByGroupTitle.same.tabId, 11);
    assert.equal(f.local.lastTabByGroupTitle.other.tabId, 22);
    assert.equal(Object.keys(f.local.lastTabByGroupTitle).length, 100);
  } finally { await f.close(); }
});

test("late startup and created events do not undo explicit opens across windows", async () => {
  const f = await fixture();
  try {
    f.local.settings = { collapseOnStartup: true, startupCollapseWindowMs: 20000 };
    await f.send({ type: "focus-group-instance", groupId: 1, windowId: 10 });
    f.hooks.startup(); f.hooks.created(f.groups[0]);
    await f.send({ type: "focus-group-instance", groupId: 2, windowId: 20 });
    assert.equal(f.groups[0].collapsed, false); assert.equal(f.groups[1].collapsed, false);
    f.hooks.created(f.groups[1]);
    await f.send({ type: "focus-group-instance", groupId: 2, windowId: 20 });
    assert.equal(f.groups[1].collapsed, false);
    await import(`../../src/ChromeExtension/service-worker.js?regression-restart=${++serial}`);
    f.hooks.created(f.groups[0]);
    await f.send({ type: "focus-group-instance", groupId: 2, windowId: 20 });
    assert.equal(f.groups[0].collapsed, false);
  } finally { await f.close(); }
});

test("visible focus response does not wait for a redundant history persistence write", async () => {
  const f = await fixture();
  let release;
  try {
    const set = f.api.storage.local.set;
    f.api.storage.local.set = async value => { await new Promise(resolve => { release = resolve; }); return set(value); };
    const response = await f.send({ type: "focus-group-instance", groupId: 1, windowId: 10 });
    assert.equal(response.ok, true); assert.equal(f.hooks.active, 11);
    await wait(0); assert.equal(typeof release, "function");
    assert.equal(f.local.lastTabByGroupTitle, undefined);
  } finally { release?.(); await f.close(); }
});

test("warm explicit operations reuse session protection without repeated storage calls", async () => {
  const f = await fixture();
  try {
    let reads = 0; let writes = 0;
    const get = f.api.storage.session.get; const set = f.api.storage.session.set;
    f.api.storage.session.get = async key => { reads++; return get(key); };
    f.api.storage.session.set = async data => { writes++; return set(data); };
    for (let i = 0; i < 4; i++) assert.equal((await f.send({ type: "focus-group-instance", groupId: 1, windowId: 10 })).ok, true);
    assert.equal(reads, 1); assert.equal(writes, 1);
  } finally { await f.close(); }
});

test("protection cap suppresses automatic collapse instead of forgetting prior intent", async () => {
  const f = await fixture();
  try {
    f.session.startupExplicitGroupIds = Array.from({ length: 1000 }, (_, i) => i + 100);
    f.local.settings = { collapseOnStartup: true };
    await f.send({ type: "focus-group-instance", groupId: 1, windowId: 10 });
    assert.deepEqual(f.session.startupExplicitGroupIds, [-1]);
    f.hooks.startup(); f.hooks.created(f.groups[0]);
    await f.send({ type: "focus-group-instance", groupId: 2, windowId: 20 });
    assert.equal(f.groups[0].collapsed, false);
  } finally { await f.close(); }
});
