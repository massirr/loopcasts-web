# Context Management Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a four-slide, left-to-right context-management presentation as a clickable HTML deck and a matching `.pptx` export for a five-minute student presentation.

**Architecture:** Keep the slide content in one small JavaScript data model. The HTML renderer provides keyboard/click navigation, progressive visual reveals, and speaker notes in comments. The PowerPoint exporter uses the same content and visual tokens through PptxGenJS, producing a static deck with the same diagrams and wording.

**Tech Stack:** Plain HTML/CSS/JavaScript, Node.js, PptxGenJS, LibreOffice for optional export validation.

---

### Task 1: Create the presentation source

**Files:**
- Create: `/Volumes/Koze_disk/TM/Feb2026-Sep2027/Automation-Tools/cowork/context-management-deck.html`

- [ ] Add four slides: the NLP hook, the context stack, the spec-driven loop, and the context-budget conclusion.
- [ ] Use the existing project-map visual language: dot grid, hard shadows, black outlines, blue/apricot/coral accents, Manrope, and JetBrains Mono.
- [ ] Add next/previous controls, keyboard arrows, clickable progress dots, and a slide counter.
- [ ] Add restrained entrance animations and a reduced-motion media query.
- [ ] Keep visible text short enough for a five-minute talk; put explanation in HTML comments and presenter notes below each slide.

### Task 2: Add the PowerPoint exporter

**Files:**
- Create: `/Volumes/Koze_disk/TM/Feb2026-Sep2027/Automation-Tools/cowork/package.json`
- Create: `/Volumes/Koze_disk/TM/Feb2026-Sep2027/Automation-Tools/cowork/export-context-deck.mjs`
- Create: `/Volumes/Koze_disk/TM/Feb2026-Sep2027/Automation-Tools/cowork/README-context-deck.md`

- [ ] Add `pptxgenjs` as the only runtime dependency.
- [ ] Reproduce the four HTML slides with editable PowerPoint text and shapes.
- [ ] Add speaker notes containing the short explanation of context files, skills, and spec-driven development.
- [ ] Add an npm script that generates `context-management-deck.pptx`.

### Task 3: Validate both deliverables

**Files:**
- Generated: `/Volumes/Koze_disk/TM/Feb2026-Sep2027/Automation-Tools/cowork/context-management-deck.pptx`

- [ ] Run the HTML syntax and asset checks available locally.
- [ ] Run the exporter and confirm the `.pptx` is created and readable by LibreOffice.
- [ ] Check the slide count is four and the HTML starts on slide one with keyboard navigation enabled.
- [ ] Report any limitation: HTML animations do not transfer to PowerPoint; the PowerPoint version uses static staged diagrams and speaker notes.

### Task 4: Prepare the presenter explanation

- [ ] Explain the purpose of `CLAUDE.md`, skill Markdown files, specs, source code, tests, and handover documentation.
- [ ] Explain that skills are reusable procedures, not magical memory, and that their instructions are loaded when relevant rather than permanently appended to every prompt.
- [ ] Identify the important lines in the existing skills: trigger/description, required workflow, hard constraints, validation steps, and handoff rules.
- [ ] Give a concise spoken explanation of spec-driven development using one LoopCasts feature as the example.