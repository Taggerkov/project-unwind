# Project Unwind – Game Design Document

**Genre:** 2.5D Fighting Game  
**Platform:** PC (Windows), Web (WebGL)  
**Engine:** Unity 6.3   
**Target audience:** Established fighting game players familiar with FG fundamentals.

---

## High Concept

Project Unwind is a 2.5D, one-versus-one fighting game set in a Medieval European Grimdark Gothic world.  
Two combatants contest a round of 1vs1 combat on a 2D plane represented by 3D assets and lighting. Play sessions are
short and high-stakes: the first player to deplete the opponent's health wins the round, and the first to win the
required number of rounds wins the set.  
The game targets the competitive fighting game community and prioritises mechanical depth over approachability for
newcomers.

The artistic and mechanic point of reference is Arc System Works (Guilty Gear Xrd, Guilty Gear Strive, BlazBlue).
Characters are rendered as 3D models in a style that reads as classic anime. The colour palette is desaturated toward
greys and blacks. Environments reference Gothic architecture: vaulted stone, iron, candlelight. Lore is delivered
through implication.

---

## Core Loop

```
Character Select
       |
   Round Start
       |
   Neutral / Footsies  <---+
       |                   |
  Commit to attack         |
       |                   |
  Combo or whiff           |
       |                   |
  Okizeme / Reset ---------+
       |
  Round End (HP depleted)
       |
  Set End or Next Round
```

A round ends when one character's HP reaches zero or time expires (higher HP wins). There is no draw. Sets are
first-to-two by default.

---

## Systems

### Health

Each character has a single HP bar. HP does not regenerate mid-round, as per default. Chip damage from blocked Specials
and Overdrive moves can kill.

### Buttons

Six action inputs per player, mapped to the arcade panel (New Input System):

| Button  | Arcade | Notation |
|---------|--------|----------|
| Light   | A      | L        |
| Medium  | B      | M        |
| Heavy   | X      | H        |
| Unique  | Y      | U        |
| Guard   | LT     | G        |
| Ability | RT     | AB       |

Direction input uses numpad notation in character space (6 is always toward the opponent).

### Movement

All movement values are character-specific stats (`CombatantStats`). Universal movement options:

- Forward and backward walk
- Forward run (double-tap 6, cancellable on negative edge)
- Back step (double-tap 4)
- Forward, backward, and neutral jump; aerial forward and backward jump (double jump supported)
- Air dash (character-dependent)

Crouch is held 2. Standing and crouching share separate hit and block pose pools.

### Cancel Chain

Moves cancel into higher-priority categories on the tick the hit or block lands (Gatling), or when the move itself
enables early exit (IASA). The priority order is:

**Normal** cancels into **Special** cancels into **Overdrive**

A 3-tick Kara-cancel window at the start of every move allows cancelling into same-tier or higher-tier moves before the
commit locks.

### Guard System

Players hold G (or moving back) to block. Guard type must match the attack:

- High guard: blocks overhead attacks, not lows
- Low guard: blocks low attacks, not overheads
- Any guard: blocks regardless of stance
- Unblockable: cannot be blocked by any guard type

Blocking incurs block-stun proportional to the blocked move. Chip damage applies to blocked Specials and Overdrives.

### Hit Levels

Five hit levels (1 through 5). Higher levels deal more hit-stun, larger knockback, and trigger higher-tier damage poses.
Hit level is set per move in `HitData` and governs the reaction, not clash priority: when two hitboxes connect on the
same tick, both land (a trade). Timing decides everything else, since whichever move goes active first hits first.

---

## Architecture

The game's technical architecture follows the SOLID design principles and patterns outlined in
Unity's *Level up your code with design patterns and SOLID* (Unity 6, 2024).

### Dependency Injection

Dependencies are managed through **Reflex**, a Unity DI container. All managers are registered as eager singletons at
the composition root
(`RootInstaller`) and injected into consumers at construction time via `[Inject]` fields or
constructor parameters. This keeps the dependency graph explicit and avoids hidden coupling.

### Tick System

