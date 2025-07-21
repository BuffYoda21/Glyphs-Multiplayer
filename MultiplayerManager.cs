using UnityEngine;
using MelonLoader;
using Il2CppSteamworks;
using Il2Cpp;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;

namespace GlyphsMultiplayer
{
    public class MultiplayerManager : MonoBehaviour
    {
        public void Awake()
        {
            sessionRequestCallback = Callback<P2PSessionRequest_t>.Create((Callback<P2PSessionRequest_t>.DispatchDelegate)OnP2PSessionRequest);
        }

        public void Start()
        {
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
            if (!File.Exists(settingsPath))
            {
                var defaultObj = new
                {
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

            try
            {
                string json = File.ReadAllText(settingsPath);
                var root = Newtonsoft.Json.Linq.JObject.Parse(json);

                displayName = root["displayName"] != null ? (string)root["displayName"] : defaultDisplayName;
                PvP = root["PvP"] != null ? (bool)root["PvP"] : defaultPvP;
                collision = root["collision"] != null ? (bool)root["collision"] : defaultCollision;
                hidePlayerMapPins = root["hidePlayerMapPins"] != null ? (bool)root["hidePlayerMapPins"] : defaultMapPins;

                targetIDs.Clear();
                if (root["playersToConnectTo"] != null && root["playersToConnectTo"].Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    foreach (var idElem in root["playersToConnectTo"])
                    {
                        if (ulong.TryParse(idElem.ToString(), out ulong id))
                            targetIDs.Add(id);
                    }
                }
                MelonLogger.Msg($"Loaded settings.json from {settingsPath}");
            }
            catch (Exception ex)
            {
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

        public void Update()
        {
            if (player == null && (SceneManager.GetActiveScene().name == "Game" || SceneManager.GetActiveScene().name == "Memory" || SceneManager.GetActiveScene().name == "Outer Void"))
            {
                player = GameObject.Find("Player");
                if (player == null)
                    return;
            }
            if (!steamInitialized && SteamManager.Initialized)
            {
                steamID = SteamUser.GetSteamID();
                MelonLogger.Msg($"Your Steam ID: {steamID}");
                steamInitialized = true;
            }
            else if (steamInitialized)
            {
                foreach (var targetID in targetIDs)
                {
                    if (!connectedPlayers.Contains(new CSteamID(targetID)))
                    {
                        if (packetDelay <= 0)
                        {
                            ConnectToPlayer(targetID);
                            packetDelay = 10;
                        }
                        else
                        {
                            packetDelay--;
                        }
                    }
                }
            }
            CheckForPackets();
            if (steamInitialized && connectedPlayers.Count > 0)
            {
                KeyBindManager km = FindFirstObjectByType<KeyBindManager>();
                Vector3 myPos = player.transform.position;
                string myScene = SceneManager.GetActiveScene().name;
                Quaternion attack = Quaternion.identity;
                GameObject attackArc = GameObject.Find("Player/attackArc(Clone)");
                if (km)
                {
                    if (km.InputGetKeyDown("attack"))
                    {
                        isAttacking = true;
                        attack = attackArc.transform.localRotation;
                    }
                    else
                    {
                        isAttacking = false;
                    }
                }
                else
                {
                    isAttacking = false;
                }
                isDashAttack = player.transform.Find("DashAttackBlades").gameObject.activeSelf;
                hat = sm.GetString("currentHat");
                BroadcastPlayerUpdate(myPos, myScene, isAttacking, attack, (byte)player.GetComponent<PlayerController>().attackBonus, isDashAttack, hat);
            }
        }

        public void BroadcastPlayerUpdate(Vector3 position, string sceneName, bool isAttack, Quaternion attack, int atkBns, bool dashAttack, string currentHat)
        {
            byte[] packet = CreatePlayerUpdatePacket(steamID, position, sceneName, isAttack, attack, atkBns, dashAttack, currentHat, displayName);
            foreach (var player in connectedPlayers)
            {
                SteamNetworking.SendP2PPacket(player, packet, (uint)packet.Length, EP2PSend.k_EP2PSendUnreliable);
            }
        }

        public void ConnectToPlayer(ulong targetSteamId)
        {
            CSteamID targetId = new CSteamID(targetSteamId);
            byte[] data = System.Text.Encoding.UTF8.GetBytes($"Connected to {steamID}");
            outgoing = SteamNetworking.SendP2PPacket(targetId, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
            /*
            if (outgoing)
                MelonLogger.Msg($"Sent connection attempt to {targetId}.");
            else
                MelonLogger.Error($"Failed to send packet to {targetId}.");
            */
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t request)
        {
            SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
            MelonLogger.Msg($"Accepted P2P session with {request.m_steamIDRemote}");
        }

        public bool SendPacket(ulong recipiant, String msg)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(msg);
            return SteamNetworking.SendP2PPacket(new CSteamID(recipiant), data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
        }

        public void CheckForPackets()
        {
            uint msgSize;
            if (SteamNetworking.IsP2PPacketAvailable(out msgSize))
            {
                var buffer = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>((long)msgSize);
                CSteamID remoteId;
                uint bytesRead;
                while (SteamNetworking.ReadP2PPacket(buffer, msgSize, out bytesRead, out remoteId))
                {
                    byte[] recvBuffer = new byte[bytesRead];
                    for (int i = 0; i < bytesRead; i++)
                        recvBuffer[i] = buffer[i];

                    string message = System.Text.Encoding.UTF8.GetString(recvBuffer, 0, (int)bytesRead);
                    //MelonLogger.Msg($"{remoteId}: {message}");        //for debug use only
                    if (message.StartsWith("Connected to "))
                    {
                        if (!connectedPlayers.Contains(remoteId))
                            connectedPlayers.Add(remoteId);

                        if (remoteId.m_SteamID != steamID.m_SteamID)
                        {
                            SendPacket((ulong)remoteId.m_SteamID, $"Connection confirmed with {steamID}");
                        }
                    }
                    else if (message.StartsWith("Connection confirmed with "))
                    {
                        if (!connectedPlayers.Contains(remoteId))
                            connectedPlayers.Add(remoteId);
                    }
                    else
                    {
                        try
                        {
                            ulong senderId;
                            Vector3 pos;
                            string scene;
                            bool isAttack;
                            Quaternion attack;
                            int atkBns;
                            bool isDashAttack;
                            string currentHat;
                            string dummyName;
                            ParsePlayerUpdatePacket(recvBuffer, out senderId, out pos, out scene, out isAttack, out attack, out atkBns, out isDashAttack, out currentHat, out dummyName);
                            GameObject dummy = GetPlayerDummy(senderId);
                            if (dummy != null)
                                dummy.GetComponent<PlayerDummy>().UpdatePlayer(pos, scene, isAttack, attack, atkBns, isDashAttack, currentHat, dummyName);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Failed to parse player update packet: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static byte[] CreatePlayerUpdatePacket(CSteamID senderId, Vector3 position, string sceneName, bool isAttack, Quaternion attack, int atkBns, bool isDashAttack, string currentHat, string displayName)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(senderId.m_SteamID);
                writer.Write(position.x);
                writer.Write(position.y);
                writer.Write(position.z);

                byte[] sceneBytes = Encoding.UTF8.GetBytes(sceneName);
                writer.Write(sceneBytes.Length);
                writer.Write(sceneBytes);

                writer.Write(isAttack);
                if (isAttack)
                {
                    writer.Write(attack.x);
                    writer.Write(attack.y);
                    writer.Write(attack.z);
                    writer.Write(attack.w);
                }

                writer.Write(atkBns);

                writer.Write(isDashAttack);

                byte[] hatBytes = Encoding.UTF8.GetBytes(currentHat);
                writer.Write(hatBytes.Length);
                writer.Write(hatBytes);

                byte[] nameBytes = Encoding.UTF8.GetBytes(displayName);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);

                return ms.ToArray();
            }
        }

        public static void ParsePlayerUpdatePacket(byte[] data, out ulong senderSteamId, out Vector3 position, out string sceneName, out bool isAttack, out Quaternion attack, out int atkBns, out bool isDashAttack, out string currentHat, out string dummyName)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                senderSteamId = reader.ReadUInt64();

                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                position = new Vector3(x, y, z);

                int sceneLen = reader.ReadInt32();
                string scene = Encoding.UTF8.GetString(reader.ReadBytes(sceneLen));
                sceneName = scene;

                isAttack = reader.ReadBoolean();
                if (isAttack)
                {
                    float ax = reader.ReadSingle();
                    float ay = reader.ReadSingle();
                    float az = reader.ReadSingle();
                    float aw = reader.ReadSingle();
                    attack = new Quaternion(ax, ay, az, aw);
                }
                else
                {
                    attack = Quaternion.identity;
                }

                atkBns = reader.ReadByte();

                isDashAttack = reader.ReadBoolean();

                int hatLen = reader.ReadInt32();
                if (hatLen > 0)
                    currentHat = Encoding.UTF8.GetString(reader.ReadBytes(hatLen));
                else
                    currentHat = "";

                int nameLen = reader.ReadInt32();
                if (nameLen > 0)
                    dummyName = Encoding.UTF8.GetString(reader.ReadBytes(nameLen));
                else
                    dummyName = "George Appreciator";
            }
        }

        public GameObject GetPlayerDummy(ulong id)
        {
            foreach (GameObject dummy in dummies)
            {
                if (dummy.GetComponent<PlayerDummy>().steamID == new CSteamID(id))
                    return dummy;
            }
            return null;
        }

        public SaveManager sm;
        private Callback<P2PSessionRequest_t> sessionRequestCallback;
        public GameObject player;
        public bool steamInitialized = false;
        private bool outgoing = false;
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