# Baby Champion - Game Design - Detailed (v0.4)

All numbers in this document are **initial tuning values for playtesting**, not final balance.

Sections 1-21 describe the base game. Sections 22-23 describe **later-stage features** (the AI partner and two-player mode), which will be implemented after the base game.

---

## 1. Revision history

See GameDesignRevisionHistory.md.

---

## 2. Basic definition

- **Title:** Baby Champion
- **Genre:** Real-time care / time-management simulation with short, playful mini-games.
- **Primary player experience:** the joy of raising a baby from birth to kindergarten - the laughs, the firsts, the cuddles - together with the real challenges (sleep deprivation, crying, juggling work), always with a smile.
- **Presentation:** 3-D, stylized low-poly, 3/4 top-down camera.
- **Platforms:** Web (itch.io, desktop and mobile browsers) and Android.
- **Session length:** 3-15 minutes per level.

---

## 3. Players

- **Single player** controls one parent who raises the baby, with occasional help from grandparents (*Call for help*).
- **Target audience:** young adults expecting their first baby. The game should leave them better prepared *and* looking forward to it. Secondary audience: new parents who will recognize (and laugh at) the situations.
- **Later stage:** an AI-controlled partner (Section 22) and two human players cooperating (Section 23).

---

## 4. Design pillars

1. **Hard, then heart.** Every challenge is paired with a reward beat. After the night shift comes the baby falling asleep on your chest; after the tantrum comes the spontaneous hug. The player should finish most levels smiling.
2. **Firsts are events.** First smile, first laugh, first steps, first word are celebrated with music, a photo and a Milestone card - never buried in a menu.
3. **Chaos is comedy.** Mischief (flying food, stolen mouse, splash attacks) is slapstick: the baby giggles, the parent reacts with humor, nothing is ever really ruined.
4. **Real enough to prepare.** Needs, rates and safety practices are accurate (Section 16), so the joy rests on a true picture of parenting.
5. **Short and kind.** Short levels, gentle failure, easy retries.

---

## 5. Objectives

### 5.1 Overall objective
Raise the baby from birth to the first day of kindergarten (age 3). **36 levels**; level *N* represents the baby's *N*-th month.

### 5.2 Level objectives
Each level has a **primary objective** tied to that age (Section 14), the general care constraints (Section 13.2), and a signature **Golden Moment** to catch.

### 5.3 Collection objective
Fill the **Memory Album** with the baby's Golden Moments (Section 8.4). The finale plays a montage of the player's own album.

### 5.4 Learning objective
Between levels, a **Milestone card** celebrates what the baby can do now ("Now I can sit up!") and adds one accurate, practical fact for that age.

---

## 6. Time model

- A **day level** covers one in-game day, 07:00 to 07:00. Default compression: **1 real second = 2 in-game minutes** (~12 real minutes per day).
- While the parent sleeps, time runs **4x faster** until something wakes them.
- A **scene level** covers one situation (a meal, a bath, a chase) and lasts 2-5 real minutes.
- The game can be paused at any time.

---

## 7. Rules (simulation)

All meters range from 0 to 100. Rates change with age (7.4).

### 7.1 Baby state

| Meter | Rises when | Falls when | Effect |
|---|---|---|---|
| **Hunger** | Constantly. | Feeding. | At 70+ the baby cries. |
| **Diaper** (Clean / Wet / Dirty) | Random events by age. | Changing. | Wet: crying after ~60 in-game min. Dirty: after ~10 min. Dirty over 2 h: diaper rash (comfort penalty for the day). |
| **Tiredness** | While awake; faster when over-stimulated. | While asleep. | At 80+ overtired: cries, Tension rises. |
| **Tension** | Overtiredness, long crying, noise, no burp after a feed. | Soothing (rock, sing, carry, walk, white noise, pacifier). | At 50+ the baby cannot fall asleep. |
| **Boredom** (month 4+) | Awake and not engaged. | Play, toys, talking, going out. | At 70+: fussing, or mischief once mobile. |
| **Wellbeing** (derived) | - | - | How well needs are met; used for scoring. |

**Crying:** the baby cries while any trigger is active. Visual hints show why (hand-to-mouth = hungry, rubbing eyes = tired, squirming = diaper or gas). Crying raises Stress.

**Self-soothing:** from month 6, a baby with low Tension may fall asleep alone in the crib.

### 7.2 Parent state

