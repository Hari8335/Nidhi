# Session 12 — Public landing and product polish

Implemented on the existing `feat/public-polish` branch. No branch or commit was created. No dependency, backend code, financial behavior, authentication behavior, migration or brand asset was added or changed. Session 13 was not started.

## Sources and scope

Consulted AGENTS.md, README.md, the seven requested requirements documents, UI_UX_DESIGN_SPEC, API_CONTRACT, API_ERRORS and Sessions 6–11 reports; inspected current public/auth/customer/admin components, both approved PNG brand variants and the running original landing page. Current code and later session reports establish the implemented wallet, purchase, holdings, transaction/receipt, goals and admin behavior. Public copy follows that behavior and approved simulation policy.

README still describes financial implementation as future work. Historical design recommends logo reconstruction and legal links, but this session explicitly requires preserving the approved artwork and forbids invented legal routes/content. Neither historical recommendation was treated as authorization to expand scope. OD-013–015 remain open; no eligibility, retention, hosting or legal policy was invented. These discrepancies are recorded here without rewriting historical business documents.

## Landing narrative and copy

The structure is navigation → asymmetric hero → How Nidhi works → product capabilities with a savings-goal story → simulation/transparency → final CTA → footer. Existing premium dark surfaces, gold accents, Manrope typography, custom circular savings visual and approved dark-theme brand artwork are preserved.

Final headline:

> Build a saving habit, measured in gold.

Final subcopy:

> Start small with simulated LKR. Allocate your wallet balance to simulated fractional gold, then follow your holdings and savings goals in grams.

The hero immediately identifies simulated gold micro-savings and includes a readable disclosure beside the account actions: wallet balances, gold purchases and holdings are simulated; no real money or physical gold is handled. The dedicated simulation section repeats these boundaries and states that holdings have no real cash value. All illustration numbers are decorative, labeled DEMO / SIMULATION PREVIEW, and hidden from the accessible tree. The goal pill explicitly says Demo goal; its prior completion-style checkmark is replaced with a directional arrow.

How it works is a semantic three-step ordered list: add simulated LKR, allocate wallet balance to simulated fractional gold after review, and follow holdings/transactions/goals. Product capabilities cover simulated wallet/purchases, exact holding quantities, indicative simulated valuation, history and purchase receipts. The goal story explains a target in grams, optional target date and progress from the current total simulated holding, with no reservation or automatic purchases.

Transparency explains purchase review, reconfirmation after price changes and original applied price/quantity on receipts. Removed engineering language such as savings architecture, audit-grade ledger precision, auditable history and transactional-integrity slogans. Removed the unattributed motivational quotation and general claims about financial calm/stability. No purity, custody, real ownership, return, regulation, security certification, testimonials, customer counts, partner logos, market charts or urgency claims were added.

Header/hero primary CTA is Create account; final CTA is Create your account. Registration links use `/register`; Sign in uses `/login`. Public navigation has three working anchors: How it works, Features, Simulation. Footer retains the approved brand, current-year copyright, concise simulation description and implemented links only. No dead legal/contact/blog/pricing links, empty hash links, waitlist or demo booking flows exist. The page remains static and uses the existing global AuthProvider without introducing role-aware public rendering or extra public data requests.

## Design, motion and accessibility

Scoped landing rules avoid changing customer/admin financial styles. Two existing global selector collisions were found: customer metric-card styling affected hero essentials, and transaction section-header styling flattened the landing hierarchy. Landing-specific rules reconcile them. Mobile reads the explanation before the visual; compact header actions and visible anchor links fit without a JavaScript menu. Goal and disclosure padding tightens on phones. Footer and final CTA reflow naturally.

Hero entrance, arc draw and rising bars use finite native CSS animations. Particle accents and the badge are static; the holding card uses one entrance instead of a perpetual float. No animation library, scroll observer, scroll hijacking or continuous public animation is introduced. The illustration is now a Server Component; its useId-based gradients render without a separate client boundary. Existing `prefers-reduced-motion: reduce` overrides disable animation/transition/smooth scrolling. Production checks confirmed no animated main-content elements under reduced motion, with readable content and usable controls.

Native header/nav/main/section/footer landmarks, one h1, h2 section headings, h3 capability headings, a three-step ordered list and meaningful link names are present. Main accepts programmatic focus so Skip to content transfers focus correctly. Keyboard activation, focus outline, anchors, registration navigation and associated client validation errors passed. The decorative visual is omitted from the browser accessibility tree. Existing Brand accessible name and image alternative are retained.

