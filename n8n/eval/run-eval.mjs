// Runs the 15 test questions against the chat assistant and writes the answers + automatic pass/fail into
// docs/ai-evaluation.md (between the "results" markers). Node 18+, no packages.
//   node n8n/eval/run-eval.mjs http://localhost:5678/webhook/<chat webhook id>/chat
// Automatic check: every "expect" pattern must match, no "forbid" pattern may match (case-insensitive).
// Read the answers yourself too: the patterns catch the obvious failures, not every bad answer.
import { readFileSync, writeFileSync } from "node:fs";

const url = process.argv[2];
if (!url) { console.error("Usage: node n8n/eval/run-eval.mjs <chat webhook URL>"); process.exit(1); }
const root = new URL("../../", import.meta.url);
const questions = JSON.parse(readFileSync(new URL("n8n/eval/questions.json", root)));
const docPath = new URL("docs/ai-evaluation.md", root);
const cell = s => s.replace(/\|/g, "\\|").replace(/\s*\n\s*/g, "<br>");
const run = Date.now();

const rows = [];
for (const q of questions) {
  let answer;
  try {
    const res = await fetch(url, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "sendMessage", sessionId: `eval-${run}-${q.id}`, chatInput: q.question }),
    });
    const body = await res.json().catch(() => ({}));
    answer = body.output ?? `(no answer: HTTP ${res.status} ${body.message ?? ""})`;
  } catch (e) { answer = `(no answer: ${e.message})`; }
  const missing = q.expect.filter(p => !new RegExp(p, "i").test(answer));
  const found = q.forbid.filter(p => new RegExp(p, "i").test(answer));
  const pass = !answer.startsWith("(no answer") && missing.length === 0 && found.length === 0;
  const why = pass ? "" : [...missing.map(p => `missing /${p}/`), ...found.map(p => `contains /${p}/`)].join(", ") || "no answer";
  console.log(`${q.id}. ${pass ? "PASS" : "FAIL"} ${q.question}\n   → ${answer.slice(0, 200)}${why ? `\n   (${why})` : ""}`);
  rows.push(`| ${q.id} | ${q.type} | ${cell(q.question)} | ${cell(answer)} | ${pass ? "pass" : `**fail** (${cell(why)})`} |`);
  await new Promise(r => setTimeout(r, 7000));   // Gemini free tier: a few requests per minute
}

const passed = rows.filter(r => r.endsWith("| pass |")).length;
const table = [`Run ${new Date().toISOString().slice(0, 16).replace("T", " ")} UTC: **${passed}/${questions.length} pass**.`, "",
  "| # | Type | Question | Answer | Result |", "|---|---|---|---|---|", ...rows].join("\n");
const doc = readFileSync(docPath, "utf8");
writeFileSync(docPath, doc.replace(/(<!-- results:start -->)[\s\S]*(<!-- results:end -->)/, `$1\n${table}\n$2`));
console.log(`\n${passed}/${questions.length} pass → docs/ai-evaluation.md`);
