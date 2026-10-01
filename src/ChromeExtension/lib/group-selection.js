import { normalizeGroupTitle } from "./normalize.js";

export function selectMatchingGroup(groups, requestedTitle, lastFocusedWindowId = null) {
  const requested = normalizeGroupTitle(requestedTitle);
  const matches = groups
    .filter((group) => normalizeGroupTitle(group.title) === requested)
    .sort((left, right) => left.windowId - right.windowId || left.id - right.id);

  if (matches.length === 0) return null;
  return matches.find((group) => group.windowId === lastFocusedWindowId) ?? matches[0];
}

