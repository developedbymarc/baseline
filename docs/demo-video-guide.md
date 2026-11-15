# BASELINE: interface guide and 2-minute video plan

For the team recording the NASA Space Apps demo video. Part 1 explains what each screen does. Part 2 is the minute-by-minute plan with the voiceover. Part 3 covers setup and the mistakes to avoid.

All crew data in BASELINE is **synthetic**. The app gives **decision support, not diagnosis**, and never shows drug doses. Say this in the video and keep the "SYNTHETIC DATA" labels visible.

---

## Part 1: What the interface does

BASELINE has two sides, picked on the start screen:

- **Crew** (tablet, landscape): an astronaut on a Mars transit takes their own readings and gets an instant, clear status.
- **Ground** (desktop): a flight surgeon on Earth sees only what has *arrived* through the radio link, which has a 12-minute one-way delay.

### Start screen (role picker)
- **Crew member**, then pick a person (use **Alex Rivera (synthetic)**), then **Sign in as …**
- **Ground control** opens the ground view.

### Crew top bar (every crew screen)
- **SYNTHETIC DATA** label, mission clock (**MET**, mission elapsed time, e.g. `0d 00:05:12 · Day 1`), link state (`Link up · 12 min one-way`), **on-board first** when ground is too far away to help in time, and the crew member's name (tap it to sign out; it asks first).
- The clock is mission time from the server, never the computer clock, so fast-forward works everywhere.

### Red alert banner
- Appears at the very top when something is ACT NOW: octagon icon, **ACT NOW**, the reason (e.g. *"Resting heart rate 130 bpm: Above the critical limit of 120 bpm…"*) and **"Ground is 12 min away. Follow on-board steps first."**
- Stays until the packet is sent or the protocol ends.

### Status is never colour alone
Every status everywhere uses word + shape + colour:

| Status | Word | Shape |
|---|---|---|
| Normal | **OK** | circle with a check |
| Look again | **WATCH** | triangle |
| Act | **ACT NOW** | octagon |

### Crew home
- **Measurements**: what is scheduled. Cards say **Overdue**, **Due now** or **Later**. The schedule was set before launch and keeps running with the link down (read-only, no editing).
- **Latest readings**: heart rate, blood pressure, temperature and SpO2, each with its status badge.
- **Chest status figure**: the heart takes the worst status of heart rate and blood pressure, the lungs take SpO2's. It turns red when those readings are ACT NOW. It shows reading status only, not a diagnosis.
- Buttons: **New measurement** (main action), **Trends**, **Report a symptom** (red outline).

### New measurement
- Big tabs with icons: Heart rate, Blood pressure, Temperature, SpO2, Sleep, Mood, Exercise.
- Each metric has a control built for gloved hands, with minimal typing:
  - **Heart rate**: big number with large − / + buttons.
  - **Blood pressure**: one **SYS / DIA** control (two numbers with a slash).
  - **Temperature**: 0.1 steps, with **"in °C"** shown under the label.
  - **SpO2**: a big slider with − / + buttons.
  - **Sleep**: quick picks 5-9 h plus fine steps, shown as "7 h 15 min".
  - **Mood**: five labelled buttons (1 Very low … 5 Very good); one tap saves.
  - **Exercise**: quick picks 0-60 min plus **Other**.
- **How to take it** panel beside the control: step-by-step instructions for that measurement.
- **Safety check**: type **98.6** for temperature and the app says *"98.6 looks like Fahrenheit (98.6 °F = 37.0 °C). This field is in °C."* with **Retake** and **Save anyway**.

### Result (instant status)
- Big value, the status block (OK / WATCH / ACT NOW), and the plain reason, e.g. *"+2.0 SD from your personal baseline (n=14)"*. The reading is compared with the fixed limits **and** with the astronaut's own pre-flight baseline.
- **Source** line: where the limit comes from (e.g. *American Heart Association*, *MedlinePlus*, or *"Team starting value, not clinically validated"*).
- **Why it may have moved**: possible contributing factors with their sources, e.g. *Adaptation window: first 14 days of the mission* (NASA, PubMed Central). A factor can only lower a WATCH to OK, and the screen then says "Lowered from WATCH by …" and that the original flag is still logged.
- The next step follows the colour: OK → **Done**; WATCH → **Retake in 10 min**; ACT NOW → **Open protocol** or **Review packet**.

