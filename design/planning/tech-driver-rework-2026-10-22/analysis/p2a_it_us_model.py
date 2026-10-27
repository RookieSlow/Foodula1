"""P2a independent design calculations. Never imports/executes Unity game code.

Authored track lengths and ordinary deck are real inputs. Heat occupancy, payment
availability, movement step size and candidate prices are explicit fixtures.
Earliest ready checkpoint is an opportunity bound, not an actual race victory.
"""
from __future__ import annotations
from dataclasses import dataclass, field, asdict
from copy import deepcopy
from fractions import Fraction
from itertools import product
from math import comb, ceil
from pathlib import Path
import hashlib, json, re, struct, time, unittest

ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).parent
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def read_inputs():
    config=ROOT/'Assets/Scripts/Config/MVPGameConfig.asset'
    text=config.read_text(encoding='utf-8-sig')
    speed_hex=re.search(r'^  speedCardDistribution: (\w+)$',text,re.M).group(1)
    values=list(struct.unpack('<'+'i'*(len(speed_hex)//8),bytes.fromhex(speed_hex)))
    hand=int(re.search(r'^  handSize: (\d+)$',text,re.M).group(1))
    catalog=ROOT/'Assets/Scripts/Core/OfficialTrackCatalog.cs'
    ids=re.findall(r'new TrackSelectionOption\("([^"]+)"',catalog.read_text(encoding='utf-8'))
    tracks=[]
    for id in ids:
        path=ROOT/f'Assets/Resources/Configs/Tracks/{id}.json'
        data=json.loads(path.read_text(encoding='utf-8-sig'))
        assert data['gameCellCount']==len(data['cells'])
        tracks.append(dict(id=id,country=data['country'],cells=data['gameCellCount'],authored_laps=data['laps'],
                           path=str(path.relative_to(ROOT)),sha256=sha(path)))
    return dict(config_path=str(config.relative_to(ROOT)),config_sha256=sha(config),
                catalog_path=str(catalog.relative_to(ROOT)),catalog_sha256=sha(catalog),
                speed_values=values,hand_size=hand,trick_cards=4,tracks=tracks,
                fixtures={'hand_heat':4,'ordinary_sampling':'Uniform fresh sample of authored 12 speed plus 4 tricks; not a live retained hand.',
                          'resource_available':True,'candidate_price_rp':1000,'candidate_ultimate_net_cap':3})


def speed_probability(speed_count,ordinary_count,draw,required):
    if not 0<=draw<=ordinary_count: raise ValueError('Invalid ordinary sample size')
    good=sum(comb(speed_count,k)*comb(ordinary_count-speed_count,draw-k)
             for k in range(required,min(speed_count,draw)+1)
             if 0<=draw-k<=ordinary_count-speed_count)
    return Fraction(good,comb(ordinary_count,draw))


def checkpoints(start,end,n,finish):
    """Current post-movement terminal gate and distinct markers per action."""
    if end<=start or end>=finish: return []
    found=[]
    for marker,offset in enumerate((0,n//2)):
        first=(start-offset)//n+1
        for k in range(first,(end-offset)//n+1):
            d=k*n+offset
            if 0<d<finish: found.append((d,marker))
    unique=[]; seen=set()
    for event in sorted(found):
        if event[1] not in seen: unique.append(event); seen.add(event[1])
    return unique


@dataclass
class Road:
    n:int
    laps:int
    policy:str
    passes:list[int]=field(default_factory=lambda:[0,0])
    repairs:int=0
    ready_at:int|None=None
    cooling_events:int=0
    processed:set=field(default_factory=set)
    trace:list=field(default_factory=list)

    @property
    def required(self): return 1 if self.laps<=3 else 2 if self.laps<=5 else 3

    def encounter(self,d,marker,can_pay=True):
        key=((d-1)//self.n,marker)
        if self.policy!='current' and key in self.processed: return
        self.processed.add(key); self.passes[marker]+=1; before=self.repairs
        if self.policy=='current':
            prosperity=self.passes[marker]<=2
        elif self.policy=='progress':
            prosperity=d<ceil(self.n*self.laps/3)
        elif self.policy=='compressed':
            prosperity=self.passes[marker]<=(0 if self.laps<=3 else 1 if self.laps<=5 else 2)
        else: raise ValueError('Unknown policy')
        if prosperity:
            if self.policy=='current' or self.cooling_events<2: self.cooling_events+=1
            phase='prosperity'
        elif self.repairs<self.required:
            phase='repair' if can_pay else 'unaffordable'
            if can_pay: self.repairs+=1
        else: phase='ready'
        # Current rule checks readiness before the repair; candidates after it.
        eligible=not prosperity and (before>=self.required if self.policy=='current' else self.repairs>=self.required)
        if eligible and self.ready_at is None: self.ready_at=d
        self.trace.append(dict(distance=d,marker=marker,visit=self.passes[marker],phase=phase,
                               repairs=self.repairs,ready=eligible))


def journey(n,laps,policy,step=1,can_pay=True):
    state=Road(n,laps,policy); finish=n*laps
    for start in range(0,finish,step):
        end=min(finish,start+step)
        for d,marker in checkpoints(start,end,n,finish): state.encounter(d,marker,can_pay)
    return dict(cells=n,laps=laps,policy=policy,step=step,required=state.required,
                ready_at=state.ready_at,ready_laps=None if state.ready_at is None else state.ready_at/n,
                ready_fraction=None if state.ready_at is None else state.ready_at/finish,
                repairs=state.repairs,cooling_events=state.cooling_events,trace=state.trace)


def segment_map(n,radius=5):
    if n<2: raise ValueError('Track needs at least two cells')
    offsets=(0,n//2)
    covered={p for p in range(n) if any(min((p-l)%n,(l-p)%n)<=radius for l in offsets)}
    remaining=set(covered); result={}; serial=0
    while remaining:
        seed=min(remaining); remaining.remove(seed)
        todo=[seed]; component={seed}
        while todo:
            p=todo.pop()
            for q in ((p-1)%n,(p+1)%n):
                if q in remaining: remaining.remove(q); component.add(q); todo.append(q)
        for p in component: result[p]=serial
        serial+=1
    return result


@dataclass
class BBQ:
    n:int
    policy:str
    position:int=0
    used_keys:set=field(default_factory=set)
    visits:dict=field(default_factory=dict)
    spent_visits:set=field(default_factory=set)

    def __post_init__(self):
        self.zones=segment_map(self.n)
        seg=self.zones.get(self.position%self.n)
        if seg is not None: self.visits[seg]=1

    def commit(self,count=1):
        seg=self.zones.get(self.position%self.n); lap=self.position//self.n
        if seg is None or count!=1: return False
        key=(seg,lap); visit=(seg,self.visits.get(seg,0))
        if key in self.used_keys: return False
        if self.policy=='visit_and_lap' and visit in self.spent_visits: return False
        self.used_keys.add(key); self.spent_visits.add(visit); return True

    def forward(self,end):
        if end<self.position: raise ValueError('Rewind is not a forward traversal')
        for d in range(self.position+1,end+1):
            prev=self.zones.get((d-1)%self.n); seg=self.zones.get(d%self.n)
            if seg is not None and seg!=prev: self.visits[seg]=self.visits.get(seg,0)+1
        self.position=end


@dataclass
class RegionalPool:
    engine:int
    hand:int=0
    debt:int=0
    bonus:int=0

    def set_bonus(self,bonus):
        if bonus>self.bonus:
            added=bonus-self.bonus; reactivated=min(added,self.debt)
            self.debt-=reactivated; self.engine+=added-reactivated
        elif bonus<self.bonus:
            removed=self.bonus-bonus; take=min(self.engine,removed)
            self.engine-=take; self.debt+=removed-take
        self.bonus=bonus

    def pay(self,count):
        actual=min(count,self.engine); self.engine-=actual; self.hand+=actual; return actual

    def ultimate(self,cap=3,temporary=0):
        actual=min(max(0,self.hand-self.debt),cap)
        if actual<=0: return dict(net=0,retired=0,raw=0,used=False,temporary=temporary)
        retired=min(self.hand,self.debt); raw=retired+actual
        self.hand-=raw; self.debt-=retired; self.engine+=actual
        return dict(net=actual,retired=retired,raw=raw,used=True,temporary=temporary)


def rp_order(state,mode,race_id,launch=True,price=1000):
    candidate=deepcopy(state)
    if mode!='free' or not launch or race_id in candidate['charged'] or candidate['rp']<price:
        return candidate,False
    candidate['rp']-=price; candidate['charged'].append(race_id); return candidate,True


def tune(value,used=False):
    return (value-1,True) if value>=2 and not used else (value,False)


class DesignChecks(unittest.TestCase):
    def test_real_configuration(self):
        x=read_inputs(); self.assertEqual((len(x['speed_values']),x['hand_size'],len(x['tracks'])),(12,7,8))
    def test_four_heat_blocks_g4_with_seven_slots(self):
        self.assertEqual(speed_probability(12,16,3,4),0)
    def test_extra_hand_option_is_conditional(self):
        self.assertEqual(speed_probability(12,16,4,4),Fraction(99,364))
    def test_ordinary_deck_never_contains_fixture_heat(self):
        self.assertEqual(len(read_inputs()['speed_values'])+4,16)
    def test_current_one_two_three_laps_never_ready(self):
        for laps in (1,2,3): self.assertIsNone(journey(42,laps,'current')['ready_at'])
    def test_current_three_laps_repairs_at_105(self):
        r=journey(42,3,'current'); self.assertEqual([(x['distance'],x['repairs']) for x in r['trace'] if x['phase']=='repair'],[(105,1)])
    def test_current_four_laps_needs_next_pass(self):
        self.assertEqual(journey(42,4,'current')['ready_at'],147)
    def test_current_eight_laps_ready_at_four_laps(self):
        self.assertEqual(journey(42,8,'current')['ready_at'],168)
    def test_progress_short_ready_before_finish(self):
        self.assertEqual([journey(42,l,'progress')['ready_at'] for l in (1,2,3)],[21,42,42])
    def test_compressed_short_uses_first_midpoint(self):
        self.assertEqual([journey(42,l,'compressed')['ready_at'] for l in (1,2,3)],[21,21,21])
    def test_progress_long_readiness(self):
        self.assertEqual(journey(42,8,'progress')['ready_at'],168)
    def test_compressed_long_readiness(self):
        self.assertEqual(journey(42,8,'compressed')['ready_at'],147)
    def test_no_fuel_no_repair_or_ready(self):
        for p in ('current','progress','compressed'): self.assertIsNone(journey(42,3,p,can_pay=False)['ready_at'])
    def test_whole_race_single_move_suppresses_all_landmarks(self):
        for p in ('current','progress','compressed'): self.assertIsNone(journey(42,1,p,step=42)['ready_at'])
    def test_post_movement_terminal_gate(self):
        self.assertEqual(checkpoints(0,42,42,42),[]); self.assertEqual(checkpoints(0,30,42,42),[(21,1)])
    def test_two_markers_route_order(self):
        self.assertEqual(checkpoints(35,70,42,126),[(42,0),(63,1)])
    def test_checkpoint_replay_does_not_repair_twice(self):
        r=Road(42,3,'progress'); r.encounter(42,0); r.encounter(42,0); self.assertEqual(r.repairs,1)
    def test_candidates_cap_free_cooling_events(self):
        for p in ('progress','compressed'): self.assertLessEqual(journey(42,8,p)['cooling_events'],2)
    def test_candidates_do_not_repair_after_ready(self):
        for p in ('progress','compressed'): self.assertEqual(journey(42,8,p)['repairs'],3)
    def test_overlapping_zones_are_one_segment(self):
        self.assertEqual(len(set(segment_map(20).values())),1)
    def test_authored_indy_has_two_segments(self):
        self.assertEqual(len(set(segment_map(42).values())),2)
    def test_one_heat_per_batch_and_per_segment_lap(self):
        b=BBQ(42,'lap'); self.assertFalse(b.commit(2)); self.assertTrue(b.commit()); self.assertFalse(b.commit())
    def test_initial_segment_other_segment_has_independent_budget(self):
        b=BBQ(42,'lap'); self.assertTrue(b.commit()); b.forward(21); self.assertTrue(b.commit())
    def test_lap_policy_replenishes_across_finish(self):
        b=BBQ(42,'lap'); b.forward(41); self.assertTrue(b.commit()); b.forward(43); self.assertTrue(b.commit())
    def test_visit_policy_does_not_replenish_inside_same_segment(self):
        b=BBQ(42,'visit_and_lap'); b.forward(41); self.assertTrue(b.commit()); b.forward(43); self.assertFalse(b.commit())
    def test_visit_policy_replenishes_after_real_traversal(self):
        b=BBQ(42,'visit_and_lap'); b.forward(41); self.assertTrue(b.commit()); b.forward(83); self.assertTrue(b.commit())
    def test_single_merged_segment_is_a_real_counterexample(self):
        b=BBQ(20,'visit_and_lap'); self.assertTrue(b.commit()); b.forward(40); self.assertFalse(b.commit())
    def test_regional_capacity_is_repaid_before_movement(self):
        r=RegionalPool(8); r.set_bonus(1); r.pay(9); r.set_bonus(0)
        result=r.ultimate(); self.assertEqual((result['net'],result['retired'],r.engine+r.hand),(3,1,8))
    def test_loan_reentry_does_not_mint_heat(self):
        r=RegionalPool(8); r.set_bonus(1); r.pay(9); r.set_bonus(0); r.set_bonus(1)
        self.assertEqual((r.engine,r.hand,r.debt),(0,9,0))
    def test_empty_net_does_not_spend_ultimate(self):
        r=RegionalPool(0,2,2); self.assertFalse(r.ultimate(temporary=3)['used']); self.assertEqual((r.hand,r.debt),(2,2))
    def test_temporary_heat_never_becomes_engine(self):
        r=RegionalPool(8); self.assertEqual(r.ultimate(temporary=3)['net'],0)
    def test_ultimate_net_cap(self):
        r=RegionalPool(0,8); self.assertEqual(r.ultimate()['net'],3)
    def test_rp_cancel_and_modes_are_isolated(self):
        state={'rp':1500,'career_rp':4000,'charged':[]}
        for mode in ('career','tutorial'):
            n,ok=rp_order(state,mode,'a'); self.assertFalse(ok); self.assertEqual(n,state)
        self.assertEqual(rp_order(state,'free','a',launch=False)[0],state)
    def test_rp_order_has_one_atomic_charge(self):
        s={'rp':1500,'career_rp':4000,'charged':[]}; n,ok=rp_order(s,'free','a'); self.assertTrue(ok)
        self.assertEqual((n['rp'],n['career_rp'],s['rp']),(500,4000,1500)); self.assertEqual(rp_order(n,'free','a')[0],n)
    def test_rp_insufficient_is_not_negative(self):
        s={'rp':999,'career_rp':4000,'charged':[]}; self.assertEqual(rp_order(s,'free','a'),(s,False))
    def test_precision_tune_is_one_for_one_not_free_movement(self):
        self.assertEqual(tune(3),(2,True)); self.assertEqual(tune(1),(1,False)); self.assertEqual(tune(3,True),(3,False))


class RecordingResult(unittest.TextTestResult):
    def __init__(self,*a,**k): super().__init__(*a,**k); self.passed=[]
    def addSuccess(self,t): self.passed.append(t.id().split('.')[-1]); super().addSuccess(t)


def run():
    started=time.perf_counter(); inputs=read_inputs()
    result=unittest.TextTestRunner(verbosity=1,resultclass=RecordingResult).run(unittest.defaultTestLoader.loadTestsFromTestCase(DesignChecks))
    assert result.wasSuccessful()
    timing=[]; sweep=0
    for track,laps,policy,step in product(inputs['tracks'],(1,2,3,4,5,6,8),('current','progress','compressed'),(1,6,12,18,24)):
        r=journey(track['cells'],laps,policy,step)
        if r['ready_at'] is not None: assert 0<r['ready_at']<track['cells']*laps
        if policy!='current': assert r['repairs']<=r['required'] and r['cooling_events']<=2
        timing.append({k:v for k,v in r.items() if k!='trace'}|{'track':track['id']}); sweep+=1
    zone_results=[]
    for n in sorted(set([20,21,22,23,24,42]+[t['cells'] for t in inputs['tracks']])):
        for policy in ('lap','visit_and_lap'):
            b=BBQ(n,policy); paid=[]
            for d in range(n*3):
                b.forward(d)
                if b.commit(): paid.append(d)
                assert not b.commit()
            assert len(paid)<=len(set(segment_map(n).values()))*3
            zone_results.append(dict(cells=n,policy=policy,successful_positions=paid,segments=len(set(segment_map(n).values())))); sweep+=1
    for engine,hand,debt,temp in product(range(11),range(11),range(4),range(4)):
        r=RegionalPool(engine,hand,debt); before=engine+hand; old_debt=debt; x=r.ultimate(temporary=temp)
        assert 0<=x['net']<=3 and r.engine+r.hand==before-x['retired']
        assert r.debt==old_debt-x['retired'] and x['temporary']==temp
        if x['net']==0: assert not x['used']
        sweep+=1
    ablation=[]
    for spin,extra_hand,extra_engine in product((0,1),repeat=3):
        prob=speed_probability(12,16,inputs['hand_size']+extra_hand-4,4)
        ablation.append(dict(extra_spin=spin,extra_hand=extra_hand,extra_engine=extra_engine,
                             spin_threshold=3+spin,hand_limit=7+extra_hand,base_engine_fixture=6+extra_engine,
                             permanent_engine_left_with_four_hand_heat=2+extra_engine,
                             g4_probability_given_fixture=f'{prob.numerator}/{prob.denominator}',probability=float(prob)))
    duration=round(time.perf_counter()-started,4)
    report=dict(kind='independent_design_model_not_unity',package='P2a',model_sha256=sha(Path(__file__)),inputs=inputs,
                checks_total=result.testsRun,checks_passed=len(result.passed),checks_failed=len(result.errors)+len(result.failures),checks_skipped=len(result.skipped),
                elapsed_seconds=duration,parameter_cases=sweep,passed_checks=result.passed,
                it_ablation=ablation,road_timing=timing,bbq_visits=zone_results,
                indy_traces=[journey(42,laps,policy) for laps in (1,2,3,4,8) for policy in ('current','progress','compressed')],
                limitations=['Timing assumes sufficient repair fuel; ready is not actual activation or win.',
                             'Steps are design fixtures, not sampled Unity turn speeds or AI play.',
                             'No lane, corner, rival, draw retention, spin rewind or actual extra-movement simulation.',
                             'BBQ visit model accepts forward movement only; rewind integration needs later acceptance.',
                             'RP protocol is an in-memory candidate, not PlayerPrefs or career save validation.',
                             'No Unity, real input, audio, UI, complete-race or balance acceptance.'])
    (HERE/'p2a-model-results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:report[k] for k in ('checks_total','checks_passed','checks_failed','checks_skipped','parameter_cases','elapsed_seconds')},ensure_ascii=False))
    for laps in (1,2,3,4,5,6,8):
        print(laps,[journey(42,laps,p)['ready_laps'] for p in ('current','progress','compressed')])
    print('tracks',[(t['id'],t['cells']) for t in inputs['tracks']])


if __name__=='__main__': run()
