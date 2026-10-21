# Contributing to Student Growth

Thanks for taking a look. This is an early-stage project, so the most useful contributions
right now are bug reports with reproduction steps and feedback on the core loop.

---

## Before you start

Read [`AGENTS.md`](AGENTS.md). It documents the project's engineering conventions and a list
of environment gotchas that have each cost real debugging time — MCP connection behaviour,
where compile errors actually live, how Play mode picks its scene, and so on.

Two things in particular will save you from wasted work:

- **The home room scenes are code-generated.** `Assets/Editor/StudentGrowth/HomeRoomsBuilder.cs`
  rebuilds all four rooms. Hand edits to those scenes are overwritten on the next rebuild.
  Furniture placement lives in `DecorSpec(x, y)` in that file.
- **Play mode loads the first scene in Build Settings**, not the scene you have open in the
  editor. To test a specific room, navigate to it in-game.

---

## Reporting a bug

Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md). The two things that
make a report actionable:

1. **Steps to reproduce** — including which scene and which interaction.
2. **Console output** — and if the Console only says
   `All compiler errors have to be fixed before you can enter playmode!`, note that the real
   errors are in `Library/Bee/tundra.log.json`; run
   `python Tools/extract_compile_errors.py` to read them.

Screenshots help a lot. Drag them into the issue.

---

## Proposing a feature

Use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).

The project follows a staged roadmap (V0.2 map interaction → V0.3 multi-location → V0.4 card
battle → V0.5 growth routes → V0.6 NPCs). Features that don't connect to the core loop
below are usually deferred:

```
Enter a location → move → interact → execute an activity
  → gain stats / cards / items → advance time → stage boss → next stage
```

---

## Submitting a pull request

1. Fork and branch from `main`.
2. Keep changes focused — one concern per PR.
3. Make sure the project compiles with **0 errors** before opening the PR.
4. If you touched anything under `Assets/Scenes/` that is code-generated, say so in the PR
   description and mention that a rebuild was run.
5. Describe **what changed and how you verified it**. "It works" is not verification;
   say what you ran, what you clicked, and what you saw.

### Code conventions

- `UI / Controller / Data` are separated. UI views only display and forward input;
  controllers own flow and rules; data lives in ScriptableObjects or static config.
- Prefer `[SerializeField] private` over public fields.
- File name must match the primary class name.
- Runtime scripts go under `Assets/Scripts`; editor-only scripts under `Assets/Editor`.
- Interaction is data-driven: add a `MapInteractionTarget` + `MapInteractionPoint` rather
  than writing a new per-object script.
- Don't hardcode scene names, card names or activity names in UI code.

### Things to avoid

- Rewriting large parts of the project in one go.
- Renaming scenes, prefabs or ScriptableObjects (GUIDs and references break).
- Deleting assets.
- Adding a third-party plugin when the existing stack already covers the need.
- Committing generated output: `__pycache__`, `_*_probe.txt`, `_layout_dump.txt`,
  `_room_shot.png`, `Assets/_Recovery/` — these are already in `.gitignore`; keep them out.

---

## Development environment

- **Unity 6**, pinned to `6000.4.6f1` in `ProjectSettings/ProjectVersion.txt`.
- Four GitHub UPM packages resolve automatically on first open (LitMotion, UIEffect,
  NavMeshPlus, Yarn Spinner). They appear under **Packages: In Project** in Package Manager,
  not under "My Assets" or "Unity Registry".
- If UPM fetches fail due to network resets, `Tools/install_git_mirrors.sh` rebuilds a local
  mirror set.

### Tests

43 test files under `Assets/Tests/`, covering scene finalization, battle flow, activity
resolution and UI behaviour. Run them from Unity's Test Runner. EditMode tests are reliable;
PlayMode tests are currently unreliable in this environment.

---

## License

By contributing you agree that your contributions are licensed under the
[MIT License](LICENSE).
