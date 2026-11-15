// Entry point - loaded by index.html as <script type="module">.
import { initShell } from "./shell.js";
import { startRouter } from "./router.js";

initShell(); // must run first: it listens for the router's first route change
startRouter(document.getElementById("app"));
