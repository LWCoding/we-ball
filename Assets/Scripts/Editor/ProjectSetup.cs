using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WeBall.Gameplay;
using WeBall.Lobby;
using WeBall.Networking;
using WeBall.Saving;
using WeBall.UI;

namespace WeBall.Editor
{
    /// <summary>
    /// Generates the starter scenes (Connect, Lobby, Game), the player prefab and placeholder art.
    /// Safe to re-run, but it overwrites those scenes, so re-running discards manual edits to them.
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenesFolder = "Assets/Scenes";
        const string ArtFolder = "Assets/Art";
        const string PrefabsFolder = "Assets/Prefabs";
        const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";

        static readonly Color MenuBackground = new(0.12f, 0.13f, 0.18f);
        static readonly Color SkyColor = new(0.55f, 0.78f, 0.95f);
        static readonly Color GroundColor = new(0.3f, 0.22f, 0.16f);
        static readonly Color PlatformColor = new(0.45f, 0.65f, 0.3f);

        static string ScenePath(string name) => $"{ScenesFolder}/{name}.unity";

        [MenuItem("Tools/We Ball/Generate Starter Scenes")]
        static void GenerateFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Generate starter scenes",
                    "This creates (or overwrites) the Connect, Lobby and Game scenes, the Player prefab and placeholder art.",
                    "Generate", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Generate();
        }

        /// <summary>No prompts, so it can be run from the Unity CLI: unity command eval ...</summary>
        public static void Generate()
        {
            EnsureFolder(ArtFolder);
            EnsureFolder(PrefabsFolder);

            Sprite circle = CreateSprite("Ball", DrawBall);
            Sprite square = CreateSprite("Square", (_, _) => Color.white);
            GameObject player = CreatePlayerPrefab(circle);
            NetworkPrefabsList prefabsList = RegisterNetworkPrefab(player);

            BuildConnectScene(prefabsList);
            BuildLobbyScene();
            BuildGameScene(square, player);

            EditorBuildSettings.scenes = new[] { SceneNames.Connect, SceneNames.Lobby, SceneNames.Game }
                .Select(name => new EditorBuildSettingsScene(ScenePath(name), true))
                .ToArray();

            EditorSceneManager.OpenScene(ScenePath(SceneNames.Connect));
            Debug.Log("We Ball: starter scenes generated. Press Play in the Connect scene.");
        }

        // ---------- Scenes ----------

        static void BuildConnectScene(NetworkPrefabsList prefabsList)
        {
            NewScene();
            CreateCamera(MenuBackground);
            CreateEventSystem();

            var networkGo = new GameObject("NetworkManager");
            var networkManager = networkGo.AddComponent<NetworkManager>();
            var transport = networkGo.AddComponent<UnityTransport>();
            networkGo.AddComponent<ConnectionManager>();
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = null; // Balls are spawned by PlayerSpawner in the Game scene.
            if (!networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(prefabsList))
                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);

            Transform panel = CreatePanel(CreateCanvas().transform, "Menu");
            CreateText(panel, "Title", "We Ball", 96);
            Button host = CreateButton(panel, "Host Game", "Host a Game");
            CreateText(panel, "Or", "- or -", 32);
            InputField codeInput = CreateInputField(panel, "Join Code", "Enter 6-character code");
            Button join = CreateButton(panel, "Join Game", "Join a Game");
            Text status = CreateText(panel, "Status", "", 32);

            var menu = panel.gameObject.AddComponent<ConnectMenu>();
            Assign(menu, "hostButton", host);
            Assign(menu, "joinButton", join);
            Assign(menu, "joinCodeInput", codeInput);
            Assign(menu, "statusText", status);

            SaveScene(SceneNames.Connect);
        }

        static void BuildLobbyScene()
        {
            NewScene();
            CreateCamera(MenuBackground);
            CreateEventSystem();

            Transform panel = CreatePanel(CreateCanvas().transform, "Lobby");
            CreateText(panel, "Title", "Lobby", 80);
            Text joinCode = CreateText(panel, "Join Code", "Join code:", 48);
            Text status = CreateText(panel, "Status", "", 32);

            Transform slotPanel = CreatePanel(panel, "Save Slots");
            Object.DestroyImmediate(slotPanel.GetComponent<ContentSizeFitter>()); // The parent layout sizes it.
            var slotButtons = new Button[SaveSystem.SlotCount];
            for (int i = 0; i < slotButtons.Length; i++)
                slotButtons[i] = CreateButton(slotPanel, $"Slot {i + 1}", $"Slot {i + 1}", 720);

            Button leave = CreateButton(panel, "Leave", "Leave");

            var lobbyGo = new GameObject("Lobby Controller");
            NetworkObject networkObject = lobbyGo.AddComponent<NetworkObject>();
            var lobby = lobbyGo.AddComponent<LobbyController>();
            Assign(lobby, "joinCodeText", joinCode);
            Assign(lobby, "statusText", status);
            Assign(lobby, "saveSlotPanel", slotPanel.gameObject);
            Assign(lobby, "saveSlotButtons", slotButtons);
            Assign(lobby, "leaveButton", leave);

            SaveScene(SceneNames.Lobby);

            // In-scene NetworkObjects need an ID derived from the saved scene, which only exists after the first save.
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            SaveScene(SceneNames.Lobby);
        }

        static void BuildGameScene(Sprite square, GameObject playerPrefab)
        {
            NewScene();
            Camera camera = CreateCamera(SkyColor);
            camera.gameObject.AddComponent<CameraFollow>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            Transform level = new GameObject("Level").transform;
            CreateBlock(level, square, "Ground", new Vector2(0f, -4f), new Vector2(40f, 1f), GroundColor);
            CreateBlock(level, square, "Left Wall", new Vector2(-20.5f, 2f), new Vector2(1f, 13f), GroundColor);
            CreateBlock(level, square, "Right Wall", new Vector2(20.5f, 2f), new Vector2(1f, 13f), GroundColor);
            CreateBlock(level, square, "Platform 1", new Vector2(-6f, -1.8f), new Vector2(4f, 0.5f), PlatformColor);
            CreateBlock(level, square, "Platform 2", new Vector2(0f, -0.3f), new Vector2(3f, 0.5f), PlatformColor);
            CreateBlock(level, square, "Platform 3", new Vector2(6f, -1.8f), new Vector2(4f, 0.5f), PlatformColor);
            CreateBlock(level, square, "Platform 4", new Vector2(12f, -0.3f), new Vector2(3f, 0.5f), PlatformColor);

            Transform spawnRoot = new GameObject("Spawn Points").transform;
            var spawnPoints = new Transform[4];
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                spawnPoints[i] = new GameObject($"Spawn {i + 1}").transform;
                spawnPoints[i].SetParent(spawnRoot);
                spawnPoints[i].position = new Vector3(-2.25f + i * 1.5f, -2.5f, 0f);
            }

            var spawner = new GameObject("Player Spawner").AddComponent<PlayerSpawner>();
            Assign(spawner, "playerPrefab", playerPrefab.GetComponent<NetworkObject>());
            Assign(spawner, "spawnPoints", spawnPoints);

            SaveScene(SceneNames.Game);
        }

        // ---------- Player prefab ----------

        static GameObject CreatePlayerPrefab(Sprite sprite)
        {
            string materialPath = $"{ArtFolder}/Ball.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if (material == null)
            {
                material = new PhysicsMaterial2D("Ball") { friction = 0.8f, bounciness = 0.15f };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            var go = new GameObject("Player");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;

            var body = go.AddComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().sharedMaterial = material;

            go.AddComponent<NetworkObject>();
            var networkTransform = go.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncPositionZ = false;
            networkTransform.SyncRotAngleX = false;
            networkTransform.SyncRotAngleY = false;
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            go.AddComponent<NetworkRigidbody2D>().UseRigidBodyForMotion = true;
            go.AddComponent<BallController>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/Player.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static NetworkPrefabsList RegisterNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);
            }

            if (!list.Contains(prefab))
            {
                list.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssets();
            }
            return list;
        }

        // ---------- Scene helpers ----------

        static void NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static void SaveScene(string name) =>
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath(name));

        static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            return camera;
        }

        static void CreateBlock(Transform parent, Sprite sprite, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            go.AddComponent<BoxCollider2D>();
        }

        // ---------- UI helpers ----------

        static DefaultControls.Resources UiResources => new()
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };

        static void CreateEventSystem() =>
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return go.GetComponent<Canvas>();
        }

        /// <summary>A centered vertical stack that sizes itself to its children.</summary>
        static Transform CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }

        static Text CreateText(Transform parent, string name, string content, int fontSize)
        {
            GameObject go = DefaultControls.CreateText(UiResources);
            go.name = name;
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            SetSize(go, 1000f, fontSize * 1.4f);
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, float width = 480f)
        {
            GameObject go = DefaultControls.CreateButton(UiResources);
            go.name = name;
            go.transform.SetParent(parent, false);

            var text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.fontSize = 36;
            SetSize(go, width, 90f);
            return go.GetComponent<Button>();
        }

        static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            GameObject go = DefaultControls.CreateInputField(UiResources);
            go.name = name;
            go.transform.SetParent(parent, false);

            var input = go.GetComponent<InputField>();
            input.textComponent.fontSize = 44;
            input.textComponent.alignment = TextAnchor.MiddleCenter;
            var placeholderText = (Text)input.placeholder;
            placeholderText.text = placeholder;
            placeholderText.fontSize = 32;
            placeholderText.alignment = TextAnchor.MiddleCenter;
            SetSize(go, 480f, 90f);
            return input;
        }

        static void SetSize(GameObject go, float width, float height)
        {
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        /// <summary>Sets a private [SerializeField] reference (or array of references) on a component.</summary>
        static void Assign(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property.isArray)
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            else
            {
                property.objectReferenceValue = values[0];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Placeholder art ----------

        const int SpriteSize = 128;

        /// <summary>A white circle with a light stripe so you can see it roll.</summary>
        static Color DrawBall(int x, int y)
        {
            float center = (SpriteSize - 1) / 2f;
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            float alpha = Mathf.Clamp01(SpriteSize / 2f - distance);
            float shade = Mathf.Abs(x - center) < SpriteSize * 0.08f ? 0.7f : 1f;
            return new Color(shade, shade, shade, alpha);
        }

        static Sprite CreateSprite(string name, System.Func<int, int, Color> draw)
        {
            string path = $"{ArtFolder}/{name}.png";
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < SpriteSize; y++)
            for (int x = 0; x < SpriteSize; x++)
                texture.SetPixel(x, y, draw(x, y));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpriteSize; // 1 sprite = 1 world unit
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path), Path.GetFileName(path));
        }
    }
}
