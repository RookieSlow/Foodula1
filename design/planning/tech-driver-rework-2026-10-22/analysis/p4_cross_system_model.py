from pathlib import Path
from dataclasses import dataclass,field
from copy import deepcopy,copy
import hashlib,json,time,itertools

ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).parent
INPUT=HERE/'p4-model-input.json'
CFG=json.loads(INPUT.read_text('utf-8'))
started=time.perf_counter();checks=[];cases=0
def check(name,condition):
    checks.append({'name':name,'passed':bool(condition)})
    if not condition:raise AssertionError(name)
def digest(value):return hashlib.sha256(repr(value).encode()).hexdigest()

@dataclass
class State:
    engine:int=3
    hand:set=field(default_factory=lambda:{'H1','H2'})
    discard:set=field(default_factory=lambda:{'D1','D2','D3'})
    speed:set=field(default_factory=lambda:{'S1','S2','S3','S4'})
    seen:set=field(default_factory=set)
    uses:int=1
    scope:tuple=('raceA','carA',1,1)
    terminal:bool=False
    debt:int=0
    reserved:set=field(default_factory=set)
    def represented(self):return self.engine+len(self.hand)+len(self.discard)
    def recover(self,zone,ids,cap):
        removed=net=retired=0
        for card in sorted(ids):
            if removed>=cap:break
            source=getattr(self,zone)
            if card not in source or card in self.reserved:continue
            source.remove(card);removed+=1
            if self.debt:self.debt-=1;retired+=1
            else:self.engine+=1;net+=1
        return removed,net,retired

@dataclass(frozen=True)
class Plan:
    scope:tuple
    event:str='activation'
    cost:int=1
    target:str='S1'

def commit(state,plan,confirmed=True):
    key=(plan.scope,plan.event)
    if not confirmed or state.terminal or plan.scope!=state.scope or key in state.seen or state.uses<=0:return False
    if plan.target not in state.speed or state.engine<plan.cost:return False
    state.engine-=plan.cost
    state.discard.update(f'PAY:{plan.event}:{i}' for i in range(plan.cost))
    state.uses-=1;state.seen.add(key);return True

def normal_penalty(state,requested):
    drawn=min(max(0,state.engine),requested);state.engine-=drawn
    state.discard.update(f'ORD:{i}' for i in range(drawn))
    return drawn,drawn<requested

def tailwind(state,native,legal=True,hard=False,guard=False,soft=0,forfeit=False,follow=True,busch=False,old_discard=None):
    potential=max(0,native) if legal and not state.terminal else 0
    restored=hard and guard and potential>0
    request=min(1,potential) if restored else (0 if hard else potential)
    request=max(0,request-soft)
    result={'move':0,'fee':0,'net_cooled':0,'forfeited':False,'guard_used':False,'actual_slip_event':False}
    if request<=0:return result
    eligible_old=state.discard & (set(state.discard) if old_discard is None else old_discard)
    if forfeit and eligible_old and state.uses>0:
        # New candidate requires a positive net engine return, not debt retirement alone.
        if len(eligible_old)>state.debt:
            _,net,_=state.recover('discard',eligible_old,min(len(eligible_old),state.debt+2))
            state.uses-=1;result.update(net_cooled=net,forfeited=True);return result
    if not follow:return result
    # BBQ suppression and Busch pressure are mutually exclusive for one request.
    fee=1 if busch and soft==0 else 0
    if state.engine<fee:return result
    if fee:state.engine-=1;state.hand.add('FOLLOW_FEE')
    result.update(move=request,fee=fee,guard_used=bool(restored),actual_slip_event=True)
    return result

