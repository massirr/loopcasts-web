# Course catalogue cross-check (#31)

LoopCasts keeps its course list by hand in three places. This report compares them by course title (ignoring case,
spaces and punctuation, so "HubSpot + n8n" = `hubspot-n8n.html`):

| Source | What it holds |
|---|---|
| Catalogue (`course catalogue.xlsx` → `Data/Seed/course-catalogue.csv`) | title, level, videos, duration, status (live/retired/draft), published, price |
| Vimeo export (`vimeo export 2026-06-30.csv`) | one row per video; the folder = the course |
| Old website (`Website/site/courses/`) | one HTML page per course; only the **file names** were listed, the pages were not opened |

Re-run: `node docs/analysis/course-catalogue-crosscheck.mjs ../LoopCasts/dropbox` (read-only; rewrites the tables below).

## Results
<!-- tables:start -->
Generated 2026-10-20 from: catalogue 60 courses (53 live,
4 retired, 3 draft) · Vimeo 599 videos in 59 folders ·
old website 54 course pages.

### In the catalogue, not on Vimeo (1)
| Course | Status |
|---|---|
| n8n basics (2026 edition) | draft |

### On Vimeo, not in the catalogue (0)
_None._

### Live, but no page on the old website (2)
| Course |
|---|
| Agents with memory |
| Multi-agent setups |

### Page on the old website, but retired or draft (3)
| Course | Status | Page |
|---|---|---|
| Zapier to n8n migration | retired | `zapier-to-n8n-migration.html` |
| Invoices without typing | draft | `invoices-without-typing.html` |
| OAuth2 without tears | retired | `oauth2-without-tears.html` |

### Page on the old website, no course in the catalogue (0)
_None._

### Video count or duration differs (31 of 59 on Vimeo)
Catalogue vs Vimeo. Duration in minutes, different when more than 5 min apart or unknown ("?").
| Course | Status | Videos | Minutes |
|---|---|---|---|
| n8n basics | live | 12 | **80** vs **103** |
| Webhooks from zero | live | 9 | **55** vs **82** |
| REST APIs for data people | live | 14 | **105** vs **119** |
| Shell scripting that saves your week | live | 11 | **70** vs **93** |
| Cron and schedulers | live | 7 | **48** vs **76** |
| GitHub Actions in practice | live | 10 | **65** vs **92** |
| n8n error handling | live | 8 | **52** vs **67** |
| Prompt engineering for workflows | live | 10 | **75** vs **95** |
| AI agents with n8n | live | 13 | **110** vs **127** |
| Invoices without typing | draft | 8 | **?** vs **74** |
| MCP servers explained | draft | 6 | **38** vs **47** |
| Self-hosting n8n with Docker | live | 9 | **62** vs **87** |
| Google Sheets as a database (and why not) | live | 14 | **178** vs **184** |
| Slack bots with n8n | live | **7** vs **8** | 102 vs 106 |
| OAuth2 without tears | retired | 12 | **176** vs **183** |
| Scraping politely | live | 13 | **175** vs **181** |
| Stripe webhooks | live | 13 | **170** vs **176** |
| HubSpot + n8n | live | **8** vs **9** | 110 vs 115 |
| Telegram bots | live | 15 | **202** vs **210** |
| Local LLMs with Ollama | live | 11 | **145** vs **151** |
| Human-in-the-loop patterns | live | 16 | **186** vs **192** |
| Rate limits and retries | live | 12 | **150** vs **156** |
| Zapier vs Make vs n8n | live | 14 | **171** vs **178** |
| Git for workflow exports | live | 16 | **186** vs **194** |
| CI/CD for n8n | live | **6** vs **7** | 96 vs 101 |
| Excel to API | retired | 15 | **186** vs **193** |
| Invoicing workflows | live | 15 | **167** vs **175** |
| CRM sync patterns | live | 11 | **135** vs **141** |
| Testing workflows | live | 14 | **217** vs **224** |
| Sub-workflows and reuse | live | **8** vs **9** | 129 vs 132 |
| Agents with memory | live | 15 | **180** vs **186** |
<!-- tables:end -->

## What it says
**The three lists agree on which courses exist, but not on what is in them.**

| Finding | Count | Meaning |
|---|---|---|
| Course list | 59 of 60 match | Every Vimeo folder and every old-website page has a catalogue row. The one gap is the draft "n8n basics (2026 edition)" (no videos yet). |
| Website out of step with status | 5 | 2 live courses have no page ("Agents with memory", "Multi-agent setups": the newest ones), 3 retired/draft courses still have one. Someone has to remember to add *and* remove pages by hand. |
| Video counts | 4 differ | Off by one each time (a video added on Vimeo, not in the catalogue). |
| Durations | 26 more than 5 min apart, 1 unknown | Vimeo is longer for 58 of the 59 courses on Vimeo. Two patterns (per-video numbers from the same data): |
| | rows 1–15 | 11 of the 13 with a duration are 1–4 min longer **per video** (9–28 min per course). These are the oldest courses: their catalogue durations look like they were never updated after re-recording. (Exceptions: "Zapier to n8n migration" and "RAG on your own documents" match.) |
| | rows 16–60 | a steady ~0.5 min per video. Likely a ~30 s intro/outro that the catalogue doesn't count (hypothesis: not visible in the data). |

**Conclusion.** Keeping the catalogue by hand in three places doesn't fail on the big things (no course is missing
anywhere). It fails on the details that change after publishing: durations, video counts, and pages for courses that
were retired or are new. Nobody updates all three. Customers see these numbers on the course cards and the chat
assistant repeats them. Durations and video counts can be **calculated** from the Vimeo export instead of typed
in, and a website page (now: /Courses) should follow the `status` column instead of being a separate file.
Of the three places, the new site keeps only one: the catalogue, imported by `CourseCsvImporter`. Durations from Vimeo
would be a next step (Vimeo API or the export), not built in #31.
