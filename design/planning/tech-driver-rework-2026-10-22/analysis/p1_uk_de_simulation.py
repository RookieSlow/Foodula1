"""Independent design model for P1. It does not load Unity or production rules.

Permanent heat uses an integer engine plus owned payment receipts, matching the
current representation at the boundary, not persistent identities inside engine.
Numbers are candidate budgets; the model establishes bounds, not race balance.
"""
from __future__ import annotations

from copy import deepcopy
from dataclasses import dataclass, field
from itertools import product
from pathlib import Path
import hashlib
import json
import time
import unittest


@dataclass
class Ledger:
    engine: int
    cards: dict[str, tuple[str, bool]] = field(default_factory=dict)
    serial: int = 0

    def permanent(self):
        return self.engine + sum(not temporary for _, temporary in self.cards.values())

    def pay(self, amount, destination="discard"):
        result = []
        for _ in range(min(amount, self.engine)):
            self.serial += 1
            receipt = f"payment-{self.serial}"
            self.cards[receipt] = (destination, False)
            self.engine -= 1
            result.append(receipt)
        return result

    def cool(self, receipts, limit=None, terminal=False):
        if terminal:
            return 0
        result = 0
        for receipt in dict.fromkeys(receipts):
            if limit is not None and result >= limit:
                break
            if receipt in self.cards and not self.cards[receipt][1]:
                del self.cards[receipt]
                self.engine += 1
                result += 1
        return result


def de_window(engine, ordinary_cost=0, requested=30, policy="cap3",
              mid_cooling=False, terminal=False, old_discard=0,
              temporary_hand=0, duplicate_receipts=False):
    if min(engine, ordinary_cost, requested, old_discard, temporary_hand) < 0:
        raise ValueError("Nonnegative design inputs required")
    if ordinary_cost > engine:
        raise ValueError("Ordinary cost must be affordable in this bounded model")
    ledger = Ledger(engine)
    ledger.cards.update({f"old-{i}": ("discard", False) for i in range(old_discard)})
    ledger.cards.update({f"temp-{i}": ("hand", True) for i in range(temporary_hand)})
    total = ledger.permanent()
    # A numeric budget is deliberate: production engine payments create new
    # CardData objects, so a frozen set of engine-card identities does not exist.
    budget = {"cap2": 2, "cap3": 3, "frozen_engine": engine,
              "unsafe_engine_only": requested}[policy]
    receipts = ledger.pay(ordinary_cost, "hand")
    fuels = 0
    for _ in range(min(requested, budget)):
        if ledger.engine < 1:
            break
        paid = ledger.pay(1)
        if len(paid) != 1:
            break
        receipts += paid
        fuels += 1
        if mid_cooling:
            ledger.cool(paid)
        assert ledger.permanent() == total
    before_refund = ledger.engine
    if duplicate_receipts:
        receipts += receipts
    restored = ledger.cool(receipts, terminal=terminal)
    assert ledger.permanent() == total
    return {"fuel_uses": fuels, "fuel_movement": 2 * fuels, "budget": budget,
            "engine_before_refund": before_refund, "engine_after": ledger.engine,
            "restored": restored, "l3_consumed": bool(receipts),
            "old_discard_remaining": sum(k.startswith("old-") for k in ledger.cards),
            "temporary_remaining": sum(t for _, t in ledger.cards.values()),
            "permanent_total": ledger.permanent()}


def uk_a(engine, hand_heat, discard_heat, speed_cards, home=False,
         already_used=False, temporary_hand=0):
    ledger = Ledger(engine)
    ledger.cards.update({f"h-{i}": ("hand", False) for i in range(hand_heat)})
    ledger.cards.update({f"d-{i}": ("discard", False) for i in range(discard_heat)})
    ledger.cards.update({f"t-{i}": ("hand", True) for i in range(temporary_hand)})
    total = ledger.permanent()
    candidates = [k for k, (zone, t) in ledger.cards.items() if zone == "hand" and not t]
    candidates += [k for k, (zone, t) in ledger.cards.items() if zone == "discard" and not t]
    restored = 0
    spent = 0
    if not already_used and speed_cards > 0 and candidates:
        restored = ledger.cool(candidates, limit=2 if home else 1)
        spent = int(restored > 0)
    assert ledger.permanent() == total
    return dict(engine_after=ledger.engine, restored=restored, speed_discarded=spent,
                speed_remaining=speed_cards-spent, quota_used=bool(spent),
                hand_heat_remaining=sum(z == "hand" and not t for z, t in ledger.cards.values()),
                temporary_remaining=sum(t for _, t in ledger.cards.values()))


