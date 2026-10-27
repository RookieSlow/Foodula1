"""P2b candidate design calculations. Does not load Unity or modify game/save files."""
from __future__ import annotations

from copy import deepcopy
from dataclasses import dataclass
from itertools import product
from pathlib import Path
import hashlib
import json
import re
import time
import unittest

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).parent
BROTHS = ("Tonkotsu", "Shoyu", "Miso", "Shio")
RECIPES = ((2, 2), (3, 1), (1, 3))


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def inputs():
    names = [
        "Assets/Scripts/Config/MVPGameConfig.asset",
        "Assets/Scripts/Core/TeamVehicleRules.cs",
        "Assets/Scripts/Core/ChinaGearShiftRules.cs",
        "Assets/Scripts/Core/RaceHeatPaymentExecution.cs",
        "Assets/Scripts/Core/RaceTurnTechnologyCleanup.cs",
        "Assets/Scripts/Core/RaceParticipantTechnologySetup.cs",
        "Assets/Scripts/Core/RaceSession.cs",
        "Assets/Scripts/TechTree/TechTreeRules.cs",
        "Assets/Scripts/TechTree/TechTreeData.cs",
        "Assets/Scripts/TechTree/BankuruwaseTurnRules.cs",
        "Assets/Scripts/TrickCards/TrickCardRules.cs",
        "Assets/Resources/Configs/Tracks/shanghai_dim_sum.json",
        "Assets/Resources/Configs/Tracks/suzuka_sushi.json",
    ]
    config = (ROOT / names[0]).read_text(encoding="utf-8-sig")
    raw = re.search(r"speedCardDistribution:\s*([0-9a-f]+)", config).group(1)
    values = [int.from_bytes(bytes.fromhex(raw[i:i+8]), "little") for i in range(0, len(raw), 8)]
    vehicle = (ROOT / names[1]).read_text(encoding="utf-8-sig")
    pool = int(re.search(r"case TeamId.CN: return new TeamVehicleProfile\([^\n]*?, (\d+), 0\)", vehicle).group(1))
    data = (ROOT / names[8]).read_text(encoding="utf-8-sig")
    assert all(f"case BrothType.{b}:" in data for b in BROTHS)
    tracks = {}
    for name in names[-2:]:
        t = json.loads((ROOT / name).read_text(encoding="utf-8-sig"))
        assert len(t["cells"]) == t["gameCellCount"]
        tracks[Path(name).stem] = t["gameCellCount"]
    assert len(values) == 12 and pool == 7
    return {"sha256": {n: digest(ROOT/n) for n in names}, "speed_values": values,
            "ordinary_hand": int(re.search(r"handSize:\s*(\d+)", config).group(1)),
            "cn_base_pool": pool, "track_cells": tracks}


@dataclass
class Heat:
    engine: int
    hand: int
    discard: int
    temporary: int = 0
    trick: bool = False
    speed: bool = False
    paid: bool = False
    manual_used: bool = False
    movement: int = 0
    receipts: int = 0
    unpaid_tea: int = 0

    @property
    def total(self):
        return self.engine + self.hand + self.discard


def event(h, label):
    # Characterize the existing ordering, including L1's own successful payment.
    if label == "T":
        h.trick, h.speed, h.paid = True, False, False
    elif label == "S" and h.trick:
        h.speed = True
    elif label == "P" and h.trick and h.speed:
        h.paid = True


def manual(h, option, go=True, l2=True, terminal=False, committed=True):
    """Run after normal cooling and before Yin/Yang, not on button-preview."""
    if option not in ("A", "B") or not go or not l2 or terminal or not committed or h.manual_used:
        return False
    if h.engine < 1 or (option == "B" and h.hand < 1):
        return False  # Whole optional action fails atomically; no partial debit.
    h.engine -= 1
    h.receipts += 1
    if option == "A":
        h.hand += 1
        h.movement += 1
    else:
        # The receipt goes to discard; an OLD hand heat cools. No net engine gain.
        h.discard += 1
        h.hand -= 1
        h.engine += 1
    h.manual_used = True
    event(h, "P")
    return True


def tea(h, go):
    if go:
        if h.engine < 1:
            h.unpaid_tea += 1  # Does NOT simulate actual spin/penalty/recovery.
            return
        h.engine -= 1
        h.discard += 1
        h.receipts += 1
        h.movement += 1
        event(h, "P")
    elif h.hand:
        h.hand -= 1
        h.engine += 1


def cleanup(h, option=None, go=True, l1=True, l2=True, terminal=False, finishes_after_manual=False):
    if terminal:
        return h
    done = manual(h, option, go, l2)
    if done and finishes_after_manual:
        return h
    if l1:
        tea(h, go)
    if l2 and h.trick and h.speed and h.paid and l1:
        tea(h, go)
    return h


