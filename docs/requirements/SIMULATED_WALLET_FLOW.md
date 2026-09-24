# Simulated wallet funding flow

This flow implements target requirements FR-WALLET-001 through FR-WALLET-003 and supports transaction history. See [functional requirements](FUNCTIONAL_REQUIREMENTS.md), [stories](USER_STORIES.md), and [decisions](OPEN_DECISIONS.md).

## Product decision

Adding simulated funds is **not a real deposit**. It does not charge a card, contact a bank/gateway, receive real money, or create a withdrawable balance. The interface must call it adding simulated funds and show the simulation disclosure.

Every successful funding action creates a stable completed `SIMULATED_FUNDING` transaction and linked ledger entries, not just a changed balance (DD-004). These records supply a receipt, customer history and reconciliation evidence. An application audit/log event may additionally record the attempt; it cannot replace the financial transaction and ledger. Funding is a Customer action, not an Administrator balance-edit capability.

## Inputs and validation

Server-authenticated, registered and email-verified Customer identity, exact decimal simulated LKR amount, and supplied idempotency key. Approved rules (OD-003/005/010/012/016):

- LKR 100.00 minimum and LKR 1,000,000.00 maximum per funding operation, inclusive; monetary storage is decimal(18,2).
- Resulting wallet balance must not exceed LKR 5,000,000.00.
- At most 20 successful funding operations per Customer per day. Failed pre-commit attempts and receipt replays do not consume additional successful operations.
- Validate configuration for these policies where appropriate rather than scattering hard-coded values. Domain/API design must document the day boundary/timezone; no timezone is silently chosen here.
- No customer suspension/status workflow. Registered and email-verified identity is the customer-feature eligibility rule.

 A new Customer starts with zero balances; no initial funding transaction is fabricated.

## Business sequence

1. Customer enters an amount and acknowledges the visible simulation context.
2. API validates authentication, verified email, ownership and request shape. New executions must satisfy amount, resulting-balance and daily-count limits.
3. Resolve the Customer + operation + key association bound to equivalent content: return the existing committed receipt for an identical retry; reject conflicting reuse; coordinate concurrent attempts.
4. Atomically credit the simulated wallet, create the completed funding transaction and balanced LKR ledger entries, and associate the durable idempotency outcome. Enforce amount, resulting-balance and daily-success caps against concurrent requests, not stale UI balances. Permanently retain the successful idempotency association. An existing successful receipt is returned without applying new-command caps or incrementing the daily counter again.
5. Commit, then return transaction identifier, simulated funding type, amount, timestamp, completed status and resulting wallet balance. Gold holding is unchanged.
6. Customer can find the same receipt in their history. A replay returns the original post-operation balance; current wallet balance is retrieved separately.

`resulting simulated wallet balance = prior simulated wallet balance + accepted funding amount`

Use C# decimal and decimal(18,2) LKR storage. The [conceptual ledger chart](../DATABASE_DESIGN.md) pairs Customer wallet credits with the simulated funding source in the LKR book; gold is unchanged. Detailed account/entity implementation remains Session 3 work; this is not bank settlement.

## Failure and retry outcomes

- Invalid amount, exceeded limits, unverified email or unauthorized ownership: reject with no financial changes.
- Same identity/content already committed: original receipt, no second credit.
- Same identity with changed amount: conflict, no credit.
- Concurrent distinct requests: enforce approved aggregate limits and correct final balance.
- Any financial write failure before commit: rollback wallet, completed transaction, ledger and success association together.
- Response lost after commit: show an uncertain outcome and use the same identity to recover; never encourage generating a new funding request to discover what happened.
- Read error: show unavailable balance, not zero. No funding history: explicit empty state.

Verify US-WALLET-001 through US-WALLET-003 and the shared financial integrity NFRs. Record failures in redacted diagnostics where appropriate; do not create a successful receipt on failure. Successful associations are permanent, differing content conflicts, and failed pre-commit commands may safely retry with the same key/content (OD-012). General retention/privacy periods remain OD-013 for launch review, not a blocker to this conceptual flow.
