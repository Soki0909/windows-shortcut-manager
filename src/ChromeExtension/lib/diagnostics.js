// Never serialize arbitrary exception/response text or the URL recovery map.
export function dispatchDiagnostic(response) {
  return { ok: response?.ok === true, code: typeof response?.code === "string" && /^[a-z-]{1,64}$/.test(response.code) ? response.code : "dispatcher-error" };
}
