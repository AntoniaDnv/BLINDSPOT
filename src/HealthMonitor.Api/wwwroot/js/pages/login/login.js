// Login screen (full-screen, no shell). Demo accounts are public test values from the README.
import { login } from "../../session.js";
import { getQuery, navigate, safeNext } from "../../router.js";

const DEMO = {
  c001: { email: "c001@blindspot.test", password: "Dragon-Orbit-7341" },
  surgeon: { email: "surgeon@blindspot.test", password: "Flight-Surgeon-9052" },
};

export function init(root) {
  const form = root.querySelector("#login-form");
  const email = root.querySelector("#login-email");
  const password = root.querySelector("#login-password");
  const submit = root.querySelector("#login-submit");
  const formError = root.querySelector("#login-error");
  const toggle = root.querySelector("#login-toggle");
  let busy = false;

  const fieldError = (input, message) => {
    const el = root.querySelector(`#${input.id}-err`);
    el.textContent = message ?? "";
    el.hidden = !message;
    input.setAttribute("aria-invalid", message ? "true" : "false");
    return message ? input : null;
  };

  toggle.addEventListener("click", () => {
    const show = password.type === "password";
    password.type = show ? "text" : "password";
    toggle.setAttribute("aria-pressed", String(show));
    toggle.setAttribute("aria-label", show ? "Hide password" : "Show password");
    toggle.querySelector(".icon").textContent = show ? "visibility_off" : "visibility";
  });

  root.querySelectorAll("[data-demo]").forEach((button) =>
    button.addEventListener("click", () => {
      const account = DEMO[button.dataset.demo];
      email.value = account.email;
      password.value = account.password;
      fieldError(email);
      fieldError(password);
      formError.hidden = true;
      submit.focus();
    }),
  );

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (busy) return;
    formError.hidden = true;
    const emailValue = email.value.trim();
    const firstInvalid = [
      fieldError(
        email,
        emailValue ? (/^\S+@\S+\.\S+$/.test(emailValue) ? null : "Enter a valid email address.") : "Enter your email.",
      ),
      fieldError(password, password.value ? null : "Enter your password."),
    ].find(Boolean);
    if (firstInvalid) {
      firstInvalid.focus();
      return;
    }

    busy = true;
    submit.disabled = true;
    submit.textContent = "Signing in...";
    try {
      await login(emailValue, password.value);
      navigate(safeNext(getQuery().get("next")));
    } catch (error) {
      formError.textContent = error.message;
      formError.hidden = false;
      password.select();
    } finally {
      busy = false;
      submit.disabled = false;
      submit.textContent = "Sign in";
    }
  });

  email.focus();
}
