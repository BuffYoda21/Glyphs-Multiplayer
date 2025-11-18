using UnityEngine;
using MelonLoader;
using Il2CppSteamworks;
using Il2Cpp;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;

namespace GlyphsMultiplayer {
    public class MultiplayerManager : MonoBehaviour {
        public void Awake() {
            sessionRequestCallback = Callback<P2PSessionRequest_t>.Create((Callback<P2PSessionRequest_t>.DispatchDelegate)OnP2PSessionRequest);
        }

        public void Start() {
            sm = GameObject.Find("Manager intro").GetComponent<SaveManager>();

            string userDataDir = Path.Combine(Environment.CurrentDirectory, "UserData");
            string settingsPath = Path.Combine(userDataDir, "settings.json");

            if (!Directory.Exists(userDataDir))
                Directory.CreateDirectory(userDataDir);

            // Default values
            string defaultDisplayName = "George Appreciator";
            bool defaultPvP = true;
            bool defaultCollision = true;
            bool defaultMapPins = false;
            List<ulong> defaultTargetIDs = new List<ulong>();

            // If no settings.json found create it
            if (!File.Exists(settingsPath)) {
                var defaultObj = new {
                    displayName = defaultDisplayName,
                    PvP = defaultPvP,
                    collision = defaultCollision,
                    hidePlayerMapPins = defaultMapPins,
                    playersToConnectTo = defaultTargetIDs
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(defaultObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(settingsPath, json);
                MelonLogger.Msg($"Created default settings.json at {settingsPath}");
            }

            try {
                string json = File.ReadAllText(settingsPath);
                var root = Newtonsoft.Json.Linq.JObject.Parse(json);

                displayName = root["displayName"] != null ? (string)root["displayName"] : defaultDisplayName;
                PvP = root["PvP"] != null ? (bool)root["PvP"] : defaultPvP;
                collision = root["collision"] != null ? (bool)root["collision"] : defaultCollision;
                hidePlayerMapPins = root["hidePlayerMapPins"] != null ? (bool)root["hidePlayerMapPins"] : defaultMapPins;

                targetIDs.Clear();
                if (root["playersToConnectTo"] != null && root["playersToConnectTo"].Type == Newtonsoft.Json.Linq.JTokenType.Array) {
                    foreach (var idElem in root["playersToConnectTo"]) {
                        if (ulong.TryParse(idElem.ToString(), out ulong id))
                            targetIDs.Add(id);
                    }
                }
                MelonLogger.Msg($"Loaded settings.json from {settingsPath}");
            } catch (Exception ex) {
                MelonLogger.Error($"Failed to read settings.json: {ex.Message}");
                // Fallback to defaults
                displayName = defaultDisplayName;
                PvP = defaultPvP;
                collision = defaultCollision;
                hidePlayerMapPins = defaultMapPins;
                targetIDs = new List<ulong>();
            }

            MelonLogger.Msg($"Display Name: {displayName}, PvP: {PvP}, Collision: {collision}, Map Pins: {hidePlayerMapPins}, Players: {string.Join(", ", targetIDs)}");
        }

        public void Update() {
            if (player && (SceneManager.GetActiveScene().name == "Game" || SceneManager.GetActiveScene().name == "Memory" || SceneManager.GetActiveScene().name == "Outer Void"))
                player = GameObject.Find("Player");
            else
                return;
            if (!steamInitialized && SteamManager.Initialized) {
                steamID = SteamUser.GetSteamID();
                MelonLogger.Msg($"Your Steam ID: {steamID}");
                steamInitialized = true;
            } else if (steamInitialized) {
                if (Time.time - lastRequestTime > 1f) {
                    foreach (var targetID in targetIDs) {
                        if (!connectedPlayers.Contains(new CSteamID(targetID)))
                            ConnectToPlayer(targetID);
                    }
                    lastRequestTime = Time.time;
                }
            }
            CheckForPackets();
            if (steamInitialized && connectedPlayers.Count > 0) {
                KeyBindManager km = FindFirstObjectByType<KeyBindManager>();
                Quaternion attack = Quaternion.identity;
                GameObject attackArc = GameObject.Find("Player/attackArc(Clone)");
                if (km) {
                    if (km.InputGetKeyDown("attack")) {
                        isAttacking = true;
                        attack = attackArc.transform.localRotation;
                    } else {
                        isAttacking = false;
                    }
                } else {
                    isAttacking = false;
                }
                isDashAttack = player.transform.Find("DashAttackBlades").gameObject.activeSelf;
                hat = sm.GetString("currentHat");
                BroadcastUnreliable(CreateLowPriorityPacket(steamID, player.transform.position, SceneManager.GetActiveScene().name, hat, displayName));
                if (isAttacking || isDashAttack)
                    BroadcastReliable(CreateHighPriorityPacket(steamID, isAttacking, attack, player.GetComponent<PlayerController>().attackBonus, isDashAttack));
            }
        }

        public void BroadcastUnreliable(byte[] packet) {
            foreach (var player in connectedPlayers) {
                SteamNetworking.SendP2PPacket(player, packet, (uint)packet.Length, EP2PSend.k_EP2PSendUnreliable);
            }
        }

        public void BroadcastReliable(byte[] packet) {
            foreach (var player in connectedPlayers) {
                SteamNetworking.SendP2PPacket(player, packet, (uint)packet.Length, EP2PSend.k_EP2PSendReliable);
            }
        }

        public void ConnectToPlayer(ulong targetSteamId) {
            CSteamID targetId = new CSteamID(targetSteamId);
            byte[] data = System.Text.Encoding.UTF8.GetBytes($"Client v1.6 Connected to {steamID}");
            SteamNetworking.SendP2PPacket(targetId, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
        }

        // Always accept the session with remote player without checks so that it can send a response wether or not to continue communication.
        private void OnP2PSessionRequest(P2PSessionRequest_t request) {
            SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
            MelonLogger.Msg($"Accepted P2P session with {request.m_steamIDRemote}");
        }

        public void CheckForPackets() {
            uint msgSize;
            if (SteamNetworking.IsP2PPacketAvailable(out msgSize)) {
                var buffer = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>((long)msgSize);
                CSteamID remoteId;
                uint bytesRead;
                while (SteamNetworking.ReadP2PPacket(buffer, msgSize, out bytesRead, out remoteId)) {
                    byte[] recvBuffer = new byte[bytesRead];
                    for (int i = 0; i < bytesRead; i++)
                        recvBuffer[i] = buffer[i];

                    string message = System.Text.Encoding.UTF8.GetString(recvBuffer, 0, (int)bytesRead);
                    //MelonLogger.Msg($"{remoteId}: {message}");        //for debug use only
                    if (message.StartsWith("Client v1.6 Connected to ")) {
                        if (!connectedPlayers.Contains(remoteId))
                            connectedPlayers.Add(remoteId);

                        if (remoteId.m_SteamID != steamID.m_SteamID) {
                            byte[] data = System.Text.Encoding.UTF8.GetBytes($"Client v1.6 Connection confirmed with {steamID}");
                            SteamNetworking.SendP2PPacket(remoteId, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
                        }
                    } else if (message.StartsWith("Client v1.6 Connection confirmed with ")) {
                        if (!connectedPlayers.Contains(remoteId))
                            connectedPlayers.Add(remoteId);
                    } else {
                        try {
                            ulong senderId;
                            bool priority;
                            Vector3 pos;
                            string scene;
                            bool isAttack;
                            Quaternion attack;
                            float atkBns;
                            bool isDashAttack;
                            string currentHat;
                            string dummyName;
                            ParsePlayerUpdatePacket(recvBuffer, out priority, out senderId, out pos, out currentHat, out scene, out dummyName, out isAttack, out attack, out atkBns, out isDashAttack);
                            GameObject dummy = GetPlayerDummy(senderId);
                            if (dummy != null)
                                if (priority)
                                    dummy.GetComponent<PlayerDummy>().UpdatePlayer(isAttack, attack, atkBns, isDashAttack);
                                else
                                    dummy.GetComponent<PlayerDummy>().UpdatePlayer(pos, currentHat, scene, dummyName);
                        } catch (Exception ex) {
                            MelonLogger.Error($"Failed to parse player update packet: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static byte[] CreateLowPriorityPacket(CSteamID senderId, Vector3 position, string sceneName, string currentHat, string displayName) {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(senderId.m_SteamID);
                writer.Write(false);

                writer.Write(position.x);
                writer.Write(position.y);

                writer.Write(ParseHatName(currentHat));

                writer.Write(ParseSceneName(sceneName));

                byte[] nameBytes = Encoding.UTF8.GetBytes(displayName);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);

                return ms.ToArray();
            }
        }

        public static byte[] CreateHighPriorityPacket(CSteamID senderId, bool isAttack, Quaternion attack, float atkBns, bool isDashAttack) {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms)) {
                writer.Write(senderId.m_SteamID);
                writer.Write(true);

                writer.Write(isAttack);
                if (isAttack)
                    writer.Write(attack.z);
                writer.Write(atkBns);

                writer.Write(isDashAttack);

                return ms.ToArray();
            }
        }

        public static void ParsePlayerUpdatePacket(byte[] data, out bool priority, out ulong senderSteamId, out Vector3 position, out string currentHat, out string sceneName, out string dummyName, out bool isAttack, out Quaternion attack, out float atkBns, out bool isDashAttack) {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms)) {
                senderSteamId = reader.ReadUInt64();
                priority = reader.ReadBoolean();

                position = Vector3.zero;
                currentHat = "";
                sceneName = "";
                dummyName = "";
                isAttack = false;
                attack = Quaternion.identity;
                atkBns = 0f;
                isDashAttack = false;

                if (priority)
                    ParsePriorityPacket(reader, out isAttack, out attack, out atkBns, out isDashAttack);
                else
                    ParseGeneralPacket(reader, out position, out currentHat, out sceneName, out dummyName);
            }
        }

        private static void ParsePriorityPacket(BinaryReader reader, out bool isAttack, out Quaternion attack, out float atkBns, out bool isDashAttack) {
            isAttack = reader.ReadBoolean();
            if (isAttack) {
                float angle = reader.ReadSingle();
                attack = new Quaternion(0f, 0f, angle, 0f);
            } else {
                attack = Quaternion.identity;
            }
            atkBns = reader.ReadSingle();
            isDashAttack = reader.ReadBoolean();
        }

        private static void ParseGeneralPacket(BinaryReader reader, out Vector3 position, out string currentHat, out string sceneName, out string dummyName) {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            position = new Vector3(x, y, 0f);

            currentHat = GetHatName(reader.ReadByte());

            sceneName = GetSceneName(reader.ReadByte());

            int nameLen = reader.ReadInt32();
            if (nameLen > 0)
                dummyName = Encoding.UTF8.GetString(reader.ReadBytes(nameLen));
            else
                dummyName = "George Appreciator";
        }

        private static byte ParseSceneName(string sceneName) {
            switch (sceneName) {
                case "Game":
                    return 1;
                case "Memory":
                    return 2;
                case "Outer Void":
                    return 3;
                default:
                    return 0;
            }
        }

        private static string GetSceneName(byte sceneId) {
            switch (sceneId) {
                case 1:
                    return "Game";
                case 2:
                    return "Memory";
                case 3:
                    return "Outer Void";
                default:
                    return "Title/Cutscene";
            }
        }

        // TODO: Check game for actual hat names
        private static byte ParseHatName(string hatName) {
            switch (hatName) {
                case "Bow":
                    return 1;
                case "Propeller":
                    return 2;
                case "Traffic":
                    return 3;
                case "John":
                    return 4;
                case "Top":
                    return 5;
                case "Fez":
                    return 6;
                case "Party":
                    return 7;
                case "Bomb":
                    return 8;
                case "Crown":
                    return 9;
                case "Chicken":
                    return 10;
                default:
                    return 0;
            }
        }

        private static string GetHatName(byte hatId) {
            switch (hatId) {
                case 1:
                    return "Bow";
                case 2:
                    return "Propeller";
                case 3:
                    return "Traffic";
                case 4:
                    return "John";
                case 5:
                    return "Top";
                case 6:
                    return "Fez";
                case 7:
                    return "Party";
                case 8:
                    return "Bomb";
                case 9:
                    return "Crown";
                case 10:
                    return "Chicken";
                default:
                    return "None";
            }
        }

        public GameObject GetPlayerDummy(ulong id) {
            foreach (GameObject dummy in dummies) {
                if (dummy.GetComponent<PlayerDummy>().steamID == new CSteamID(id))
                    return dummy;
            }
            return null;
        }

        public SaveManager sm;
        private Callback<P2PSessionRequest_t> sessionRequestCallback;
        public GameObject player;
        public bool steamInitialized = false;
        private float lastRequestTime = 0f;
        public List<GameObject> dummies = new List<GameObject>();
        public List<CSteamID> connectedPlayers = new List<CSteamID>();
        public CSteamID steamID = new CSteamID();
        public List<ulong> targetIDs = new List<ulong>();
        public int packetDelay = 0;
        public bool isAttacking = false;
        public bool isDashAttack = false;
        public string hat = "";
        public Quaternion attack = Quaternion.identity;
        public string displayName = "";
        public bool PvP = true;
        public bool collision = true;
        public bool hidePlayerMapPins = false;
    }
}