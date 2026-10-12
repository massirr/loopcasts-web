// LoopCasts: the only JavaScript besides form validation (loopcasts-ui §7, §10).
// The server sends the final state; this file only adds motion and small conveniences. No JS → everything still works.
document.documentElement.classList.remove("no-js");
const calm = matchMedia("(prefers-reduced-motion: reduce)").matches;

// Menus (account menu + phone sheet): button with aria-expanded opens .menu-panel.
// Esc closes and gives focus back to the button, a click outside closes, focus moves to the first item.
document.querySelectorAll("[data-menu]").forEach(menu => {
  const button = menu.querySelector("[aria-expanded]");
  const items = () => [...menu.querySelectorAll(".menu-item")];
  const setOpen = open => {
    menu.classList.toggle("open", open);
    button.setAttribute("aria-expanded", open);
  };
  button.addEventListener("click", () => {
    const open = button.getAttribute("aria-expanded") !== "true";
    setOpen(open);
    if (open) items()[0]?.focus();
  });
  menu.addEventListener("keydown", e => {
    if (button.getAttribute("aria-expanded") !== "true") return;
    if (e.key === "Escape") { setOpen(false); button.focus(); }
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {   // arrows walk the items, Tab still works too
      e.preventDefault();
      const list = items(), i = list.indexOf(document.activeElement);
      list[(i + (e.key === "ArrowDown" ? 1 : -1) + list.length) % list.length].focus();
    }
  });
  document.addEventListener("click", e => { if (!menu.contains(e.target)) setOpen(false); });
  menu.addEventListener("focusout", e => { if (e.relatedTarget && !menu.contains(e.relatedTarget)) setOpen(false); });
});

// Submit buttons: busy state + no double submit (e.g. "Paying…"). Listening on document runs after jQuery
// validation, so an invalid form (defaultPrevented) is never blocked.
document.addEventListener("submit", e => {
  const form = e.target, button = e.submitter;
  if (form.dataset.busy) { e.preventDefault(); return; }
  if (e.defaultPrevented || form.hasAttribute("data-no-busy")) return;   // e.g. a download: the page stays
  form.dataset.busy = "1";
  if (!button) return;
  button.classList.add("is-busy");
  button.setAttribute("aria-disabled", "true");
  if (button.dataset.busyText) button.textContent = button.dataset.busyText;
});
// Back button brings the page back from the browser cache: make the buttons usable again
addEventListener("pageshow", e => {
  if (!e.persisted) return;
  document.querySelectorAll("form[data-busy]").forEach(f => delete f.dataset.busy);
  document.querySelectorAll(".is-busy").forEach(b => {
    b.classList.remove("is-busy");
    b.removeAttribute("aria-disabled");
    if (b.dataset.label) b.textContent = b.dataset.label;
  });
});
document.querySelectorAll("[data-busy-text]").forEach(b => b.dataset.label = b.textContent);

// "Copy link" buttons: shown only with JS; the plain text field next to it is the fallback
document.querySelectorAll("[data-copy]").forEach(button => {
  const fallback = document.getElementById(button.dataset.copy);
  const label = button.textContent;
  button.hidden = false;
  fallback.hidden = true;
  button.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(fallback.value);
      button.textContent = "Copied";
      button.classList.add("is-done");
      setTimeout(() => { button.textContent = label; button.classList.remove("is-done"); }, 2000);
    } catch {
      fallback.hidden = false;   // no clipboard access: show the text, selected, to copy by hand
      fallback.select();
    }
  });
});

// Home: "Play again" restarts the hero drawing (remove the class, force a reflow, add it again)
document.querySelectorAll("[data-replay]").forEach(button => {
  button.addEventListener("click", () => {
    const svg = document.getElementById(button.dataset.replay);
    svg.classList.remove("draw");
    void svg.getBoundingClientRect();
    svg.classList.add("draw");
  });
});

// Order confirmation: the steps that really happened (class "on" from the server) light up one by one
document.querySelectorAll("[data-sequence]").forEach(list => {
  const done = [...list.querySelectorAll(".step.on")];
  if (calm) return;
  done.forEach(step => step.classList.remove("on"));
  done.forEach((step, i) => setTimeout(() => step.classList.add("on"), 250 + i * 350));
});