Representative text contrast ratios against the brightest established dark surface (#181D25): primary text 15.77:1, secondary text 7.74:1, gold highlight 11.85:1; dark text on gold CTA 8.04:1. Desktop reflow at a half-width CSS viewport and a supplemental 200% text-size check had no page overflow. This is browser/semantic evidence, not a manual assistive-technology audit or accessibility certification.

## Auth, 404 and SEO

All five auth routes retain the Session 6 split layout, form behavior, existing status/error components and simulation note. Removed aria-hidden from the desktop showcase because it contained focusable home links. Its header can wrap. Registration success now directs customers to verify before using the implemented simulated wallet/gold features instead of calling them future features. No auth request, validation, session or redirect semantics were changed.

A lightweight branded root not-found page has a named main landmark, clear 404 message and implemented Home / Sign in links. Recovery navigation was verified in production.

Landing metadata includes factual title, description, Open Graph website/site-name/title/description and Twitter summary/title/description. Root description is reconciled; the existing approved dark PNG is referenced as the icon without changing the artwork. No title template is introduced because existing route titles already include Nidhi. No fabricated host, metadataBase, canonical, absolute social image or production URL is added.

`robots.ts` allows public information and disallows API, customer, admin and auth route families. Customer/admin/auth layouts emit noindex, nofollow. Production checks verified private-layout metadata, the generated robots response and the absence of an invented canonical. Robots rules are crawler guidance; existing ASP.NET Core authorization remains authoritative. Sitemap and deployment-specific canonical/social-image URLs are deferred until a real public origin is approved.

## Validation

| Check | Result |
|---|---|
| Frontend Node suite | 71 total / 71 passed / 0 failed / 0 skipped; baseline 63 retained |
| Backend unit suite | 15 total / 15 passed / 0 failed / 0 skipped |
| Backend PostgreSQL integration suite | 112 total / 112 passed / 0 failed / 0 skipped |
| Frontend lint | Passed |
| Frontend strict typecheck | Passed |
| Frontend production Webpack build | Passed; landing, auth, 404 and robots prerendered |
| Backend restore/build from backend/ | Passed; build 0 warnings / 0 errors |
| Live normal-development Kestrel GET /health | HTTP 200, {"status":"ok"} |
| Git diff --check | Passed |
| Branch | feat/public-polish; no commit |

Eight focused Node tests were added in `public.test.mjs`: account routes/anchors; immediate disclosure; three-step sequence; goal semantics; claim/social-proof/stale-copy guards; factual metadata; robots/private indexing; auth accessibility and 404 recovery links. They do not snapshot the entire page. The existing suites remain present.

`public-browser.mjs` adds repeatable read-only browser coverage using an externally installed Playwright driver supplied by environment variables. It adds no package dependency and performs no account creation or financial mutation. Production Chrome checks covered `/`, `/login`, `/register`, `/forgot-password`, `/verify-email`, `/reset-password` and a deliberately missing route at 320, 390, 768, 1024 and 1440px. Every width passed page/text overflow checks; screenshots were captured and final hero/auth/full-page screenshots visually reviewed. Navigation anchors, account CTAs, skip/focus/keyboard activation, invalid-password UX, reduced motion, metadata, robots, 404 recovery and accessible-tree checks passed. No unexpected console errors, page errors, hydration warnings, key warnings, failed assets or navigation exceptions were observed. Expected anonymous `/auth/me` 401 and the deliberately requested 404 are excluded from error assertions.

The first browser assertion incorrectly counted the intentionally requested 404 as a runtime error; the check now explicitly permits that expected response only during the missing-route visit while retaining failed-asset detection. Initial sandbox socket/process restrictions were resolved with supported execution; they were not treated as application failures. Backend PostgreSQL tests used the ignored local connection settings and suite-owned disposable databases. No application database writes were made by public browser checks.

## Performance and review

Production landing remained statically prerendered; no new third-party scripts, client state, large image media or animation dependency was added. Responsive Next Image sizes serve the approved logo at about 5.3 KB in the observed desktop run. The custom hero is inline SVG/CSS; backdrop blur is removed from its card. One local production observation recorded cumulative layout shift 0, which is a sanity check rather than a performance benchmark/SLA. Existing framework/AuthProvider code and automatic account-link prefetching remain. The shared savings-visual client boundary was removed.

Changed/untracked content was reviewed for credential values, tokens, private keys, analytics secrets, fake production hostnames and private URLs; no secrets were introduced. `.env` remains ignored, untracked and owner-only. Brand assets, package manifests, lockfile, backend sources and existing regression tests are unchanged. Task-started servers were stopped after checks; the existing development PostgreSQL container/volume was retained.

## Exact review inventory

Created:

- `docs/SESSION_12_REPORT.md`
- `frontend/web/src/app/not-found.tsx`
- `frontend/web/src/app/robots.ts`
- `frontend/web/tests/public-browser.mjs`
- `frontend/web/tests/public.test.mjs`

Modified:

- `frontend/web/src/app/(auth)/layout.tsx`
- `frontend/web/src/app/(customer)/layout.tsx`
- `frontend/web/src/app/admin/layout.tsx`
- `frontend/web/src/app/globals.css`
- `frontend/web/src/app/layout.tsx`
- `frontend/web/src/app/page.tsx`
- `frontend/web/src/components/auth/auth-form.tsx`
- `frontend/web/src/components/shared/brand.tsx`
- `frontend/web/src/components/shared/gold-savings-visual.tsx`

Review all fourteen files above before committing. Session 12 is ready for review; deployment/legal decisions remain future launch gates. Suggested Conventional Commit: `feat(public): polish simulated gold savings landing and metadata`.
