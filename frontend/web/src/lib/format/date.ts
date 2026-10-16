// Explicit UTC keeps server/client output identical and avoids calendar-day shifts.
const timestamp = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: true, timeZone: "UTC",
});
export function formatDateTime(value: string) { return `${timestamp.format(new Date(value))} UTC`; }