All combat logic runs through **TickManager** at a fixed 60 Hz, implemented as an accumulator
in Unity's `Update` loop. Each tick fires three ordered phases on every `ITickable<TickManager>`:
`InputTick` then `LogicTick` then `UITick`. Frame data (startup, active, recovery) is expressed
in ticks rather than Unity frames, which makes hitbox timing deterministic across hardware and
independent of render frame rate.

### ScriptableObject Data Layer

Static game data is authored as ScriptableObjects. Tuning does not require code changes:

- `CombatantMoveSetDefinition`: holds a character's full move list and the `CombatantStats`
  prototype (a `[SerializeReference]` field, not itself a ScriptableObject). Move instances are
  cloned from this asset at Awake, so each combatant gets an independent runtime state.
- `AudioEvent`: sound event definition (clip reference, category, volume, loop flag), played
  via `AudioManager`.
- `CombatantDatabaseSO`: the full character roster, consumed by Character Select.
- `CombatantDataSO`: per-character asset bundle reference and selection metadata.

`CombatantStats` are plain structs and serialisable classes respectively,
they live inside ScriptableObjects but are not ScriptableObjects themselves.

### Pose Animator

Character animation is driven by `PoseAnimator`, a custom tick-aligned system separate from
Unity's Animator component. There is no Animator Controller or animation clip. Each move's
`Script()` coroutine issues `yield return Pose(id, ticks)` calls that tell PoseAnimator
which pose to hold and for how long. PoseAnimator sets bone transforms directly every tick
from `CombatantPoseSheet` assets.

Hitbox active windows are defined in the same Script() coroutine via `BeginActiveState()`,
immediately adjacent to the pose yields they correspond to. This makes the visual frame and the
hitbox frame the same tick, with no offset.

### Physics

Character motion uses **KinematicCharacterController (KCC)**, driven inside
`CombatManager.LogicTick` after all combatant logic for that tick has resolved. Velocity is
written to the combatant's `CombatantCharacterController` during move execution and consumed by
KCC during simulation. This ordering ensures velocity changes in a move's Script() always
take effect in the same tick they are written.

### Manager Layer

| Manager          | Responsibility                                                                        |
|------------------|---------------------------------------------------------------------------------------|
| `GameManager`    | Drives screen flow across `MainMenu`, `CharacterSelect`, and `Combat` states.         |
| `CombatManager`  | Combatant lifecycle, hit overlap solving (`CombatOverlapSolver`), KCC simulation.     |
| `AudioManager`   | Plays `AudioEvent` ScriptableObjects by Guid. Categories: music, SFX, voice.          |
| `TickManager`    | Fixed 60 Hz accumulator loop; three-phase tick dispatch.                              |
| `PlayerRegistry` | Wraps Unity's `PlayerInputManager`; routes per-player `IInputProvider` to combatants. |

---

## Signature Mechanics

### Stamina

Stamina replaces traditional combo scaling and damage reduction. Every move that enters an active state costs a fixed
amount of Stamina. Stamina depletes across a combo. When Stamina reaches zero mid-combo, the current move is interrupted
with a Stamina Break state – the character staggers, the combo ends, and the opponent is pushed back to neutral.

Stamina regenerates at a fixed rate when the character is in a neutral or movement state and not attacking. It does not
regenerate during hit-stun, while block-stun inflicts a slower regeneration rate.

This creates resource management inside combos: players must decide when to spend Stamina on an extra cancel route
versus conserving it to threaten a longer sequence next interaction. It also acts as a natural anti-infinite mechanism
without requiring explicit combo counter detection.

### Unique Ability

Every character has a Unique Ability (UA) accessed via the U button or in combination with motions. UA is not a
universally defined super. Each character defines their own UA independently, with character-specific:

- **Requirements**: minimum resource threshold, stance condition, hit-confirm, or sequence requirement
- **Resource model**: UA may consume a shared pool, individual stacks, or a charge that accumulates on hit
- **Outcome**: may be a move, a stance toggle, a buff application, or a multi-hit sequence

The UA system replaces the modern "EX Super" or "Ultimate" pattern and is designed to produce character-specific
identity rather than a universal power escalation. No two characters share a UA pattern.

---

## Characters

### RDR – The Redeemer

**Name:** Tancred.

**Quote:** "Actions speak louder than any promise. Though respect and honour are built on the persona behind. Be it
actions alone, how are we any different from the blades we use?"

