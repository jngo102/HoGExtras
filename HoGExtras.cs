using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vasi;
using UObject = UnityEngine.Object;
using USceneMgr = UnityEngine.SceneManagement.SceneManager;

namespace HoGExtras
{
    public class HoGExtras : Mod, ILocalSettings<LocalSettings>
    {
        private static LocalSettings _localSettings = new LocalSettings();
        public static LocalSettings LocalSettings = _localSettings;

        internal static HoGExtras Instance;

        private Dictionary<string, GameObject> _preloads = new();
        public Dictionary<string, GameObject> Preloads => _preloads;

        public override string GetVersion() => "1.0.0.0";

        public override List<ValueTuple<string, string>> GetPreloadNames()
        {
            return new List<ValueTuple<string, string>>
            {
                ("Room_Final_Boss_Core", "Boss Control"),
                ("Dream_Final_Boss", "Boss Control"),
            };
        }

        public HoGExtras() : base("Hall of Gods Extras")
        {
            Instance = this;
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            _preloads.Add("THK", preloadedObjects["Room_Final_Boss_Core"]["Boss Control"]);
            _preloads.Add("Rad", preloadedObjects["Dream_Final_Boss"]["Boss Control"]);

            ModHooks.LanguageGetHook += OnLangGet;
            ModHooks.GetPlayerVariableHook += GetVarHook;
            ModHooks.SetPlayerVariableHook += SetVarHook;
            USceneMgr.activeSceneChanged += OnSceneChange;
        }

        private string OnLangGet(string key, string sheetTitle, string orig)
        {
            switch (key)
            {
                case "NAME_THK":
                    return "The Hollow Knight";
                case "GG_S_THK":
                    return "Corrupted god of nothingness";
                case "NAME_RAD":
                    return "The Radiance";
                case "GG_S_RAD":
                    return "Forgotten god of light";
                default:
                    return Language.Language.GetInternal(key, sheetTitle);
            }
        }


        private object SetVarHook(Type t, string key, object obj)
        {
            switch (key)
            {
                case "statueStateTHK":
                    _localSettings.StatueStateHollowKnight = (BossStatue.Completion)obj;
                    break;
                case "statueStateRad":
                    _localSettings.StatueStateRadiance = (BossStatue.Completion)obj;
                    break;
            }

            return obj;
        }

        private object GetVarHook(Type t, string key, object orig)
        {
            switch (key)
            {
                case "statueStateTHK":
                    return _localSettings.StatueStateHollowKnight;
                case "statueStateRad":
                    return _localSettings.StatueStateRadiance;
                default:
                    return orig;
            }
        }

        private void OnSceneChange(Scene prevScene, Scene nextScene)
        {
            switch (nextScene.name)
            {
                case "GG_Workshop":
                    SetStatue("HollowKnight", "NAME_THK", "GG_S_THK", "statueStateTHK", "statueStateHollowKnight", 2);
                    SetStatue("Radiance", "NAME_RAD", "GG_S_RAD", "statueStateRad", "statueStateRadiance", 0);

                    break;
                case "GG_Hollow_Knight":
                    if (_localSettings.StatueStateHollowKnight.usingAltVersion) break;

                    var bossCtrl = UObject.Instantiate(Preloads["THK"]);
                    bossCtrl.transform.Translate(10, 0, 0);
                    bossCtrl.transform.Find("break_chains").Translate(-10, 0, 0);
                    bossCtrl.transform.Find("Title").Translate(-10, 0, 0);
                    bossCtrl.SetActive(true);

                    PlayMakerFSM battleStart = bossCtrl.LocateMyFSM("Battle Start");
                    battleStart.ChangeTransition("Init", "FINISHED", "Revisit");

                    GameObject thk = bossCtrl.transform.Find("Hollow Knight Boss").gameObject;

                    PlayMakerFSM control = thk.LocateMyFSM("Control");
                    PlayMakerFSM phaseCtrl = thk.LocateMyFSM("Phase Control");

                    var bsc = BossSceneController.Instance;
                    if (bsc.BossLevel >= 1)
                    {
                        thk.GetComponent<HealthManager>().hp = 1450;
                        phaseCtrl.Fsm.GetFsmInt("Phase2 HP").Value = 870;
                        phaseCtrl.Fsm.GetFsmInt("Phase3 HP").Value = 460;
                    }

                    control.GetState("Long Roar End").RemoveAction<PlayerDataBoolTest>();
                    phaseCtrl.GetState("Set Phase 4").RemoveAction<PlayerDataBoolTest>();
                    GameObject bossCorpse = thk.transform.Find("Boss Corpse").gameObject;
                    PlayMakerFSM corpse = bossCorpse.LocateMyFSM("Corpse");
                    corpse.GetState("Burst").RemoveAction<SendEventByName>();
                    corpse.GetState("Blow").AddMethod(() => bsc.EndBossScene());
                    corpse.GetState("Set Knight Focus").RemoveAction<SetFsmBool>();

                    control.SetState("Init");

                    var battleScene = GameObject.Find("Battle Scene");
                    GameObject godseeker = battleScene.transform.Find("Godseeker Crowd").gameObject;
                    godseeker.transform.SetParent(null);
                    FsmGameObject target = godseeker.LocateMyFSM("Control").Fsm.GetFsmGameObject("Target");
                    target.Value = thk;
                    battleStart.GetState("Roar Antic").AddMethod(() => target.Value = HeroController.instance.gameObject);
                    UObject.Destroy(battleScene);

                    break;
                case "GG_Radiance":
                    if (_localSettings.StatueStateRadiance.usingAltVersion) break;

                    bossCtrl = UObject.Instantiate(Preloads["Rad"]);
                    bossCtrl.SetActive(true);

                    Transform rad = bossCtrl.transform.Find("Radiance");
                    control = rad.gameObject.LocateMyFSM("Control");
                    FsmState ballTween = control.GetState("Ball Tween");
                    ballTween.AddCoroutine(RadDeath);
                    control.CreateState("Dummy");
                    ballTween.ChangeTransition("FINISHED", "Dummy");

                    bossCtrl = GameObject.Find("Boss Control");
                    PlayMakerFSM arCtrl = bossCtrl.transform.Find("Absolute Radiance").gameObject.LocateMyFSM("Control");
                    _bossExplode = arCtrl.GetState("Knight Break").GetAction<AudioPlayerOneShotSingle>().audioClip.Value as AudioClip;
                    _finalHitPt3 = arCtrl.GetState("Statue Death 2").GetAction<AudioPlayerOneShotSingle>().audioClip.Value as AudioClip;
                    _rumble = arCtrl.GetState("Statue Death 1").GetAction<AudioPlayerOneShotSingle>().audioClip.Value as AudioClip;
                    godseeker = bossCtrl.transform.Find("Godseeker Crowd").gameObject;
                    godseeker.transform.SetParent(null);
                    UObject.Destroy(bossCtrl);

                    break;
            }
        }