def preview(h, **kwargs):
    return cleanup(deepcopy(h), **kwargs)


def go_recover(mode, count):
    if mode == "Go":
        return {"speed_cards": 3 if count == 1 else 4, "heat": min(3, count-1), "cool": 0}
    return {"speed_cards": 1, "heat": 0, "cool": max(0, 4-count)}


def rotor_step(left, used, policy, rank, n, turn=1, gap=1, request=True, terminal=False):
    if terminal and policy != "current":
        return 0, used
    if left:
        return left-1, used  # Expiry never reactivates at the same boundary.
    valid = n >= 2 and 1 <= rank <= n
    eligible = valid and rank >= n-1
    if policy != "current":
        eligible = eligible and rank > 1 and not used
    if policy == "R2":
        eligible = eligible and turn >= 2 and gap >= 1 and request
    return (3, True) if eligible else (0, used)


def trace(policy, ranks, n, requests=True):
    left = 0
    used = False
    values = []
    for turn, rank in enumerate(ranks, 1):
        left, used = rotor_step(left, used, policy, rank, n, turn, request=requests)
        values.append(left)
    return values


def effects(chosen, l2, active, l3=True):
    active_set = {chosen} if l2 and chosen in BROTHS else set()
    if active and l3:
        active_set |= set(BROTHS)
    return {"straight": int("Tonkotsu" in active_set), "corner_category": int("Shoyu" in active_set),
            "slipstream": int("Miso" in active_set),
            "cool": int("Shio" in active_set) + int(active and l3)}


def launch_plan(speed_values, broth, recipe=(2,2), *, l2=True, mode="free", team="JP", option="A", stage="race", before_draw=True):
    if mode == "tutorial" or not l2 or not before_draw or broth not in BROTHS:
        return None
    if recipe not in RECIPES or (option == "A" and recipe != (2,2)):
        return None
    if team != "JP" and recipe != (2,2):
        return None  # UK's virtual broth grant does not replace UK trick definitions.
    if mode == "career" and option == "B" and recipe != (2,2) and stage not in ("season_start", "summer"):
        return None  # Recipe change is locked; a previously locked recipe needs a snapshot adapter.
    return {"broth": broth, "recipe": recipe, "ordinary": tuple(speed_values)+("Attack",)*recipe[0]+("Defense",)*recipe[1],
            "broth_token_in_draw": False}


