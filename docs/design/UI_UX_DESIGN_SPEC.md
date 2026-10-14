# NIDHI UI/UX Design Specification v1.0

**Product:** Nidhi – Digital Gold Micro-Savings Platform  
**Document Type:** UI/UX Design Specification  
**Version:** 1.0  
**Status:** Approved design direction for MVP implementation  
**Theme:** Single premium dark theme  
**Primary Reference:** Cash App-inspired motion and bold simplicity (reference only; no copying of brand assets or layouts)

## 1. Purpose

This document is the visual and interaction source of truth for Nidhi v1. It defines how the product should look, feel, behave, and respond across public, authentication, customer, and administrator experiences. It complements the SRS and engineering design documents: the SRS defines what the system must do; this specification defines how those capabilities should be presented to users.

## 2. Product and Brand Direction

Nidhi helps people save small LKR amounts as simulated fractional gold. The interface must communicate trust, clarity, progress, and premium value without looking like a speculative crypto exchange or a luxury jewellery site.

### 2.1 Brand personality
- Premium and elegant
- Modern financial technology
- Calm and trustworthy
- Simple enough for first-time savers
- Progress-oriented rather than trading-oriented
- Transparent about simulated money/gold

### 2.2 Design principles
1. **Gold is an accent, not the background of everything.** Use gold to highlight value, progress, primary actions, and investment-related moments.
2. **Dark surfaces create the premium base.** Deep navy/charcoal should dominate large surfaces.
3. **Financial information must be effortless to read.** Balances, grams, prices, dates, and transaction status take visual priority.
4. **Motion explains and rewards.** Animation should reinforce saving progress and conversion, not distract from forms or financial decisions.
5. **Every financial action must feel deliberate.** Confirmation, price information, amount, and result must be obvious before and after a transaction.
6. **Mobile responsiveness is mandatory.** Desktop may be richer, but every flow must work comfortably on a phone browser.

## 3. Logo System

The approved logo direction is a premium NIDHI wordmark paired with a circular investment/gold symbol. The icon combines an `N` motif, an enclosing coin/ring, and rising bars to represent incremental saving and growth.

### 3.1 Production rule
The generated logo artwork is the visual concept. Before production use, rebuild the mark as a simplified SVG so it remains crisp at every size and its ring/bars can be animated independently.

### 3.2 Logo variants
- **Primary horizontal:** symbol + NIDHI wordmark; dark background.
- **Compact:** symbol only; favicon, mobile header, loading mark.
- **Monochrome:** single gold or single white version for constrained surfaces.
- **Do not:** stretch, recolor with unrelated colors, add glow behind the full logo, or place on noisy imagery.

### 3.3 Logo sizing
- Desktop navigation: 30–34 px symbol height.
- Mobile header: 28–32 px.
- Auth/landing hero: 48–72 px symbol or 160–220 px horizontal wordmark depending on layout.
- Favicon/app icon: symbol only.
- Maintain clear space equal to at least half the symbol height around the mark.

### 3.4 Logo motion
- Entry: outer gold ring draws/fades in, N mark rises subtly, bars stagger upward.
- Duration: 650–900 ms total.
- Shimmer: one restrained metallic highlight pass after reveal; do not loop continuously.
- Hover (desktop): maximum 1–2° tilt or 1.02 scale.
- `prefers-reduced-motion`: render immediately without drawing/shimmer effects.

## 4. Color System

Nidhi uses one premium dark theme. There is no light/dark toggle in v1.

| Token | Hex | Usage |
|---|---|---|
| Ink / Black | `#0B0D10` | Primary page background, nav, modal backdrop base |
| Deep Navy | `#0A1424` | Hero gradients, elevated premium sections |
| Surface | `#12161D` | Cards, panels, inputs |
| Surface Elevated | `#181D25` | Hover/elevated cards, dialogs |
| Gold 700 | `#8C6A1D` | Dark metallic edge, pressed states |
| Gold 500 | `#C9A227` | Primary accent and CTA |
| Gold 300 | `#F0D77B` | Highlights, subtle shimmer, key figures |
| Primary Text | `#F7F7F5` | Main text on dark surfaces |
| Secondary Text | `#A9B0BA` | Supporting text and metadata |
| Border | `#2A313B` | Dividers and control borders |
| Success | `#2FBF71` | Successful operations |
| Warning | `#F3B33D` | Warnings / stale price notice |
| Danger | `#E45B5B` | Errors / destructive action |