def uk_b(receipts, home=False, three_types=True, trick_played=True, used=False):
    if not three_types or not trick_played or used:
        return 0
    return min(receipts, 2 if home else 1)


def breakfast(heat, speed, trick, l2, l3, country):
    return int(heat and speed and trick) * (int(l2) + int(l3 and country in ("UK", "GB")))


class DesignChecks(unittest.TestCase):
    def test_empty_engine(self):
        r=de_window(0); self.assertEqual((r['fuel_uses'],r['l3_consumed']), (0,False))

    def test_three_use_budget(self):
        r=de_window(8,2); self.assertEqual((r['fuel_uses'],r['fuel_movement'],r['engine_after']),(3,6,8))

    def test_two_use_sensitivity(self):
        self.assertEqual(de_window(8,2,policy='cap2')['fuel_movement'],4)

    def test_frozen_budget_with_other_costs(self):
        self.assertEqual(de_window(8,2,policy='frozen_engine')['fuel_movement'],12)

    def test_frozen_full_engine_exposure(self):
        self.assertEqual(de_window(8,policy='frozen_engine')['fuel_movement'],16)

    def test_frozen_budget_survives_mid_cooling(self):
        self.assertEqual(de_window(1,policy='frozen_engine',mid_cooling=True)['fuel_uses'],1)

    def test_fixed_budget_survives_mid_cooling(self):
        self.assertEqual(de_window(1,mid_cooling=True)['fuel_movement'],6)

    def test_unsafe_engine_only_is_horizon_dependent(self):
        for horizon in (10,100,1000):
            self.assertEqual(de_window(1,requested=horizon,policy='unsafe_engine_only',mid_cooling=True)['fuel_movement'],2*horizon)

    def test_no_mid_cooling_resource_bound(self):
        r=de_window(2,1); self.assertEqual((r['fuel_uses'],r['engine_after']),(1,2))

    def test_old_discard_is_not_a_current_payment(self):
        r=de_window(2,1,old_discard=3); self.assertEqual((r['restored'],r['old_discard_remaining']),(2,3))

    def test_duplicate_receipt_is_not_double_cooling(self):
        r=de_window(8,2,duplicate_receipts=True); self.assertEqual((r['restored'],r['engine_after']),(5,8))

    def test_temporary_is_not_permanent_fuel(self):
        r=de_window(0,temporary_hand=2); self.assertEqual((r['fuel_uses'],r['restored'],r['temporary_remaining']),(0,0,2))

    def test_terminal_cancels_cooling(self):
        r=de_window(4,1,terminal=True); self.assertEqual((r['restored'],r['engine_after']),(0,0))

    def test_preview_and_cancel_do_not_mutate(self):
        state={'engine':8,'used':False,'intent':None}; before=deepcopy(state)
        de_window(state['engine'],2); self.assertEqual(state,before)

    def test_repeated_cleanup_is_idempotent(self):
        l=Ledger(2); receipts=l.pay(2); self.assertEqual((l.cool(receipts),l.cool(receipts),l.engine),(2,0,2))

    def test_uk_a_hand_first(self):
        r=uk_a(5,2,1,4); self.assertEqual((r['engine_after'],r['hand_heat_remaining'],r['speed_discarded']),(6,1,1))

    def test_uk_a_home_cooling_doubles_not_uses(self):
        r=uk_a(5,2,1,4,home=True); self.assertEqual((r['engine_after'],r['speed_discarded'],r['quota_used']),(7,1,True))

    def test_uk_a_without_source_does_not_spend(self):
        r=uk_a(8,0,0,4); self.assertEqual((r['restored'],r['speed_discarded'],r['quota_used']),(0,0,False))

    def test_uk_a_without_speed_card(self):
        self.assertEqual(uk_a(7,1,0,0)['restored'],0)

    def test_uk_a_only_temporary_heat(self):
        r=uk_a(8,0,0,4,temporary_hand=2); self.assertEqual((r['restored'],r['temporary_remaining']),(0,2))

    def test_uk_a_cannot_refresh_quota(self):
        self.assertEqual(uk_a(5,2,1,4,home=True,already_used=True)['restored'],0)

    def test_uk_b_only_actual_receipts(self):
        self.assertEqual(uk_b(1,home=True),1); self.assertEqual(uk_b(0),0)

    def test_uk_b_requires_actual_trick(self):
        self.assertEqual(uk_b(3,trick_played=False),0); self.assertEqual(uk_b(3,three_types=False),0)

    def test_breakfast_home_matrix(self):
        for country in ('UK','GB','DE'):
            self.assertEqual(breakfast(True,True,True,True,True,country),2 if country in ('UK','GB') else 1)
            self.assertEqual(breakfast(True,True,True,False,True,country),1 if country in ('UK','GB') else 0)
            self.assertEqual(breakfast(False,True,True,True,True,country),0)

    def test_frozen_budget_uses_borrowers_engine(self):
        self.assertEqual(de_window(3,policy='frozen_engine')['fuel_movement'],6)