### Chest pain protocol (on-board steps)
- One step per screen, with very large answer buttons.
- Each step shows its **Source** line. Steps marked **Needs medical review** carry a dashed badge. No doses are shown anywhere.
- Some steps ask for measurements or have a countdown timer (10 min, in mission time).
- **Notify ground** opens the handoff packet. After sending, **Continue protocol** goes on to the next step.

### Handoff packet (SBAR)
- Built by the app from stored data in four boxes: **Situation, Background, Assessment, Recommendation**. The crew types nothing except an optional note.
- **Send to ground** → *"Sent. Arrives in about 12 min."* With the link down it says the packet is held until the link returns.

### Trends
- Chart per metric with a **normal band** and the astronaut's **personal baseline band**. Points use the status shapes. The time axis is in mission time. A text summary under the chart says it in words.

### Ground: overview
- Strip on every ground screen: *"Everything here is what has arrived through the link. Crew data is at least 12 min old."*
- Table of crew × metric: status, value and how old the data is (mission time). Empty cells say "nothing yet". **Arrived** list on the right.

### Ground: inbox and packet view
- Large age line, e.g. **"Written 1 d ago (mission time)"**, with sent / arrived MET and the link delay.
- The same S / B / A / R boxes, the readings from the last 72 h, and **Follow-ups, prepared from data** (onset, medications, exercise, sleep, environment, each with "from symptom log / measurements / …").
- **Show plain text** gives a version to read aloud over a voice link.

### Ground: reports
- **Biweekly** / **Monthly**, a period, and whole crew or one member.
- Per metric: window mean vs baseline, change, **SD from baseline** (the headline number), trend, missed / expected, and a **low confidence** chip when there are few readings.
- Alert totals with an **Unexplained** count. **Print** gives a light, paper-friendly version.

### Works offline
- Fonts and charts are bundled; nothing loads from the internet. If the BASELINE server is unreachable, a banner says *"No connection to the BASELINE server"* and the screen stays readable.

### Presenter bar (for us, not part of the crew app)
The striped bar at the bottom of every screen. Click **▸ PRESENTER** to open it. Every button has a caption, and the result appears under the buttons.

| Button | What it does |
|---|---|
| **Fast-forward: on/off** | 1 mission day passes every 5 s. Stored mission history appears and delayed messages arrive almost at once. |
| **+1 day / +1 week / +3 weeks** | Jumps the mission clock. |
| **Scene 5 (chest pain)** | Real time, link up, 12 min delay. Logs nothing by itself. |
| **Chest pain example** | Alex logs chest pain with heart rate 130, BP 145/92, SpO2 92 (all ACT NOW): instant red banner and red chest figure. |
| **Link: up/down** | Cuts or restores the radio link. |
| **Reset demo…** | Reloads all data and sets the clock back to day 0 (asks to confirm). |

---

## Part 2: The 2-minute video

**Roles**

| Who | Does what |
|---|---|
| Narrator | Reads the voiceover |
| Crew operator | Drives the crew browser tab |
| Ground operator | Drives the ground tab and the Presenter bar |

**Tip:** record each scene as a separate clip and cut them together. The red path has many taps; show the important screens and cut the in-between steps.

