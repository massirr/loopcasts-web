// Cross-check of the course catalogue (#31): catalogue vs Vimeo export vs the old website's course pages.
// Read-only. Node 18+, no packages. Fills the tables in course-catalogue-crosscheck.md (between the "tables" markers).
//   node docs/analysis/course-catalogue-crosscheck.mjs ../LoopCasts/dropbox
// The old website folder is only LISTED (file names), never read: its pages stay off-limits.
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const dropbox = process.argv[2] ?? "../LoopCasts/dropbox";
const root = new URL("../../", import.meta.url);
const docPath = new URL("docs/analysis/course-catalogue-crosscheck.md", root);

// "HubSpot + n8n", "hubspot-n8n.html" and "HUBSPOT  n8n" all become "hubspot n8n"
const key = s => s.toLowerCase().replace(/\.html$/, "").replace(/[^a-z0-9]+/g, " ").trim();

// Duration in minutes, the same rules as Data/CourseCsvImporter.cs (ponytail: copied, 6 lines; the C# one is tested)
const minutes = raw => {
  let m;
  if ((m = raw.match(/^(\d+)\s*h\s*(\d+)?$/i))) return +m[1] * 60 + +(m[2] ?? 0);
  if ((m = raw.match(/^(\d+)\s*min$/i))) return +m[1];
  if ((m = raw.match(/^(\d+):(\d{2}):(\d{2})$/))) return +m[1] * 60 + +m[2];
  if ((m = raw.match(/^(\d+)$/))) return +m[1];
  return null;
};

// 1. Catalogue: the committed export of "course catalogue.xlsx"
const catalogue = readFileSync(new URL("Data/Seed/course-catalogue.csv", root), "utf8").trim().split("\n").slice(1)
  .map(l => l.split(";")).map(([title, , videos, duration, status]) =>
    ({ title, status, videos: videos ? +videos : null, minutes: minutes(duration) }));

// 2. Vimeo export: one row per video, grouped by folder (simple CSV: no quoted commas in this export)
const vimeoRows = readFileSync(join(dropbox, "Courses", "vimeo export 2026-06-30.csv"), "utf8").trim().split("\n").slice(1)
  .map(l => l.split(","));
const vimeo = new Map();
for (const [, , folder, seconds] of vimeoRows) {
  const f = vimeo.get(key(folder)) ?? { folder, videos: 0, seconds: 0 };
  f.videos++; f.seconds += +seconds;
  vimeo.set(key(folder), f);
}

// 3. Old website: file names only
const pages = new Map(readdirSync(join(dropbox, "Website", "site", "courses"))
  .filter(f => f.endsWith(".html") && !f.startsWith("._")).map(f => [key(f), f]));

const inCatalogue = new Map(catalogue.map(c => [key(c.title), c]));
const table = (head, rows) => rows.length === 0 ? "_None._" :
  [`| ${head.join(" | ")} |`, `|${head.map(() => "---").join("|")}|`, ...rows.map(r => `| ${r.join(" | ")} |`)].join("\n");

const notOnVimeo = catalogue.filter(c => !vimeo.has(key(c.title))).map(c => [c.title, c.status]);
const notInCatalogue = [...vimeo.values()].filter(v => !inCatalogue.has(key(v.folder))).map(v => [v.folder, v.videos]);
const liveNoPage = catalogue.filter(c => c.status === "live" && !pages.has(key(c.title))).map(c => [c.title]);
const pageNotLive = catalogue.filter(c => c.status !== "live" && pages.has(key(c.title)))
  .map(c => [c.title, c.status, `\`${pages.get(key(c.title))}\``]);
const pageNoCourse = [...pages].filter(([k]) => !inCatalogue.has(k)).map(([, f]) => [`\`${f}\``]);

const TOLERANCE = 5;   // minutes: the catalogue rounds, Vimeo counts seconds
const mismatches = catalogue.filter(c => vimeo.has(key(c.title))).map(c => {
  const v = vimeo.get(key(c.title)), vMin = Math.round(v.seconds / 60);
  const videos = c.videos !== v.videos, duration = c.minutes === null || Math.abs(c.minutes - vMin) > TOLERANCE;
  return (videos || duration) && [c.title, c.status,
    videos ? `**${c.videos ?? "?"}** vs **${v.videos}**` : `${v.videos}`,
    duration ? `**${c.minutes ?? "?"}** vs **${vMin}**` : `${c.minutes} vs ${vMin}`];
}).filter(Boolean);

const count = s => catalogue.filter(c => c.status === s).length;
const tables = `Generated ${new Date().toISOString().slice(0, 10)} from: catalogue ${catalogue.length} courses (${count("live")} live,
${count("retired")} retired, ${count("draft")} draft) · Vimeo ${vimeoRows.length} videos in ${vimeo.size} folders ·
old website ${pages.size} course pages.

### In the catalogue, not on Vimeo (${notOnVimeo.length})
${table(["Course", "Status"], notOnVimeo)}

### On Vimeo, not in the catalogue (${notInCatalogue.length})
${table(["Vimeo folder", "Videos"], notInCatalogue)}

### Live, but no page on the old website (${liveNoPage.length})
${table(["Course"], liveNoPage)}

### Page on the old website, but retired or draft (${pageNotLive.length})
${table(["Course", "Status", "Page"], pageNotLive)}

### Page on the old website, no course in the catalogue (${pageNoCourse.length})
${table(["Page"], pageNoCourse)}

### Video count or duration differs (${mismatches.length} of ${catalogue.length - notOnVimeo.length} on Vimeo)
Catalogue vs Vimeo. Duration in minutes, different when more than ${TOLERANCE} min apart or unknown ("?").
${table(["Course", "Status", "Videos", "Minutes"], mismatches)}`;

const doc = readFileSync(docPath, "utf8");
writeFileSync(docPath, doc.replace(/(<!-- tables:start -->)[\s\S]*(<!-- tables:end -->)/, `$1\n${tables}\n$2`));
console.log(tables);