### 4.1 Metallic gold treatment
Use metallic effects sparingly. The default gold gradient is:

`linear-gradient(135deg, #7A5815 0%, #C9A227 38%, #F3DE8A 60%, #B88919 100%)`

Use flat `Gold 500` for most buttons and UI states. Reserve gradients for the logo, hero ornamentation, progress celebration, and premium marketing sections.

### 4.2 Color usage ratio
- 65–75% black/charcoal/navy
- 15–25% white/gray text and neutral surfaces
- 5–10% gold accent
- semantic green/amber/red only when status requires it

## 5. Typography

### 5.1 Font family
**Primary UI font: Manrope** (modern, geometric, elegant, highly readable). Use `next/font` in implementation. If Manrope cannot be used, fall back to `Inter`, then system sans-serif.

### 5.2 Typography scale
| Style | Size | Weight | Use |
|---|---:|---:|---|
| Display XL | 56–64 px desktop / 40–44 px mobile | 700 | Landing hero |
| Display | 40–48 px / 32–36 px | 700 | Major marketing section |
| H1 | 32–36 px / 28–32 px | 700 | Application page title |
| H2 | 24–28 px | 650–700 | Section heading |
| H3 | 18–20 px | 600 | Card heading |
| Body | 16 px | 400–500 | Main copy |
| Small | 14 px | 400–500 | Supporting content |
| Caption | 12 px | 500 | Metadata, timestamps |
| Financial Hero | 36–48 px | 650–700 | Wallet/holding values |

Financial numbers should use tabular numerals (`font-variant-numeric: tabular-nums`). Do not use stylized fonts for amounts or prices.

## 6. Spacing, Radius, and Elevation

Use an 8 px spacing rhythm with 4 px half-steps.

- 4: micro spacing
- 8: tight
- 12: compact control gap
- 16: standard
- 24: card padding / section gap
- 32: large component separation
- 48: page section spacing
- 64–96: marketing section spacing

### Radius
- Inputs/buttons: 12 px
- Standard cards: 16 px
- Feature/hero cards: 20–24 px
- Pills/badges: 999 px

### Shadows
Keep shadows subtle on the dark theme. Prefer borders + soft shadow over bright glows. Gold glow may be used only as a low-opacity decorative effect around hero investment visuals.

## 7. Motion System

Motion is inspired by the bold, smooth, product-led feel requested from Cash App, while remaining original to Nidhi.

### 7.1 Timing
- Micro interaction: 120–180 ms
- Standard component transition: 180–250 ms
- Page/section reveal: 350–550 ms
- Hero/logo storytelling: 650–900 ms

### 7.2 Easing
Prefer smooth ease-out for entry and standard ease-in-out for state changes. Avoid spring effects on critical financial confirmations.

### 7.3 Approved motion patterns
- Scroll-triggered fade + 12–20 px translate for marketing sections.
- Number interpolation for balance/holding changes after confirmed transactions.
- Savings goal progress fills smoothly after data load/update.
- Gold particles/bars may drift slowly in hero artwork at low opacity.
- Purchase confirmation may show a one-time coin/bar conversion animation.
- Card hover may lift 2–4 px on pointer devices.
- Mobile navigation transitions should be short and direct.

### 7.4 Motion restrictions
- No infinite shimmer on balances or CTA buttons.
- No animation that delays form input.
- No rapid flashing.
- Never animate values in a way that makes the final amount ambiguous.
- Honor `prefers-reduced-motion` everywhere.

## 8. Responsive System

- Mobile: `< 640 px`
- Tablet: `640–1023 px`
- Desktop: `>= 1024 px`
- Wide content maximum: 1280 px
- Reading/forms maximum: 480–560 px

