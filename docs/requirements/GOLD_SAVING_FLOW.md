# Core gold-saving flow

This is the business operation behind FR-GOLD-001 through FR-GOLD-004 and FR-HOLDING-001 in the [functional requirements](FUNCTIONAL_REQUIREMENTS.md). It spends simulated LKR to acquire simulated gold; it is not a payment or purchase of real gold. [Approved decisions](OPEN_DECISIONS.md) OD-004/005/006/010/012/016 govern this flow; implementation remains future work.

## Inputs and authority

- Registered, email-verified Customer identity derived from the authenticated ASP.NET Core Identity cookie session; a supplied Customer identifier is never sufficient authority.
- Requested simulated LKR amount, represented exactly as a decimal.
- Idempotency identifier for the Customer's intended operation.
- The priceVersionId displayed to and confirmed by the Customer (OD-006). The API obtains and validates the effective stored price/version; the client cannot set an authoritative price or gold quantity.

There are no fees, bonuses or interest in v1 (DD-006). The wallet debit equals the accepted LKR amount. The requested amount is LKR 100.00–1,000,000.00 inclusive, uses two decimal places and cannot exceed available wallet funds. LKR uses decimal(18,2), price/gram decimal(18,4), and gold decimal(20,8).

## Process

1. Customer chooses an LKR amount and sees the simulation disclosure and available price context.
2. ASP.NET Core authenticates the Customer, checks ownership and email verification, and validates input against the approved limits, scale and capacity. No customer suspension/status workflow is modeled.
3. Resolve the Customer + operation + supplied-key association, bound to equivalent content including amount and priceVersionId. A prior committed identical operation returns its original receipt without another purchase. Conflicting reuse fails. Concurrent in-progress attempts must converge on a single result or an explicit retryable state, not execute twice.
4. For a new execution, check available wallet funds and revalidate that the submitted priceVersionId is the current positive version, no older than 24 hours. A changed version requires a price-changed conflict and explicit reconfirmation; never substitute a newer version. A successful prior receipt replay does not re-execute or revalidate current price freshness.
5. Calculate gold using C# decimal and round DOWN to 8 decimal places. Reject zero after rounding or numeric overflow. Preserve exact conversion inputs/results sufficient to calculate/store the residual for reconciliation/audit.
6. Within one atomic financial operation, create the transaction, debit the wallet, credit the gold holding, create linked separately balanced LKR and gold-gram ledger entries and conversion/residual evidence, and persist the permanent successful idempotency association. Revalidate/protect balances and price eligibility against concurrent changes as required by OD-006. The concrete locking/isolation strategy is deferred.
7. Commit all financial records together and return a completed receipt only after durable success.
8. Refresh Customer summaries from committed data. Refresh failure does not undo a committed save; the receipt and safe retry/history path remain available.

This ordering describes business obligations, not a prescribed sequence of SQL statements. A transaction record is not externally reported as completed until its operation commits.

## Calculation and conservation

`unrounded gold grams = simulated LKR amount / simulated LKR price per gram`

`credited gold grams = round DOWN(unrounded gold grams, 8 decimal places)`

`conversion residual in LKR = requested LKR amount − (credited gold grams × exact price per gram)`

Retain requested amount, immutable price/version and credited quantity so the residual remains exactly derivable; it may also be stored with sufficient precision. It is reconciliation evidence, not a fee, refund or extra wallet mutation. Do not force a sub-cent residual into decimal(18,2) and lose it. Its representation/posting details are Session 3 work.

`indicative portfolio LKR value = held gold grams × current usable simulated LKR price per gram`

For illustration: LKR 100.00 at simulated price 30000.0000 LKR/gram credits 0.00333333 grams after rounding DOWN. The full LKR 100.00 is debited; 0.00333333 × 30000.0000 = LKR 99.999900000000, leaving a residual of LKR 0.000100000000. This example price is not a production default.

The wallet debit and holding credit must match the committed receipt and ledger under the approved conservation/rounding equations. Ledger balancing applies separately within each unit; LKR and grams cannot be summed together. OD-016 selects separate unit books. The [conceptual chart](../DATABASE_DESIGN.md) identifies the necessary v1 accounts; detailed implementation is deferred.

## Outputs

A durable receipt contains transaction identifier, type (gold-saving), completed status, Customer association, exact LKR debit, simulated gold price and version used, credited gold quantity, resulting wallet balance, resulting gold holding, and timestamp. Receipts retain post-operation balances as of that operation; a replay does not replace them with today's balances. The UI may fetch current balances separately.

A later price change affects indicative value only, not holding quantity or historic receipt values. Any shown price/valuation must include its time/version context; it is not a cash-out quote.

## Atomicity, concurrency and idempotency

Wallet, holding, transaction, ledger and successful idempotency association are one atomic unit. Failure anywhere before commit leaves none partially applied. Same-key retries after response loss return the original committed result; they are not new purchases. Different keys identify different intended operations but must still serialize/conflict safely against one wallet so it never becomes negative.

Authentication and ownership checks apply even to replay. Successful financial transactions permanently retain the Customer/operation/key association. Same key with equivalent content returns the original success, even after price replacement/expiry; changed content conflicts. Pre-commit failure permits safe retry with the same key/content, subject to current execution validation. Reconfirmation at a different priceVersionId changes request content and therefore uses a new key. Exact HTTP/header naming and canonicalization belong to API design (OD-012). Do not implement an in-memory-only duplicate guard as the durability policy.

## Failure outcomes

| Condition | Required observable result |
|---|---|
| Missing/invalid authentication, wrong Customer, unverified email | Denied; no unauthorized data or financial mutation |
| Nonpositive, out-of-range, invalid-scale or overflow amount | Validation failure; no financial mutation |
| Insufficient simulated funds | Explain insufficient funds; balance/holding/history unchanged |
| No usable positive price | Saving unavailable; no fallback real market feed or invented price |
| Submitted version is no longer current at execution | Price-changed conflict; require reconfirmation and a new key for changed content, with no silent substitution |
| Current version is older than 24 hours | New purchases disabled; exactly 24 hours is not older than 24 hours |
| Identical committed retry | Original receipt; no new debit, holding credit or ledger entries |
| Identifier reused with different intent | Conflict; original transaction unchanged |
| Concurrent in-progress duplicate | Single eventual result or explicit retryable state under OD-012; no duplicate completion |
| Database or ledger write failure before commit | Rollback all financial changes; safe non-sensitive error |
| Timeout/connection loss with uncertain commit | Report uncertain outcome, not definite failure or success; retry/reconcile the same identity |

Failed attempts may appear in redacted operational logs; whether durable rejected transaction rows exist is deferred. They must never appear as completed purchases or create financial ledger effects. There is no v1 reversal endpoint; future corrections require approved compensating records.

## Acceptance evidence

Use US-GOLD-001 through US-GOLD-004 plus NFR-DATA-001 through NFR-DATA-005 and NFR-REL-001. Include success, approved boundary/rounding/residual cases, two simultaneous saves, identical/conflicting retries, a lost response, price change during submission, and injected failure at each persistence step.