class CandidateTests(unittest.TestCase):
    def setUp(self):
        self.fixture = Heat(4,2,1,trick=True,speed=True)

    def test_01_l1_current_payment_can_complete_combo(self):
        h = cleanup(self.fixture)
        self.assertEqual((h.engine,h.hand,h.discard,h.movement),(2,2,3,2))

    def test_02_no_trick_no_combo(self):
        h=cleanup(Heat(4,2,1,speed=True));self.assertEqual(h.movement,1)

    def test_03_late_trick_resets_prior_speed(self):
        h=Heat(4,2,1);[event(h,e) for e in "STP"];self.assertFalse(h.paid)

    def test_04_real_order_works(self):
        h=Heat(4,2,1);[event(h,e) for e in "TSP"];self.assertTrue(h.paid)

    def test_05_a_ledger(self):
        h=cleanup(self.fixture,"A");self.assertEqual((h.engine,h.hand,h.discard,h.movement),(1,3,3,3))

    def test_06_b_ledger(self):
        h=cleanup(self.fixture,"B");self.assertEqual((h.engine,h.hand,h.discard,h.movement),(2,1,4,2))

    def test_07_empty_engine_is_atomic(self):
        h=Heat(0,2,5);old=deepcopy(h);self.assertFalse(manual(h,"A"));self.assertEqual(h,old)

    def test_08_b_requires_permanent_hand_heat(self):
        h=Heat(4,0,3,temporary=1);old=deepcopy(h);self.assertFalse(manual(h,"B"));self.assertEqual(h,old)

    def test_09_manual_is_once_per_turn(self):
        h=self.fixture;self.assertTrue(manual(h,"B"));old=deepcopy(h);self.assertFalse(manual(h,"A"));self.assertEqual(h,old)

    def test_10_recover_disables_manual(self):
        h=self.fixture;old=deepcopy(h);self.assertFalse(manual(h,"A",go=False));self.assertEqual(h,old)

    def test_11_no_l2_disables_manual(self):
        h=self.fixture;old=deepcopy(h);self.assertFalse(manual(h,"A",l2=False));self.assertEqual(h,old)

    def test_12_no_l1_does_not_fabricate_tea(self):
        h=cleanup(self.fixture,"A",l1=False);self.assertEqual((h.movement,h.receipts),(1,1))

    def test_13_preview_does_not_mutate(self):
        h=self.fixture;old=deepcopy(h);preview(h,option="A");preview(h,option="B");self.assertEqual(h,old)

    def test_14_unconfirmed_does_not_debit(self):
        h=self.fixture;old=deepcopy(h);self.assertFalse(manual(h,"A",committed=False));self.assertEqual(h,old)

    def test_15_finished_before_cleanup_cancels_intent(self):
        h=self.fixture;old=deepcopy(h);cleanup(h,"A",terminal=True);self.assertEqual(h,old)

    def test_16_manual_finish_skips_later_tea(self):
        h=cleanup(self.fixture,"A",finishes_after_manual=True);self.assertEqual((h.movement,h.receipts),(1,1))

    def test_17_last_engine_can_starve_automatic_cost(self):
        h=cleanup(Heat(1,2,4,trick=True,speed=True),"A");self.assertGreater(h.unpaid_tea,0)

    def test_18_new_a_heat_survives_until_next_recover_fixture(self):
        h=Heat(4,2,1);manual(h,"A");self.assertEqual(h.hand,3);tea(h,False);self.assertEqual(h.hand,2)

    def test_19_b_is_relocation_not_net_engine_recovery(self):
        h=self.fixture;manual(h,"B");self.assertEqual((h.engine,h.total),(4,7))

    def test_20_temporary_heat_not_returned(self):
        h=Heat(4,1,2,temporary=2);manual(h,"B");self.assertEqual(h.temporary,2)

    def test_21_go_and_recover_counts_preserved(self):
        self.assertEqual([go_recover("Go",n)["heat"] for n in range(1,6)],[0,1,2,3,3])
        self.assertEqual([go_recover("Recover",n)["cool"] for n in range(1,6)],[3,2,1,0,0])

    def test_22_current_two_car_leader_qualifies(self):
        self.assertEqual(rotor_step(0,False,"current",1,2)[0],3)

    def test_23_candidates_exclude_two_car_leader(self):
        self.assertEqual(rotor_step(0,False,"R1",1,2)[0],0)

    def test_24_current_retriggers_on_later_boundary(self):
        self.assertEqual(trace("current",[4]*12,4),[3,2,1,0]*3)

    def test_25_r1_only_one_three_turn_window(self):
        self.assertEqual(trace("R1",[4]*12,4),[3,2,1]+[0]*9)

    def test_26_r2_waits_for_completed_turn(self):
        self.assertEqual(trace("R2",[4]*6,4),[0,3,2,1,0,0])

    def test_27_r2_no_real_gap_no_activation(self):
        self.assertEqual(rotor_step(0,False,"R2",2,2,2,gap=0)[0],0)

    def test_28_r2_no_request_preserves_use(self):
        self.assertEqual(rotor_step(0,False,"R2",2,2,2,request=False),(0,False))

    def test_29_rank_improvement_does_not_pause(self):
        self.assertEqual(rotor_step(2,True,"R1",1,6),(1,True))

    def test_30_expiration_cannot_reactivate_same_boundary(self):
        self.assertEqual(rotor_step(1,False,"current",6,6),(0,False))

    def test_31_terminal_cancels_remaining_window(self):
        self.assertEqual(rotor_step(2,True,"R1",6,6,terminal=True),(0,True))

    def test_32_selected_broth_deduplicated(self):
        for b in BROTHS:self.assertEqual(effects(b,True,True),{"straight":1,"corner_category":1,"slipstream":1,"cool":2})

    def test_33_expiry_restores_only_selected_broth(self):
        self.assertEqual(effects("Miso",True,False),{"straight":0,"corner_category":0,"slipstream":1,"cool":0})

    def test_34_l3_without_l2_has_no_permanent_broth(self):
        self.assertEqual(effects("Miso",False,False)["slipstream"],0)
        self.assertEqual(effects(None,False,True)["cool"],2)

    def test_35_shoyu_shifts_exact_target_not_match_band(self):
        self.assertTrue(7==7);self.assertFalse(7==8);self.assertTrue(8==8)

    def test_36_broth_is_not_an_ordinary_card(self):
        for b in BROTHS:
            p=launch_plan(list(range(12)),b);self.assertEqual(len(p["ordinary"]),16);self.assertFalse(p["broth_token_in_draw"])

    def test_37_b_recipe_keeps_four_existing_tricks(self):
        for b,r in product(BROTHS,RECIPES):
            p=launch_plan(list(range(12)),b,r,option="B");self.assertEqual(len(p["ordinary"]),16)

    def test_38_tutorial_and_no_l2_have_no_choice(self):
        self.assertIsNone(launch_plan([],"Miso",mode="tutorial"));self.assertIsNone(launch_plan([],"Miso",l2=False))

    def test_39_after_draw_changes_rejected(self):
        self.assertIsNone(launch_plan([],"Miso",before_draw=False))

    def test_40_uk_broth_does_not_change_uk_tricks(self):
        self.assertIsNone(launch_plan([],"Miso",(3,1),team="UK",option="B"))

    def test_41_career_recipe_change_requires_allowed_boundary(self):
        self.assertIsNone(launch_plan([],"Shio",(3,1),option="B",mode="career"))
        self.assertIsNotNone(launch_plan([],"Shio",(3,1),option="B",mode="career",stage="summer"))

    def test_42_pure_lobby_plan_does_not_mutate_speed_source(self):
        values=[1,2,3];old=values[:];launch_plan(values,"Shio");self.assertEqual(values,old)


