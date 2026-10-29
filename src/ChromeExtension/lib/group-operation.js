// Owner approved foreground-independent toggling on 2026-10-29.
export function shouldCollapse(action, collapsed) {
  return action === "toggle-group" && collapsed === false;
}