| Meter | Rises / falls | Effect |
|---|---|---|
| **Energy** | Drains while awake, faster when working, carrying or chasing; **2x faster while the baby cries**. Restored by sleep and rest. | At 20 or below: slower walking, harder mini-games. **At 0 the parent collapses - level failed.** |
| **Hunger** | Rises over time; reset by eating. | At 70+: Energy recovers at half rate and drains faster. |
| **Stress** | Rises while the baby cries, with mess, with low supplies or money. Falls when the baby is calm, when eating, resting, showering, using *Step away*, and with high Joy. | At 60+ the parent cannot fall asleep. High Stress makes mini-games shakier. |
| **Joy** | See Section 8. | High Joy slows Energy drain and lowers Stress. |

### 7.3 Supplies
Feeds, diaper changes and meals consume supplies. If one is out, the action is unavailable until a delivery arrives.

### 7.4 Age-dependent rates

| Age | Feeds per day | Hunger 0 to 70 | Awake window | Diapers per day |
|---|---|---|---|---|
| 0-2 mo | 8-12 | ~2.5 h | 45-60 min | 8-10 |
| 3-5 mo | 6-8 | ~3 h | 1.5-2 h | 6-8 |
| 6-11 mo | 4-5 milk + 2-3 solid meals | ~3.5 h | 2-3 h | 5-7 |
| 12-24 mo | 3 meals + 2 snacks | ~4 h | 4-6 h, one nap | 4-6 |
| 24-36 mo | 3 meals + 2 snacks | ~4 h | 5-6 h, one nap | potty training |

To be verified by the consultant (Section 16).

---

## 8. Joy system

### 8.1 Joy meter
- A household meter, 0-100, shown as a warm glow around the parent portrait.
- **Rises** with Golden Moments, play mini-games, the baby laughing, cuddles, and completing hard tasks (small "you did it!" boost).
- **Decays slowly** through the day. Joy never causes failure.
- **Effects:** at Joy 60+, Energy drains 25% slower and Stress falls 50% faster. This models a real feeling - a laughing baby makes tiredness easier to bear - and gives the player a positive strategy: *playing with the baby is not a distraction from survival, it helps you survive.*

### 8.2 Golden Moments
- Each level has **one signature Golden Moment** (scripted, listed in Section 14) and **1-3 random ones** from an age-appropriate pool (grabs your finger, laughs at the cat, falls asleep holding a toy, dances to music, says your name...).
- When a Golden Moment starts: sparkle on the baby, soft chime, edge-of-screen indicator if off-screen.
- The player has a window (~30 in-game minutes, ~15 real seconds) to tap **Enjoy**. The camera eases in, parent and baby share the moment, and a photo is saved to the Album. Big Joy boost.
- Missing one is never a failure: "You missed that one - there will be many more."
- Golden Moments can happen in the middle of chaos; choosing to stop and enjoy is a real, rewarded decision.

### 8.3 Play mini-games (unlocked by age)

| From | Mini-game | Interaction |
|---|---|---|
| 2 mo | Silly faces | Tap the face matching the baby's expression; baby coos. |
| 4 mo | Peekaboo | Timing taps: hide and reveal at the right moment for a laugh. |
| 4 mo | Tickles and raspberries | Tap spots on the baby; find the most ticklish one (changes per baby). |
| 7 mo | Airplane spoon | Bonus swipe path during spoon feeding: a perfect flight earns a laugh and a bite. |
| 9 mo | Tower and crash | Stack blocks; the baby knocks them down; repeat for a giggle combo. |
| 12 mo | Dance party | Rhythm taps; the toddler copies your moves. |
| 15 mo | Story time | Choose pages; point at pictures the toddler asks for. |
| 18 mo | Chase and catch | Gentle chase around the living room; catching is a tickle hug. |
| 24 mo | Pretend play | Tea party, toy doctor, building a blanket fort. |

### 8.4 Memory Album
- Every Golden Moment caught becomes a photo with a date and a short caption ("Month 3: first real smile").
- Album pages per chapter; the player can add stickers (cosmetic rewards).
- Photos can be shared through the device share sheet (Android) or saved as images (Web). This also helps word-of-mouth for a free game.
- **Finale:** the last level ends with a montage of the player's own album.

### 8.5 Celebrations
- **Firsts** (first smile, laugh, roll, crawl, steps, word, sentence, potty success) trigger a short celebration: music sting, confetti, Milestone card.
- **Level success screen:** an album photo from that level, the baby's reaction, and stars.
- **Growth:** the baby's model visibly grows every chapter; the nursery evolves (crib, then toddler bed, then a growth chart on the wall marking height each level).

### 8.6 Tone of failure
Failure screens stay warm and funny ("Grandma arrived and took over the night shift. Try again?"), followed by a helpful hint. The baby is never shown harmed.

---

## 9. Procedures (player actions)

All actions are triggered by click / touch (Section 15).

