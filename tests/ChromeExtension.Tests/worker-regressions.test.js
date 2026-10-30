import test from "node:test";
import assert from "node:assert/strict";
import { runtimeRequest } from "../../src/ChromeExtension/lib/runtime-request.js";

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

test("runtime rejection, missing response and timeout remain actionable without automatic replay", async () => {
  assert.equal((await runtimeRequest(() => Promise.reject(new Error("closed")), {})).code, "message-failed");
  assert.equal((await runtimeRequest(() => undefined, {})).code, "invalid-response");
  let calls = 0;
  assert.equal((await runtimeRequest(() => { calls++; return new Promise(() => {}); }, {}, 5)).code, "message-timeout");
  assert.equal(calls, 1);
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
