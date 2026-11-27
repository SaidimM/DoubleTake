# DoubleTake — Promotional Video Concept & Production Notes

> **Status:** Concept & Hybrid Production Plan (WIP)
> **Goal:** Create a 35–45s Apple / Squarespace–style promotional short film that tells a relatable story about **protecting immersion (flow state)**—so it feels like a short film rather than a traditional software ad.

---

## 1. Core Premise

Most PC translation tools break your flow: highlight text → open browser → search translate → paste → click translate → return to reading → realize your focus is gone.

**DoubleTake** solves this with a zero-friction, cursor-anchored floating popup triggered by double-tapping `Ctrl`. The video dramatizes this contrast through a **Before vs. After** narrative where the physical environment around the reader visually reflects their mental immersion.

---

## 2. Refined Storyboard (35–45s)

| Beat | Time | Visual & Camera | Sound Design |
| :--- | :--- | :--- | :--- |
| **1. The Pull-In** | `0:00–0:07` | Medium over-the-shoulder shot in a quiet cafe or desk. Camera slowly pushes in toward the laptop screen as the person reads a rich novel (e.g., *Harry Potter*–style fantasy), PDF, or deep article. | Soft cafe murmur, distant rain, gentle ambient tone. |
| **2. The Immersion** | `0:07–0:13` | As the camera pushes closer, the mundane cafe/office background **dissolves into the world of the book** (e.g., warm candlelight, stone arches, drifting dust motes, rain on castle glass). The reader's attention is completely locked. | Cafe noise fades out completely; rich, cinematic book-world ambience swells in. |
| **3. The Stumble** | `0:13–0:16` | Close-up on the screen. The cursor slows and highlights a line containing unfamiliar words. The immersive background subtly pauses. | Ambience holds on a single sustained note. |
| **4. The Shatter (Before)** | `0:16–0:24` | Quick, jarring cuts: `Alt+Tab` → glaring white browser with dozens of tabs → typing *"translate"* → `Ctrl+V` → clicking translate. **The instant the browser opens, the magical world snaps away**, hard-cutting back to harsh, cold fluorescent lighting. | **Hard silence cut**, followed by grating real-world audio: loud mouse clicks, fluorescent buzz, a Slack/email notification *ding*. |
| **5. The Lost Spell** | `0:24–0:28` | They switch back to the reading content. **The magical background does not return.** It's just flat black text on a stark screen. They sigh, rub their eyes, and scroll back up to re-read. | Flat, sterile room tone. No music. |
| **6. The Reset** | `0:28–0:30` | Minimalist white background, crisp black typography: **`Instead.`** *(or **`Or...`**)* — held for ~1.5 seconds of breathing room. | Clean beat of silence. |
| **7. The DoubleTake (After)** | `0:30–0:38` | Back in the warm, immersive book world. Same highlighted line. Close-up on fingers: **two crisp taps on `Ctrl`**. The frosted-glass DoubleTake popup glides in softly beside the cursor. They understand the line, smile subtly, and keep reading. **The immersed world never breaks.** | Crisp, rhythmic mechanical *tik-tik* of `Ctrl`, soft crystalline chime as the popup appears, cinematic ambience flowing seamlessly on. |
| **8. The Payoff** | `0:38–0:44` | Clean minimalist canvas. Slogan fades in, then transitions smoothly into the **DoubleTake** app logo + wordmark beside it (e.g., two key-tap ripples morphing into the logo). | Warm, resolving musical chord. |

---

## 3. Storytelling Principles (Avoiding "Ad" Energy)

1. **Sound Design Does 50% of the Work:** Contrast the warm, enveloping book soundscape with the sterile, jarring browser/fluorescent/notification soundscape. Let viewers *feel* the lost focus instead of explaining it.
2. **Stay Invisible Until the End:** No feature callout badges or marketing bullets during the story. Let the cursor-anchored Fluent 2 glass popup demonstrate itself naturally.
3. **Make `Ctrl` `Ctrl` an Audio-Visual Signature:** The tactile *tik-tik* double-tap ties directly into the **DoubleTake** name and gives the reveal a memorable rhythm.

---

## 4. Slogan Candidates

*Original draft:* `"Keep your attention only takes Double Take"`

**Polished options:**
- *"Stay in the story. It only takes a DoubleTake."*
- *"Don't break the spell. Just DoubleTake."* (strongest if using a fantasy novel scene)
- *"Keep your focus. It only takes a DoubleTake."*
- *"Never lose the thread. Just DoubleTake."*
- *"Zero detours. Just DoubleTake."*

---

## 5. AI-Assisted Hybrid Production Pipeline

Pure AI video struggles with small UI text and cursors, so the plan is a **hybrid workflow**: AI for cinematic world-building, real capture for crisp UI and tactile details.

| Shot Type | Production Method | Notes |
| :--- | :--- | :--- |
| **Cafe → Book World morph** | **AI Image-to-Video** (Start Frame → End Frame) | Use over-the-shoulder / 3/4 back framing for strong character consistency. Generate matching Start (cafe) and End (book world) keyframes, then animate with first/last frame control (Veo, Kling, Runway, Luma, etc.). |
| **Hard cut to sterile office** | **AI Image-to-Video** | Same camera framing, cold fluorescent lighting, flat expression. |
| **Reading, browser detour, & DoubleTake popup** | **Real 4K Screen Recording** (OBS / Screen Studio) | Ensures razor-sharp text and authentic Windows 11 Mica/Acrylic glass. Cut cleanly between AI over-the-shoulder shots and full-frame screen recordings with subtle slow zoom / shallow DOF. |
| **Finger double-tapping `Ctrl`** | **Real 2s phone/camera close-up** (or AI macro) | Shooting a quick moody close-up on a real mechanical keyboard grounds the film in reality. |
| **`Instead.` & Logo Reveal** | **Motion Graphics** (DaVinci Resolve / After Effects / CapCut / Remotion) | Clean Apple-style typography and smooth vector logo morph. |
| **Audio & SFX** | **Layered SFX + Ambient Music** | Cafe murmur, rain/orchestral swell, fluorescent hum, notification ping, crisp mechanical `Ctrl` *tik-tik*, soft glass chime. |

---

## 6. Next Steps (When Resuming)

- [ ] Lock the reading scene theme (e.g., *Harry Potter*–style fantasy novel vs. sci-fi vs. deep academic/tech article).
- [ ] Pick the final slogan and ending logo animation style.
- [ ] Write the **Shot-by-Shot AI Prompt Pack** (consistent Start/End keyframe image prompts + video motion prompts).
- [ ] Record the real 4K screen-capture clips (Before: browser detour; After: `Ctrl` `Ctrl` DoubleTake popup) and the 2s `Ctrl` keycap close-up.
- [ ] Gather SFX & ambient audio tracks and assemble the rough cut.