**Description:** A veteran knight of the Holy Order's elite frontline. Decades of service, black operations, and
institutional loyalty distilled into a singular instrument of the order's will. Now branded a traitor and deserter.

**Move signature:**

- Passive (Bulwark): stance switch between offensive and defensive toolkit; defensive grants damage reduction and
  tighter guard windows at the cost of offensive output.
- Unique (Retribution): once per match, context-defined activation; offensive use enters a power state with damage
  reduction, required-hit lifedrain and amplified output; defensive use forces neutral and grants frame advantage.
- Territorial mid-range neutral with a dominant ground footprint.
- Sequential special structure (rekka chain) rewards commitment and punishes hesitation.
- Heavy throws as primary punish and space control tools.
- Stamina outlasting through attrition, not pressure.
- Bulwark stance as a mental game layer, switching reads opponent adaptation rather than extending combos.

**Archetype:** Tancred is a territorial mid-range built on attrition and commitment
reads.Archetype: Tancred is a territorial mid-range built on attrition and commitment reads. His toolkit rewards
patience and situational discipline, only unlocked by mastering Bulwark's stance trade-offs. Appealing to players that
focus on Discovery and Mastery.

### GMR – The Grim Reaper

**Name:** Tiphaine

**Quote:** "Pain, suffering, death. Dark whispers to be ignored. As the Order's high-guard it is my duty to walk through
hell so you don't."

**Description** A witch huntress of the Holy Order's high-guard. Deployed against high-value targets. She leaves no
witnesses.

**Move signature:**

- Passive (Death Omen): dodge activation grants a confirm window based on perceived incoming attack.
- Unique (Deadly Conclusion): once per match, forces opponent aggression; successful 'Death Omen' refills stamina.
- Evasion-focused neutral with superior dodge windows and rapid gap closing.
- Stamina drain through volume and pressure, not single tools.
- Pre-commitment counter playstyle. Reads one step ahead of reaction.
- Short confirmed strings with branching mixup structure.

**Archetype:** Tiphaine is a pressure rushdown based on reads and precommits. Her toolkit gives her a vast mixup,
only limited by her Stamina drain. Appealing to players that focus on Challenge and Expression.

---

## User Interface

Three game states drive screen flow: `MainMenu`, `CharacterSelect`, `Combat`.

- **Main Menu:** start match, options, quit.
- **Character Select:** players each select a character from the database (`CombatantDatabaseSO`). Selection confirms
  the encounter data and triggers a scene load.
- **Combat HUD:** two HP bars (top of screen), round timer, Stamina bars per character, UA resource indicator per
  character.

All UI navigation is configured per selectable element through Unity's Navigation field.

---

## Audio

### AudioEvent

The unit of audio authoring is the `AudioEvent` ScriptableObject (Create menu: Audio / Sound Event). Each asset holds:

- An Addressables clip reference (`AssetReferenceT<AudioClip>`)
- A category (`Music`, `Sfx`, `Ambient`, or `Voice`)
- Default volume (float, 1 = unity gain)
- Default speed (float pitch multiplier, 1 = normal)
- Loop flag (caller must retain the playback Guid and call `Stop` explicitly; looping sounds are never auto-stopped)

Clips are loaded via Addressables on demand. `AudioManager.PreloadAsync` must complete before the first `Play` call on
that event. This is the preload contract; violating it produces a silent playback.

### AudioManager

`AudioManager` is the only public surface for audio, keyed by the `Guid` returned from `Play`.
It supports per-playback control (play, stop, pause, resume, volume, speed, state query) and
category-level control (stop all, pause all, resume all, category volume, category speed).
All playback routes through the configured `IAudioService` backend.

### Backend

The active backend is selected via the `AudioSettings` ScriptableObject. The currently active
backend is `BuiltIn` (Unity `AudioSource` pool, 16 sources, auto-resizing). FMOD is wired but
not yet implemented; switching requires the FMOD Studio Unity package and a backend
implementation behind `IAudioService`.

### SFX Library

