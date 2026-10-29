// Legacy focus-group and Popup always open. Only an explicit pre-launch sample can collapse.
export function shouldCollapse(action, chromeWasForeground, collapsed) {
  return action === "toggle-group" && chromeWasForeground === true && collapsed === false;
}
