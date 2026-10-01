export function normalizeGroupTitle(value) {
  return String(value ?? "").trim().normalize("NFKC");
}

