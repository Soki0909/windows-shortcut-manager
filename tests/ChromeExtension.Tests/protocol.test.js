import test from "node:test";
import assert from "node:assert/strict";
import { normalizeGroupTitle } from "../../src/ChromeExtension/lib/normalize.js";
import { parseDispatchQuery } from "../../src/ChromeExtension/lib/dispatch-protocol.js";
import { selectMatchingGroup } from "../../src/ChromeExtension/lib/group-selection.js";
import { selectTabForGroup } from "../../src/ChromeExtension/lib/tab-selection.js";

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