Desktop customer/admin apps use a side navigation. Mobile customer navigation becomes a bottom bar for primary destinations plus a menu for secondary actions. Admin remains optimized for tablet/desktop but must still be usable on mobile for essential inspection tasks.

## 9. Global Component Library

### 9.1 Buttons
**Primary:** metallic/flat gold surface, near-black text.  
**Secondary:** charcoal surface with neutral border and white text.  
**Ghost:** transparent, white/gray text.  
**Danger:** red-tinted dark surface or red outline; reserve for destructive actions.

Button requirements:
- Minimum height 44 px; 48 px preferred for primary forms.
- Visible focus ring.
- Loading state preserves width.
- Disabled state reduces contrast but remains readable.
- Icon-only buttons have accessible labels.

### 9.2 Inputs
- Dark surface with clear neutral border.
- Gold/white focus ring, not a full gold-filled field.
- Labels stay visible; never rely only on placeholder text.
- Validation message directly below field.
- Password controls include show/hide action.
- Financial amount inputs clearly show `LKR` and decimal expectations.

### 9.3 Cards
Card hierarchy:
- Standard information card
- Financial summary card
- Action card
- Warning/notice card
- Receipt card

Avoid putting every piece of content in a bordered card. Use whitespace and section hierarchy where possible.

### 9.4 Badges/status
Use icon + text + color. Never communicate state by color alone.

Examples:
- Completed – green
- Pending/attention – amber
- Failed/error – red
- Simulated – neutral/gold badge where disclosure is useful

### 9.5 Tables
Admin tables use:
- Sticky header where useful
- Clear row hover
- Numeric alignment to the right
- Date/time consistently formatted
- Mobile fallback to stacked row cards if the table becomes unreadable

### 9.6 Dialogs
Use dialogs only for high-importance confirmation such as confirming a gold purchase. The confirmation must repeat amount, price, grams, and any price-change warning.

## 10. Navigation and Information Architecture

### Public navigation
- NIDHI logo
- How It Works
- Why Gold Saving
- Security
- Sign In
- Start Saving

### Customer desktop navigation
- Dashboard
- Wallet
- Gold
- Transactions
- Savings Goal
- Profile

### Customer mobile primary navigation
- Home
- Wallet
- Gold
- Activity
- More

### Admin navigation
- Overview
- Customers
- Transactions
- Ledger
- Gold Prices
- Audit Logs

## 11. Public Landing Page

### 11.1 Hero
Goal: communicate Nidhi in one glance.

Recommended headline direction: **“Small savings. Real progress in gold.”**  
Supporting copy must explicitly say the current product is a simulation/portfolio project and does not represent real gold custody or monetary value in v1.

Hero visual:
- dark/navy background
- large Nidhi logo/mark
- abstract animated gold bars/particles/coin-ring
- primary gold CTA: `Start Saving`
- secondary action: `How Nidhi Works`

### 11.2 How it works
Three steps:
1. Add simulated LKR to your wallet.
2. Convert an amount into simulated fractional gold.
3. Track holdings, transactions, and progress toward a gold goal.

### 11.3 Value section
Use 3–4 focused benefits, not a dense feature grid:
- Start small
- Clear gold pricing
- Track every transaction
- Build toward a goal

### 11.4 Trust/security section
Explain secure authentication, immutable transaction history, transparent simulated pricing, and responsible disclosures in plain language.

### 11.5 Final CTA/footer
Simple final action, legal/disclosure links, Terms, Privacy, Contact.

## 12. Authentication Experience

Auth pages should be visually calm and focused.

### Desktop layout
Split-screen or centered composition:
- brand/animated gold visual on one side
- form card on the other

### Mobile
Single-column form; decorative animation moves behind/above the content and must not interfere with keyboard/form interaction.

### Screens
- Register
- Login
- Verify Email
- Forgot Password
- Reset Password

### Rules
- One dominant CTA per screen.
- Show password requirements during registration.
- Errors remain near the relevant field and in a page-level alert when needed.
- Do not expose account existence during recovery.
- Verification success should use a short celebratory gold-ring animation and clear next action.

