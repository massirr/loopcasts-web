// How much text does an agent load before it starts working? (#35)  Run: node docs/analysis/context-budget.mjs
// Tokens ≈ bytes / 4 (rough rule for English text and code; good enough to compare before/after).
// DEFAULT = what CLAUDE.md, the skills and the workflow make every session load. Update it when they change.
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";

const DEFAULT = [
  // [label, path, part]  part "front" = only the frontmatter (Claude Code loads every skill's name + description)
  ["auto: project instructions", "CLAUDE.md"],
  ...readdirSync(".claude/skills").map(s => [`auto: skill list (${s})`, `.claude/skills/${s}/SKILL.md`, "front"]),
  ["session start: read first", "docs/handover.md"],
  ["every issue: spec template", "docs/specs/_template.md"],
  ["every commit: skill body", ".claude/skills/security-check/SKILL.md"],
  ["every issue: skill body", ".claude/skills/agent-log/SKILL.md"],
  // before #35 also: docs/agent-log.md, read whole to append an entry with Edit (7,618 tokens, see context-budget.md)
];

const text = (p, part) => {
  const s = readFileSync(p, "utf8");
  return part === "front" ? s.slice(0, s.indexOf("\n---", 4) + 4) : s;
};
const row = (label, p, bytes) => `| ${label} | ${p && `\`${p}\``} | ${bytes.toLocaleString("en")} | ${Math.round(bytes / 4).toLocaleString("en")} |`;

let total = 0;
console.log("| Loaded by default | File | Bytes | ~Tokens |\n|---|---|---:|---:|");
for (const [label, p, part] of DEFAULT) {
  const bytes = Buffer.byteLength(text(p, part));
  total += bytes;
  console.log(row(label, p, bytes));
}
console.log(row("**Total**", "", total));

const walk = d => readdirSync(d).flatMap(f => {
  const p = join(d, f);
  return f.startsWith("._") || f === ".DS_Store" ? [] : statSync(p).isDirectory() ? walk(p) : [p];
});
const docs = walk("docs").filter(p => /\.(md|mjs|html)$/.test(p)).sort();
let docsTotal = 0;
console.log("\n| docs/ text file | Bytes | ~Tokens |\n|---|---:|---:|");
for (const p of docs) {
  const bytes = statSync(p).size;
  docsTotal += bytes;
  console.log(`| \`${p}\` | ${bytes.toLocaleString("en")} | ${Math.round(bytes / 4).toLocaleString("en")} |`);
}
console.log(`| **Total (${docs.length} files)** | ${docsTotal.toLocaleString("en")} | ${Math.round(docsTotal / 4).toLocaleString("en")} |`);
