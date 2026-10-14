import Link from "next/link";
import { Card } from "@/components/ui/primitives";
import { grams, percentage, remainingGrams, targetDateLabel, visualProgress, type SavingsGoal } from "@/lib/goals/presentation";

export function GoalCard({ goal, compact = false }: { goal: SavingsGoal | null; compact?: boolean }) {
  return <Card className="goal-card">
    <p className="eyebrow">SAVINGS GOAL · SIMULATED GOLD</p>
    {!goal ? <><h2>Choose a gold target</h2><p>Track progress as your total simulated gold holding grows.</p>{compact && <Link href="/goals" className="button button-primary">Create a savings goal</Link>}</> : <>
      <h2>{goal.isCompleted ? "Goal reached" : "Your gold target"}</h2>
      <dl className="goal-metrics">
        <div><dt>Current simulated holding</dt><dd>{grams(goal.currentHoldingGrams)} g</dd></div>
        <div><dt>Target</dt><dd>{grams(goal.targetGrams)} g</dd></div>
        <div><dt>Remaining</dt><dd>{remainingGrams(goal.targetGrams, goal.currentHoldingGrams)} g</dd></div>
      </dl>
      <p>{percentage(goal.progressPercentage)}% of target{goal.isCompleted ? " · Achieved" : ""}</p>
      <div className="goal-progress" role="progressbar" aria-label="Savings goal progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={visualProgress(goal)} aria-valuetext={`${percentage(goal.progressPercentage)}% of target${goal.isCompleted ? ", goal reached" : ""}`}><span style={{ width: `${visualProgress(goal)}%` }} /></div>
      {goal.targetDateUtc && <p>Target date: <time dateTime={goal.targetDateUtc}>{targetDateLabel(goal.targetDateUtc)}</time> · Planning only</p>}
      {compact && <Link href="/goals" className="button button-secondary">View savings goal</Link>}
    </>}
    <p className="field-hint">Progress uses your overall simulated gold holding. This goal reserves no funds or gold and makes no automatic purchases.</p>
  </Card>;
}
