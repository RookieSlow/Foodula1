"""Finite candidate ledger and timing checks for P3b, not a Unity test suite."""
from __future__ import annotations
from dataclasses import dataclass, field
from pathlib import Path
import hashlib
import itertools
import json
import time

HERE = Path(__file__).resolve().parent
INPUT = HERE / 'p3b-model-input.json'
CFG = json.loads(INPUT.read_text('utf-8'))
started = time.perf_counter()
checks = []
parameter_cases = 0


def check(name, condition):
    checks.append({'name': name, 'passed': bool(condition)})


@dataclass
class Heat:
    engine: list[str]
    hand: list[str]
    discard: list[str]
    initial: frozenset[str] = field(init=False)

    def __post_init__(self):
        self.initial = frozenset(self.engine + self.hand + self.discard)
        self.audit()

    @classmethod
    def make(cls, cap=6, engine=2, hand=2):
        ids = [f'H{i}' for i in range(cap)]
        return cls(ids[:engine], ids[engine:engine + hand], ids[engine + hand:])

    def audit(self):
        ids = self.engine + self.hand + self.discard
        assert len(ids) == len(set(ids)) == len(self.initial)
        assert frozenset(ids) == self.initial

    def pay(self, count, destination):
        if len(self.engine) < count:
            return False
        paid = self.engine[:count]
        del self.engine[:count]
        getattr(self, destination).extend(paid)
        self.audit()
        return True

    def recover_old(self, old_ids, cap):
        returned = [x for x in self.hand if x in old_ids][:cap]
        self.hand[:] = [x for x in self.hand if x not in returned]
        self.engine.extend(returned)
        self.audit()
        return len(returned)


def ascari_plan(tier, values, index, apexes, limits, l3_index=None,
                confirm=True, alive=True, uses=1):
    cards = list(values)
    if l3_index is not None:
        cards[l3_index] = max(1, cards[l3_index] - 1)
    invalid = (not confirm or not alive or uses <= 0 or index == l3_index
               or not 0 <= index < len(cards) or cards[index] < 2)
    if invalid:
        return {'used': 0, 'reward': 0, 'reason': 'uncommitted_or_invalid'}
    cards[index] -= 1
    speed = sum(cards)
    passed = [(d, lim) for d, lim in zip(apexes, limits) if 0 < d <= speed]
    if not passed:
        return {'used': 0, 'reward': 0, 'reason': 'cannot_reach_apex_after_reduction'}
    reward = CFG['ascari']['active_after_corner_movement'][tier - 1] if speed <= passed[0][1] else 0
    movement = speed + reward
    # Synthetic straight-line fixture, not the board/pathfinding algorithm.
    fee_requests = [max(0, speed - lim) for d, lim in zip(apexes, limits) if 0 < d <= movement]
    return {'used': 1, 'speed': speed, 'base_movement': speed, 'reward': reward,
            'movement': movement, 'fee_requests': fee_requests}


def ascari_passive(pool, tier, margin, old_ids, used=0, repeated=False,
                   spun=False, alive=True):
    if (not alive or spun or repeated or margin < 0
            or margin > CFG['ascari']['passive_margin'][tier - 1]
            or used >= CFG['ascari']['passive_per_lap'][tier - 1]):
        return 0
    return pool.recover_old(old_ids, 1)


def nova_passive(pool, tier, engine_at_input, old_ids, corner_heat_paid=1,
                 used=False, spun=False, alive=True):
    if (not alive or spun or used or corner_heat_paid < 1
            or engine_at_input > CFG['nuvolari']['passive_entry_engine_max'][tier - 1]):
        return 0
    return pool.recover_old(old_ids, CFG['nuvolari']['passive_old_hand_return'][tier - 1])


