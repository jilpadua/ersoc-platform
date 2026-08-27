const DEFAULT_TZ = "Asia/Manila";

/** Format an ISO/UTC timestamp in the organization's IANA timezone. */
export function formatOrgDateTime(
  value: string | Date | null | undefined,
  timeZoneId?: string | null
): string {
  if (!value) return "—";
  const date = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) return "—";
  const timeZone = timeZoneId?.trim() || DEFAULT_TZ;
  try {
    return new Intl.DateTimeFormat(undefined, {
      timeZone,
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    }).format(date);
  } catch {
    return date.toLocaleString();
  }
}
