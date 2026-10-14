// Preserve decimal JSON tokens for goal quantities before JavaScript can round them.
export function parseGoalJson<T>(text: string): T {
  return JSON.parse(text.replace(/("(?:\\.|[^"\\])*")(\s*:\s*)(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|"(?:\\.|[^"\\])*"/g,
    (token, key, separator, value) => key && ["targetGrams", "currentHoldingGrams", "progressRatio", "progressPercentage", "visualProgressPercentage"].includes(JSON.parse(key))
      ? `${key}${separator}${JSON.stringify(value)}` : token)) as T;
}
export type SavingsGoal = {
  goalId: string; targetGrams: string; targetDateUtc: string | null;
  currentHoldingGrams: string; progressRatio: string; progressPercentage: string;
  visualProgressPercentage: string; isCompleted: boolean; createdAtUtc: string;
};
export type GoalResult = SavingsGoal | { goal: null };
export function activeGoal(result: GoalResult): SavingsGoal | null { return "goal" in result ? null : result; }
export function validateTarget(value: string): string | undefined {
  if (!/^\d{1,12}(\.\d{1,8})?$/.test(value.trim())) return "Enter grams with up to 12 integer and 8 decimal places.";
  if (!/[1-9]/.test(value)) return "Enter a target greater than zero.";
}
export function grams(value: string): string {
  const [whole, fraction = ""] = value.split(".");
  return `${whole}.${fraction.padEnd(8, "0")}`;
}
export function remainingGrams(target: string, holding: string): string {
  const units = (value: string) => BigInt(grams(value).replace(".", ""));
  const remaining = units(target) - units(holding);
  const digits = (remaining > BigInt(0) ? remaining : BigInt(0)).toString().padStart(9, "0");
  return `${digits.slice(0, -8)}.${digits.slice(-8)}`;
}
export function visualProgress(goal: SavingsGoal): number {
  // Presentation width only; authoritative calculations are server-side decimal.
  return Math.max(0, Math.min(100, Number(goal.visualProgressPercentage)));
}
export function goalRequest(target: string, date: string) {
  return { targetGrams: target.trim(), targetDateUtc: date ? `${date}T00:00:00.000Z` : null };
}
export function targetDateLabel(utc: string): string {
  return new Intl.DateTimeFormat("en", { dateStyle: "long", timeZone: "UTC" }).format(new Date(utc));
}
export function percentage(value: string): string {
  return value.replace(/(\.\d*?[1-9])0+$|\.0+$/, "$1");
}