def nova_active(tier, engine, spin, uses=1, confirm=True, alive=True):
    cap = CFG['nuvolari']['spin_limit']
    if not confirm or not alive or not uses or engine > 2 or spin + 1 >= cap:
        return {'used': 0, 'spin': spin, 'bonus': 0, 'ignored': 0, 'recovery': False}
    return {'used': 1, 'spin': spin + 1,
            'bonus': CFG['nuvolari']['active_movement'][tier - 1],
            'ignored': 1, 'recovery': tier < 3}


def nova_corner_fees(fees, ignored=1):
    # Exemption is tied to first encountered apex, even when its fee is already zero.
    return [0 if i < ignored else f for i, f in enumerate(fees)]


@dataclass
class Stewart:
    tier: int
    remaining: int
    seen: set[str] = field(default_factory=set)

    def step(self, pool, event_id, base_move=4, apexes=(), kind='drive', confirm=True, finish=False):
        if event_id in self.seen or not confirm or self.remaining == 0:
            return 0, 0
        if kind != 'drive':
            self.remaining = 0
            return 0, 0
        if not pool.pay(1, 'hand'):
            self.remaining = 0
            return 0, 0
        self.seen.add(event_id)
        candidate = CFG['stewart']['active_straight_movement'][self.tier - 1]
        # Do not oscillate between no-bonus straight and with-bonus corner.
        bonus = 0 if any(0 < d <= base_move + candidate for d in apexes) else candidate
        self.remaining = 0 if finish else self.remaining - 1
        return bonus, 1


def fury(tier, overtaken=False, same_round_duplicate=False, next_kind='drive',
         old_heat=None, pool=None, safe_straight=True, used_lap=False):
    if not overtaken or same_round_duplicate or next_kind != 'drive' or used_lap:
        return 0, 0
    cold = pool.recover_old(old_heat, 1) if tier == 3 and safe_straight and pool is not None else 0
    return CFG['stewart']['passive_first_speed_bonus'][tier - 1], cold


@dataclass
class Pressure:
    target: str
    expired: bool = False

    def resolve(self, target, pool, accept, native_slip=2, nearest_bus=True,
                smoke=False, alive=True, personal_input=True):
        if self.expired or target != self.target or not personal_input:
            return 0, 0
        self.expired = True
        if not alive or not nearest_bus:
            return native_slip if alive else 0, 0
        if smoke:
            return max(0, native_slip - 1) if accept else 0, 0
        if native_slip <= 0:
            return 0, 0
        if not accept or not pool.pay(1, 'hand'):
            return 0, 0
        return native_slip, 1


def busch_passive(tier, start_gap, actual_pass, nearest=True, quota_used=False,
                  source='ordinary', alive=True, pending=False):
    return (alive and actual_pass and nearest and not quota_used and not pending and source == 'ordinary'
            and 0 < start_gap <= CFG['busch']['passive_pre_overtake_gap'][tier - 1])


def busch_active(pool, tier, rear_gap, used_lap=False, confirm=True, alive=True, uses=1, pending=False):
    if not confirm or not alive or not uses or not 0 < rear_gap <= 3 or not pool.pay(1, 'discard'):
        return 0, False, 0
    return CFG['busch']['active_straight_movement'][tier - 1], not used_lap and not pending, 1


