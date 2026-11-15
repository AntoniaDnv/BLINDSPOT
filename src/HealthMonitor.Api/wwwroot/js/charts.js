// Chart.js helpers themed to the design system (css/style.css tokens).

/** Hairline grid colour from DESIGN.md (outline-variant at 40%). */
const GRID_COLOR = "rgba(62, 72, 79, 0.4)";

/** Read a CSS custom property from :root, e.g. cssVar("--secondary"). */
export function cssVar(name, fallback = "") {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(name)
    .trim();

  return value || fallback;
}

/** Theme colours for charts, read from the CSS tokens (call after styles load). */
export function themeColors() {
  return {
    primary: cssVar("--primary-container", "#38bdf8"),
    primaryLight: cssVar("--primary", "#8ed5ff"),
    secondary: cssVar("--secondary", "#44e2cd"),
    tertiary: cssVar("--tertiary", "#c5c9ff"),
    ok: cssVar("--secondary", "#44e2cd"),
    warning: cssVar("--warning", "#fbbf24"),
    critical: cssVar("--critical", "#f87171"),
    text: cssVar("--on-surface", "#dee2f4"),
    textMuted: cssVar("--on-surface-variant", "#bdc8d1"),
    grid: GRID_COLOR,
    surface: cssVar("--surface-container-low", "#161b28"),
    tooltip: cssVar("--surface-container-high", "#252a37"),
    border: cssVar("--outline-variant", "#3e484f"),
  };
}

/** "#38bdf8" + 0.2 -> "rgba(56, 189, 248, 0.2)". Non-hex colours are returned unchanged. */
export function withAlpha(color, alpha) {
  const hex = String(color).trim().replace("#", "");
  const full =
    hex.length === 3 ? [...hex].map((c) => c + c).join("") : hex;
  if (!/^[0-9a-f]{6}$/i.test(full)) {
    return color;
  }
  const [r, g, b] = [0, 2, 4].map((i) => parseInt(full.slice(i, i + 2), 16));
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function palette() {
  const c = themeColors();
  return [c.primary, c.secondary, c.tertiary, c.warning, c.critical, c.primaryLight];
}

/** Apply the theme to Chart.js global defaults once (Inter, muted ticks, hairline grid). */
function applyThemeDefaults() {
  const Chart = window.Chart;
  if (typeof Chart === "undefined" || Chart.__blindspotThemed) {
    return;
  }
  const c = themeColors();
  const d = Chart.defaults;

  d.font.family = '"Inter", system-ui, sans-serif';
  d.font.size = 12;
  d.color = c.textMuted;
  d.borderColor = GRID_COLOR;

  d.scale.grid.color = GRID_COLOR;
  d.scale.border.color = GRID_COLOR;
  d.scale.ticks.color = c.textMuted;

  d.elements.line.borderWidth = 1.5;
  d.elements.point.radius = 2;
  d.elements.point.hoverRadius = 4;

  d.plugins.legend.labels.color = c.textMuted;
  d.plugins.legend.labels.usePointStyle = true;
  d.plugins.legend.labels.boxWidth = 8;
  d.plugins.legend.labels.boxHeight = 8;

  const tooltip = d.plugins.tooltip;
  tooltip.backgroundColor = c.tooltip;
  tooltip.borderColor = c.border;
  tooltip.borderWidth = 1;
  tooltip.titleColor = c.text;
  tooltip.bodyColor = c.textMuted;
  tooltip.padding = 10;
  tooltip.cornerRadius = 6;
  tooltip.boxPadding = 4;
  tooltip.titleFont = { family: d.font.family, weight: "600", size: 12 };
  tooltip.bodyFont = { family: d.font.family, size: 12 };

  Chart.__blindspotThemed = true;
}

applyThemeDefaults();

/**
 * Line chart for values over time.
 * @param {HTMLCanvasElement} canvas
 * @param {string[]} labels     x-axis labels
 * @param {{label: string, data: (number|null)[]}[]} datasets
 * @param {object} [extraOptions] merged into Chart.js options
 */
export function createLineChart(canvas, labels, datasets, extraOptions = {}) {
  if (typeof window.Chart === "undefined") {
    throw new Error(
      "Chart.js is not loaded. Check the <script> tag in index.html.",
    );
  }
  applyThemeDefaults();
  const colors = palette();

  return new window.Chart(canvas, {
    type: "line",
    data: {
      labels,
      datasets: datasets.map((ds, i) => ({
        borderColor: colors[i % colors.length],
        backgroundColor: colors[i % colors.length],
        tension: 0.3,
        spanGaps: true,
        ...ds,
      })),
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "index", intersect: false },
      ...extraOptions,
    },
  });
}
