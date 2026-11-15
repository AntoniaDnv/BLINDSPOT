// Sign-up screen (full-screen, no shell). The server always creates an astronaut.
import { signup } from "../../session.js";
import { navigate } from "../../router.js";

const MIN_PASSWORD = 8;

export function init(root) {
  const form = root.querySelector("#signup-form");
  const name = root.querySelector("#signup-name");
  const email = root.querySelector("#signup-email");
  const password = root.querySelector("#signup-password");
  const submit = root.querySelector("#signup-submit");
  const formError = root.querySelector("#signup-error");
  const toggle = root.querySelector("#signup-toggle");
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

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (busy) return;
    formError.hidden = true;
    const nameValue = name.value.trim();
    const emailValue = email.value.trim();
    const firstInvalid = [
      fieldError(name, nameValue ? null : "Enter a display name."),
      fieldError(
        email,
        emailValue ? (/^\S+@\S+\.\S+$/.test(emailValue) ? null : "Enter a valid email address.") : "Enter your email.",
      ),
      fieldError(
        password,
        password.value.length >= MIN_PASSWORD && /[A-Za-z]/.test(password.value) && /[0-9]/.test(password.value)
          ? null
          : `Use at least ${MIN_PASSWORD} characters, with a letter and a digit.`,
      ),
    ].find(Boolean);
    if (firstInvalid) {
      firstInvalid.focus();
      return;
    }

    busy = true;
    submit.disabled = true;
    submit.textContent = "Creating account...";
    try {
      await signup(nameValue, emailValue, password.value);
      navigate("/");
    } catch (error) {
      formError.textContent = error.message;
      formError.hidden = false;
    } finally {
      busy = false;
      submit.disabled = false;
      submit.textContent = "Create account";
    }
  });

  name.focus();
}