# Boundary scenarios, including combinations that would make an apparent synergy degenerate.
a = ascari_plan(1, [2, 4], 0, [4, 6], [5, 3])
check('Ascari sacrifices one before safe apex and gains two after it', a['speed'] == 5 and a['movement'] == 7)
check('Ascari extra movement still requests lower-limit second-apex heat', a['fee_requests'] == [0, 2])
check('Ascari cannot use unearned bonus to reach first apex', ascari_plan(1, [2, 3], 0, [5], [5])['used'] == 0)
check('Ascari same physical speed card cannot also take IT L3 reduction', ascari_plan(2, [2, 4], 0, [3], [5], l3_index=0)['used'] == 0)
check('Ascari different-card IT L3 reduction changes movement and apex speed', ascari_plan(2, [2, 4], 0, [3], [4], l3_index=1)['speed'] == 4)
check('Ascari no safe first apex means no post-apex reward', ascari_plan(3, [2, 4], 0, [4], [4])['reward'] == 0)
check('Ascari value one cannot be reduced below one', ascari_plan(1, [1, 4], 0, [4], [5])['used'] == 0)
check('Ascari cancel or terminal state consumes no activation', all(ascari_plan(1, [2, 4], 0, [4], [5], **x)['used'] == 0 for x in [{'confirm':False}, {'alive':False}, {'uses':0}]))
h = Heat.make(); old = set(h.hand); h.pay(1, 'hand')
check('Ascari returns only one old hand heat, not newly paid heat', ascari_passive(h, 1, 1, old) == 1 and 'H0' in h.hand)
check('Ascari passive has repeated-apex, spin and per-lap gates', all(ascari_passive(Heat.make(), 1, 0, {'H2'}, **x) == 0 for x in [{'repeated':True}, {'spun':True}, {'used':1}, {'alive':False}]))
check('Ascari tier three opens margin two, not more than two rewards per lap', ascari_passive(Heat.make(), 3, 2, {'H2'}) == 1 and ascari_passive(Heat.make(), 3, 2, {'H2'}, used=2) == 0)
check('Old IT six-card 90 percent threshold requires empty engine', [e for e in range(7) if (6-e)*10 >= 6*9] == [0])
check('Nova candidate allows low but nonempty engine without changing heat capacity', all(nova_active(1, e, 0)['used'] == 1 for e in [0,1,2]) and nova_active(1,3,0)['used'] == 0)
check('Nova precommits one spin tolerance', nova_active(1,2,0)['spin'] == 1)
check('Nova cannot spend last remaining survival tolerance', nova_active(3,2,2)['used'] == 0)
check('Nova ignores first apex only, including a zero-fee first apex', nova_corner_fees([0,3,2]) == [0,3,2] and nova_corner_fees([2,3]) == [0,3])
check('Nova tiers one and two recover next input, tier three still pays spin', nova_active(2,1,0)['recovery'] and not nova_active(3,1,0)['recovery'] and nova_active(3,1,0)['spin'] == 1)
check('Nova cancel does not spend, committed finish cannot refund spin', nova_active(3,1,0,confirm=False)['spin'] == 0 and nova_active(3,1,0)['spin'] == 1)
h=Heat.make(engine=2,hand=3); old=set(h.hand); h.pay(1,'discard')
check('Nova risky ordinary corner can return two old heat at tier three', nova_passive(h,3,2,old) == 2 and len(h.engine)==3)
check('Nova forced spin, no actual fee, or repeated lap blocks passive refund', all(nova_passive(Heat.make(),3,2,{'H2'},**x)==0 for x in [{'spun':True},{'corner_heat_paid':0},{'used':True},{'alive':False}]))
s=Stewart(1,2); h=Heat.make(cap=8,engine=4,hand=3); old=set(h.hand)
check('Stewart straight boost pays real heat into hand after chosen cards close', s.step(h,'t1')==(2,1) and len(h.engine)==3 and old < set(h.hand))
check('Stewart duplicate submit cannot pay or boost twice', s.step(h,'t1')==(0,0) and len(h.engine)==3)
check('Stewart extra movement cannot classify itself as a free straight', s.step(h,'t2',base_move=4,apexes=[5])==(0,1) and s.remaining==0)
s=Stewart(3,3);h=Heat.make(cap=8,engine=5,hand=2)
check('Stewart preview and cancel preserve duration and resources', s.step(h,'preview',confirm=False)==(0,0) and s.remaining==3 and len(h.engine)==5)
check('Stewart skip or pit ends window without new fee', s.step(h,'pit',kind='pit')==(0,0) and s.remaining==0 and len(h.engine)==5)
s=Stewart(2,3);h=Heat.make(cap=8,engine=1,hand=4); s.step(h,'t1')
check('Stewart empty engine ends duration without granting a free later boost', s.step(h,'t2')==(0,0) and s.remaining==0)
s=Stewart(3,3);h=Heat.make(cap=8,engine=4,hand=2)
check('Stewart finish clears remaining duration but retains paid heat', s.step(h,'finish',finish=True)==(3,1) and s.remaining==0 and len(h.engine)==3)
check('Stewart passive reacts only to an actual prior overtake once per lap', fury(2,overtaken=True)==(2,0) and fury(2,overtaken=False)==(0,0) and fury(3,overtaken=True,used_lap=True)==(0,0))
h=Heat.make(cap=8,engine=2,hand=4);old=set(h.hand)
check('Stewart tier three safe straight can return one old hand heat', fury(3,True,old_heat=old,pool=h)==(2,1))
check('Busch prediction alone cannot create a pressure mark', not busch_passive(1,1,False))
check('Busch only actual ordinary overtake of locked nearest target qualifies', busch_passive(1,2,True) and not busch_passive(1,3,True) and not busch_passive(3,1,True,source='slipstream') and not busch_passive(3,1,True,nearest=False))
check('Busch active and passive share per-lap cap and cannot stack across laps', not busch_passive(3,1,True,quota_used=True) and busch_active(Heat.make(cap=8),3,2,used_lap=True)==(3,False,1) and not busch_passive(3,1,True,pending=True) and busch_active(Heat.make(cap=8),3,2,pending=True)==(3,False,1))
p=Pressure('A');h=Heat.make(cap=8,engine=2,hand=4)
check('Busch target may refuse follow with or without smoke and has no fee or forced spin', p.resolve('A',h,False)==(0,0) and len(h.engine)==2 and Pressure('A').resolve('A',h,False,smoke=True)==(0,0))
p=Pressure('A');h=Heat.make(cap=8,engine=2,hand=4)
check('Busch voluntary positive slip pays one target-owned heat only', p.resolve('A',h,True)==(2,1) and len(h.engine)==1 and len(h.hand)==5)
check('Busch repeated resolution has no second fee', p.resolve('A',h,True)==(0,0) and len(h.engine)==1)
p=Pressure('A');h=Heat.make(cap=8,engine=0,hand=5)
check('Busch empty target engine declines this source rather than manufacturing heat', p.resolve('A',h,True)==(0,0) and len(h.hand)==5)
p=Pressure('A');h=Heat.make(cap=8,engine=2,hand=4)
check('BBQ smoke has priority and cannot also charge driver pressure', p.resolve('A',h,True,smoke=True)==(1,0) and len(h.engine)==2)
p=Pressure('A');h=Heat.make(cap=8,engine=2,hand=4)
check('Busch zero or blocked slip gives no fee', p.resolve('A',h,True,native_slip=0)==(0,0) and len(h.engine)==2)
p=Pressure('A');h=Heat.make(cap=8,engine=2,hand=4)
check('Busch cannot retarget another car or consume window on unrelated input', p.resolve('B',h,True)==(0,0) and not p.expired and p.resolve('A',h,True,personal_input=False)==(0,0) and not p.expired)
check('Busch active cost is real and requires rear target or survives no activation', busch_active(Heat.make(cap=8,engine=0,hand=4),2,1)==(0,False,0) and busch_active(Heat.make(cap=8),2,4)==(0,False,0))
p=Pressure('A');h=Heat.make(cap=8,engine=2,hand=4)
check('Busch dead target expires mark without heat fee', p.resolve('A',h,True,alive=False)==(0,0) and p.expired and len(h.engine)==2)