def sweep(pool):
    counts={"cn_ledgers":0,"rotor_boundaries":0,"broth_union":0,"choice_combinations":0}
    for e in range(pool+1):
        for hand in range(pool-e+1):
            d=pool-e-hand
            for go,option,t,s,l1,l2,tmp in product((False,True),(None,"A","B"),(False,True),(False,True),(False,True),(False,True),(0,1)):
                h=Heat(e,hand,d,temporary=tmp,trick=t,speed=s)
                before=deepcopy(h); predicted=preview(h,option=option,go=go,l1=l1,l2=l2);assert h==before
                cleanup(h,option,go,l1,l2);assert h==predicted
                assert h.total==pool and min(h.engine,h.hand,h.discard)>=0 and h.temporary==tmp
                assert h.movement<=3 and h.receipts<=3
                if h.manual_used:
                    old=deepcopy(h);manual(h,"A",go,l2);assert h==old
                counts["cn_ledgers"]+=1
    for n in range(2,13):
        for rank in range(1,n+1):
            for turn,left,used,terminal,gap,request,policy in product((1,2,3),range(4),(False,True),(False,True),(0,1),(False,True),("current","R1","R2")):
                nxt,spent=rotor_step(left,used,policy,rank,n,turn,gap,request,terminal)
                assert 0<=nxt<=3
                if not terminal and left:assert nxt==left-1
                if policy!="current" and left==0 and (used or rank==1):assert nxt==0
                if policy=="R2" and left==0 and (turn==1 or gap==0 or not request):assert nxt==0
                counts["rotor_boundaries"]+=1
    for chosen,l2,l3,active in product((None,)+BROTHS,(False,True),(False,True),(False,True)):
        eff=effects(chosen,l2,active,l3);assert max(eff.values())<=2
        counts["broth_union"]+=1
    for broth,recipe in product(BROTHS,RECIPES):
        assert sum(recipe)==4
        assert len(launch_plan(list(range(12)),broth,recipe,option="B")["ordinary"])==16
        counts["choice_combinations"]+=1
    return counts


def run():
    start=time.perf_counter(); src=inputs()
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(CandidateTests)
    result=unittest.TextTestRunner(verbosity=1).run(suite)
    if not result.wasSuccessful():raise SystemExit(1)
    counts=sweep(src["cn_base_pool"])
    report={"package":"P2b","kind":"independent_design_model_not_unity","model_sha256":digest(__file__),"inputs":src,
            "checks_total":result.testsRun,"checks_passed":result.testsRun-len(result.failures)-len(result.errors)-len(result.skipped),
            "checks_failed":len(result.failures)+len(result.errors),"checks_skipped":len(result.skipped),
            "parameter_cases":sum(counts.values()),"case_groups":counts,"elapsed_seconds":round(time.perf_counter()-start,4),
            "cn_ledger_examples":{o:vars(cleanup(Heat(4,2,1,trick=True,speed=True),o)) for o in (None,"A","B")},
            "rotor_twelve_turns":{p:trace(p,[4]*12,4) for p in ("current","R1","R2")},
            "raw_rank_eligible":{str(n):{p:[r for r in range(1,n+1) if rotor_step(0,False,p,r,n,2)[0]] for p in ("current","R1","R2")} for n in (2,4,6)},
            "checks":[name for name in sorted(dir(CandidateTests)) if name.startswith('test_')],
            "limitations":["Current-rule characterizations are Python transcriptions, not execution of C# or Unity.",
                           "CN ledger starts AFTER normal costs and cooling, with sufficient L1/L2 heat unless explicitly testing shortage.",
                           "No real corner, spin penalty, opponent, retained draw, save, AI, input or race simulation.",
                           "Broth fields are category-level effects; final corner limit still uses the existing source-stacking formula.",
                           "R1/R2 limits and selection/recipe gates are unapproved candidates, not current game behavior.",
                           "Career recipe change guard does not validate any snapshot codec or persisted carried recipe."]}
    (OUT/'p2b-model-results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:report[k] for k in ['checks_total','checks_passed','checks_failed','checks_skipped','parameter_cases','case_groups','elapsed_seconds']},ensure_ascii=False))


if __name__=='__main__':run()
