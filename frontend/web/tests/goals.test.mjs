import test from "node:test";
import assert from "node:assert/strict";
import { activeGoal, grams, goalRequest, parseGoalJson, remainingGrams, targetDateLabel, validateTarget, visualProgress } from "../src/lib/goals/presentation.ts";
import { createApiClient, ApiError } from "../src/lib/api/client.ts";
test("positive target syntax accepts full precision and rejects scale, zero and overflow", () => {
  for (const v of ["5", "0.00000001", "999999999999.99999999"]) assert.equal(validateTarget(v), undefined);
  for (const v of ["0", "-5", "1.123456789", "1000000000000", "1e3", "NaN"]) assert.ok(validateTarget(v));
});
test("goal JSON preserves exact quantities and unrelated strings", () => {
  const d = parseGoalJson('{"activeGoal":{"targetGrams":999999999999.99999999,"currentHoldingGrams":1.12345678},"text":"targetGrams: 123"}');
  assert.equal(d.activeGoal.targetGrams, "999999999999.99999999"); assert.equal(grams(d.activeGoal.currentHoldingGrams), "1.12345678"); assert.equal(d.text, "targetGrams: 123");
});
test("remaining uses exact integers and clamps over-target", () => {
  assert.equal(remainingGrams("5", "2.25"), "2.75000000"); assert.equal(remainingGrams("5", "6.25"), "0.00000000");
  assert.equal(remainingGrams("999999999999.99999999", "999999999999.99999998"), "0.00000001");
});
test("empty and achieved representations preserve actual progress and cap only bar", () => {
  assert.equal(activeGoal({ goal: null }), null); const g = { isCompleted: true, progressPercentage: "125", visualProgressPercentage: "100" };
  assert.equal(activeGoal(g), g); assert.equal(visualProgress(g), 100); assert.equal(g.progressPercentage, "125");
});
test("optional date is timezone safe UTC midnight", () => {
  assert.deepEqual(goalRequest(" 1.12345678 ", ""), { targetGrams: "1.12345678", targetDateUtc: null });
  assert.equal(goalRequest("5", "2026-12-31").targetDateUtc, "2026-12-31T00:00:00.000Z");
  assert.match(targetDateLabel("2026-12-31T00:00:00.000Z"), /December 31, 2026/);
});
test("create uses shared credentials and CSRF with exact target DTO", async () => {
  const calls = []; const api = createApiClient("/api/v1", async (url, init) => {
    calls.push({ url, init }); if (url.endsWith("antiforgery")) return Response.json({ token: "csrf" });
    return new Response('{"targetGrams":999999999999.99999999}', { status: 201 });
  });
  const result = await api.post("/customer/savings-goal", goalRequest("999999999999.99999999", ""), undefined, parseGoalJson);
  assert.equal(result.targetGrams, "999999999999.99999999"); assert.equal(calls[1].init.credentials, "include");
  assert.equal(calls[1].init.headers["X-CSRF-TOKEN"], "csrf"); assert.equal(JSON.parse(calls[1].init.body).targetGrams, "999999999999.99999999");
});
test("Problem Details failure cannot map to an empty or false zero", async () => {
  const api = createApiClient("/api", async () => Response.json({ code: "ACTIVE_GOAL_ALREADY_EXISTS" }, { status: 409 }));
  await assert.rejects(api.get("/customer/savings-goal", parseGoalJson), e => e instanceof ApiError && e.status === 409 && /already/.test(e.message));
});
import { percentage } from "../src/lib/goals/presentation.ts";
test("percentage text removes only insignificant trailing zeros", () => {
  assert.equal(percentage("20.00000000"), "20"); assert.equal(percentage("125.00"), "125");
  assert.equal(percentage("50.330000"), "50.33"); assert.equal(percentage("0.00000001"), "0.00000001");
});