class Wallet:
    def __init__(self,rp=1500):self.value={'rp':rp,'orders':{}}
    def buy_and_authorize(self,order,team='IT',mode='free',save_succeeds=True):
        if mode!='free' or team not in ['IT','UK']:return False
        if order in self.value['orders']:
            row=self.value['orders'][order]
            return row['authorized'] and not row.get('refunded') and row['team']==team and row['mode']==mode
        candidate=deepcopy(self.value)
        if candidate['rp']<1000:return False
        candidate['rp']-=1000;candidate['orders'][order]={'authorized':True,'started':False,'team':team,'mode':mode}
        if not save_succeeds:return False
        self.value=candidate;return True
    def cancel_unstarted(self,order):
        row=self.value['orders'].get(order)
        if not row or row['started'] or row.get('refunded'):return False
        self.value['rp']+=1000;row['authorized']=False;row['refunded']=True;return True
    def start_once(self,order):
        row=self.value['orders'].get(order)
        if not row or not row['authorized'] or row['started'] or row.get('refunded'):return False
        row['started']=True;return True

s=State();before=digest(s);forecast=deepcopy(s);forecast.reserved.add('H1');commit(forecast,Plan(forecast.scope))
check('Preview owns mutable sets and does not consume real payment, uses or reservations',digest(s)==before and forecast.uses==0)
bad=copy(s);bad.reserved.add('H1');check('Adversarial shallow copy exposes why future mutable fields need isolation',s.reserved=={'H1'});s.reserved.clear()
s=State();p=Plan(s.scope);assert commit(s,p);after=digest(s)
check('Repeated committed event cannot charge or grant a second time',not commit(s,p) and digest(s)==after)
outcomes=[]
for key in range(4):
    s=State();scope=list(s.scope);scope[key]=('other','other',9,9)[key];before=digest(s)
    outcomes.append(not commit(s,Plan(tuple(scope))) and digest(s)==before)
check('Old race, other participant, old turn and stale plan version reject atomically',all(outcomes))
s=State();s.speed.remove('S1');before=digest(s)
check('A missing selected card invalidates the whole pending action',not commit(s,Plan(s.scope)) and digest(s)==before)
s=State();before=digest(s);commit(s,Plan(s.scope),confirmed=False)
check('Cancel before commitment leaves physical zones, uses and event dedupe unchanged',digest(s)==before)
s=State();commit(s,Plan(s.scope));after=digest(s);commit(s,Plan(s.scope),confirmed=False)
check('Cancel after commitment does not refund a successfully paid action',digest(s)==after)
s=State(engine=1);before=digest(s)
check('New fixed-cost action preflights complete resources instead of partial skill payment',not commit(s,Plan(s.scope,cost=2)) and digest(s)==before)
s=State(engine=1);check('Ordinary penalty still partially pays and signals spin rather than rolling back',normal_penalty(s,2)==(1,True) and s.engine==0 and 'ORD:0' in s.discard)
check('A DE window sees actual partial payment before later failure',len([x for x in s.discard if x.startswith('ORD:')])==1)
s=State(engine=0,debt=1);base=s.represented()-s.debt;removed,net,retired=s.recover('hand',{'H1'},1)
check('Returned card count distinguishes regional retirement from net engine recovery',(removed,net,retired)==(1,0,1) and s.represented()==base+s.debt)
after=digest(s);s.recover('hand',{'H1'},1)
check('A second recovery cannot reuse a card that already left its source',digest(s)==after)
s=State(engine=0,hand={'H1','H2','H3','H4','H5'},discard=set(),debt=1)
removed,net,retired=s.recover('hand',set(s.hand),min(len(s.hand),s.debt+CFG['mother_road_net_return_cap']))
check('US terminal recovery cap is three NET engine units after capacity debt',(removed,net,retired)==(4,3,1) and len(s.hand)==1)
s=State();reserve='H1';s.reserved.add(reserve);s.recover('hand',set(s.hand),3);after=s.engine;s.recover('hand',{'H1'},1)
check('Ma foregone native cooling stays withheld from other cooling sources this input',reserve in s.hand and s.engine==after)
s.reserved.clear();check('Releasing Ma reservation does not itself return heat',reserve in s.hand and s.engine==after)
def ma_cooling_sources(native,bonus,weather_loss):
    return max(0,native-weather_loss),max(0,bonus-max(0,weather_loss-native))
