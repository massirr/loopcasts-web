// LoopCasts: the only JavaScript besides form validation (loopcasts-ui §7).
// The server sends the final state; this file only replays it as motion. No JS / reduced motion → final state.
const calm = matchMedia("(prefers-reduced-motion: reduce)").matches;

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