# One actual ledger showing US BBQ and revival may not spend/return the same heat twice.
h=Heat.make(cap=8,engine=5,hand=3); original=set(h.initial)
bbq_card=h.hand.pop(0);h.discard.append(bbq_card)  # already submitted in a normal gear slot
s=Stewart(3,3);s.step(h,'us_combo');returned=h.recover_old(set(h.hand),3)
check('US combo pays driver heat, BBQ card stays discarded, net revival at most three', returned==3 and bbq_card in h.discard and len(h.engine)==7 and set(h.initial)==original)
check('US combo cannot recover or BBQ the already spent physical card again', h.recover_old({bbq_card},3)==0)

# Enumerations test resource invariants across legal permanent-heat partitions.
grid_ok=True
for cap in CFG['italy_engine_capacities'] + [CFG['us_permanent_heat_count']]:
    for engine in range(cap+1):
        for hand in range(cap-engine+1):
            for tier in [1,2,3]:
                for safe,confirm in itertools.product([False,True],repeat=2):
                    parameter_cases += 1
                    h=Heat.make(cap,engine,hand);old=set(h.hand)
                    if cap in CFG['italy_engine_capacities']:
                        a=ascari_passive(h,tier,0,old,spun=not safe,alive=confirm)
                        n=nova_passive(h,tier,engine,old,spun=not safe,alive=confirm)
                        grid_ok &= a<=1 and n<=2
                    else:
                        st=Stewart(tier,CFG['stewart']['active_duration_personal_turns'][tier-1])
                        st.step(h,'grid',apexes=[] if safe else [1],confirm=confirm)
                        Pressure('A').resolve('A',h,confirm,smoke=not safe)
                    h.audit()
