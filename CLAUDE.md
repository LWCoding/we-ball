# We Ball

2D co-op platformer where every player is a ball. Uses Netcode for GameObjects and Unity Relay (6-character join codes).
Unity 6000.3.24f1, URP 2D, new Input System only. Flow: Connect → Lobby (host picks save slot) → Game.

## Non-obvious decisions

- **Scenes and Player.prefab are generated** by `WeBall.Editor.ProjectSetup.Generate()` (*Tools > We Ball*). Re-running
  overwrites them. After the user hand-edits scenes, edit the scenes directly instead of re-running.
- **Relay uses `wss` + `UseWebSockets` on all platforms**, so WebGL (itch.io) works and web and desktop players can mix. Don't switch to dtls/udp.
- Player movement is **owner-authoritative** (`NetworkTransform.AuthorityMode = Owner`). It's simple, but not cheat-proof.
- `NetworkConfig.PlayerPrefab` is null on purpose. `PlayerSpawner` spawns balls only in the Game scene.
- The Relay SDK is in `com.unity.services.multiplayer`. The standalone `com.unity.services.relay` is deprecated.
- Editor and dev builds sign in with a random auth profile so several local copies count as different players.
- NGO gotcha: `OnLoadEventCompleted` can fire before `Start()` in the new scene, so subscribe in `Awake()`.
- Keep `NetworkRigidbody2D.UseRigidBodyForMotion` **off**. In NGO 2.13.3 its 2D path breaks remote rotation
  (`InternalMoveRotation2D` uses `quaternion.z` as degrees).
- Browsers pause hidden tabs, so a web client can stall mid-scene-load. `LoadSceneTimeOut` is 15 s, and
  `PlayerSpawner` spawns stragglers on their late `OnLoadComplete`.
- Remote balls are kinematic locally (owner authority), so players can't push each other.
- For package APIs, read `Library/PackageCache/<pkg>@<hash>/` instead of guessing.

## Unity CLI (`unity`) — use this; the Editor is usually open, so batchmode is blocked

- `unity --json recompile --project-path .` after script changes. `unity command eval --code '...; return x;'` runs editor code.
- The first `eval` after a reload often times out (5000ms) while Roslyn warms up, so retry. Long evals report that
  timeout but still finish, so verify the result afterward.
- `capture_game_view --save_path "Temp/x.png"` writes to `Assets/Temp/`. Move the file out and
  `AssetDatabase.DeleteAsset("Assets/Temp")`.
- Simulated keys (`InputSystem.QueueStateEvent`) only work after `editor_focus`. Release them with an empty `KeyboardState()`.
- Smoke test: `editor_play` → invoke the `Host Game` button → reach Game alone by reflecting `LobbyController.ChooseSaveSlot(int)`.
  Delete the save it writes in `AppData/LocalLow/DefaultCompany/we-ball/`.
- Two-player test without Multiplayer Play Mode: `unity command build --target StandaloneWindows64 --outputPath Builds/WinTest/we-ball.exe
  --options '["Development"]' --confirm true`, then launch it with `-join CODE -logFile <path>` (dev builds only).
  Building switches the active target, so switch back to WebGL afterward. It opens windows on the user's desktop,
  so ask first, and never screen-capture the desktop.