| Action | Where | Requires | Effect | In-game duration |
|---|---|---|---|---|
| **Move** | Anywhere | - | Walk to a point or object. | - |
| **Pick up / Put down baby** | Anywhere safe | Hands free | Carrying soothes but drains Energy. Put down in crib, playpen, bouncer, high chair, play mat. | instant |
| **Breastfeed** | Sofa / bed | Breastfeeding parent | Hunger to 0. No cost; parent Hunger rises faster. | 20-30 min |
| **Prepare bottle** | Kitchen | Formula, clean bottle | Produces a bottle. | 10 min |
| **Bottle-feed** | Seated | Bottle | Hunger to 0. | 15-20 min |
| **Burp** | Anywhere | Just fed | Prevents gas. | 3-5 min |
| **Spoon-feed** (month 7+) | High chair | Baby food | Skill mini-game. | 10-20 min |
| **Change diaper** | Changing table | Diaper, wipes | Diaper to Clean. From month 5, one hand stays on the baby. | 5 min |
| **Put baby to sleep** | Crib | Tension below 50 | Baby sleeps. | 5-15 min |
| **Soothe** (rock, sing, walk, white noise, pacifier) | Anywhere | - | Lowers Tension; each baby has a favorite method. | continuous |
| **Play** (mini-games, Section 8.3) | Play mat, living room | Baby awake | Lowers Boredom; raises Joy. | 5-15 min |
| **Enjoy** | Near the baby | Golden Moment active | Photo to Album; big Joy boost. | 2 min |
| **Bathe** | Bathroom | - | Every 2-3 days. Parent stays at the tub. | 15 min |
| **Clean up** | A mess | - | Removes mess; lowers Stress. | 2-10 min |
| **Eat** | Kitchen | Adult food | Parent Hunger to 0. | 15 min |
| **Sleep / Rest** | Bed / sofa | Stress below 60 | Restores Energy. | until woken |
| **Shower** | Bathroom | Baby safe | Lowers Stress. | 10 min |
| **Work** (month 6+) | Desk | Baby asleep or safely occupied | Earns money; drains Energy. | continuous |
| **Order supplies** | Phone | Money | Delivery in 2 h (express 30 min, extra cost). | instant |
| **Babyproof** (month 8+) | Hazard spots | Safety items | Installs covers, gates, locks, anchors. | 5 min each |
| **Step away** | Room with crib | Baby in crib | Leave for 5 min; Stress falls sharply. Teaches a real coping strategy for overwhelming crying. | 5 min |
| **Call for help** | Phone | Once per level | Grandparent helps for 2 h; caps the level at 2 stars. | 2 h |

**Feeding choice:** breastfeeding, formula, or both, presented neutrally.

---

## 10. Resources

| Resource | Source | Used by |
|---|---|---|
| **Time** | - | Everything |
| **Money** | Parental-leave allowance (months 1-5), Work (month 6+) | Supplies, safety items, toys |
| **Diapers, wipes, formula, baby food, adult food** | Ordering | Care actions |
| **Safety items** | Ordering | Babyproofing |
| **Energy, Stress, Joy** | Parent meters | Sections 7.2 and 8 |

- Money and supplies carry over between levels; a retry restores start-of-level values.
- Safety net: if the player cannot afford one day of essentials, grandparents send a gift (once per chapter).

---

## 11. Conflicts

- **Time:** needs keep rising and pile up.
- **The baby:** mischief once mobile - grabbing, throwing, hiding, splashing, sneaking items. Always comedic.
- **Self-care vs. baby care**, and **money vs. time**.
- **Chaos vs. joy:** stopping to enjoy a Golden Moment costs time but pays back in Joy.

---

## 12. Boundaries

- **The house:** living room, kitchen, nursery, parents' bedroom, bathroom, home-office corner, entrance. Rooms unlock as they become relevant.
- **Outside** (special levels): grocery store (23), playground (31), kindergarten (35-36).
- The baby is never left alone in or outside the house.

---

## 13. Outcomes

### 13.1 Level structure
Primary objective + general care constraints + time limit (end of day, or a real-time limit for scene levels).

### 13.2 Failure conditions
- Parent Energy reaches 0.
- Crying budget exceeded (e.g. 60 in-game min per day level), or continuous crying over 30 in-game min.
- A level-specific failure (e.g. the mouse ends up in the toilet).
- Time runs out before the objective is completed.

### 13.3 Stars
- **1 star:** objective completed, no failure.
- **2 stars:** also average baby Wellbeing 70+.
- **3 stars:** also the level's signature Golden Moment caught and Joy 60+ at the end. *Call for help* caps the level at 2 stars.
- Stars unlock cosmetics: outfits, nursery decorations, lullabies, album stickers.

### 13.4 Retry
Restart from the level start with money and supplies restored.

---

## 14. Level roadmap

