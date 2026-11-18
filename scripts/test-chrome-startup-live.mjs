import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { createRequire } from "node:module";
import { setTimeout as delay } from "node:timers/promises";

// Opt-in Chrome for Testing only. Every run uses a new, private profile.
const chrome = process.env.TEST_CHROME_PATH;
assert.ok(chrome && path.isAbsolute(chrome) && path.basename(chrome).toLowerCase() === "chrome.exe",
  "Set TEST_CHROME_PATH to the absolute path of official Chrome for Testing chrome.exe.");
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE_PATH ?? "playwright");
const root = path.resolve(import.meta.dirname, "..");
const local = path.join(root, "local");
await fs.mkdir(local, { recursive: true });
const output = await fs.mkdtemp(path.join(local, "chrome-startup-"));
const profile = path.join(output, "profile");
const extension = path.join(output, "extension");
const profileDefault = path.join(profile, "Default");
const evidence = {};
const titles = ["Startup fixture A", "Startup fixture B"];
let context;

async function waitFor(check, message, timeout = 10000) {
  const until = Date.now() + timeout;
  do {
    const result = await check();
    if (result) return result;
    await delay(100);
  } while (Date.now() < until);
  throw new Error(message);
}

async function launch(installing) {
  context = await chromium.launchPersistentContext(profile, {
    executablePath: chrome,
    headless: true,
    ignoreDefaultArgs: ["--disable-extensions", "about:blank"],
    // CfT requires this DevTools switch to keep its CDP-installed unpacked
    // extension enabled. Only the first session calls loadUnpacked.
    args: ["--enable-unsafe-extension-debugging", ...(installing ? [] : ["--restore-last-session"])],
  });
  const cdp = await context.browser().newBrowserCDPSession();
  const pid = (await cdp.send("SystemInfo.getProcessInfo")).processInfo.find(process => process.type === "browser")?.id;
  assert.ok(Number.isInteger(pid), "Isolated browser PID was not observed");
  return { cdp, pid };
}

async function closeAndConfirm({ pid }) {
  await context.close();
  context = null;
  await waitFor(() => {
    try { process.kill(pid, 0); return false; }
    catch (error) { if (error.code === "ESRCH") return true; throw error; }
  }, "Isolated browser process was still running after graceful close");
}

const state = page => page.evaluate(async () => ({
  groups: await chrome.tabGroups.query({}),
  tabs: (await chrome.tabs.query({})).map(({ groupId, windowId, url, pendingUrl }) => ({ groupId, windowId, url, pendingUrl })),
  marker: (await chrome.storage.session.get("liveStartupReceivedAt")).liveStartupReceivedAt,
  protection: (await chrome.storage.session.get("startupExplicitGroupIds")).startupExplicitGroupIds ?? [],
  deadline: (await chrome.storage.local.get("startupCollapseUntil")).startupCollapseUntil,
}));
const named = (snapshot, title) => snapshot.groups.find(group => group.title === title);
const memberUrls = title => [1, 2].map(number => "about:blank#fixture-" + encodeURIComponent(title.toLowerCase()) + number);

async function makeGroup(page, title, secondWindow = false) {
  const members = await page.evaluate(async ({ urls, secondWindow }) => {
    const first = secondWindow
      ? (await chrome.windows.create({ url: "about:blank", focused: false })).tabs[0]
      : await chrome.tabs.getCurrent();
    return Promise.all(urls.map(async url => {
      const tab = await chrome.tabs.create({ windowId: first.windowId, url, active: false });
      return { id: tab.id, url };
    }));
  }, { urls: memberUrls(title), secondWindow });
  // Session restore saves committed navigations. A returned tabs.create is not
  // proof the member URL has committed before Chrome is closed.
  await waitFor(() => page.evaluate(async expected => {
    const ready = await Promise.all(expected.map(async ({ id, url }) => {
      const tab = await chrome.tabs.get(id);
      return tab.url === url && tab.status === "complete";
    }));
    return ready.every(Boolean);
  }, members), `Fixture tabs did not commit for ${title}`);
  await page.evaluate(async ({ title, members }) => {
    const { windowId } = await chrome.tabs.get(members[0].id);
    const id = await chrome.tabs.group({ tabIds: members.map(tab => tab.id), createProperties: { windowId } });
    await chrome.tabGroups.update(id, { title });
  }, { title, members });
}

