# Spec: Full course catalogue (60) + cross-check report  (#31)

## User story
As a visitor (and LoopCasts staff), I want the site to show the full catalogue instead of the 15-row sample, and a
report that cross-checks it against Vimeo and the old website, so that every live course is on the site and staff see
where the hand-kept lists disagree.

## Acceptance criteria
- [x] `Data/Seed/course-catalogue.csv` = export of `course catalogue.xlsx` (60 rows: 53 live, 4 retired, 3 draft).
- [x] `CourseCsvImporter` reads the new messy values and logs every fix (only for new or changed rows).
- [x] Existing database: update by title (case/spaces ignored), Ids kept, nothing deleted, no duplicates.
- [x] live = /Courses, home page, `/api/public/courses`; draft hidden; retired hidden but kept.
- [x] /Courses 53, API 53, chat assistant finds a course that wasn't in the sample.
- [x] `docs/analysis/course-catalogue-crosscheck.md` with the five tables + conclusion.

## Design
- Data/model changes: none (no migration). `Order` has no `CourseId`, so no order points to a course; Ids are kept anyway.
- Export (one-time, done): xlsx → CSV with Python/openpyxl; text cells kept as typed (the importer cleans them),
  real Excel date cells written as `yyyy-MM-dd` (unambiguous).
- `Data/CourseCsvImporter.cs`: runs at every start (was: only when the table is empty). Per row: parse; same title →
  `SetValues` (EF marks only changed columns), else add. Fixes are logged only for rows that are new or changed:
  the row is parsed once silently, then again with the real logger if needed.
  New rules: bare number duration = minutes; dates `dd.MM.yyyy`, `d-M-yy`, `MM/dd/yyyy` (only when day-first is
  impossible), all logged; a date without a year ("24 okt") → null, logged.
- Cross-check: `docs/analysis/course-catalogue-crosscheck.mjs` (node, read-only), reads the committed CSV, the Vimeo
  export and the **file names** of the old website's course pages (folder listed, pages never opened).
- Security: the catalogue is public (it's on /Courses). The Vimeo export and website stay in the Dropbox copy (never
  committed, hard rule 3); the report only holds course titles, counts and durations.

## Out of scope
- /Admin page with the same check: it would need the Vimeo export and website file names on the server, which may
  not be committed. Not simple → skipped.
- Durations/video counts from Vimeo (see the report's conclusion).

## Test plan
- Unit: repo CSV → 60 courses (53/4/3); messy values cleaned + logged; second import on an existing db updates by
  title, keeps the Id and the hand-made course, logs nothing.
- Manual: start the site on the existing loopcasts.db → "45 added, 0 updated", /Courses 53, API 53, restart = no log;
  chat: "Telegram bots" found, "Excel to API" (retired) not offered.