check('Enumerated permanent-heat identities remain unique and conserved', grid_ok)
envelope_ok=True
for tier in [1,2,3]:
    for engine in range(9):
        for turns in range(5):
            parameter_cases+=1
            h=Heat.make(8,engine,0); st=Stewart(tier,CFG['stewart']['active_duration_personal_turns'][tier-1])
            rows=[st.step(h,f'e{i}') for i in range(turns)]
            paid=sum(x[1] for x in rows);bonus=sum(x[0] for x in rows)
            envelope_ok &= paid<=min(engine,CFG['stewart']['active_duration_personal_turns'][tier-1]) and bonus<=paid*CFG['stewart']['active_straight_movement'][tier-1]
check('Stewart duration envelope is bounded by paid personal turns', envelope_ok)
pressure_ok=True
for engine,accept,smoke,native in itertools.product(range(9),[False,True],[False,True],range(4)):
    parameter_cases+=1
    h=Heat.make(8,engine,0);p=Pressure('A');first=p.resolve('A',h,accept,native_slip=native,smoke=smoke);second=p.resolve('A',h,accept,native_slip=native,smoke=smoke)
    pressure_ok &= first[1]<=1 and second==(0,0) and (not smoke or first[1]==0)
    h.audit()
check('Busch single-target fee stays bounded for all follow and smoke options', pressure_ok)

result={
    'package':'P3b', 'kind':'independent_design_model_not_unity',
    'total':len(checks),'passed':sum(x['passed'] for x in checks),
    'failed':sum(not x['passed'] for x in checks),'skipped':0,
    'parameter_cases':parameter_cases,'elapsed_seconds':round(time.perf_counter()-started,4),
    'input_sha256':hashlib.sha256(INPUT.read_bytes()).hexdigest(),
    'script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'checks':checks,
    'example_ascari_linear_fixture':a if isinstance(a,dict) else ascari_plan(1,[2,4],0,[4,6],[5,3]),
    'example_us_combo':{'engine':7,'hand':0,'discard':1,'bbq_card_returned':False,'revival_actual_return':3},
    'limits':CFG['limits']
}
(HERE/'p3b-model-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k in ['package','total','passed','failed','skipped','parameter_cases','elapsed_seconds']},ensure_ascii=False))
if result['failed']:
    print(json.dumps([x for x in checks if not x['passed']],ensure_ascii=False));raise SystemExit(1)
