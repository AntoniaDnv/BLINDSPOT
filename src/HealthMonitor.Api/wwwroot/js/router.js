import { RELOAD_EVENT, renderErrorState } from "./utils.js";

const routes = {
  "/": "home",
  "/samples": "samples",
  "/external": "external",
  "/blood-panels": "blood-panels",
  "/nutrition": "nutrition",
  "/radiation": "radiation",
  "/log": "log",
  // '/dashboard': 'dashboard',
};

const NOT_FOUND_PAGE = "not-found";

/**
 * Fired on window at the start of every render with detail { path, pageName }.
 * js/shell.js uses it to highlight the nav and to close the mobile drawer on
 * every route change (nav click, Back/Forward, navigate()).
 */
export const ROUTE_CHANGE_EVENT = "app:routechange";

const templateCache = new Map();

let currentPage = null;
let appRoot = null;
let renderId = 0;

export function startRouter(root) {
  appRoot = root;
  window.addEventListener("hashchange", render);
  window.addEventListener(RELOAD_EVENT, render); // Retry buttons re-run the page
  render();
}

/** Navigate from code, e.g. navigate('/samples'). */
export function navigate(path) {
  window.location.hash = `#${path}`;
}

async function render() {
  const myId = ++renderId;
  const path = getPath();
  const pageName = routes[path] ?? NOT_FOUND_PAGE;

  window.dispatchEvent(
    new CustomEvent(ROUTE_CHANGE_EVENT, { detail: { path, pageName } }),
  );

  try {
    currentPage?.destroy?.();
    currentPage = null;

    const html = await loadTemplate(pageName);
    if (myId !== renderId) return; // a newer navigation started meanwhile
    appRoot.innerHTML = html;

    const module = await import(`./pages/${pageName}/${pageName}.js`);
    if (myId !== renderId) return;
    currentPage = module;
    await module.init?.(appRoot);

    if (myId === renderId) window.scrollTo(0, 0);
  } catch (err) {
    if (myId !== renderId) return;
    console.error(`Failed to load page "${pageName}"`, err);
    renderErrorState(appRoot, {
      title: "This page could not be loaded",
      error: err,
      message:
        err instanceof TypeError
          ? undefined
          : `The page "${pageName}" failed to load. Details are in the browser console.`,
      onRetry: render,
    });
  }
}

function getPath() {
  // "#/samples?x=1" -> "/samples"
  const hash = window.location.hash.slice(1) || "/";
  return hash.split("?")[0] || "/";
}

async function loadTemplate(pageName) {
  if (templateCache.has(pageName)) {
    return templateCache.get(pageName);
  }

  const response = await fetch(`/js/pages/${pageName}/${pageName}.html`);
  if (!response.ok) {
    throw new Error(
      `Template not found for page "${pageName}" (HTTP ${response.status})`,
    );
  }

  const html = await response.text();
  templateCache.set(pageName, html);
  return html;
}