        private AudioClip _bossExplode, _finalHitPt3, _rumble;
        private IEnumerator RadDeath()
        {
            Transform bossCtrl = GameObject.Find("Boss Control(Clone)(Clone)").transform;
            Transform rad = bossCtrl.Find("Radiance");

            Transform knightSplit = rad.Find("Death/Knight Split");
            var splitAnim = knightSplit.GetComponent<tk2dSpriteAnimator>();

            yield return new WaitForSeconds(splitAnim.PlayAnimGetTime("Knight Split Antic"));

            splitAnim.Play("Knight Split");
            knightSplit.gameObject.GetComponent<PlayMakerFSM>().SendEvent("SPLIT");

            Transform knightBall = knightSplit.Find("Knight Ball");
            knightBall.gameObject.SetActive(true);
            knightBall.SetParent(null);
            var ballAnim = knightBall.GetComponent<tk2dSpriteAnimator>();
            ballAnim.Play("Knight Unform");

            Hashtable hashtable = new();
            hashtable.Add("amount", Vector3.down * 20);
            hashtable.Add("speed", 10);
            hashtable.Add("easetype", iTween.EaseType.easeInSine);
            iTween.MoveBy(ballAnim.gameObject, hashtable);

            GameObject actorPrefab = GameManager.instance.transform.Find("GlobalPool/Audio Player Actor 2D(Clone)").gameObject;

            GameObject audioPlayerActor = actorPrefab.Spawn(HeroController.instance.transform.position);
            var audioSource = audioPlayerActor.GetComponent<AudioSource>();
            audioSource.clip = _bossExplode;
            audioSource.Play();

            yield return new WaitForSeconds(3);

            var ps = rad.Find("Death Pt").GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 500;
            ps.Play();
            
            GameCameras.instance.cameraShakeFSM.SendEvent("HugeShake");

            var heroAudio = HeroController.instance.GetComponent<AudioSource>();
            heroAudio.clip = _rumble;
            heroAudio.Play();

            yield return new WaitForSeconds(3);

            audioPlayerActor = actorPrefab.Spawn(HeroController.instance.transform.position);
            audioSource = audioPlayerActor.GetComponent<AudioSource>();
            audioSource.clip = _finalHitPt3;
            audioSource.Play();

            BossSceneController.Instance.EndBossScene();
        }

        private void SetStatue(string statueName, string bossName, string bossDesc, string statueStatePD, string dreamStatueStatePD, float switchOffset)
        {
            GameManager.instance.StartCoroutine(DoSet());

            IEnumerator DoSet()
            {
                var statue = GameObject.Find("GG_Statue_" + statueName);
                var bossStatue = statue.GetComponent<BossStatue>();
                bossStatue.altPlaqueL.gameObject.SetActive(true);
                bossStatue.altPlaqueR.gameObject.SetActive(true);
                bossStatue.regularPlaque.gameObject.SetActive(false);
                BossStatue.BossUIDetails details = new();
                details.nameKey = details.nameSheet = bossName;
                details.descriptionKey = details.descriptionSheet = bossDesc;
                bossStatue.dreamBossDetails = bossStatue.bossDetails;
                bossStatue.bossDetails = details;
                bossStatue.dreamBossScene = bossStatue.bossScene;
                bossStatue.statueStatePD = statueStatePD;
                bossStatue.dreamStatueStatePD = dreamStatueStatePD;
                var dreamSwitch = statue.transform.Find("dream_version_switch").gameObject;
                dreamSwitch.transform.Find("lit_pieces/Base Glow").Translate(-switchOffset, 0, 0);
                dreamSwitch.SetActive(true);
                dreamSwitch.transform.Translate(switchOffset, 0, 0);

                yield return new WaitUntil(() => dreamSwitch.transform.Find("GG_statue_plinth_orb_off").GetComponent<BossStatueDreamToggle>() != null);

                var dreamToggle = dreamSwitch.transform.Find("GG_statue_plinth_orb_off").GetComponent<BossStatueDreamToggle>();
                dreamToggle.SetOwner(bossStatue);
            }
        }

        public void OnLoadLocal(LocalSettings localSettings) => _localSettings = localSettings;
        public LocalSettings OnSaveLocal() => _localSettings;
    }
}