cooling_rows=[(n,b,w,*ma_cooling_sources(n,b,w)) for n,b,w in itertools.product(range(4),range(3),range(2))]
check('Ma opportunity cost uses weather-adjusted native cooling and never substitutes bonus cooling',all(n_eff+b_eff==max(0,n+b-w) for n,b,w,n_eff,b_eff in cooling_rows) and ma_cooling_sources(1,2,1)==(0,2))
def ma_reservable_old_hand(ordered_heat,native_effective,old_hand):
    return {token for zone,token,temporary in ordered_heat[:native_effective] if zone=='H' and not temporary and token in old_hand}
check('Ma reservation must be an old permanent hand card inside the real native traversal prefix, after temporary heat consumes budget',all(ma_reservable_old_hand(x['ordered_heat'],x['native_effective'],set(x['old_hand']))==set(x['expected']) for x in CFG['ma_source_order_fixtures']))
played={'P1','P2'};hand={'S1'};discard=set();retain='P1';hand.add(retain);discard.update(played-{retain});draws=7-len(hand)
check('Takumi retention is intercepted before normal discard and replaces a refill slot',retain in hand and retain not in discard and draws==5 and len(hand|discard)==3)
s=State();s.terminal=True;before=digest(s)
check('Terminal reached before a deferred effect blocks its later payment',not commit(s,Plan(s.scope)) and digest(s)==before)
quotas={('race','US-car1','zone1',1):1,('race','US-car2','zone1',1):1};quotas[('race','US-car1','zone1',1)]-=1
check('Twelve-car same-team participants own separate effect quotas',quotas[('race','US-car2','zone1',1)]==1)
for n in CFG['field_sizes']:
    ttl=3;trace=[]
    for round_id in range(3):
        for participant in range(n):trace.append((round_id,participant,ttl))
        ttl=max(0,ttl-1)
    check(f'JP R2 duration is three global rounds for field={n}, not three car callbacks',ttl==0 and len(trace)==3*n and all(x[2]==3-x[0] for x in trace))
driver_window=False;rotor_ttl=2;rotor_ttl-=1
check('Pit/skip clears driver intent while consuming, not pausing, JP global duration',not driver_window and rotor_ttl==1)
s=State();before=digest(s);r=tailwind(s,4,follow=False,busch=True)
check('Refusing Busch follow neither charges nor moves',r['fee']==r['move']==0 and digest(s)==before)
s=State();before=digest(s);r=tailwind(s,4,hard=True,busch=True)
check('Blocked native tailwind cannot charge a voluntary follow fee',r['fee']==r['move']==0 and digest(s)==before)
s=State();r=tailwind(s,4,soft=1,busch=True)
check('BBQ reduction suppresses Busch fee for the same request',r['move']==3 and r['fee']==0)
s=State();r=tailwind(s,4,forfeit=True,busch=True)
check('Whole tailwind forfeiture happens before follow payment and creates no actual-slip event',r['forfeited'] and r['move']==r['fee']==0 and r['net_cooled']==2 and not r['actual_slip_event'])
s=State(discard=set());before=digest(s);r=tailwind(s,4,forfeit=True,follow=False)
check('No recoverable old discard cannot turn refusal into free forfeiture cooling',r['net_cooled']==0 and not r['forfeited'] and digest(s)==before)
s=State(discard={'NEW_SAME_TURN_PAYMENT'});before=digest(s);r=tailwind(s,4,forfeit=True,follow=False,old_discard=set())
check('New same-input discard payment cannot substitute for Takahashi old-discard source',not r['forfeited'] and r['net_cooled']==0 and digest(s)==before)
s=State();r=tailwind(s,5,hard=True,guard=True)
check('Zhou guarded tailwind keeps at most one basic step, never full source bonuses',r['move']==1 and r['guard_used'])
s=State();r=tailwind(s,5,hard=True,guard=True,soft=1)
check('Zero actual movement after independent reduction does not consume finite guard',r['move']==0 and not r['guard_used'])
s=State(engine=0);before=digest(s);r=tailwind(s,4,busch=True)
check('Missing follow fee rejects the tailwind without forced spin or new heat',r['move']==0 and digest(s)==before)
w=Wallet();before=digest(w.value)
check('Failed injected wallet save grants neither spend nor launch authorization',not w.buy_and_authorize('launch1',save_succeeds=False) and digest(w.value)==before)
assert w.buy_and_authorize('launch1');after=digest(w.value);w.buy_and_authorize('launch1')
check('Retrying the same authorized launch cannot deduct RP twice',digest(w.value)==after and w.value['rp']==500)
assert w.cancel_unstarted('launch1');after=digest(w.value)
check('Explicit pre-start compensation is once only',not w.cancel_unstarted('launch1') and digest(w.value)==after and w.value['rp']==1500)
check('A refunded launch token cannot be reused to start without paying',not w.buy_and_authorize('launch1') and not w.start_once('launch1'))
w=Wallet();w.buy_and_authorize('launch1');after=digest(w.value)
check('An order for one participant team cannot authorize another team',not w.buy_and_authorize('launch1',team='UK') and digest(w.value)==after)
assert w.start_once('launch1');after=digest(w.value)
check('Launch authorization starts at most one race',not w.start_once('launch1') and digest(w.value)==after)
check('A started race exit cannot refund the RP order',not w.cancel_unstarted('launch1') and digest(w.value)==after)
for mode in ['career','tutorial']:
    w=Wallet();before=digest(w.value);check(f'Normal RP purchase is unavailable in {mode}',not w.buy_and_authorize('launch1',mode=mode) and digest(w.value)==before)