async function observeStartup(page, startedAfter, previousDeadline) {
  let snapshot;
  const observed = await waitFor(async () => {
    snapshot = await state(page);
    evidence.lastStartupObservation = snapshot;
    return snapshot.marker >= startedAfter && snapshot.deadline > (previousDeadline ?? 0) &&
      snapshot.deadline > Date.now() ? snapshot : null;
  }, "Fresh Chrome onStartup marker and newly generated deadline were not both observed");
  const restored = await waitFor(async () => {
    snapshot = await state(page);
    evidence.lastStartupObservation = snapshot;
    return titles.every(title => named(snapshot, title)?.collapsed) ? snapshot : null;
  }, "Both restored groups did not collapse");
  assert.equal(restored.marker, observed.marker);
  assert.equal(restored.deadline, observed.deadline);
  assert.notEqual(named(restored, titles[0]).windowId, named(restored, titles[1]).windowId);
  for (const title of titles) {
    const group = named(restored, title);
    const urls = restored.tabs.filter(tab => tab.groupId === group.id && tab.windowId === group.windowId).map(tab => tab.url).sort();
    assert.deepEqual(urls, memberUrls(title));
  }
  delete evidence.lastStartupObservation;
  return restored;
}

try {
  evidence.stage = "initial installation";
  await fs.mkdir(profileDefault, { recursive: true });
  await fs.writeFile(path.join(profileDefault, "Preferences"), JSON.stringify({
    session: { restore_on_startup: 1 },
    extensions: { ui: { developer_mode: true } },
  }));
  await fs.cp(path.join(root, "src/ChromeExtension"), extension, { recursive: true });
  await fs.writeFile(path.join(extension, "startup-observer.html"), "<!doctype html><title>Startup test observer</title>\n");
  // Observe only the real browser event. The production handler owns the deadline.
  await fs.appendFile(path.join(extension, "service-worker.js"),
    "\nchrome.runtime.onStartup.addListener(() => { void chrome.storage.session.set({ liveStartupReceivedAt: Date.now() }); });\n");

  const initial = await launch(true);
  evidence.browser = (await initial.cdp.send("Browser.getVersion")).product;
  const { id } = await initial.cdp.send("Extensions.loadUnpacked", { path: extension });
  const origin = `chrome-extension://${id}`;
  const popupUrl = origin + "/popup.html";
  const observerUrl = origin + "/startup-observer.html";
  async function openPage(url) {
    const page = await context.newPage();
    await page.goto(url);
    return page;
  }
  const popup = await openPage(popupUrl);
  await popup.locator("#collapse-startup").check();
  await popup.waitForFunction(async () => (await chrome.storage.local.get("settings")).settings?.collapseOnStartup === true);
  let observer = await openPage(observerUrl);
  await makeGroup(observer, titles[0]);
  await makeGroup(observer, titles[1], true);
  const initialState = await state(observer);
  assert.ok(titles.every(title => named(initialState, title)?.collapsed === false));
  assert.equal(initialState.marker, undefined);
  assert.equal(initialState.deadline, undefined);
  await popup.close();
  await closeAndConfirm(initial);

  evidence.stage = "first profile restart";
  const start = Date.now();
  const second = await launch(false);
  observer = await openPage(observerUrl);
  const restored = await observeStartup(observer, start);
  assert.deepEqual(restored.protection, []);
  evidence.startup = { receivedAt: restored.marker, deadline: restored.deadline, restoredGroups: 2, windows: 2 };
  console.log("PASS real onStartup and collapse of restored groups in two windows");

  // A real created event, Dispatcher URI and Popup click overlap in the live window.
  evidence.stage = "explicit operations during the startup window";
  assert.ok(Date.now() < restored.deadline);
  const otherTitle = "Startup late fixture";
  const dispatchUrl = origin + "/dispatch.html?v=1&action=toggle-group&group=" + encodeURIComponent(titles[0]);
  const controlPopup = await openPage(popupUrl);
  await Promise.all([
    makeGroup(observer, otherTitle, true),
    observer.evaluate(url => chrome.tabs.create({ url }), dispatchUrl),
    controlPopup.locator(".group").filter({ hasText: titles[1] }).getByRole("button", { name: "開く", exact: true }).click(),
  ]);
  const protectedState = await waitFor(async () => {
    const snapshot = await state(observer);
    const explicit = titles.map(title => named(snapshot, title));
    return explicit.every(group => group?.collapsed === false && snapshot.protection.includes(group.id)) &&
      named(snapshot, otherTitle)?.collapsed && !snapshot.tabs.some(tab => tab.url === dispatchUrl || tab.pendingUrl === dispatchUrl) ? snapshot : null;
  }, "Explicit Dispatcher and Popup opens did not win during startup folding");
  assert.ok(Date.now() < restored.deadline, "Explicit operation evidence arrived after the startup window");
  if (!controlPopup.isClosed()) await controlPopup.close();
  console.log("PASS late group collapsed while real Dispatcher and Popup kept explicit groups open");

  evidence.stage = "worker stop and restart";
  const versions = new Map();
  const workerCdp = await context.newCDPSession(observer);
  workerCdp.on("ServiceWorker.workerVersionUpdated", ({ versions: updated }) => {
    for (const version of updated) versions.set(version.versionId, version);
  });
  await workerCdp.send("ServiceWorker.enable");
  const worker = await waitFor(() => [...versions.values()].find(version =>
    version.scriptURL === origin + "/service-worker.js" && version.runningStatus === "running"), "Running extension worker not observed");
  await workerCdp.send("ServiceWorker.stopWorker", { versionId: worker.versionId });
  await waitFor(() => versions.get(worker.versionId)?.runningStatus === "stopped", "Extension worker did not stop");
  const afterStop = "Startup after worker stop";
  await makeGroup(observer, afterStop);
  const afterWorker = await waitFor(async () => {
    const snapshot = await state(observer);
    return versions.get(worker.versionId)?.runningStatus === "running" && named(snapshot, afterStop)?.collapsed && snapshot;
  }, "A real created event did not wake the worker and collapse the group");
  assert.ok(Date.now() < restored.deadline, "Worker restart exceeded the startup window");
  assert.equal(afterWorker.marker, restored.marker);
  assert.deepEqual(afterWorker.protection, protectedState.protection);
  assert.ok(titles.every(title => named(afterWorker, title)?.collapsed === false));
  await workerCdp.detach();
  evidence.explicit = { protectedGroups: 2, workerStopped: true, newGroupFoldedAfterWorkerRestart: true };
  console.log("PASS stopped worker resumed startup folding with explicit protection intact");
  await closeAndConfirm(second);

  evidence.stage = "second profile restart";
  const nextStart = Date.now();
  const third = await launch(false);
  observer = await openPage(observerUrl);
  const next = await observeStartup(observer, nextStart, restored.deadline);
  assert.deepEqual(next.protection, []);
  evidence.nextSession = { receivedAt: next.marker, deadline: next.deadline, resetProtection: true, restoredGroups: 2 };
  await closeAndConfirm(third);
  evidence.stage = "complete";
  evidence.passed = true;
  console.log("PASS next Chrome session reset explicit protection and folded both restored groups");
} catch (error) {
  evidence.passed = false;
  evidence.error = String(error);
  throw error;
} finally {
  try {
    await context?.close();
  } finally {
    await fs.writeFile(path.join(output, "result.json"), JSON.stringify(evidence, null, 2));
    console.log(`Evidence: ${path.join(output, "result.json")}`);
  }
}