## 13. Customer Application Shell

### Desktop
- Left sidebar, 240–264 px.
- Top area inside main content for page title, optional contextual actions, profile menu.
- Main content centered with max-width around 1200–1280 px.

### Mobile
- Compact top bar with symbol/logo and profile access.
- Bottom navigation for Home, Wallet, Gold, Activity, More.
- Primary actions remain thumb-accessible.

## 14. Customer Dashboard

Dashboard priority order:
1. Total simulated gold holding
2. Estimated holding value in LKR
3. Simulated wallet balance
4. Active gold price and freshness/status
5. Savings goal progress
6. Recent transactions
7. Primary actions: `Add Simulated Funds` and `Buy Gold`

Use one premium hero summary card rather than many equally weighted cards.

Recommended hero visual: a subtle gold arc behind the holding value with animated progress only on initial load.

## 15. Wallet Experience

Wallet page includes:
- simulated LKR balance
- funding limits disclosure
- funding form
- recent funding activity

Funding flow:
1. Enter amount
2. See resulting projected wallet balance
3. Confirm
4. Show immutable receipt/result

Do not visually resemble a real bank transfer flow; clearly label funds as simulated.

## 16. Gold Purchase Experience

This is Nidhi's signature interaction and should feel premium but precise.

Purchase card shows:
- active price per gram
- price timestamp/freshness
- wallet balance
- LKR input
- calculated gold grams
- clear round-down disclosure if relevant
- confirmation action

Confirmation screen/dialog repeats:
- LKR amount
- price/version
- grams to be credited
- resulting wallet balance when available

If price changes, interrupt the action with a clear `Price updated` state and require explicit reconfirmation. Never silently substitute a new price.

Success animation: a short gold bar/coin conversion visual, then settle immediately into the immutable receipt.

## 17. Holdings and Portfolio

Show:
- total grams prominently
- current estimated LKR value
- active price used for valuation
- acquisition/transaction context where supported

Charts should be secondary. Do not imply investment return projections. If a simple history chart is used, label it as simulated valuation history and avoid green/red trading semantics unless actually meaningful.

## 18. Transaction History and Receipt

History list/table contains:
- transaction type
- amount LKR
- gold grams where applicable
- status
- timestamp
- transaction ID/detail link

Receipt detail should visually resemble a trustworthy financial receipt:
- immutable transaction ID
- amount
- applied gold price/version
- credited grams
- resulting balances where contract returns them
- UTC/server timestamp formatted for the UI

Use copy-to-clipboard for IDs only where helpful.

## 19. Savings Goal Experience

The goal is based on **gold grams**, not market value.

Goal card:
- target grams
- current grams
- percentage complete
- optional target date
- progress bar/ring

When progress exceeds 100%, the visual bar may cap at 100% while text can show that the user has exceeded the target.

Use a subtle celebratory shimmer only when the target is first reached.

## 20. Admin Application

Admin UI should be more data-dense than customer UI while retaining the same brand system.

### Dashboard
- customer count / high-level operational metrics as available
- transaction activity
- current gold price and age
- operational warnings such as stale/unavailable price

### Customers
Search/filterable list and detail. Avoid unnecessary personally identifying information.

### Transactions
Filter by type/date/customer as supported. Financial values right-aligned.

### Ledger
Present LKR and GRAMS as visibly separate books. Never visually imply they sum together.

### Gold Price Management
This is a high-risk admin action. Show:
- current price
- publication timestamp
- price history
- new price input
- mandatory confirmation before publish
- explicit message that publication creates a new immutable active version

### Audit Logs
Chronological, filterable operational record with actor, action, resource, timestamp, and safe metadata.

## 21. State Design

Every page/feature must define:
- Loading
- Empty
- Success
- Validation error
- Permission error
- Network/server error
- Stale data where relevant

### Skeletons
Use skeletons for dashboard/list loading. Do not fake financial numbers during loading.

### Empty states
Use concise text and one next action. Example: `No gold purchases yet` + `Buy Gold`.

