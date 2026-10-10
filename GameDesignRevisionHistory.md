# Baby Champion - Game Design - Revision History

Revision history of 2-design-detailed.md. Section numbers in each entry refer to the document as it was in that version. v0.1 is the original draft, 1-design-raw.md.

**File names:** the documents were renamed by reading order: GameDesign.md → 1-design-raw.md, GameDesignDetails.md → 2-design-detailed.md, PLAN.md → 3-plan.md. Entries below use the names that were current at the time.

---

## v0.4.1
- **Builds use Unity 6 Build Profiles** with the built-in `-activeBuildProfile` and `-build` command-line arguments, replacing the planned custom build script (Section 19.3). Only a small pre-build hook remains, to stamp the version number.
- Command-line calls go through a wrapper script, `tools/unity.sh`, which adds the common flags once. Exact commands moved to 3-plan.md, Section 2.

## v0.4
- **Partner NPC moved to a later stage.** All partner content is now in Section 22 at the end of the document, under "Later-stage features", together with the two-player mode (Section 23).
- The base game is a **single-parent game** with occasional grandparent help (*Call for help*). Removed partner references from players, Golden Moments, procedures, money, Level 1 (tutorial) and Level 2 (night plan), HUD and setup screen. They are listed in Section 22.9 as changes to make when the partner is added.
- **Balance rule reversed:** the base game is tuned for one parent; partner mode will be retuned to stay comparably challenging (Section 22.7). Previously, levels were tuned for partner mode and single-parent mode was compensated.
- The parent-controller interface stays in the base architecture (Section 19.4), so the partner and two-player mode can be added later without changing the simulation.
- Partner open questions moved to Section 22.11.
- Sections renumbered: old 10-20 are now 9-19; old 22-23 (open questions, development tasks) are now 20-21; the partner (old 9) is now 22; two players (old 21) is now 23.

## v0.3.1
- Revision history moved from GameDesignDetails.md into this file. Section 1 of the design document now points here; all other section numbers are unchanged.

## v0.3
- **Joy as a core pillar** (Sections 4 and 8). The game should make future parents *excited*, not only prepared. Added a Joy meter, Golden Moments, play mini-games, a Memory Album, and milestone celebrations. Every level now has a signature Golden Moment, and the third star is earned through Joy.
- **New levels:** *Hide and Seek* (level 22), *Cart Sneak* (level 23, replaces the plain grocery run), *Splash Attack* (level 25). Detailed designs are in Section 15.3.
- **Partner NPC** designed (Section 9), plus a "On my own" single-parent option.
- **Technology decided:** Unity 6.3+, URP 3-D template, new Input System only, command-line build/test support (Section 20).
- **Business model decided:** free to play, ads later (Section 19).
- Routine levels renamed "A Day Together" and given their own Golden Moments.
- Tip cards replaced by **Milestone cards** that celebrate what the baby can now do.

## v0.2 - Review of the original draft

| # | Issue in the draft | Resolution |
|---|---|---|
| 1 | Newborn feeding used "baby food"; newborns drink only breast milk or formula. Solids start at about 6 months. | Newborn feeding uses breast or bottle; spoon feeding starts at month 7; resources split into formula and baby food. |
| 2 | Level (c): a *crawling* baby "runs away" and throws the mouse into the toilet, which requires walking. | Split into *Mouse Thief* (month 10, crawling) and *Toilet Dash* (month 15, walking). |
| 3 | ~36 levels implied, only 3 described. | Full 36-level roadmap based on developmental milestones; milestone and routine levels. |
| 4 | Money was a resource with no way to earn it; work was undecided. | Work from home (procedure *Work*); parental-leave allowance in early months. |
| 5 | Store undecided. | Supplies ordered by phone with delivery delay; store appears as a special level. |
| 6 | Sleeping, putting the baby to sleep, burping, bathing, playing, soothing were not procedures. | Procedures list completed. |
| 7 | "Tired" and "Energy" duplicated; "anxious" had no representation. | One Energy meter plus a Stress meter. |
| 8 | No failure conditions; objectives not measurable. | Measurable objectives, fail conditions, time limits, star score. |
| 9 | Time scale unclear. | One in-game day per level, compressed time. |
| 10 | Singing's purpose unclear. | Singing is one of several soothing actions. |
| 11 | Baby always "him". | Player chooses name and sex; document uses "the baby" / "they". |
| 12 | No camera/controls for a 3-D game. | Control and camera scheme defined. |
| 13 | Realism must be accurate and safe for this audience. | Realism and Safety section added. |
| 14 | Missing UI, saving, tutorial, difficulty, art, audio, accessibility, tech. | Added. |
| 15 | Typos. | Corrected. |

## v0.1
- Original draft (GameDesign.md).
