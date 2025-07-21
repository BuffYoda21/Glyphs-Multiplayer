using UnityEngine;
using MelonLoader;
using Il2CppInterop.Runtime.Injection;
using Il2CppSteamworks;
using System.Linq;
using Il2Cpp;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(GlyphsMultiplayer.Main), "Glyphs Multiplayer", "1.4.0", "BuffYoda21")]
[assembly: MelonGame("Vortex Bros.", "GLYPHS")]

namespace GlyphsMultiplayer
{
    public class Main : MelonMod
    {
        [System.Obsolete]
        public override void OnApplicationStart()
        {
            ClassInjector.RegisterTypeInIl2Cpp<MultiplayerManager>();
            ClassInjector.RegisterTypeInIl2Cpp<PlayerDummy>();
            managerObj = new GameObject("Multiplayer Manager");
            managerObj.AddComponent<MultiplayerManager>();
            manager = managerObj.GetComponent<MultiplayerManager>();
            UnityEngine.Object.DontDestroyOnLoad(managerObj);
        }

        public override void OnUpdate()
        {
            if (SceneManager.GetActiveScene().name != "Game" && SceneManager.GetActiveScene().name != "Memory" && SceneManager.GetActiveScene().name != "Outer Void")
                return;
            if (manager.connectedPlayers.Count() > manager.dummies.Count() && dummyParent != null)
                UnityEngine.Object.DestroyImmediate(dummyParent);
            if (dummyParent == null)
            {
                dummyParent = new GameObject("Dummies");
                foreach (CSteamID id in manager.connectedPlayers)
                {
                    manager.dummies.Add(UnityEngine.Object.Instantiate(GameObject.Find("Player"), dummyParent.transform));
                    GameObject dummy = manager.dummies.Last<GameObject>();
                    UnityEngine.Object.DestroyImmediate(dummy.GetComponent<PlayerController>());
                    dummy.AddComponent<PlayerDummy>();
                    dummy.GetComponent<PlayerDummy>().steamID = id;
                    UnityEngine.Object.DestroyImmediate(dummy.GetComponent<Rigidbody2D>());
                    dummy.layer = 3;
                    if (manager.hidePlayerMapPins)
                        dummy.transform.Find("PlayerMapPin").gameObject.SetActive(false);
                }
            }
            List<GameObject> toRemove = new List<GameObject>();
            foreach (GameObject dummy in manager.dummies)
            {
                if (dummy == null)
                    toRemove.Add(dummy);
            }
            foreach (GameObject dummy in toRemove)
            {
                manager.dummies.Remove(dummy);
            }
            foreach (CSteamID id in manager.connectedPlayers)
            {
                bool hasDummy = manager.dummies.Any(d =>
                {
                    var pd = d.GetComponent<PlayerDummy>();
                    return pd != null && pd.steamID == id;
                });
                if (!hasDummy)
                {
                    GameObject dummy = UnityEngine.Object.Instantiate(GameObject.Find("Player"), dummyParent.transform);
                    UnityEngine.Object.DestroyImmediate(dummy.GetComponent<PlayerController>());
                    dummy.AddComponent<PlayerDummy>();
                    dummy.GetComponent<PlayerDummy>().steamID = id;
                    UnityEngine.Object.DestroyImmediate(dummy.GetComponent<Rigidbody2D>());
                    dummy.layer = 3;
                    if (manager.hidePlayerMapPins)
                        dummy.transform.Find("PlayerMapPin").gameObject.SetActive(false);
                    manager.dummies.Add(dummy);
                }
            }
        }

        public static GameObject managerObj;
        public static MultiplayerManager manager;
        public static GameObject dummyParent;
    }
}