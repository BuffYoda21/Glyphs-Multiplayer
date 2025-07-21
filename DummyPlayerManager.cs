using UnityEngine;
using MelonLoader;
using UnityEngine.SceneManagement;
using Il2CppSteamworks;
using Il2Cpp;

namespace GlyphsMultiplayer
{
    public class PlayerDummy : MonoBehaviour
    {
        public void Start()
        {
            collider = GetComponent<BoxCollider2D>();
            sprite = GetComponent<SpriteRenderer>();
            sm = GameObject.Find("Manager intro").GetComponent<SaveManager>();
            manager = GameObject.Find("Multiplayer Manager").GetComponent<MultiplayerManager>();
        }

        public void Update()
        {
            scene = SceneManager.GetActiveScene().name;
            if (!dashAttackBlades)
            {
                dashAttackBlades = transform.Find("DashAttackBlades").gameObject;
                if (dashAttackBlades != null && manager.PvP)
                {
                    dashAttackBlades.GetComponent<AttackBox>().attackType = "enemy";
                }
            }
        }

        public void UpdatePlayer(Vector3 newPos, string newScene, bool isAttack, Quaternion attack, int atkBns, bool dashAttack, string currentHat, string displayName)
        {
            if (SceneManager.GetActiveScene().name != "Game" && SceneManager.GetActiveScene().name != "Memory" && SceneManager.GetActiveScene().name != "Outer Void")
                return;
            if (!culled)
            {
                pos = newPos;
                scene = newScene;
                isAttacking = isAttack;
                attackRotation = attack;
                attackPwr = 14 + atkBns;
                isDashAttack = dashAttack;
                transform.position = pos;
                if (isAttacking)
                {
                    GameObject attackSlash = Instantiate(Resources.Load<GameObject>("Prefabs/Game/attackArc"), transform.position, Quaternion.identity);
                    attackSlash.GetComponentInChildren<AttackBox>().damage = attackPwr;
                    attackSlash.GetComponentInChildren<AttackBox>().attackType = "enemy";
                    attackSlash.transform.parent = transform;
                    attackSlash.transform.rotation = attack;
                }
                if (dashAttackBlades != null)
                {
                    if (isDashAttack)
                    {
                        dashAttackBlades.SetActive(true);
                    }
                    else
                    {
                        dashAttackBlades.SetActive(false);
                    }
                }
                if (currentHat != "")
                {
                    if (hat != currentHat)
                    {
                        if (equippedHat != null)
                        {
                            Destroy(equippedHat);
                        }
                        equippedHat = Instantiate(Resources.Load<GameObject>("Prefabs/Game/Hats/" + sm.GetString("currentHat")), transform.position, Quaternion.identity);
                        equippedHat.transform.position += new Vector3(0, .5f, 0);
                        equippedHat.transform.parent = gameObject.transform;
                    }
                }
                else if (currentHat == "" && equippedHat != null)
                {
                    Destroy(equippedHat);
                }
                hat = currentHat;
                if (nametag == null)
                {
                    MelonLogger.Msg("Creating nametag for " + displayName);
                    nametag = new GameObject("Nametag");
                    nametag.transform.SetParent(transform);
                    nametag.transform.localPosition = new Vector3(0, 1.5f, 0);

                    BuildText bt = nametag.AddComponent<BuildText>();
                    bt.text = displayName;
                    if (bt.text == "")
                        bt.text = "George Appreciator";
                    bt.textsize = .25f;
                    //bt.col = Color.white;
                    bt.col = new Color(255f, 0f, 163f, 255f);
                    bt.order = 999;
                    bt.normaltext = true;
                    bt.center = true;
                }
            }
            if (SceneManager.GetActiveScene().name == scene)
                UncullSelf();
            else
                CullSelf();
        }

        public void CullSelf()
        {
            sprite.enabled = false;
            collider.enabled = false;
            culled = true;
        }

        public void UncullSelf()
        {
            sprite.enabled = true;
            collider.enabled = true;
            transform.position = pos;
            culled = false;
        }

        public int attackPwr;
        public SaveManager sm;
        public MultiplayerManager manager;
        private bool culled = true;
        public CSteamID steamID;
        public Vector3 pos;
        public bool isAttacking;
        public bool isDashAttack;
        public Quaternion attackRotation;
        public string scene;
        public string hat;
        public GameObject equippedHat;
        public GameObject dashAttackBlades;
        public GameObject nametag = null;
        public string displayName = "George Appreciator";

        public BoxCollider2D collider;
        public SpriteRenderer sprite;
    }
}