import test from "node:test";
import assert from "node:assert/strict";

test("ID copy works without Worker settings and clipboard rejection selects the ID", { timeout: 5000 }, async () => {
  const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
  const controls = new Map();
  for (const id of ["extension-id", "copy-id", "copy-status", "groups", "status", "identity", "collapse-startup", "debug-mode", "remember-urls", "confirm-clear", "clear-history", "history-count", "refresh", "collapse-all"]) {
    const input = ["extension-id", "collapse-startup", "debug-mode", "remember-urls", "confirm-clear"].includes(id);
    controls.set(id, { id, tagName: input ? "INPUT" : "BUTTON", disabled: false, checked: false, listeners: {},
      addEventListener(event, action) { this.listeners[event] = action; },
      focus() { this.focused = true; }, select() { this.selected = true; } });
  }
  const extensionId = "abcdefghijklmnopabcdefghijklmnop";
  let clipboard; let reject = false; let finishSettings;
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: { clipboard: { async writeText(value) { if (reject) throw new Error("denied"); clipboard = value; } } } });
  globalThis.document = { querySelector: selector => controls.get(selector.slice(1)), querySelectorAll: () => [...controls.values()] };
  globalThis.chrome = { runtime: { id: extensionId, getManifest: () => ({ version: "0.3.1" }), async sendMessage(message) {
    if (message.type === "privacy-status") return new Promise(resolve => { finishSettings = () => resolve({ ok: false }); });
    return { ok: false, message: "fixture unavailable" };
  } } };
  try {
    const loading = import("../../src/ChromeExtension/popup.js?onboarding");
    for (let attempt = 0; !finishSettings && attempt < 100; attempt++) await new Promise(resolve => setTimeout(resolve, 5));
    assert.equal(typeof finishSettings, "function");
    assert.equal(controls.get("extension-id").disabled, false);
    assert.equal(controls.get("copy-id").disabled, false);
    await controls.get("copy-id").listeners.click();
    assert.equal(clipboard, extensionId);
    assert.match(controls.get("copy-status").textContent, /コピーしました/);
    finishSettings(); await loading;
    assert.equal(controls.get("remember-urls").disabled, true);
    assert.equal(controls.get("extension-id").disabled, false);
    assert.equal(controls.get("copy-id").disabled, false);
    reject = true; await controls.get("copy-id").listeners.click();
    assert.equal(controls.get("extension-id").focused, true);
    assert.equal(controls.get("extension-id").selected, true);
    assert.match(controls.get("copy-status").textContent, /Ctrl\+C/);
    assert.equal(controls.get("extension-id").value, extensionId);
  } finally {
    delete globalThis.chrome; delete globalThis.document;
    if (originalNavigator) Object.defineProperty(globalThis, "navigator", originalNavigator); else delete globalThis.navigator;
  }
});
