const title = "Issue110 test";
const status = document.querySelector("#status");
async function refresh() {
  const groups = await chrome.tabGroups.query({});
  const tabs = await chrome.tabs.query({});
  status.textContent = JSON.stringify({ extensionId: chrome.runtime.id, groups, tabs: tabs.map(({ id, groupId, windowId, active, url }) => ({ id, groupId, windowId, active, url })) }, null, 2);
}
document.querySelector("#setup").onclick = async () => {
  const current = await chrome.tabs.getCurrent();
  const one = await chrome.tabs.create({ windowId: current.windowId, url: "https://example.com/", active: false });
  const two = await chrome.tabs.create({ windowId: current.windowId, url: "https://example.org/", active: false });
  const id = await chrome.tabs.group({ tabIds: [one.id, two.id] });
  await chrome.tabGroups.update(id, { title, collapsed: false });
  await refresh();
};
document.querySelector("#duplicate").onclick = async () => {
  const window = await chrome.windows.create({ url: "about:blank", focused: false });
  const id = await chrome.tabs.group({ tabIds: [window.tabs[0].id] });
  await chrome.tabGroups.update(id, { title, collapsed: false });
  await refresh();
};
for (const [id, collapsed] of [["collapse", true], ["expand", false]]) {
  document.getElementById(id).onclick = async () => {
    const group = (await chrome.tabGroups.query({})).sort((a, b) => a.id - b.id)[0];
    await chrome.tabGroups.update(group.id, { collapsed });
    await refresh();
  };
}
document.querySelector("#refresh").onclick = refresh;
await refresh();
