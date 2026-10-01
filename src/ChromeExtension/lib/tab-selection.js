export function selectTabForGroup(tabs, remembered) {
  if (!tabs.length) return null;
  if (remembered?.tabId != null) {
    const byId = tabs.find((tab) => tab.id === remembered.tabId);
    if (byId) return byId;
  }
  if (remembered?.url) {
    const byUrl = tabs.find((tab) => tab.url === remembered.url);
    if (byUrl) return byUrl;
  }
  return tabs.find((tab) => tab.active) ?? tabs[0];
}

