import { SERVER_DOWN_MESSAGE } from "./utils.js";
import { clearSession } from "./session.js";

export class ApiError extends Error {
  constructor(status, message, details = null) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.details = details;
  }
}

async function request(method, url, body) {
  const options = {
    method,
    headers: { Accept: "application/json" },
  };

  if (body !== undefined) {
    options.headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(body);
  }

  let response;

  try {
    response = await fetch(url, options);
  } catch (networkError) {
    // status 0 = network failure; describeError() in utils.js relies on it.
    throw new ApiError(0, SERVER_DOWN_MESSAGE, networkError);
  }

  if (response.status === 204) {
    return null;
  }

  const isJson = response.headers.get("content-type")?.includes("json");
  const data = isJson ? await response.json() : await response.text();

  if (response.status === 401 && !url.startsWith("/api/auth/")) {
    // Session expired or signed out: drop it and go to the login screen.
    clearSession();
    const here = window.location.hash.slice(1) || "/";
    if (!here.startsWith("/login") && !here.startsWith("/signup")) {
      window.location.hash = `#/login?next=${encodeURIComponent(here)}`;
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, extractMessage(data, response), data);
  }

  return data;
}

function extractMessage(data, response) {
  if (data && typeof data === "object") {
    // Validation errors: { errors: { Name: ["The Name field is required."] } }
    if (data.errors) {
      return Object.values(data.errors).flat().join(" ");
    }

    return data.error || data.detail || data.title || `Request failed (${response.status})`;
  }

  return data || `Request failed (${response.status})`;
}

export const api = {
  get: (url) => request("GET", url),
  post: (url, body) => request("POST", url, body),
  put: (url, body) => request("PUT", url, body),
  delete: (url) => request("DELETE", url),
};
