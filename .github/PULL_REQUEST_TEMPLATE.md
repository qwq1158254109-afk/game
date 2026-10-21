# Pull request

## What this changes

<!-- One or two sentences. Link the issue it closes, if any: "Closes #12" -->

## Type

- [ ] Bug fix
- [ ] Feature
- [ ] Content / data
- [ ] Refactor (no behaviour change)
- [ ] Tooling / docs

## Which systems are touched

<!-- e.g. Map, Interaction, Activity, Battle, Cards, Inventory, UI, Save, Tools -->

## How it was verified

<!--
Not "it works" — what did you actually do?
e.g. "Ran EditMode tests: 43/43 pass", "Walked to the bedroom door, pressed E,
scene switched, walked back with E."
-->

## Checklist

- [ ] Project compiles with **0 errors**
- [ ] Existing behaviour still works (describe what you re-checked)
- [ ] No generated output committed (`__pycache__`, `_*_probe.txt`, `_layout_dump.txt`, `_room_shot.png`, `Assets/_Recovery/`)
- [ ] No scenes / prefabs / ScriptableObjects renamed
- [ ] If a code-generated scene was touched: a rebuild was run and this is mentioned above
- [ ] If a runtime probe was added for testing: it has been deleted again
      (`.cs` + `.meta`), and `Assets/Refresh` + recompile confirms 0 errors

## Screenshots

<!-- For visual changes. Drag images in here. -->
