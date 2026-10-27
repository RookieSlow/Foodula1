using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class UkPrepMealManagerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false)] [TestCase(true)]
    public void FirstFormalSpeedCommitImmediatelyDisablesPrepMeal(bool validSelection)
    {
        using (var scope = CareerUiPointerTestScope.Create(() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset")))
        {
            var host = new GameObject("PrepMeal formal speed fixture"); host.SetActive(false);
            var config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = true;
            try
            {
                var gm = host.AddComponent<MVPGameManager>(); gm.config = config;
                gm.cardHandUI = host.AddComponent<CardHandUI>();
                gm.cardHandUI.handContainer = new GameObject("Hand", typeof(RectTransform)).transform;
                gm.cardHandUI.handContainer.SetParent(host.transform, false);
                gm.cardHandUI.cardPrefab = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardUI));
                gm.cardHandUI.cardPrefab.transform.SetParent(host.transform, false);
                var race = new RaceSession(new SystemRandomSource(1));
                var p = new PlayerState("UK", false, 0, 1) { teamId = TeamId.UK, techState = new TechTreeState(TeamId.UK) };
                p.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
                var speed = new CardData(CardType.Speed, 2);
                var spare = new CardData(CardType.Speed, 3);
                p.deck.InitializeExactOrder(new[] { speed, spare }, new HeatPool(8));
                p.deck.DrawToHand(2); p.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand);
                race.Players.Add(p); race.BeginPrepMealInput(p, 1);
                typeof(MVPGameManager).GetField("session", Private).SetValue(gm, race);
                typeof(MVPGameManager).GetField("raceTurnNumber", Private).SetValue(gm, 1);
                ((RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(gm)).BeginCardSelection();
                ((RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(gm)).BeginCardSelection();
                HUDUI hud = scope.Root.AddComponent<HUDUI>(); gm.hudUI = hud;
                var slot = new GameObject("DriverSlot", typeof(RectTransform), typeof(Image), typeof(Button));
                slot.transform.SetParent(scope.Root.transform, false);
                slot.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 70);
                hud.driverSkillButton = slot.GetComponent<Button>();
                var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(slot.transform, false); text.GetComponent<TMP_Text>().font = scope.Font;
                hud.SetGameManager(gm); hud.RefreshUkPrepMeal(gm, p);
                Assert.IsTrue(hud.UkPrepMealButton.IsInteractable(), "Before formal commit preparation remains available.");

                CardData[] selected = validSelection ? new[] { speed } : new[] { speed, spare };
                bool committed = (bool)typeof(MVPGameManager).GetMethod("ConfirmPlayerSpeedCards", Private)
                    .Invoke(gm, new object[] { p, selected });

                Assert.AreEqual(validSelection, committed);
                Assert.AreEqual(!validSelection, p.techState.prepMeal.IsOpen);
                Assert.AreEqual(!validSelection, hud.UkPrepMealButton.IsInteractable(), "HUD must reflect the formal commit without another click.");
                Assert.AreEqual(7, p.deck.heatPool.remaining);
                Assert.AreEqual(-1, p.techState.prepMeal.LastUsedLap, "Playing a card never spends the technology use.");
                Assert.IsTrue(p.deck.ContainsInHand(spare));
                if (validSelection)
                {
                    StringAssert.Contains("仅首次正式出牌前可用", hud.UkPrepMealButton.GetComponentInChildren<TMP_Text>().text);
                    gm.OnUkPrepMealButtonClicked();
                    Assert.AreEqual(7, p.deck.heatPool.remaining);
                    Assert.IsTrue(p.deck.ContainsInHand(spare), "A stale action cannot discard the remaining speed.");
                }
                gm.hudUI = null;
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
        }
    }

    [TestCase(false, false)] [TestCase(false, true)]
    [TestCase(true, false)] [TestCase(true, true)]
    public void RealTechnologyButtonCancelOrCommitExactCost(bool commit, bool confirmations)
    {
        using(var scope = CareerUiPointerTestScope.Create(() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset")))
        {
            var host = new GameObject("PrepMeal manager fixture"); host.SetActive(false);
            var config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = true;
            int savedMask=GameSettingsRuntime.Current.inRaceConfirmationMask;
            GameSettingsRuntime.Current.inRaceConfirmationMask=confirmations ? (int)InRaceConfirmationAction.All : 0;
            try
            {
                var gm = host.AddComponent<MVPGameManager>(); gm.config = config;
                var race = new RaceSession();
                var p = new PlayerState("UK",false,0,1) {teamId=TeamId.UK,techState=new TechTreeState(TeamId.UK)};
                p.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
                var cost = new CardData(CardType.Speed,2);
                p.deck.InitializeExactOrder(new[]{cost,new CardData(CardType.Speed,3)},new HeatPool(8));
                p.deck.DrawToHand(2); p.deck.DrawHeatFromPool(1,HeatPaymentDestination.Hand);
                race.Players.Add(p); race.BeginPrepMealInput(p,1);
                typeof(MVPGameManager).GetField("session",Private).SetValue(gm,race);
                typeof(MVPGameManager).GetField("raceTurnNumber",Private).SetValue(gm,1);
                var phase=(RacePhaseState)typeof(MVPGameManager).GetField("phaseState",Private).GetValue(gm);
                var input=(RaceInputState)typeof(MVPGameManager).GetField("inputState",Private).GetValue(gm);
                phase.BeginCardSelection(); input.BeginCardSelection();
                HUDUI hud=scope.Root.AddComponent<HUDUI>(); gm.hudUI=hud;
                var buttonHost=new GameObject("DriverSlot",typeof(RectTransform),typeof(Image),typeof(Button));
                buttonHost.transform.SetParent(scope.Root.transform,false);
                buttonHost.GetComponent<RectTransform>().sizeDelta=new Vector2(220,70);
                hud.driverSkillButton=buttonHost.GetComponent<Button>();
                var text=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
                text.transform.SetParent(buttonHost.transform,false); text.GetComponent<TMP_Text>().font=scope.Font;
                hud.SetGameManager(gm); hud.RefreshUkPrepMeal(gm,p);
                Assert.IsTrue(hud.UkPrepMealButton.IsInteractable());
                Assert.IsFalse(hud.driverSkillButton.gameObject.activeSelf);
                scope.Click(hud.UkPrepMealButton);
                Assert.IsTrue(hud.IsReturnToMenuConfirmationVisible);
                hud.confirmReturnToMenuButton.onClick.AddListener(hud.ConfirmReturnToMenu);
                hud.cancelReturnToMenuButton.onClick.AddListener(hud.CancelReturnToMenu);
                Assert.AreEqual(7,p.deck.heatPool.remaining);
                scope.Click(commit ? hud.confirmReturnToMenuButton : hud.cancelReturnToMenuButton);
                Assert.AreEqual(commit ? 8 : 7,p.deck.heatPool.remaining);
                Assert.AreEqual(commit ? 0 : -1,p.techState.prepMeal.LastUsedLap);
                Assert.AreEqual(!commit,p.deck.ContainsInHand(cost));
                hud.ConfirmReturnToMenu(); Assert.AreEqual(commit ? 8 : 7,p.deck.heatPool.remaining);
                phase.BeginGearSelection(); input.BeginGearSelection(1); hud.RefreshUkPrepMeal(gm,p);
                Assert.IsFalse(hud.UkPrepMealButton.gameObject.activeSelf);
                Assert.IsTrue(hud.driverSkillButton.gameObject.activeSelf);
                gm.hudUI=null;
            }
            finally { GameSettingsRuntime.Current.inRaceConfirmationMask=savedMask; Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
        }
    }
}
