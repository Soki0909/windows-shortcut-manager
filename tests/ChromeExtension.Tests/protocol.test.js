import test from "node:test";
import assert from "node:assert/strict";
import { normalizeGroupTitle } from "../../src/ChromeExtension/lib/normalize.js";
import { parseDispatchQuery } from "../../src/ChromeExtension/lib/dispatch-protocol.js";
import { selectMatchingGroup } from "../../src/ChromeExtension/lib/group-selection.js";
import { selectTabForGroup } from "../../src/ChromeExtension/lib/tab-selection.js";
import { shouldCollapse } from "../../src/ChromeExtension/lib/group-operation.js";

test("toggle requires an explicit foreground sample, preserving legacy focus", () => {
  assert.equal(parseDispatchQuery("?v=1&action=toggle-group&group=x").code, "invalid-foreground");
  assert.equal(parseDispatchQuery("?v=1&action=toggle-group&group=x&foreground=chrome").request.payload.chromeWasForeground, true);
  assert.equal(parseDispatchQuery("?v=1&action=toggle-group&group=x&foreground=other").request.payload.chromeWasForeground, false);
  for (const collapsed of [true, false]) {
    assert.equal(shouldCollapse("focus-group", true, collapsed), false);
    assert.equal(shouldCollapse("toggle-group", false, collapsed), false);
  }
  assert.equal(shouldCollapse("toggle-group", true, true), false);
  assert.equal(shouldCollapse("toggle-group", true, false), true);
});

test("normalizes whitespace and Unicode NFKC", () => assert.equal(normalizeGroupTitle(" ７脱 "), "7脱"));
test("parses Japanese group", () => assert.equal(parseDispatchQuery("?v=1&action=focus-group&group=%E7%A0%94%E7%A9%B6").request.payload.groupName, "研究"));
test("parses spaces and emoji", () => assert.equal(parseDispatchQuery("?v=1&action=focus-group&group=hello%20%F0%9F%9A%80").request.payload.groupName, "hello 🚀"));
test("rejects unknown protocol", () => assert.equal(parseDispatchQuery("?v=2&action=focus-group&group=x").code, "invalid-protocol"));
test("rejects unknown action", () => assert.equal(parseDispatchQuery("?v=1&action=other&group=x").code, "invalid-action"));
test("rejects empty group", () => assert.equal(parseDispatchQuery("?v=1&action=focus-group&group=%20").code, "empty-group-name"));
test("selects matching group in last focused window", () => {
  const groups = [{ id: 4, windowId: 3, title: "研究" }, { id: 2, windowId: 1, title: "研究" }];
  assert.equal(selectMatchingGroup(groups, "研究", 3).id, 4);
});
test("selects deterministic first duplicate", () => {
  const groups = [{ id: 4, windowId: 3, title: "研究" }, { id: 9, windowId: 1, title: "研究" }, { id: 2, windowId: 1, title: "研究" }];
  assert.equal(selectMatchingGroup(groups, "研究", null).id, 2);
});
test("selects remembered tab by id then URL", () => {
  const tabs = [{ id: 1, url: "a", active: true }, { id: 2, url: "b", active: false }];
  assert.equal(selectTabForGroup(tabs, { tabId: 2, url: "a" }).id, 2);
  assert.equal(selectTabForGroup(tabs, { tabId: 9, url: "b" }).id, 2);
});
test("falls back to active then first tab", () => {
  assert.equal(selectTabForGroup([{ id: 1 }, { id: 2, active: true }], null).id, 2);
  assert.equal(selectTabForGroup([{ id: 1 }, { id: 2 }], null).id, 1);
});

test("worker dispatch opens from outside, alternates in Chrome, and survives worker reload", async () => {
  const events = [];
  const group = { id: 7, windowId: 2, title: "test", collapsed: false };
  const member = { id: 10, groupId: 7, windowId: 2, url: "https://example.com/", index: 0 };
  const outside = { id: 11, groupId: -1, windowId: 2, url: "about:blank", index: 1 };
  let listener;
  let tabs = [member, outside, { id: 99, groupId: -1, windowId: 2, url: "chrome-extension://test-id/dispatch.html", index: 2, active: true }];
  const storage = {};
  globalThis.chrome = {
    runtime: { id: "test-id", onMessage: { addListener(value) { listener = value; } }, onStartup: { addListener() {} } },
    storage: { local: { async get(key) { return { [key]: storage[key] }; }, async set(value) { Object.assign(storage, value); } } },
    windows: { async getLastFocused() { return { id: 2 }; }, async update() { events.push("window"); } },
    tabGroups: { TAB_GROUP_ID_NONE: -1, async query() { return [group]; }, async get() { return group; }, async update(_id, update) { group.collapsed = update.collapsed; events.push(update.collapsed ? "collapse" : "expand"); }, onCreated: { addListener() {} } },
    tabs: {
      onActivated: { addListener() {} },
      async query(query) { return query.groupId != null ? [member] : tabs; },
      async update(id) { events.push(`activate:${id}`); },
      async create() { events.push("blank"); return { id: 12 }; },
    },
    notifications: { async create() {} },
  };
  const dispatch = (foreground, action = "toggle-group") => new Promise((resolve) => {
    assert.equal(listener({ type: "dispatch", protocolVersion: 1, action, payload: { groupName: "test", chromeWasForeground: foreground } }, { tab: { id: 99 } }, resolve), true);
  });
  try {
    await import(`../../src/ChromeExtension/service-worker.js?test=${Date.now()}`);
    assert.equal((await dispatch(false)).code, "ok");
    assert.equal(group.collapsed, false);
    events.length = 0;
    assert.equal((await dispatch(true)).code, "collapsed");
    assert.deepEqual(events, ["window", "activate:11", "collapse"]);
    assert.equal((await dispatch(true)).code, "ok");
    assert.equal(group.collapsed, false);
    await Promise.all([dispatch(true), dispatch(true)]);
    assert.equal(group.collapsed, false);
    await import(`../../src/ChromeExtension/service-worker.js?restart=${Date.now()}`);
    assert.equal((await dispatch(true)).code, "collapsed");
    assert.equal((await dispatch(false)).code, "ok");
    assert.equal((await dispatch(true, "focus-group")).code, "ok");
    tabs = [member, tabs[2]];
    events.length = 0;
    assert.equal((await dispatch(true)).code, "collapsed");
    assert.deepEqual(events, ["window", "blank", "collapse"]);
  } finally { delete globalThis.chrome; }
});