| Time | Scene | What to show (click by click) | Voiceover |
|---|---|---|---|
| 0:00-0:12 | **The problem** | Crew home for Alex: top bar with MET, *Link up · 12 min one-way*, *on-board first*, SYNTHETIC DATA. | "On the way to Mars, a message to Earth takes 12 minutes. When something feels wrong, the astronaut is the first responder. BASELINE is their on-board health check." |
| 0:12-0:35 | **Daily check** | **New measurement** → **Blood pressure** → SYS **114**, DIA **70** (use the + buttons once to show the gloved-hand controls) → **Save reading**. Result: **WATCH**, *+2.0 SD from your personal baseline (n=14)*, the **Source** line, and **Why it may have moved → Adaptation window** with NASA as a source. | "Big controls, no typing. BASELINE compares each reading with fixed limits and with the astronaut's own pre-flight baseline. 114 is normal for most people, but high for Alex, so it says WATCH, and explains possible causes, with sources." |
| 0:35-0:42 | **Safety net** (optional) | **Temperature** → type **98.6** → message *"looks like Fahrenheit"* → **Retake**. | "It catches mistakes, like a Fahrenheit value in a Celsius field." |
| 0:42-1:12 | **Emergency: chest pain** | *(Off camera: Presenter → Scene 5.)* **Report a symptom** → red **ACT NOW** banner, *"Ground is 12 min away. Follow on-board steps first."* Protocol: **No** → **Done, next** … show the step with **Needs medical review** and its **Source** line → **Notify ground** → packet with **S B A R** → **Send to ground** → *"Sent. Arrives in about 12 min."* | "Chest pain. The on-board protocol comes first: one step per screen, big buttons, every step sourced, steps not yet approved by a flight surgeon clearly marked, no drug doses. The app builds the handoff packet from the data automatically and sends it to Earth." |
| 1:12-1:35 | **Ground, 12 minutes later** | Ground tab → **Inbox** (no packet yet: it is still in flight). *(Presenter → Fast-forward: on, then off.)* The **Packet · Alex Rivera** appears: big *"Written … ago (mission time)"*, *link delay 12 min*, S B A R, **Follow-ups, prepared from data**. Then **Crew** overview grid. | "Ground only sees what has actually arrived, and always how old it is. The flight surgeon gets the full picture at once: readings, protocol steps with times, and follow-up answers filled in from the data." |
| 1:35-1:52 | **Long-term health** | *(Presenter → +3 weeks.)* **Reports** → Biweekly, whole crew: **SD from baseline**, **low confidence** chip, **Unexplained** count. Optionally crew **Trends** with the baseline band. | "Over months, BASELINE tracks slow drift against each person's own baseline, with biweekly and monthly reports, and it is honest about low-confidence data." |
| 1:52-2:00 | **Close** | Crew home with the chest figure (use **Chest pain example** after a reset for the red version). | "Offline-first, explainable, sourced. Decision support, not diagnosis. All data shown is synthetic. BASELINE." |

The voiceover is about 260 words, which fits 2 minutes at a calm pace. Trim the optional safety-net scene if you run long.

---

## Part 3: Setup and what to avoid

### Before recording
1. Start the backend and the frontend (`npm run dev`, open http://localhost:5173).
2. Open **two browser tabs**: tab 1 → **Crew member → Alex Rivera (synthetic)**; tab 2 → **Ground control**. Each tab keeps its own sign-in.
3. In either tab: **Presenter → Reset demo… → Yes, reset** (a clean day 0).
4. Window size: crew about **1180 × 820** (tablet), ground about **1280 × 800** or larger. Use full screen (F11), browser zoom 100 %, bookmarks bar hidden.
5. Close the Presenter bar (**▾ PRESENTER**) before a shot, or crop it out. It is labelled as demo controls, so it is fine if it shows.

### Order of the takes (so the data stays right)
1. **Reset demo** → daily check (BP 114/70 gives WATCH on day 1) → Fahrenheit check.
2. **Scene 5** → Report a symptom → protocol → packet → send.
3. Ground inbox empty → **Fast-forward on** → packet appears → **Fast-forward off**.
4. **+3 weeks** → Reports.
5. For the red chest figure shot: **Reset demo** → **Chest pain example**, then show crew home as Alex.

### Common mistakes
- **No Scene 5 before the chest pain take.** With fast-forward on, the 30-minute chest-pain window and the 12-minute delay pass in a fraction of a second.
- **Fast-forwarding after "Chest pain example".** Newer normal readings from the stored history become the latest, and the figure turns green. Show the red figure first.
- **Expecting the packet on ground right away.** It really takes 12 mission minutes. Use fast-forward, or wait 12 real minutes in real time.
- **"Reset demo" during a take.** It wipes everything entered. The crew tab signs back in as the same person automatically.
- **Saying "diagnosis".** Say "status", "flag" or "decision support". Never mention drug doses.

### If something looks wrong
- Banner stuck or odd state: **Presenter → Reset demo…**
- Clock racing: the Presenter bar shows `×17280 · fast-forward`. Press **Fast-forward: on** to go back to real time.
- "No connection to the BASELINE server": the backend is not running.
