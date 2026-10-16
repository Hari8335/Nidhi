"use client";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { Alert, Button, Card, FormField, Input, LoadingState } from "@/components/ui/primitives";
import { GoalCard } from "./goal-card";
import { api, ApiError } from "@/lib/api/client";
import { activeGoal, goalRequest, parseGoalJson, validateTarget, type GoalResult, type SavingsGoal } from "@/lib/goals/presentation";

export function GoalsPage() {
  const [goal, setGoal] = useState<SavingsGoal | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [loadFailed, setLoadFailed] = useState(false);
  const [pending, setPending] = useState(false);
  const [target, setTarget] = useState("");
  const [date, setDate] = useState("");
  const [fields, setFields] = useState<Record<string, string>>({});
  const [status, setStatus] = useState("");
  const heading = useRef<HTMLHeadingElement>(null);
  const load = useCallback(async () => {
    setLoading(true); setError(""); setLoadFailed(false);
    try { setGoal(activeGoal(await api.get<GoalResult>("/customer/savings-goal", parseGoalJson<GoalResult>))); }
    catch (e) { setLoadFailed(true); setError(e instanceof ApiError ? e.message : "Your goal is unavailable. Please retry."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);
  const createLock = useRef(false);
  async function create(event: FormEvent) {
    event.preventDefault();
    if (createLock.current) return;
    const invalid = validateTarget(target);
    setFields(invalid ? { targetGrams: invalid } : {});
    if (invalid) { document.getElementById("targetGrams")?.focus(); return; }
    createLock.current = true; setPending(true); setError("");
    try {
      setGoal(await api.post<SavingsGoal>("/customer/savings-goal", goalRequest(target, date), undefined, parseGoalJson<SavingsGoal>));
      setStatus("Savings goal created."); heading.current?.focus();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Your goal could not be created. Please reload before retrying.");
      if (e instanceof ApiError && e.problem.errors) setFields(Object.fromEntries(Object.entries(e.problem.errors).map(([key, value]) => [key.charAt(0).toLowerCase() + key.slice(1), value.join(" ")])));
    } finally { createLock.current = false; setPending(false); }
  }
  return <div className="dashboard-root"><div className="page-heading"><p className="eyebrow">PLAN YOUR SAVINGS</p><h1 tabIndex={-1} ref={heading}>Savings Goals</h1><p>A gold target for your simulated saving journey.</p></div>
    <p role="status" className="field-hint">{status}</p>
    {loading ? <LoadingState text="Loading your savings goal…" /> : <>
      {error && <><Alert>{error}</Alert><Button className="button-secondary" onClick={() => void load()} disabled={pending}>Reload goal</Button></>}
      {!loadFailed ? <GoalCard goal={goal} /> : null}
      {!goal && !loadFailed && <Card className="goal-form"><h2>Create a savings goal</h2><form onSubmit={create} aria-busy={pending}>
        <FormField id="targetGrams" label="Target simulated gold (grams)" error={fields.targetGrams} hint="Positive target, with up to 8 decimal places."><Input id="targetGrams" inputMode="decimal" required value={target} disabled={pending} onChange={e => setTarget(e.target.value)} aria-invalid={!!fields.targetGrams} aria-describedby={`targetGrams-hint${fields.targetGrams ? " targetGrams-error" : ""}`} /></FormField>
        <FormField id="targetDateUtc" label="Target date (optional)" error={fields.targetDateUtc} hint="A planning date only; completion depends on your holding."><Input id="targetDateUtc" type="date" value={date} disabled={pending} onChange={e => setDate(e.target.value)} aria-invalid={!!fields.targetDateUtc} aria-describedby={`targetDateUtc-hint${fields.targetDateUtc ? " targetDateUtc-error" : ""}`} /></FormField>
        <Button type="submit" className="button-primary" busy={pending}>Create savings goal</Button>
      </form></Card>}
      {goal && <p className="field-hint">Your goal stays visible when reached. Editing and removal are not available in v1.</p>}
    </>}
  </div>;
}