### 14.1 Overview
**M** = milestone level (new mechanic). **R** = routine level ("A Day Together": a full day mixing learned mechanics with random events). Ages of milestones vary between children; Milestone cards say so.

| Lvl | Age | Title | Type | Primary objective | Signature Golden Moment |
|---|---|---|---|---|---|
| 1 | 0-1 mo | First Day Home | M | Tutorial: feed, burp, change, sleep. Stay within the crying budget. | The baby grips your finger. |
| 2 | 1-2 mo | Night Shift | M | Night feeds every 2-3 h; sleep between feeds. Reach morning with Energy above 20. | Asleep on your chest at 3 AM. |
| 3 | 2-3 mo | Witching Hour | M | Evening fussiness. Find the favorite soothing method; asleep by 21:00. | First social smile. |
| 4 | 3-4 mo | First Laughs | M | Play mini-games: make the baby laugh 5 times; 15 min tummy time. | First real belly laugh. |
| 5 | 4-5 mo | The Roller | M | Baby rolls: 6 diaper changes without breaking contact. | First roll over - on the play mat, toward you. |
| 6 | 5-6 mo | Back to Work | M | Parental leave ends: earn a money target within the crying budget. | The baby coos into your video meeting; colleagues melt. |
| 7 | 6-7 mo | First Spoon | M | Solids: feed 10 spoonfuls. | The first-taste face. |
| 8 | 7-8 mo | Spoon Swatter | M | The baby swats the spoon: time spoonfuls between swats; 70%+ eaten. | The baby "feeds" you a spoonful back. |
| 9 | 8-9 mo | Crawler | M | Babyproof 5 hazards before the baby reaches them. | First crawl - straight to you. |
| 10 | 9-10 mo | Mouse Thief | M | The baby steals the mouse and cables while you work: retrieve and redirect 3 times; finish the work task. | Baby's first "email": a row of gibberish sent to your boss, who replies with a heart. |
| 11 | 10-11 mo | Drop It! | M | Food thrown from the high chair: finish the meal, clean up before the delivery arrives. | First clap. |
| 12 | 11-12 mo | Don't Leave Me | M | Separation anxiety: do chores while keeping line of sight or using a carrier. | Crawls over with arms up for a hug. |
| 13 | 12-13 mo | Happy Birthday | M | First birthday party with guests; keep the baby happy and on schedule. | First steps; cake smash. |
| 14 | 13-14 mo | A Day Together I | R | Routine day. | First word - your name. |
| 15 | 14-15 mo | Toilet Dash | M | The walking toddler runs off with your mouse to the bathroom: catch them before it goes in the toilet. | A proud "uh-oh!" |
| 16 | 15-16 mo | Climber | M | Anchor furniture; keep the toddler off shelves for the day. | Climbs onto the sofa to "read" next to you. |
| 17 | 16-17 mo | A Day Together II | R | Routine day. | Dance party. |
| 18 | 17-18 mo | "No!" | M | Defuse 3 tantrums with distraction and simple choices. | A spontaneous hug after the storm. |
| 19 | 18-19 mo | A Day Together III | R | Routine day. | First scribble for the fridge. |
| 20 | 19-20 mo | Bath Time | M | Slippery, splashy toddler: finish the bath without leaving the tub area. | Bubble beard. |
| 21 | 20-21 mo | Picky Eater | M | Get the toddler to try 3 new foods. | "Yum!" |
| 22 | 21-22 mo | **Hide and Seek** | M | Bath time - but the toddler hides somewhere in the house. Find them and get them into the bath by 19:30. *(14.3)* | Terrible hiding: feet sticking out, giggling. |
| 23 | 22-23 mo | **Cart Sneak** | M | Grocery store: the toddler sneaks items into the cart when you look away. Check out with exactly your list. *(14.3)* | The toddler waves at everyone in the store; strangers wave back. |
| 24 | 23-24 mo | Chatterbox | M | Understand and answer the toddler's requests (icon speech bubbles). | First two-word sentence. |
| 25 | 24-25 mo | **Splash Attack** | M | The toddler grabs the shower hose and sprays you. Dodge, get it back, finish the bath. *(14.3)* | Both of you soaked and laughing. |
| 26 | 25-26 mo | Big Bed | M | Toddler bed and bedtime escapes: in bed and staying there by 21:00. | The toddler "reads" you a bedtime story. |
| 27 | 26-27 mo | Potty Training I | M | Spot the signs; get to the potty in time. | First potty success - victory dance. |
| 28 | 27-28 mo | Potty Training II | M | At most 2 accidents today. | "I did it myself!" announcement. |
| 29 | 28-29 mo | A Day Together IV | R | Routine day. | A block tower taller than the toddler. |
| 30 | 29-30 mo | I Do It Myself! | M | The toddler insists on dressing alone: out of the house by 08:30. | Shoes on the wrong feet, worn with pride. |
| 31 | 30-31 mo | Playground | M | Outdoor level; leave without a tantrum. | First slide alone. |
| 32 | 31-32 mo | A Day Together V | R | Routine day. | Pretend tea party - you're the guest of honor. |
| 33 | 32-33 mo | Sick Day | M | Fever: call the doctor, comfort, fluids - with a work deadline. | The toddler "nurses" their teddy the way you nursed them. |
| 34 | 33-34 mo | A Day Together VI | R | Routine day. | Sings your lullaby back to you. |
| 35 | 34-35 mo | Big Kid Prep | M | Visit the kindergarten; practice the morning routine. | A drawing of the whole family. |
| 36 | 35-36 mo | First Day of Kindergarten | M | Morning routine, arrive on time, the goodbye. | Runs back for one more hug. Album montage. |