class RecordingResult(unittest.TextTestResult):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs); self.passed=[]
    def addSuccess(self,test):
        self.passed.append(test.id().split('.')[-1]); super().addSuccess(test)


def run(output):
    started=time.perf_counter()
    runner=unittest.TextTestRunner(verbosity=1,resultclass=RecordingResult)
    result=runner.run(unittest.defaultTestLoader.loadTestsFromTestCase(DesignChecks))
    sweep=0
    for engine,requested,policy,mid in product(range(11),range(13),('cap2','cap3','frozen_engine'),(False,True)):
        for ordinary in range(engine+1):
            r=de_window(engine,ordinary,requested,policy,mid)
            expected_budget={'cap2':2,'cap3':3,'frozen_engine':engine}[policy]
            assert r['fuel_uses'] <= min(requested,expected_budget)
            if not mid: assert r['fuel_uses'] <= engine-ordinary
            assert r['permanent_total'] == engine and r['engine_after'] <= engine
            sweep+=1
    for engine,hand,discard,speed,home,used in product(range(5),range(4),range(4),range(5),(False,True),(False,True)):
        r=uk_a(engine,hand,discard,speed,home,used)
        assert r['restored'] <= min(hand+discard,2 if home else 1)
        assert r['engine_after'] == engine+r['restored']
        assert r['speed_discarded'] in (0,1) and r['speed_remaining'] >= 0
        sweep+=1
    matrix=[dict(country=c,l2=l2,l3=l3,condition=cond,triggers=breakfast(cond,cond,cond,l2,l3,c))
            for c,l2,l3,cond in product(('UK','GB','DE'),(False,True),(False,True),(False,True))]
    cap_table=[dict(engine=e,cap2=de_window(e,policy='cap2')['fuel_movement'],cap3=de_window(e)['fuel_movement'],
                    frozen=de_window(e,policy='frozen_engine')['fuel_movement']) for e in (0,1,2,3,4,6,8,10)]
    report=dict(kind='independent_design_model_not_unity',package='P1',candidate_numbers_not_balanced=True,
                model_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                checks_total=result.testsRun,checks_passed=len(result.passed),checks_failed=len(result.failures)+len(result.errors),
                checks_skipped=len(result.skipped),elapsed_seconds=round(time.perf_counter()-started,4),passed_checks=result.passed,
                parameter_cases=sweep,breakfast_matrix=matrix,de_upper_bounds=cap_table,
                assumptions=['Permanent engine is a count; payment creates a new owned receipt.',
                             'Every fuel bonus requires one actual permanent engine payment.',
                             'Mid-cooling is a stress input, not a claim about production timing.',
                             'Ordinary payment is affordable; the model does not resolve spin, lanes, rankings or RNG.',
                             'Terminal status suppresses return; temporary heat never increases engine.'],
                limitations=['No Unity/AI/full race/player-handling/balance acceptance.',
                             'No evidence for actual trigger frequency or win-rate fairness.'])
    Path(output).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    assert result.wasSuccessful()
    print(json.dumps({k:v for k,v in report.items() if k in ('checks_total','checks_passed','checks_failed','checks_skipped','elapsed_seconds','parameter_cases')},ensure_ascii=False))


if __name__ == '__main__':
    run(Path(__file__).with_name('p1-model-results.json'))