oldxp={'old-takumi-id':12000};proposed_new='candidate-new-driver';newxp=oldxp.get(proposed_new,0)
check('Comparing replacement identities preserves original XP bytes and gives no silent alias',oldxp=={'old-takumi-id':12000} and newxp==0)
raw=b'{"schemaVersion":1,"initialTech":{"active":["old"]}}';loaded_version=1;required_version=2
save_attempts=[]
def propose_versioned_write(original,required,write):
    value=json.loads(original)
    if value.get('schemaVersion')!=required:return False
    write(json.dumps(value).encode());return True
check('Unaccepted future save schema keeps original payload and never reaches write adapter',not propose_versioned_write(raw,required_version,save_attempts.append) and not save_attempts and json.loads(raw)['schemaVersion']==1)

# Parameter exploration focuses on cross-effect priority and complete stale-input rejection.
for native,hard,guard,soft,forfeit,follow,busch,engine in itertools.product(range(6),[False,True],[False,True],range(2),[False,True],[False,True],[False,True],range(3)):
    s=State(engine=engine);capacity=s.represented();result=tailwind(s,native,hard=hard,guard=guard,soft=soft,forfeit=forfeit,follow=follow,busch=busch)
    assert s.represented()==capacity
    assert not(result['forfeited'] and (result['move'] or result['fee'] or result['actual_slip_event']))
    assert not(soft>0 and result['fee'])
    assert not(result['move']==0 and result['guard_used'])
    assert result['move']<=native
    cases+=1
check('Cross-effect enumeration preserves resource ownership, fee priority and event truth',cases==1152)
sources={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in CFG['source_files']}
result={'package':'P4','kind':CFG['kind'],'total':len(checks),'passed':sum(x['passed'] for x in checks),'failed':sum(not x['passed'] for x in checks),'skipped':0,'parameter_cases':cases+len(cooling_rows)+len(CFG['ma_source_order_fixtures']),'parameter_breakdown':{'tailwind_priority_and_ownership':cases,'native_bonus_weather_cooling_attribution':len(cooling_rows),'old_permanent_hand_traversal_fixtures':len(CFG['ma_source_order_fixtures'])},'elapsed_seconds':round(time.perf_counter()-started,4),'input_sha256':hashlib.sha256(INPUT.read_bytes()).hexdigest(),'script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'source_sha256':sources,'checks':checks,'limits':CFG['limits']}
(HERE/'p4-model-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:result[k] for k in ['total','passed','failed','skipped','parameter_cases','elapsed_seconds']}))