The common SFX library covers weapon swing sounds by weapon type (Axe, Grapple, Hammer, Pole, Slash, Sword) and by
impact weight (Heavy, Sharp) in three sizes (S, M, L) with A and B alternates per size. Hit level maps to size: higher
hit levels select larger variants. Background music loops per scene. Scene transitions call
`StopAll(AudioCategory.Music)`
before loading the next scene's music event.

---

## Design Intent

Project Unwind targets three aesthetic goals and produces three further aesthetics implicitly through genre and
composition.

### Targeted:

**Sensation:** the game's visual and audio identity, achieved through the deliberate collision of Gothic architecture,
desaturated colour, and combat sound design into a unified atmosphere.

**Challenge:** the Stamina mechanic adds a recoverable resource to manage mid-combination, layering decision-making on
top of execution. Players must decide when to spend Stamina on an extra cancel route versus conserving it to threaten a
longer sequence next interaction.

**Fantasy:** the grimdark medieval setting gives the player a fiction that has weight and consequence. The Unique
Ability extends this further, grounding each character's UA in what that character is rather than what a universal power
tier requires. The UA is an extension of the character.

### Implicit:

**Fellowship:** produced structurally by local two-player play on a shared cabinet. The adversarial relationship between
two players physically present at the same machine creates social investment without requiring design intervention.

**Discovery:** produced by system depth. Character-specific UA patterns, distinct cancel routes, hit level interactions,
and move properties give players a permanent incentive to lab and learn. The depth is not surfaced; it is found.

**Expression:** produced by composition. A player who wins through patient spacing expresses something different from
one who wins through aggressive cancel chains. The game does not direct this; the neutral game and character variety
make it available.

---

## Technical Constraints

| Constraint      | Value                             |
|-----------------|-----------------------------------|
| Target hardware | GTX 980 Ti, i5-6600K, 8 GB RAM    |
| Build size      | 500 MB max                        |
| Input           | Two gamepads via New Input System |
| Platform        | Windows + WebGL                   |

---

## Monetisation

Project Unwind follows a modified traditional fighter monetisation model.

**Base release:** four characters. One character is free-to-play permanently. The remaining
three require purchasing the full game.  
Free-to-play access is limited to Training Ground and non-ranked public matchmaking. All other modes, offline and
online, require ownership.

**Post-launch updates:** the roster expands to eight characters, all included in the base game
purchase at no additional cost.

**Paid DLCs:** any character released after the base eight is sold individually, priced
between 15 and 20 EUR.

**Try before you buy:** all characters are playable in Training Ground regardless of ownership
status. Ownership is required for every other mode.

---

## Development Milestones

### Milestone 1 – Tick System

**Goal:** Design and implement the internal tick system that all combat logic will run through.

Deliverables:

- `TickManager` running a fixed 60 Hz accumulator in Unity's `Update` loop
- Three-phase tick dispatch: `InputTick` → `LogicTick` → `UITick`
- `ITickable<TickManager>` interface consumed by all combat systems
- `SetTimeScale` method functional
- No combat logic runs outside the tick system

### Milestone 2 – Combat System

**Goal:** Design and implement move execution, cancel chains, and combo scripting through `CombatManager`.

Deliverables:

- Move `Script()` coroutine system functional via `PoseAnimator`
- Gatling cancel and IASA exit windows resolving correctly on hit and block
- Kara-cancel window functional at move startup
- `BeginActiveState()` hitbox windows tick-aligned to pose yields
- `CombatOverlapSolver` resolving hitbox/hurtbox overlaps each tick, deduplicated per attack so reciprocal overlaps trade
- Stamina depletion and Stamina Break state functional

### Milestone 3 – Playable Core

**Goal:** A complete playable session from character select to round end, with UI and audio integrated.

Deliverables:

- Character select screen functional for both players
- The Redeemer fully playable through the combat system built in Milestone 2
- HP bars, round timer, Stamina bars, and UA resource indicator live in HUD
- Round and set flow: KO detection, win screen, rematch
- All menus navigable by gamepad only, no cursor
- Audio integrated: SFX on hit and block, music looped per scene
- AI opponent: priority state-machine input provider covering reposition, attack, and reactive guard on detected threats (satisfies course topic 7)
- WebGL build passing and hosted on GitHub Pages
- YouTube demonstration video (~2 minutes) recorded and linked in README
- README complete: all six blog post links, third-party asset and code credits