### Error states
Explain what the user can do next. Do not expose raw backend messages or stack traces.

## 22. Accessibility

Minimum target: WCAG 2.2 AA principles.

- Text/background contrast must meet AA.
- All controls keyboard accessible.
- Minimum interactive target around 44×44 px.
- Visible focus states.
- Form errors announced/accessibly associated.
- No status communicated only by color.
- Motion honors reduced-motion preference.
- Financial values have meaningful labels for screen readers.
- Decorative gold animation should be ignored by assistive technology.

## 23. Content and Voice

Voice should be:
- direct
- calm
- transparent
- encouraging without promising returns

Prefer:
- `Buy simulated gold`
- `Current simulated gold price`
- `Your gold holding`
- `Price updated. Review the new price before continuing.`

Avoid:
- `Guaranteed growth`
- `Earn profit`
- `Risk-free`
- aggressive trading language

## 24. Frontend Implementation Tokens

Suggested CSS variable foundation:

```css
:root {
  --background: #0B0D10;
  --navy: #0A1424;
  --surface: #12161D;
  --surface-elevated: #181D25;
  --foreground: #F7F7F5;
  --muted-foreground: #A9B0BA;
  --border: #2A313B;
  --gold: #C9A227;
  --gold-highlight: #F0D77B;
  --gold-dark: #8C6A1D;
  --success: #2FBF71;
  --warning: #F3B33D;
  --danger: #E45B5B;
  --radius-control: 12px;
  --radius-card: 16px;
}
```

The Tailwind implementation should map design tokens rather than scattering raw hex values across components.

## 25. Session 6 Implementation Scope

Session 6 should implement the design foundation and authentication experience only:

1. Add centralized theme/design tokens.
2. Configure Manrope through `next/font`.
3. Rebuild the approved logo concept as an SVG suitable for animation.
4. Build reusable primitives: Button, Input, FormField, Card, Alert, Badge, Spinner/Skeleton, Dialog where required.
5. Build public/auth layout foundations.
6. Implement Register, Login, Verify Email, Forgot Password, Reset Password.
7. Implement CSRF-aware API client and current-user/session handling.
8. Build customer and admin application shells/navigation without implementing their business pages yet.
9. Add responsive behavior and reduced-motion support.
10. Integrate with the real Session 5 authentication API.

Do not build wallet/gold business flows in Session 6; those begin in later vertical-slice sessions.

## 26. Design QA Checklist

A frontend change is not design-complete until:
- No ad-hoc colors outside approved tokens without justification.
- Forms have labels, error states, loading states, and keyboard focus.
- Desktop and mobile layouts are reviewed.
- Financial values are aligned/readable and use tabular numbers.
- Gold accent remains restrained.
- Motion has a functional purpose and reduced-motion fallback.
- No crypto/trading visual language accidentally dominates the product.
- Simulated-money/gold disclosures remain clear.
- Customer and admin experiences are visually distinct but belong to the same brand.

## 27. Screen Inventory for MVP

### Public
- Landing
- Terms
- Privacy
- Contact

### Authentication
- Register
- Login
- Verify Email
- Forgot Password
- Reset Password

### Customer
- Dashboard
- Wallet
- Gold Purchase
- Holdings
- Transactions
- Transaction Detail / Receipt
- Savings Goal
- Profile

### Admin
- Dashboard
- Customers
- Customer Detail
- Transactions
- Transaction Detail
- Ledger
- Gold Price History / Publish
- Audit Logs

## 28. Design Decision Summary

- Direction: premium/elegant fintech.
- Theme: one dark premium theme; no theme switcher in v1.
- Palette: deep navy/charcoal base, metallic gold accents, crisp white/soft gray neutrals.
- Gold use: restrained accent, not full-surface saturation.
- Density: balanced.
- Typography: Manrope-first modern sans serif.
- Motion: bold and polished in marketing/product milestones; restrained during financial decisions.
- Reference: Cash App supplied by the product owner for motion simplicity and confidence, not for direct visual copying.
- Logo: N + coin/ring + rising bars; production SVG required for animation.
