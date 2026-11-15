/** Escape user/API text before putting it into innerHTML (prevents XSS). */
export function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

/** "2026-11-14T08:00:00Z" -> local, human-readable date + time. */
export function formatDateTime(iso) {
  if (!iso) {
    return "";
  }

  return new Date(iso).toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/** "2026-11-14T08:00:00Z" -> short label for chart axes, e.g. "Nov 14, 08:00". */
export function formatShortDateTime(iso) {
  if (!iso) {
    return "";
  }

  return new Date(iso).toLocaleString(undefined, {
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/** Message shown whenever the browser cannot reach the BLINDSPOT server at all. */
export const SERVER_DOWN_MESSAGE =
  "The BLINDSPOT server is not responding. Start it with `dotnet run`, then retry.";

/** Event the router listens to: re-runs the current page (destroy + init). */
export const RELOAD_EVENT = "app:reload";

/** Ask the router to reload the current page. Default action of every Retry button. */
export function reloadCurrentPage() {
  window.dispatchEvent(new CustomEvent(RELOAD_EVENT));
}

/**
 * Human-readable message for a failed request.
 * Network failures (fetch TypeError, or ApiError with status 0) get SERVER_DOWN_MESSAGE;
 * API errors keep their own message.
 */
export function describeError(error) {
  if (!error) {
    return "Something went wrong.";
  }
  if (error instanceof TypeError || error.status === 0) {
    return SERVER_DOWN_MESSAGE;
  }
  return error.message || "Something went wrong.";
}

/** Escape text, then turn `backticks` into <code> (used for the dotnet run hint). */
function formatMessageHtml(text) {
  return escapeHtml(text).replace(/`([^`]+)`/g, "<code>$1</code>");
}

function stateIcon(name) {
  return `<span class="state__icon" aria-hidden="true"><span class="icon material-symbols-outlined">${name}</span></span>`;
}

/**
 * Replace a container's content with the styled error state (.state--error).
 * @param {HTMLElement} container
 * @param {object} [options]
 * @param {string} [options.title]    default "Could not load data"
 * @param {string} [options.message]  shown under the title; `code` is allowed
 * @param {unknown} [options.error]   used for the message when `message` is not given
 * @param {() => void} [options.onRetry]  default: reload the current page
 * @param {boolean} [options.compact] inline row layout for use inside a card
 */
export function renderErrorState(container, options = {}) {
  if (!container) {
    return;
  }
  const {
    title = "Could not load data",
    error,
    message = describeError(error),
    onRetry = reloadCurrentPage,
    compact = false,
  } = options;

  container.classList.remove("hidden");
  container.innerHTML = `
    <div class="state state--error${compact ? " state--compact" : ""}" role="alert">
      ${stateIcon("cloud_off")}
      <div class="state__body">
        <p class="state__title">${escapeHtml(title)}</p>
        <p class="state__text">${formatMessageHtml(message)}</p>
      </div>
      <div class="state__actions">
        <button type="button" class="btn btn--primary" data-action="retry">
          <span class="icon material-symbols-outlined" aria-hidden="true">refresh</span>
          Retry
        </button>
      </div>
    </div>`;

  const button = container.querySelector('[data-action="retry"]');
  button.addEventListener("click", () => {
    button.disabled = true;
    onRetry();
  });
}

/**
 * Replace a container's content with a loading placeholder (skeleton shimmer).
 * @param {HTMLElement} container
 * @param {string} [label] announced to screen readers
 */
export function renderLoading(container, label = "Loading data") {
  if (!container) {
    return;
  }
  container.classList.remove("hidden");
  container.innerHTML = `
    <div class="loading-layout" role="status" aria-busy="true">
      <span class="visually-hidden">${escapeHtml(label)}</span>
      <span class="skeleton skeleton--title"></span>
      <span class="skeleton skeleton--text"></span>
      <span class="skeleton skeleton--text" style="width: 70%"></span>
      <div class="grid-3">
        <span class="skeleton skeleton--block"></span>
        <span class="skeleton skeleton--block"></span>
        <span class="skeleton skeleton--block"></span>
      </div>
    </div>`;
}

/**
 * Legacy helper used by the pre-redesign pages: show a message in an element.
 * type: 'error' | 'success' | 'info'. Errors render the styled error state with
 * a Retry button (reloads the page); other types stay a simple .alert line.
 */
export function showMessage(element, text, type = "info") {
  if (type === "error") {
    element.className = "";
    renderErrorState(element, { title: "Something went wrong", message: text, compact: true });
    return;
  }
  element.removeAttribute("role");
  element.textContent = text;
  element.className = `alert alert-${type}`;
}

export function hideMessage(element) {
  element.classList.add("hidden");
}
