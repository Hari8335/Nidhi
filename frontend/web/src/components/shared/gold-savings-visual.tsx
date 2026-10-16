import { useId } from "react";

/**
 * Custom Nidhi gold-savings visual component.
 * Illustrates simulated savings accumulation:
 * - Partial circular gold arc derived from the Nidhi coin mark
 * - Ascending fractional savings bars representing micro-saving steps
 * - Floating simulated holding card with explicit decorative values
 * - Static gold accents
 * - Restrained metallic depth and lighting
 * Respects prefers-reduced-motion.
 */
export function GoldSavingsVisual() {
  const arcGradId = useId();
  const glowGradId = useId();
  const cardBorderGradId = useId();

  return (
    <div className="gold-visual-root" aria-hidden="true">
      {/* Ambient background glow */}
      <div className="gold-visual-glow" />

      {/* SVG Arc and Orbit System */}
      <svg
        className="gold-visual-svg"
        viewBox="0 0 540 540"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <defs>
          <linearGradient id={arcGradId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#7A5815" />
            <stop offset="35%" stopColor="#C9A227" />
            <stop offset="65%" stopColor="#F3DE8A" />
            <stop offset="100%" stopColor="#B88919" />
          </linearGradient>

          <radialGradient id={glowGradId} cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#C9A227" stopOpacity="0.25" />
            <stop offset="50%" stopColor="#8C6A1D" stopOpacity="0.08" />
            <stop offset="100%" stopColor="#0A1424" stopOpacity="0" />
          </radialGradient>

          <linearGradient id={cardBorderGradId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#C9A227" stopOpacity="0.6" />
            <stop offset="50%" stopColor="#2A313B" stopOpacity="0.3" />
            <stop offset="100%" stopColor="#F0D77B" stopOpacity="0.4" />
          </linearGradient>
        </defs>

        {/* Ambient radial fill */}
        <circle cx="270" cy="270" r="230" fill={`url(#${glowGradId})`} />

        {/* Concentric subtle guide rings */}
        <circle
          cx="270"
          cy="270"
          r="210"
          stroke="#2A313B"
          strokeWidth="1"
          strokeDasharray="4 8"
          opacity="0.4"
        />
        <circle
          cx="270"
          cy="270"
          r="165"
          stroke="#2A313B"
          strokeWidth="1"
          strokeDasharray="2 6"
          opacity="0.35"
        />

        {/* Outer primary gold accumulation arc */}
        <circle
          className="gold-arc-primary"
          cx="270"
          cy="270"
          r="210"
          stroke={`url(#${arcGradId})`}
          strokeWidth="3.5"
          strokeLinecap="round"
          strokeDasharray="1319"
          strokeDashoffset="380"
          transform="rotate(-90 270 270)"
        />

        {/* Inner secondary gold accent arc */}
        <circle
          className="gold-arc-secondary"
          cx="270"
          cy="270"
          r="165"
          stroke={`url(#${arcGradId})`}
          strokeWidth="2"
          strokeLinecap="round"
          strokeDasharray="1036"
          strokeDashoffset="450"
          transform="rotate(45 270 270)"
          opacity="0.75"
        />

        {/* Ascending savings growth bars */}
        <g className="gold-growth-bars" transform="translate(195, 360)">
          <rect
            className="growth-bar growth-bar-1"
            x="0"
            y="-20"
            width="8"
            height="24"
            rx="4"
            fill="#8C6A1D"
          />
          <rect
            className="growth-bar growth-bar-2"
            x="18"
            y="-38"
            width="8"
            height="42"
            rx="4"
            fill="#C9A227"
          />
          <rect
            className="growth-bar growth-bar-3"
            x="36"
            y="-58"
            width="8"
            height="62"
            rx="4"
            fill="#C9A227"
          />
          <rect
            className="growth-bar growth-bar-4"
            x="54"
            y="-82"
            width="8"
            height="86"
            rx="4"
            fill="#F0D77B"
          />
          <rect
            className="growth-bar growth-bar-5"
            x="72"
            y="-112"
            width="8"
            height="116"
            rx="4"
            fill="#F3DE8A"
          />
        </g>

        {/* Static gold accents */}
        <circle className="gold-particle particle-1" cx="370" cy="95" r="3.5" fill="#F0D77B" />
        <circle className="gold-particle particle-2" cx="445" cy="180" r="2.5" fill="#C9A227" />
        <circle className="gold-particle particle-3" cx="115" cy="220" r="3" fill="#F3DE8A" />
        <circle className="gold-particle particle-4" cx="420" cy="390" r="2" fill="#C9A227" />
        <circle className="gold-particle particle-5" cx="160" cy="380" r="2.5" fill="#8C6A1D" />
      </svg>

      {/* Floating simulated holding card */}
      <div className="holding-card-wrapper">
        <div className="floating-holding-card">
          <div className="card-top-row">
            <span className="holding-pill">
              <span className="holding-dot" />
              DEMO
            </span>
            <span className="holding-karat">SIMULATION PREVIEW</span>
          </div>

          <div className="holding-balance-section">
            <p className="holding-sublabel">SIMULATED GOLD HOLDING</p>
            <div className="holding-primary-val">
              <span className="holding-grams">1.4825</span>
              <span className="holding-unit">g</span>
            </div>
            <p className="holding-equiv">≈ LKR 48,250.00 (indicative)</p>
          </div>

          <div className="holding-progress-divider" />

          <div className="holding-footer-row">
            <div className="holding-metric">
              <span className="metric-label">Simulated save</span>
              <span className="metric-value">LKR 1,500</span>
            </div>
            <div className="holding-metric-arrow">→</div>
            <div className="holding-metric">
              <span className="metric-label">Simulated gold</span>
              <span className="metric-value font-gold">+0.0460 g</span>
            </div>
          </div>
        </div>
      </div>

      {/* Secondary micro milestone pill */}
      <div className="milestone-pill-wrapper">
        <div className="milestone-pill">
          <span className="milestone-icon">→</span>
          <span className="milestone-text">Demo goal: 1.4825 g of 5.0000 g</span>
        </div>
      </div>
    </div>
  );
}

/**
 * Compact Nidhi savings illustration for Auth pages and compact headers.
 */
export function AuthSavingsMark() {
  return (
    <div className="auth-mark-root" aria-hidden="true">
      <div className="auth-mark-coin">
        <svg viewBox="0 0 120 120" fill="none" className="auth-coin-svg">
          <circle
            cx="60"
            cy="60"
            r="48"
            stroke="url(#auth-coin-grad)"
            strokeWidth="3"
            strokeDasharray="301"
            className="auth-coin-ring"
          />
          <defs>
            <linearGradient id="auth-coin-grad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#8C6A1D" />
              <stop offset="45%" stopColor="#C9A227" />
              <stop offset="100%" stopColor="#F3DE8A" />
            </linearGradient>
          </defs>
          {/* Stylized N mark with ascending bars */}
          <path
            d="M44 76V44M44 44L72 76M72 44V76"
            stroke="#F7F7F5"
            strokeWidth="3.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity="0.9"
          />
          <rect x="78" y="66" width="3.5" height="10" rx="1.75" fill="#8C6A1D" />
          <rect x="84" y="58" width="3.5" height="18" rx="1.75" fill="#C9A227" />
          <rect x="90" y="48" width="3.5" height="28" rx="1.75" fill="#F0D77B" />
        </svg>
      </div>
      <div className="auth-mark-caption">
        <span className="caption-gold">Simulated gold micro-savings</span>
        <span className="caption-muted">One small step at a time</span>
      </div>
    </div>
  );
}
