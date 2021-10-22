using HutongGames.PlayMaker.Actions;
using Modding;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;
using USceneMgr = UnityEngine.SceneManagement.SceneManager;
using Vasi;
using HutongGames.PlayMaker;
using System.Collections;

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
                new ValueTuple<string, string>("Room_Final_Boss_Core", "Boss Control"),
                new ValueTuple<string, string>("Dream_Final_Boss", "Boss Control"),
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

                    FsmUtil.RemoveAction<PlayerDataBoolTest>(control.GetState("Long Roar End"));
                    FsmUtil.RemoveAction<PlayerDataBoolTest>(phaseCtrl.GetState("Set Phase 4"));
                    GameObject bossCorpse = thk.transform.Find("Boss Corpse").gameObject;
                    PlayMakerFSM corpse = bossCorpse.LocateMyFSM("Corpse");
                    FsmUtil.RemoveAction<SendEventByName>(corpse.GetState("Burst"));
                    FsmUtil.AddMethod(corpse.GetState("Blow"), () =>
                    {
                        GameCameras.instance.StopCameraShake();
                        bsc.EndBossScene();
                    });

                    control.SetState("Init");

                    var battleScene = GameObject.Find("Battle Scene");
                    GameObject godseeker = battleScene.transform.Find("Godseeker Crowd").gameObject;
                    godseeker.transform.SetParent(null);
                    var target = godseeker.LocateMyFSM("Control").Fsm.GetFsmGameObject("Target");
                    target.Value = thk;
                    FsmUtil.AddMethod(battleStart.GetState("Roar Antic"), () => target.Value = HeroController.instance.gameObject);

                    UObject.Destroy(battleScene);

                    break;
                case "GG_Radiance":
                    if (_localSettings.StatueStateRadiance.usingAltVersion) break;

                    bossCtrl = UObject.Instantiate(Preloads["Rad"]);
                    bossCtrl.SetActive(true);
                    control = bossCtrl.transform.Find("Radiance").gameObject.LocateMyFSM("Control");
                    FsmState tendrils2 = control.GetState("Tendrils 2");
                    FsmUtil.RemoveTransition(tendrils2, "FINISHED");
                    FsmUtil.AddMethod(tendrils2, () => BossSceneController.Instance.EndBossScene());

                    bossCtrl = GameObject.Find("Boss Control");
                    godseeker = bossCtrl.transform.Find("Godseeker Crowd").gameObject;
                    godseeker.transform.SetParent(null);
                    UObject.Destroy(bossCtrl);

                    break;
            }
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