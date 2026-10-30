import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { createRequire } from "node:module";
import { execFileSync, spawn } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

// Explicit opt-in E2E: only a newly created profile and the isolated Debug edition.
// PLAYWRIGHT_MODULE_PATH may point at the user's already installed test runtime.
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH ?? "playwright");
const chromePath = path.resolve(process.env.TEST_CHROME_PATH ?? process.argv[2] ?? "");
assert.ok(chromePath.endsWith("chrome.exe"), "Set TEST_CHROME_PATH to official Chrome for Testing chrome.exe");
const root = path.resolve(import.meta.dirname, "..");
const output = path.join(root, "local", `chrome-live-${Date.now()}`);
const profile = path.join(output, "profile");
const extension = path.join(output, "extension");
await fs.mkdir(output, { recursive: true });
await fs.cp(path.join(root, "src/ChromeExtension"), extension, { recursive: true });
await fs.copyFile(path.join(root, "tests/ChromeExtension.Live/harness.html"), path.join(extension, "harness.html"));
await fs.copyFile(path.join(root, "tests/ChromeExtension.Live/harness.js"), path.join(extension, "harness.js"));
const workerFile = path.join(extension, "service-worker.js");
const workerSource = await fs.readFile(workerFile, "utf8");
await fs.writeFile(workerFile, `
// Test-only exception observer; excluded from the distributed extension.
function liveWorkerError(message) {
  chrome.storage.session.get('liveWorkerErrors').then(data => chrome.storage.session.set({ liveWorkerErrors: [...(data.liveWorkerErrors ?? []), message] })).catch(() => {});
}
self.addEventListener('error', event => liveWorkerError(String(event.message)));
self.addEventListener('unhandledrejection', event => liveWorkerError(String(event.reason)));
` + workerSource);
// Observe responses only in the disposable test copy; production source is unchanged.
const dispatchFile = path.join(extension, "dispatch.js");
const dispatchSource = await fs.readFile(dispatchFile, "utf8");
const observedLine = "const response = await chrome.runtime.sendMessage(parsed.request);";
assert.ok(dispatchSource.includes(observedLine));
await fs.writeFile(dispatchFile, dispatchSource.replace(observedLine, observedLine + "\n  await chrome.storage.session.set({ liveLastDispatch: { request: parsed.request, response } });"));
const records = [];
const pageErrors = [];
let context;
let debug;
let cleanupRequired = false;
let originalDebugConfig;
let desktopLink;
let desktopLinkBytes;
let initialExternal;
const debugConfigPath = path.join(process.env.LOCALAPPDATA, "ShortcutManager.Debug/config.json");
const app = path.join(root, "src/ShortcutManager.App/bin/Debug/net10.0-windows/ShortcutManager.Debug.exe");
const driver = path.join(root, "tests/ShortcutManager.LiveDriver/bin/Debug/net10.0-windows/ShortcutManager.LiveDriver.exe");
const externalKeys = scan => scan.items.filter(item => !item.isManaged && item.hotkey).map(item => [item.sourcePath, item.hotkey, item.target, item.arguments]).sort((a, b) => a[0].localeCompare(b[0]));
function run(executable, args) { return execFileSync(executable, args, { windowsHide: true, encoding: "utf8", timeout: 120000 }); }
async function waitFor(test, message, timeout = 15000) {
  const until = Date.now() + timeout;
  while (Date.now() < until) { if (await test()) return; await delay(100); }
  throw new Error(message);
}
async function record(name, check) {
  try { const evidence = await check(); records.push({ name, passed: true, evidence }); console.log(`PASS ${name}`); }
  catch (error) { records.push({ name, passed: false, error: error.stack }); throw error; }
}
async function command(args, name) {
  const result = path.join(output, name + ".json");
  run(app, [...args, "--result", result]);
  const response = JSON.parse(await fs.readFile(result, "utf8"));
  if (args.includes("--scan")) assert.equal(response.ok, true, response.message ?? "Inventory failed");
  return response;
}
try {
  context = await chromium.launchPersistentContext(profile, {
    executablePath: chromePath, headless: false,
    args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`, "--no-first-run", "--no-default-browser-check"],
  });
  context.on("page", page => page.on("pageerror", error => pageErrors.push(String(error))));
  const browserCdp = await context.browser().newBrowserCDPSession();
  const browserPid = (await browserCdp.send("SystemInfo.getProcessInfo")).processInfo.find(process => process.type === "browser").id;
  const worker = context.serviceWorkers()[0] ?? await context.waitForEvent("serviceworker");
  const id = new URL(worker.url()).host;
  const harness = await context.newPage();
  await harness.goto(`chrome-extension://${id}/harness.html`);
  await harness.getByRole("button", { name: "テストグループ作成", exact: true }).click();
  await waitFor(() => harness.evaluate(async () => (await chrome.tabGroups.query({})).length === 1), "Group fixture not created");
  const groupId = await harness.evaluate(async () => (await chrome.tabGroups.query({}))[0].id);
  await harness.evaluate(id => chrome.tabGroups.update(id, { collapsed: true }), groupId);
  const snapshot = () => harness.evaluate(async () => ({ groups: await chrome.tabGroups.query({}), tabs: (await chrome.tabs.query({})).map(({ id, groupId, windowId, active, url, pendingUrl }) => ({ id, groupId, windowId, active, url, pendingUrl })) }));
  const group = async () => (await snapshot()).groups.find(item => item.id === groupId);
  const noDispatcher = async () => !(await snapshot()).tabs.some(tab => (tab.url || tab.pendingUrl)?.includes("/dispatch.html?"));
  async function dispatch(action = "toggle-group", title = "Issue110 test", windowId = null) {
    const url = `chrome-extension://${id}/dispatch.html?v=1&action=${action}&group=${encodeURIComponent(title)}`;
    const tab = await harness.evaluate(({ url, windowId }) => chrome.tabs.create({ url, active: true, ...(windowId === null ? {} : { windowId }) }), { url, windowId });
    await waitFor(() => harness.evaluate(async id => { try { await chrome.tabs.get(id); return false; } catch { return true; } }, tab.id), "Created Dispatcher did not close");
    await waitFor(noDispatcher, "Dispatcher did not close");
    return harness.evaluate(async () => (await chrome.storage.session.get("liveLastDispatch")).liveLastDispatch.response);
  }
  await record("real Chrome: collapsed group opens without foreground input", async () => {
    await dispatch(); assert.equal((await group()).collapsed, false); return await snapshot();
  });
  await record("real Chrome: expanded group collapses without deleting member tabs", async () => {
    await dispatch(); assert.equal((await group()).collapsed, true);
    assert.equal((await snapshot()).tabs.filter(tab => tab.groupId === groupId).length, 2); return await snapshot();
  });
  await record("real Chrome: collapsed group reopens", async () => {
    await dispatch(); assert.equal((await group()).collapsed, false); return await snapshot();
  });
  await record("real Chrome: legacy focus-group remains open-only", async () => {
    await dispatch("focus-group"); assert.equal((await group()).collapsed, false); return await snapshot();
  });
  await record("real Chrome: last-used member is restored", async () => {
    const member = (await snapshot()).tabs.filter(tab => tab.groupId === groupId)[1];
    await harness.evaluate(id => chrome.tabs.update(id, { active: true }), member.id);
    await waitFor(() => harness.evaluate(async id => (await chrome.storage.local.get("lastTabByGroupTitle")).lastTabByGroupTitle?.["Issue110 test"]?.tabId === id, member.id), "Last-used tab not recorded");
    await dispatch(); await dispatch();
    assert.equal((await snapshot()).tabs.find(tab => tab.groupId === groupId && tab.active)?.id, member.id);
    return { restoredTabId: member.id };
  });
  await record("real Chrome: repeated requests alternate without losing tabs", async () => {
    for (let index = 0; index < 6; index++) { await dispatch(); assert.equal((await group()).collapsed, index % 2 === 0); }
    await Promise.all(Array.from({ length: 6 }, () => dispatch()));
    assert.equal((await group()).collapsed, false);
    assert.equal((await snapshot()).tabs.filter(tab => tab.groupId === groupId).length, 2);
    return await snapshot();
  });
  await record("real Chrome: stopped Service Worker restarts and toggles correctly", async () => {
    const versions = new Map();
    const workerCdp = await context.newCDPSession(harness);
    workerCdp.on("ServiceWorker.workerVersionUpdated", event => { for (const version of event.versions) versions.set(version.versionId, version); });
    await workerCdp.send("ServiceWorker.enable");
    await waitFor(() => [...versions.values()].some(version => version.scriptURL === worker.url()), "Worker version not observed");
    const version = [...versions.values()].find(version => version.scriptURL === worker.url());
    await workerCdp.send("ServiceWorker.stopWorker", { versionId: version.versionId });
    await waitFor(() => versions.get(version.versionId)?.runningStatus === "stopped", "Worker did not stop");
    await dispatch(); assert.equal((await group()).collapsed, true);
    await dispatch(); assert.equal((await group()).collapsed, false);
    await workerCdp.detach();
    return { versionId: version.versionId, stoppedObserved: true, state: await snapshot() };
  });
  await record("real Chrome: missing group leaves all existing groups and tabs intact", async () => {
    const before = await snapshot();
    assert.equal((await dispatch("toggle-group", "Missing test group")).code, "group-not-found");
    const after = await snapshot();
    assert.deepEqual(after.groups, before.groups);
    assert.deepEqual(after.tabs.map(tab => tab.id).sort(), before.tabs.map(tab => tab.id).sort());
    return after;
  });
  await record("real Chrome: Popup open remains open-only", async () => {
    const popup = await context.newPage();
    await popup.goto(`chrome-extension://${id}/popup.html`);
    await popup.getByRole("button", { name: "開く", exact: true }).click();
    await waitFor(async () => (await group()).collapsed === false, "Popup did not open group");
    if (!popup.isClosed()) await popup.close();
    return await snapshot();
  });
  await record("real Chrome: duplicate groups select Dispatcher window and preserve other window", async () => {
    const duplicate = await harness.evaluate(async title => {
      const window = await chrome.windows.create({ url: "about:blank", focused: false });
      const groupId = await chrome.tabs.group({ tabIds: [window.tabs[0].id], createProperties: { windowId: window.id } });
      await chrome.tabGroups.update(groupId, { title, collapsed: false });
      return { groupId, windowId: window.id, tabId: window.tabs[0].id };
    }, "Issue110 test");
    try {
      await record("real Popup: each duplicate row selects its own window", async () => {
        const first = await group();
        for (const selected of [{ groupId, windowId: first.windowId }, duplicate]) {
          await harness.evaluate(async ids => { for (const id of ids) await chrome.tabGroups.update(id, { collapsed: true }); }, [groupId, duplicate.groupId]);
          const popup = await context.newPage(); await popup.goto(`chrome-extension://${id}/popup.html`);
          await popup.locator(".group").filter({ hasText: `Window ${selected.windowId} ·` }).getByRole("button", { name: "開く", exact: true }).click();
          await waitFor(async () => !(await snapshot()).groups.find(item => item.id === selected.groupId).collapsed, "Popup selected wrong duplicate");
          assert.equal((await snapshot()).groups.find(item => item.id !== selected.groupId && item.title === "Issue110 test").collapsed, true);
          if (!popup.isClosed()) await popup.close();
        }
        await harness.evaluate(async ids => { for (const id of ids) await chrome.tabGroups.update(id, { collapsed: false }); }, [groupId, duplicate.groupId]);
      });
      await dispatch("toggle-group", "Issue110 test", duplicate.windowId);
      const state = await snapshot();
      assert.equal(state.groups.find(item => item.id === duplicate.groupId).collapsed, true);
      assert.equal(state.groups.find(item => item.id === groupId).collapsed, false);
      assert.ok(state.tabs.some(tab => tab.id === duplicate.tabId));
      await dispatch("toggle-group", "Issue110 test", duplicate.windowId);
      assert.equal((await snapshot()).groups.find(item => item.id === duplicate.groupId).collapsed, false);
      return state;
    } finally { await harness.evaluate(async id => { try { await chrome.windows.remove(id); } catch { /* A failed test may already have closed its test window. */ } }, duplicate.windowId); }
  });
  await record("real Popup: transport/storage failures keep retry available and preserve settings", async () => {
    const rejected = await context.newPage();
    await rejected.addInitScript(() => { chrome.runtime.sendMessage = async () => { throw new Error("isolated transport fault"); }; });
    await rejected.goto(`chrome-extension://${id}/popup.html`);
    await rejected.getByText("拡張との通信に失敗しました。状態を更新して再試行してください。", { exact: true }).waitFor();
    assert.equal(await rejected.getByRole("button", { name: "更新", exact: true }).isEnabled(), true);
    await rejected.close();
    const unavailable = await context.newPage();
    await unavailable.addInitScript(() => { chrome.storage.local.get = async () => { throw new Error("isolated settings read fault"); }; });
    await unavailable.goto(`chrome-extension://${id}/popup.html`);
    await unavailable.getByText("設定を読み込めませんでした。グループ一覧は更新して再試行できます。", { exact: true }).waitFor();
    assert.equal(await unavailable.locator("#collapse-startup").isDisabled(), true);
    assert.equal(await unavailable.getByRole("button", { name: "更新", exact: true }).isEnabled(), true);
    await unavailable.close();
    const unwritable = await context.newPage();
    await unwritable.addInitScript(() => { chrome.storage.local.set = async () => { throw new Error("isolated settings write fault"); }; });
    await unwritable.goto(`chrome-extension://${id}/popup.html`);
    await unwritable.locator("#collapse-startup").click();
    await unwritable.getByText("操作に失敗しました。「更新」で状態を確認して再試行してください。", { exact: true }).waitFor();
    assert.equal(await unwritable.locator("#collapse-startup").isChecked(), false);
    await unwritable.close();
  });
  await record("real Chrome: another selected group is preserved when target collapses", async () => {
    const other = await harness.evaluate(async () => {
      const tab = await chrome.tabs.create({ url: "about:blank", active: true });
      const groupId = await chrome.tabs.group({ tabIds: [tab.id] });
      await chrome.tabGroups.update(groupId, { title: "Other test group", collapsed: false });
      return { tabId: tab.id, groupId };
    });
    try {
      await dispatch(); assert.equal((await group()).collapsed, true);
      assert.ok((await snapshot()).tabs.some(tab => tab.id === other.tabId)); return await snapshot();
    } finally { await harness.evaluate(id => chrome.tabs.remove(id), other.tabId); }
  });

  // Windows .lnk integration uses only an unused key and an initially absent Debug config.
  try {
    originalDebugConfig = await fs.readFile(debugConfigPath);
    const original = JSON.parse(originalDebugConfig);
    assert.deepEqual(original.shortcuts, [], "Refuse to replace a nonempty Debug configuration");
    assert.ok(!original.settings.chrome.extensionId && !original.settings.chrome.executablePath && !original.settings.managedShortcutDirectory, "Refuse to replace configured Debug settings");
  } catch (error) { if (error.code !== "ENOENT") throw error; }
  const scan = await command(["--scan"], "scan");
  initialExternal = externalKeys(scan);
  const key = ["U", "F", "J", "K"].find(key => !scan.items.some(item => item.hotkey?.toUpperCase() === `CTRL+ALT+${key}`));
  assert.ok(key, "No free E2E key");
  const config = { schemaVersion: 1, dispatchProtocolVersion: 1, settings: { chrome: { executablePath: chromePath, extensionId: id } }, shortcuts: [{ id: "39bc83dd-f951-43fc-b446-3fa95344325e", name: "Issue110 live test", enabled: true, hotkey: { modifiers: ["Ctrl", "Alt"], key }, target: { kind: "chromeTabGroup", groupName: "Issue110 test" } }] };
  const configPath = path.join(output, "config.json");
  await fs.writeFile(configPath, JSON.stringify(config));
  const apply = await command(["--apply-config", configPath, "--chrome-test-profile", profile], "apply");
  assert.equal(apply.succeeded, true, apply.message);
  cleanupRequired = true;
  debug = spawn(driver, ["window"], { windowsHide: false, stdio: "ignore" });
  await delay(1000);
  const afterApply = await command(["--scan"], "after-apply-scan");
  const managedLink = afterApply.items.find(item => item.isManaged && item.hotkey?.toUpperCase() === `CTRL+ALT+${key}`);
  assert.ok(managedLink, "Applied .lnk missing from readback");
  assert.equal(managedLink.target.toLowerCase(), chromePath.toLowerCase());
  assert.ok(managedLink.arguments.includes("action=toggle-group") && !managedLink.arguments.includes("foreground="));
  if (process.env.TEST_DESKTOP_HOTKEY === "1") {
    const desktop = run(driver, ["desktop-directory"]).trim();
    desktopLink = path.join(desktop, `Shortcut Manager Debug E2E ${Date.now()}.lnk`);
    await fs.copyFile(managedLink.sourcePath, desktopLink, (await import("node:fs")).constants.COPYFILE_EXCL);
    desktopLinkBytes = await fs.readFile(desktopLink);
    run(driver, ["notify-created", desktopLink]);
  }
  if (process.env.TEST_ALLOW_EXPLORER_RESTART === "1") {
    records.push({ name: "owner-consented Explorer reload after test Apply", passed: true, evidence: JSON.parse(run(driver, ["reload-shell"])) });
  }
  console.log(`Single test-key registration probe: ${run(driver, ["probe-hotkey", key]).trim()}`);
  await record("generated .lnk: direct Shell execution reaches isolated Dispatcher", async () => {
    const newPage = context.waitForEvent("page", { timeout: 10000 });
    run(driver, ["launch-link", managedLink.sourcePath]);
    const dispatcher = await newPage;
    await waitFor(() => dispatcher.isClosed(), "Shell launched Dispatcher did not close");
    return managedLink;
  });
  for (const chromeForeground of [false, true]) {
    await record(`generated .lnk: Chrome foreground=${chromeForeground} still toggles`, async () => {
      await harness.evaluate(id => chrome.tabGroups.update(id, { collapsed: false }), groupId);
      await harness.bringToFront();
      run(driver, ["focus", String(chromeForeground ? browserPid : debug.pid)]);
      const newPage = context.waitForEvent("page", { timeout: 10000 });
      run(driver, ["launch-link", managedLink.sourcePath]);
      const dispatcher = await newPage;
      await waitFor(() => dispatcher.isClosed(), "Native foreground Dispatcher did not close");
      assert.equal((await group()).collapsed, true);
      return await snapshot();
    });
  }
  let manualStep = 0;
  async function hotkey(expectedChrome) {
    const manual = process.env.TEST_MANUAL_HOTKEY === "1";
    const newPage = context.waitForEvent("page", { timeout: manual ? 180000 : 10000 });
    if (manual) {
      if (expectedChrome) {
        await harness.bringToFront();
        await harness.evaluate(() => {
          window.manualReady = false;
          const button = document.createElement("button");
          button.id = "manual-ready";
          button.textContent = "Chrome前面の確認を開始";
          button.style = "font-size:24px;padding:24px;background:#dff5df";
          button.onclick = () => { window.manualReady = true; button.remove(); document.querySelector("#status").textContent = "この画面のまま Ctrl+Alt+U を1回押し、5秒待ってください。"; };
          document.body.prepend(button);
        });
        console.log("MANUAL CLICK READY: click Chrome前面の確認を開始 in isolated harness, then Ctrl+Alt+U");
        await waitFor(() => harness.evaluate(() => window.manualReady), "Manual Chrome-page button was not clicked", 180000);
      }
      const input = JSON.parse(run(driver, ["foreground", chromePath]));
      assert.equal(input.chromeWasForeground, expectedChrome);
      console.log(`MANUAL READY ${++manualStep}/4: Ctrl+Alt+${key}, Chrome foreground=${expectedChrome}`);
    } else {
    const input = JSON.parse(run(driver, ["hotkey", key, chromePath]));
    assert.equal(input.chromeWasForeground, expectedChrome);
    assert.equal(input.keysDown, true, "Windows did not observe the injected chord");
    }
    const dispatcher = await newPage;
    await waitFor(() => dispatcher.isClosed(), "Native HotKey Dispatcher did not close");
    await waitFor(noDispatcher, "Native HotKey Dispatcher residue");
  }
  if (process.env.TEST_SKIP_HOTKEY !== "1") {
  await record("Windows .lnk HotKey: outside Chrome, expanded target collapses", async () => {
    await harness.evaluate(async id => chrome.tabGroups.update(id, { collapsed: false }), groupId);
    run(driver, ["focus", String(debug.pid)]);
    await hotkey(false); assert.equal((await group()).collapsed, true); return { key, state: await snapshot() };
  });
  await record("Windows .lnk HotKey: Chrome foreground collapses", async () => {
    await harness.evaluate(id => chrome.tabGroups.update(id, { collapsed: false }), groupId);
    await harness.bringToFront();
    run(driver, ["focus", String(browserPid)]);
    await hotkey(true); assert.equal((await group()).collapsed, true); return await snapshot();
  });
  await record("Windows .lnk HotKey: Chrome foreground expands", async () => {
    await harness.bringToFront();
    run(driver, ["focus", String(browserPid)]);
    await hotkey(true); assert.equal((await group()).collapsed, false); return await snapshot();
  });
  await record("Windows .lnk HotKey: outside Chrome, collapsed target opens", async () => {
    await harness.evaluate(async id => chrome.tabGroups.update(id, { collapsed: true }), groupId);
    run(driver, ["focus", String(debug.pid)]);
    await hotkey(false); assert.equal((await group()).collapsed, false); return await snapshot();
  });
  }
  await harness.bringToFront();
  await harness.getByRole("button", { name: "状態を更新", exact: true }).click();
  await harness.screenshot({ path: path.join(output, "verified.png"), fullPage: true });
  await record("no unhandled page or Service Worker exceptions", async () => {
    assert.deepEqual(pageErrors, []);
    assert.deepEqual(await harness.evaluate(async () => (await chrome.storage.session.get("liveWorkerErrors")).liveWorkerErrors ?? []), []);
  });
  await record("cold Chrome startup handles absent group without Dispatcher residue", async () => {
    await context.close(); context = null;
    const portFile = path.join(profile, "DevToolsActivePort");
    await fs.unlink(portFile).catch(error => { if (error.code !== "ENOENT") throw error; });
    const coldUri = `chrome-extension://${id}/dispatch.html?v=1&action=toggle-group&group=Cold%20absent%20group`;
    const cold = spawn(chromePath, [`--user-data-dir=${profile}`, "--remote-debugging-port=0", `--load-extension=${extension}`, `--disable-extensions-except=${extension}`, "--no-first-run", "--no-default-browser-check", coldUri], { windowsHide: true, stdio: "ignore" });
    let coldBrowser;
    try {
      await waitFor(async () => { try { await fs.access(portFile); return true; } catch { return false; } }, "Cold Chrome did not start");
      const port = (await fs.readFile(portFile, "utf8")).split(/\r?\n/)[0];
      coldBrowser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
      const observer = await coldBrowser.contexts()[0].newPage(); await observer.goto(`chrome-extension://${id}/harness.html`);
      await waitFor(() => observer.evaluate(async () => { const value = (await chrome.storage.session.get("liveLastDispatch")).liveLastDispatch; return value?.request.payload.groupName === "Cold absent group" && value.response.code === "group-not-found"; }), "Cold response missing");
      await waitFor(() => observer.evaluate(async () => !(await chrome.tabs.query({})).some(tab => (tab.url || tab.pendingUrl)?.includes("/dispatch.html?"))), "Cold Dispatcher residue");
      const workerErrors = await observer.evaluate(async () => (await chrome.storage.session.get("liveWorkerErrors")).liveWorkerErrors ?? []);
      assert.deepEqual(workerErrors, [], "Cold Worker unhandled exceptions");
      assert.deepEqual(pageErrors, [], "Unhandled page exceptions");
      return await observer.evaluate(async () => (await chrome.storage.session.get("liveLastDispatch")).liveLastDispatch);
    } finally { if (coldBrowser) await coldBrowser.close(); cold.kill(); }
  });
} finally {
  if (debug) debug.kill();
  if (desktopLinkBytes) {
    assert.deepEqual(await fs.readFile(desktopLink), desktopLinkBytes, "Refuse to remove a changed test fixture");
    await fs.unlink(desktopLink);
    run(driver, ["notify-deleted", desktopLink]);
  }
  if (cleanupRequired) {
    const empty = path.join(output, "empty.json");
    await fs.writeFile(empty, JSON.stringify({ schemaVersion: 1, dispatchProtocolVersion: 1, settings: { chrome: { extensionId: "" } }, shortcuts: [] }));
    const cleanup = await command(["--apply-config", empty], "cleanup");
    assert.equal(cleanup.succeeded, true, cleanup.message);
    if (process.env.TEST_ALLOW_EXPLORER_RESTART === "1") {
      records.push({ name: "owner-consented Explorer reload after test HotKey removal", passed: true, evidence: JSON.parse(run(driver, ["reload-shell"])) });
    }
    if (originalDebugConfig) await fs.writeFile(debugConfigPath, originalDebugConfig);
    else await fs.unlink(debugConfigPath);
    const finalScan = await command(["--scan"], "cleanup-scan");
    assert.deepEqual(externalKeys(finalScan), initialExternal);
    assert.ok(!finalScan.items.some(item => item.isManaged && item.hotkey));
    records.push({ name: "all external HotKey files unchanged; test keys removed", passed: true });
  }
  if (context) await context.close();
  await fs.writeFile(path.join(output, "evidence.json"), JSON.stringify(records, null, 2));
  console.log(`Evidence: ${output}`);
}