**Chapters:** 1 Newborn (1-3), 2 Getting to Know You (4-6), 3 Solids & Squirms (7-9), 4 On the Move (10-13), 5 Toddler (14-24), 6 Big Kid (25-36).

### 14.2 Routine levels
"A Day Together" levels mix every unlocked mechanic, draw 2-4 random events from an age pool (unexpected visitor, delivery mix-up, teething night, power cut, rainy day indoors), and always include a signature Golden Moment so they feel special, not filler.

### 14.3 Detailed designs of selected levels

#### Level 22 - Hide and Seek (21-22 months, day level, evening segment)
- **Setup:** after dinner it's bath time. When you say "Bath time!", the toddler giggles and runs off to hide.
- **Hiding spots** (randomized each attempt, more unlock on retries): under the blanket on the sofa, behind the curtain, inside the laundry basket, under the bed, behind the door, in the wardrobe among the coats.
- **Clues:** giggles (directional audio, also shown as a small visual ripple for players without sound), feet sticking out, a moving curtain, a trail of dropped toys. Toddlers this age often hide in plain sight - that is the joke.
- **Search:** tap a spot to look. Wrong spots cost a few seconds and produce a funny find (a lost sock, last week's cookie).
- **Found:** the toddler may bolt to a new spot (up to 2 times). Getting them to the bath is a choice: carry them (works, but the toddler may protest - Tension up), make it a race ("Last one to the bath is a rotten egg!" - needs Energy), or bring the favorite bath toy (must be found first in the toy box).
- **Objective:** toddler in the bath by 19:30, bath completed.
- **Joy:** each time you find them, a "Found you!" tickle gives Joy; the signature Golden Moment is the feet sticking out from under the blanket.
- **Safety detail:** washing machine, dryer and fridge are never hiding spots; the Milestone card mentions keeping appliance doors closed.

#### Level 23 - Cart Sneak (22-23 months, scene level, the grocery store)
- **Setup:** first proper shopping trip with the toddler buckled into the cart seat. You have a shopping list of 8-10 items and a budget.
- **Mechanic:** every time you turn to a shelf to take an item (a short "look away" while you pick), the toddler may grab something within reach from the neighboring shelf and drop it in the cart. Grabs are faster in tempting aisles (sweets, toys) and when Boredom is high.
- **Spotting extras:** the cart view shows all items; extras are not marked - compare against the list. Sneaky toddlers hide items under others (tap to lift). The toddler's face gives a hint: a guilty grin after a grab.
- **Returning:** tap an extra item, then return it to **its own shelf** (aisle signs help). Putting it on the wrong shelf costs a little time and triggers a staff member's comic sigh.
- **Keeping the toddler busy** lowers grabbing: give a snack, let them hold a list item, sing, play "I spy".
- **Joy option:** you may let the toddler keep **one** chosen item - a small treat that gives Joy, if the budget allows.
- **Objective:** reach the checkout with every list item and no unwanted extras, within the time and budget. Extras that reach the checkout are charged to your money and reduce the score.
- **Signature Golden Moment:** the toddler waves at everyone; strangers wave back.

#### Level 25 - Splash Attack (24-25 months, scene level, bath)
- **Setup:** bath time with a handheld shower. While you reach for the shampoo, the toddler grabs the shower hose.
- **Mechanic:** the toddler aims the spray at you in sweeping patterns (telegraphed by a giggle and a wind-up). Dodge by stepping or leaning within the bath area - you must never leave the tub area.
- **Getting it back:** when the toddler pauses (laughing, or distracted by a bath toy you toss in), tap and hold the hose to take it back. The toddler may grab it again later; hide it on the high hook once washing is done.
- **Soak meter:** each hit raises your Soak meter. Getting soaked is funny, not a failure: at full Soak you must change clothes, costing time.
- **Finish the bath:** wash hair (the toddler protests unless you make a "bubble crown"), rinse, towel off.
- **Objective:** bath completed within the time limit.
- **Joy:** every dodge-and-laugh exchange raises Joy; the signature Golden Moment is both of you soaked and laughing.
- **Safety detail:** before the bath, check the water temperature (a quick thermometer/elbow check step). The Milestone card covers lukewarm bath water and never leaving a child alone in the bath.

---

## 15. Controls, camera and UI

### 15.1 Controls
- **Click/tap floor:** walk. **Click/tap object or baby:** radial action menu; unavailable actions greyed out with a reason.
- **Mini-games:** drag, timing taps, hold.
- Desktop shortcuts (optional): Space = pause, number keys = quick actions.

### 15.2 Camera
3/4 top-down, following the parent, room-based framing. Zoom by scroll/pinch; rotate by right-drag/two-finger drag in 90° steps. Edge indicators for off-screen crying, Golden Moments and deliveries. During *Enjoy*, the camera eases in close.

### 15.3 HUD
- Baby needs icons (hunger, diaper, sleep, tension, boredom) with shape-coded urgency.
- Parent bars: Energy, Hunger, Stress; Joy as a glow around the parent portrait.
- Clock, day, objective, crying budget, money, supplies, phone button.
- Album button with a counter of moments caught this level.

### 15.4 Menus and flow
Title → baby setup (baby name, sex, appearance; feeding choice) → level map by chapter → level → success screen (album photo, stars) → Milestone card. Album accessible from the level map. Settings: sound, crying volume, difficulty, accessibility.

---

## 16. Realism and safety guidelines

- **Consultant** (pediatric nurse or pediatrician) reviews facts, rates and cards before release. Cards carry a short disclaimer: general information, not medical advice.
- **Safe sleep:** baby on their back in a crib with no pillows, bumpers or loose blankets; no action allows otherwise.
- **Changing table:** from month 5, one hand on the baby.
- **Bath:** never leave the tub area; check water temperature (Splash Attack).
- **Hide and seek:** appliances are never hiding spots.
- **Shopping cart:** the toddler is always buckled into the cart seat.
- **Overwhelming crying:** *Step away* models real advice; the game never depicts shaking or harming the baby.
- **Babyproofing** reflects real recommendations.
- **Feeding:** no solids before level 7; breast and formula presented without judgment.

---

## 17. Art, audio and tone

- **Tone:** joyful and funny first, honest about the hard parts (pillar 1: hard, then heart).
- **Art:** stylized low-poly 3-D, soft warm palette, expressive faces. Baby grows visibly every chapter; nursery evolves.
- **Animation priority:** baby expressions, giggles, body-language cues; Golden Moment close-ups.
- **Audio:** baby laughter and coos as often as crying; distinct cries for hunger, tiredness, pain. Bright, playful music by day, soft music at night; celebration stings for firsts. Lullabies original or public-domain.
- **Accessibility:** separate crying volume and softer-alert option; shape-coded icons; subtitles; visual equivalents for audio clues (Hide and Seek giggles); difficulty settings *Relaxed*, *Normal*, *Realistic*.

---

## 18. Business model

**Free to play. Ads will be added in a later version.**

Principles, so that ads never damage the warm experience:
- **No ads during a level.** It is a real-time game; an interruption would also break the emotional moments.
- **Interstitial ads** only between levels, with a frequency cap (e.g. at most one every 3 levels), never right after a Golden Moment celebration or the finale.
- **Rewarded ads** are always optional and never required to progress. Candidates: free express delivery, an extra *Call for help* that does not cap stars, bonus album stickers and frames, a mid-level checkpoint on retry.
- **No pay-to-win;** stars and the ending are fully reachable without ads.
- **Ad content:** family-friendly categories only (block alcohol, gambling, dating, etc.).
- **Audience declaration:** the game is about babies but made for adults; the store audience setting must reflect that (to be checked against current Google Play policy when ads are added).
- **Privacy:** ad consent flow (e.g. GDPR) added together with ads.
- **Web build:** starts without ads; web ads to be evaluated later.
- **Now:** code calls ads only through an internal ad-service interface with a no-op implementation, so an ad SDK can be plugged in later without touching gameplay code.

---

## 19. Technical notes

### 19.1 Engine and template
- **Unity 6.3 or later**, created from the **3D (URP)** template.
- URP assets per quality tier: *Web*, *Android Low*, *Android High*. Forward rendering, baked lighting with a few real-time lights, minimal post-processing.

### 19.2 Input
- **New Input System only:** Player Settings → Active Input Handling = *Input System Package (New)*. No legacy `UnityEngine.Input` calls anywhere.
- One Input Actions asset with pointer-based actions (Point, Click/Tap, Hold, Drag, Scroll/Zoom) so mouse and touch share one path; Enhanced Touch for pinch and two-finger rotate.
- UI uses the *Input System UI Input Module*.
- Desktop keyboard shortcuts are optional bindings, never required.

### 19.3 Command-line (CLI) support
Everything a developer or CI needs runs headless from the command line:
- **Builds** through static editor methods, for example:
  - `Unity -batchmode -quit -projectPath . -executeMethod BabyChampion.Editor.BuildScript.BuildWeb -logFile -`
  - `Unity -batchmode -quit -projectPath . -executeMethod BabyChampion.Editor.BuildScript.BuildAndroid -logFile -`
  - Build methods read options (output path, development build, version) from command-line arguments and return a non-zero exit code on failure.
- **Tests:** `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml` (and `PlayMode`).
- **Data validation:** `-executeMethod BabyChampion.Editor.DataValidator.Run` checks all level and tuning data.
- **Balance simulator:** `-executeMethod BabyChampion.Editor.BalanceSim.Run -level 8 -runs 500` plays a level headlessly with a simple bot and reports failure rates, so tuning can be checked without playing by hand.
- **Runtime arguments for development builds:** `-level 10 -timescale 4 -seed 123 -skipIntro` (on the Web build, the same as URL parameters, e.g. `?level=10`).

### 19.4 Architecture
- **Simulation core in plain C#** (no MonoBehaviour dependency): meters, rules, time, economy. This keeps it unit-testable in EditMode tests and fast enough for the CLI balance simulator.
- **Data-driven content:** ScriptableObjects for levels, age rates, prices and Golden Moments, with JSON export/import for tooling.
- **Parent actions go through a controller interface**, so a later AI partner or second human player (Sections 22-23) can drive a parent without changes to the simulation.
- **Ad-service interface** with a no-op implementation (Section 18).
- Assembly definitions for Runtime, Editor and Tests.

### 19.5 Platforms
- **Web:** Unity Web platform build for itch.io. Use compression with *Decompression Fallback* enabled (itch.io does not serve the compression headers Unity's default setup expects), keep download size small, test early in mobile browsers.
- **Android:** IL2CPP, ARM64, App Bundle (AAB) for Google Play; target API level per current Play requirements; landscape.

### 19.6 Saving
Automatic save at the end of each level as JSON in `Application.persistentDataPath` (browser storage on Web - verify persistence on itch.io early). One save slot per baby, up to 3 babies. The Memory Album stores level and moment IDs, and photos are regenerated from them rather than stored as images, to keep saves small.

---

## 20. Open questions

1. Should the baby have a temperament that varies between playthroughs (easy vs. sensitive)?
2. Final names, prices and currency.
3. Which consultant reviews the content, and when?
4. Localization: which languages, and when?
5. Ad SDK choice and web ad strategy (when ads are added).

Open questions about the partner NPC are in Section 22.11.

---

## 21. Development tasks

1. **Project setup:** Unity 6.3+ URP project, new Input System only, assembly definitions, CLI build and test scripts, CI.
2. **Core simulation** in plain C# with EditMode tests: baby and parent meters, Joy, supplies, money, time.
3. **Vertical slice:** Levels 1 (tutorial), 2 (night shift), 4 (first laughs - Joy system), 8 (spoon swatter), 10 (mouse thief). Includes Golden Moments and the Album. Playtest with the target audience - measure whether players finish levels smiling.
4. **Controls, camera, HUD** on mouse and touch; test on a mid-range Android phone and in a mobile browser.
5. **Balance simulator** and first tuning pass.
6. **Content review** by the consultant.
7. **Remaining levels**, chapter by chapter, including Hide and Seek, Cart Sneak and Splash Attack; then routine levels and the random-event pool.
8. **Art and audio pass**; accessibility options.
9. **Release** on itch.io (web) and Android; ad-service stub in place.
10. **Later:** ads integration, partner NPC (Section 22), two-player mode (Section 23), more levels.

---
---

# Later-stage features

The features below are designed now but will be implemented **after the base game**. The base game (Sections 1-21) must not depend on them.

---

## 22. Partner NPC (later stage)

### 22.1 Purpose
- Makes parenting feel like the team effort it often is, and teaches real cooperation (shift sleeping, sharing tasks, looking after each other).
- Adds warmth and humor (shared Golden Moments, banter, quirks).
- Must **help, not play the game for you**: the player always remains the main caregiver in the level.

### 22.2 Setup
- At game start, a new choice: "Raising the baby: **with a partner** / **on my own**". *On my own* is the base game.
- The player names the partner and picks their appearance.
- The partner gets **one strength and one quirk**, chosen by the player or rolled at random:

| Strengths | Quirks |
|---|---|
| **Lullaby Star** - soothes Tension twice as fast. | **Riles them up** - plays wild games right before bedtime (raises baby Tension; very funny, slightly inconvenient). |
| **Kitchen Hero** - cooks meals for both of you (feeds the parent). | **Forgetful shopper** - sometimes orders the wrong item (e.g. wrong diaper size). |
| **Night Owl** - loses less Energy on night shifts. | **Deep sleeper** - does not wake up for the first cry. |
| **Fun Parent** - earns extra Joy from play. | **Messy** - leaves dishes and toys around (raises Stress). |

### 22.3 Availability
- **Level 1:** home all day; joins the tutorial (an extra step introducing requests).
- **Levels 2-3:** home in the evenings and nights.
- **From level 4:** works outside the home on weekdays, about 08:00-18:00; home evenings, nights and weekend levels.
- Some levels specify the partner's schedule (e.g. away on a business trip, or hosting the birthday party with you in level 13).
- When the partner is out, they can still send a text message (a supportive line, a funny photo request: "Send me a picture of the baby!" - taking one gives Joy).

### 22.4 Behavior (AI)
- **Autonomous helping:** when idle, the partner picks a useful task by urgency (utility scoring): comfort a crying baby, change a diaper, wash dishes, cook. They never take the level's primary objective task (e.g. they won't do the spoon mini-game in *Spoon Swatter*).
- **Requests:** tap the partner for a radial menu:
  - *Take the baby* / *Feed the baby* / *Change the diaper*
  - *Cook for us* / *Order supplies*
  - *Your turn tonight* / *I'll take tonight* / *Let's alternate* (night plan)
  - *Take a break* (sends the partner to rest)
- **Responses:** accept ("On it!"), delay ("After I finish this"), or, when exhausted, decline with a swap offer ("I'm wiped - can you take this one and I'll do the next?"). Speech is shown as short bubbles with icons.
- **Night plan:** at bedtime, a quick choice of who handles night wakings. Alternating keeps both Energy bars healthy - a real strategy many parents use.

### 22.5 Partner state
- Simplified **Energy** and **Stress**, shown as a mood face above their head.
- Exhausted partner: slower, quirks appear more often, then goes to sleep and is unavailable until rested.
- The partner never causes a level failure by themselves.

### 22.6 Couple moments
- **Sit together** (both on the sofa while the baby sleeps): both Stress falls, Joy rises.
- **High five** after a hard task: small Joy boost.
- **Shared Golden Moments:** when a Golden Moment starts and the partner is nearby, they call "Come quick, look!". If both parents tap Enjoy, the photo shows both, with a bigger Joy boost.
- If both are exhausted, a short comedic bicker bubble appears; *Hug* resolves it.

### 22.7 Balance
- The base game is tuned for one parent. In partner mode, the challenge must stay comparable: levels get faster need rates and more simultaneous events, so that the partner covers roughly 20-30% of the work while the player stays the main caregiver.
- Both modes are presented as equal choices; neither is a "hard mode".

### 22.8 Additional procedures

| Action | Where | Requires | Effect | In-game duration |
|---|---|---|---|---|
| **Ask partner** | Near partner | Partner home | Requests from 22.4. | instant |
| **Sit together / High five / Hug** | Near partner | Partner home | 22.6. | 2-15 min |

### 22.9 Changes to the base game when the partner is added
- **Setup screen:** *with a partner / on my own* choice; partner name, look, strength and quirk.
- **Controls and HUD:** the partner can be tapped for the radial menu; partner mood face on the HUD.
- **Feeding:** with formula or expressed milk, the partner can do feeds too.
- **Money:** the partner's salary adds a fixed daily amount.
- **Golden Moments:** the partner's "Come quick" call (22.6).
- **Level 1:** extra tutorial step on requests. **Level 2:** objective adds "agree a night plan".
- **Technical:** the partner AI drives a parent through the controller interface of Section 19.4.

### 22.10 Technical link to two-player mode
The partner is driven through the same parent-controller interface as the player (human input or AI). The two-player mode (Section 23) simply replaces the AI controller with a second human.

### 22.11 Open questions
1. Visual identity and voice of the partner.
2. How often quirks trigger.
3. Whether the partner has a long-term relationship meter across levels.

---

## 23. Two players (later stage)

- Two parents cooperate - local network or online.
- The second player takes the partner's role through the same parent-controller interface (Section 22.10); the partner's strength and quirk are dropped.
- Shared Golden Moments with both players tapping Enjoy give the biggest Joy boosts.
- Levels are retuned for two players (faster needs, more simultaneous events).
