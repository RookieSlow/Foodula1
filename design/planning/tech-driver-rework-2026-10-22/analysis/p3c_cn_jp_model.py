"""P3c candidate timing/resource experiments, deliberately independent of Unity."""
from __future__ import annotations
from pathlib import Path
from dataclasses import dataclass, field
import hashlib,itertools,json,time

HERE=Path(__file__).resolve().parent
INPUT=HERE/'p3c-model-input.json'
CFG=json.loads(INPUT.read_text('utf-8'))
start=time.perf_counter();checks=[];parameter_cases=0
def check(name,condition):checks.append({'name':name,'passed':bool(condition)})

@dataclass
class Heat:
    engine:list[str]
    hand:list[str]
    discard:list[str]
    locked:set[str]=field(default_factory=set)
    def __post_init__(self):self.ids=frozenset(self.engine+self.hand+self.discard);self.audit()
    @classmethod
    def make(cls,e=2,h=3,cap=7):
        cards=[f'H{i}' for i in range(cap)]
        return cls(cards[:e],cards[e:e+h],cards[e+h:])
    def audit(self):
        cards=self.engine+self.hand+self.discard
        assert len(cards)==len(set(cards))==len(self.ids) and set(cards)==self.ids
        assert self.locked<=set(self.hand)
    def cool(self,n,source='hand',allowed=None):
        zone=getattr(self,source)
        eligible=[c for c in zone if c not in self.locked and (allowed is None or c in allowed)][:n]
        zone[:]=[c for c in zone if c not in eligible];self.engine.extend(eligible);self.audit()
        return len(eligible)

def china(mode,count):
    count=max(1,count)
    return (3 if count==1 else 4,min(3,count-1),0) if mode=='Go' else (1,0,max(0,4-count))

def zhou_qualifies(tier,source,amount,mode='Go'):
    return mode=='Go' and amount>0 and (source=='go_extra' or tier>=2 and source=='ordinary_corner')

def zhou_recover(pool,tier,ready,base_cooling,old_hand,used=False):
    base=pool.cool(base_cooling)
    extra=pool.cool(CFG['zhou']['passive_extra_cooling'][tier-1],allowed=old_hand) if ready and not used else 0
    return base,extra

@dataclass
class Guard:
    remaining:int
    token:bool=True
    def next_personal_input(self,kind='drive'):
        self.remaining=max(0,self.remaining-1) if kind=='drive' else 0
    def protect(self,blocker,potential,legal=True,accept=True,alive=True):
        if (self.remaining<=0 or not self.token or not alive or not legal or not accept
            or potential<=0 or blocker not in ['ice_jelly','driver_smoke']):return 0
        self.token=False
        return min(1,potential)

def zhou_active(pool,tier,speeds,required,confirm=True,alive=True,uses=1):
    if not confirm or not alive or uses<1 or len(speeds)<required+1 or not pool.hand:return 0,None,0
    speeds.pop(0)
    returned=pool.cool(CFG['zhou']['active_old_hand_cooling'][tier-1],allowed=set(pool.hand))
    return returned,Guard(CFG['zhou']['active_personal_inputs'][tier-1]),1

def ma_corner(tier,previous,mode,apex_index,speed,limit,used_lap=0,replayed=False,previous_completed=True):
    permitted=previous_completed and previous=='Recover' and mode=='Go' and apex_index==0 and not replayed and used_lap<CFG['ma']['passive_changes_per_lap'][tier-1]
    target=limit+1 if permitted else limit
    fee=max(0,speed-target-(CFG['ma']['first_apex_heat_reduction'][tier-1] if permitted else 0))
    return target,fee,permitted

@dataclass
class MaReady:
    tier:int
    ready:bool=True
    def next_input(self,kind='Go',cards_ok=True,fees_ok=True,confirm=True,alive=True):
        if not confirm or not self.ready:return 0
        self.ready=False
        return CFG['ma']['next_go_movement'][self.tier-1] if kind=='Go' and cards_ok and fees_ok and alive else 0

def ma_prepare(pool,tier,native_cooling,confirm=True,mode='Recover',alive=True,uses=1):
    available=[c for c in pool.hand if c not in pool.locked]
    if not confirm or not alive or not uses or mode!='Recover' or native_cooling<1 or not available:return None,0,0
    # Exclude a physical old heat receipt from all cooling for the rest of this Recover.
    reserved=available[0];pool.locked.add(reserved)
    returned=pool.cool(min(native_cooling,len(available))-1)
    return MaReady(tier),returned,1

def tsuchiya_discounts(tier,fees,ids=None):
    remaining=CFG['tsuchiya']['discounted_positive_apexes_per_turn'][tier-1]
    seen=set();result=[]
    for key,fee in zip(ids or range(len(fees)),fees):
        if key not in seen and fee>0 and remaining>0:result.append(max(0,fee-1));remaining-=1
        else:result.append(fee)
        seen.add(key)
    return result

def tsuchiya_pending(crossed_apex,spun=False,alive=True):return 1 if crossed_apex and not spun and alive else 0

def smoke(tier,rear_gap,nearest=True,legal=True,active=True):
    return active and nearest and legal and 0<rear_gap<=CFG['tsuchiya']['smoke_rear_range'][tier-1]

@dataclass
class SpeedCard:
    id:str
    printed:int
    attack:bool=False
    kind:str='speed'

@dataclass
class Deck:
    hand:list[SpeedCard]
    played:list[SpeedCard]
    draw:list[SpeedCard]
    discard:list[SpeedCard]=field(default_factory=list)
    def __post_init__(self):self.ids=frozenset(c.id for c in self.hand+self.played+self.draw+self.discard);self.audit()
    def audit(self):
        cards=self.hand+self.played+self.draw+self.discard
        assert len(cards)==len({c.id for c in cards})==len(self.ids)
        assert {c.id for c in cards}==self.ids
    def retain_and_refill(self,tier,choice,familiar=True,used_lap=False,spun=False,ignored=False,alive=True,first_lap=True,limit=7):
        eligible=[c for c in self.played if c.id==choice and c.kind=='speed' and not c.attack and 1<=c.printed<=CFG['takumi_retention']['retained_original_speed_max'][tier-1]]
        retained=False
        if familiar and not used_lap and not spun and not ignored and alive and len(self.hand)<limit and eligible:
            c=eligible[0];self.played.remove(c);self.hand.append(c);retained=True
        self.discard.extend(self.played);self.played.clear()
        drawn=[]
        while len(self.hand)<limit and self.draw:
            c=self.draw.pop(0);self.hand.append(c);drawn.append(c.id)
        self.audit();return retained,drawn

def familiar_choice(corner,started=False):return not started and corner in CFG['takumi_retention']['synthetic_public_corner_ids']

def gutter_targets(tier,route,choice=0):
    if not route:return ()
    if tier==1:return (route[0],) if choice==0 else ()
    if tier==2:return (route[choice],) if 0<=choice<min(2,len(route)) else ()
    return tuple(route[:2])

def takahashi_passive(pool,tier,defense_committed,used=0,old_hand=None,old_discard=None,replayed=False):
    if not defense_committed or replayed or used>=CFG['takahashi_replacement_option']['passive_defense_events_per_lap'][tier-1]:return 0
    cold=pool.cool(1,allowed=old_hand or set())
    if cold==0 and tier>=2:cold=pool.cool(1,source='discard',allowed=old_discard or set())
    return cold

def takahashi_active(pool,tier,native_slip,old_discard,legal=True,confirm=True,alive=True,uses=1):
    if not legal or not alive:return 0,0,0
    if not confirm or not uses or native_slip<=0 or not any(c in old_discard for c in pool.discard):return max(0,native_slip),0,0
    cold=pool.cool(CFG['takahashi_replacement_option']['active_cooling_from_old_discard'][tier-1],source='discard',allowed=old_discard)
    return 0,cold,1

check('Native China Go remains three then four cards and 0/1/2/3 fee', [china('Go',i) for i in range(1,6)]==[(3,0,0),(4,1,0),(4,2,0),(4,3,0),(4,3,0)])
check('Native China Recover remains one card and 3/2/1/0 cooling', [china('Recover',i) for i in range(1,6)]==[(1,0,3),(1,0,2),(1,0,1),(1,0,0),(1,0,0)])
check('Zhou passive is not weather-dependent or a voluntary tech-fee rebate', zhou_qualifies(1,'go_extra',1) and not zhou_qualifies(1,'weather',1) and not zhou_qualifies(3,'tech_warm_cup',1))
check('Zhou higher tiers accept ordinary Go corner costs, never preview or Recover', zhou_qualifies(2,'ordinary_corner',1) and not zhou_qualifies(2,'ordinary_corner',0) and not zhou_qualifies(3,'go_extra',1,'Recover'))
h=Heat.make(e=1,h=5);old=set(h.hand);b,e=zhou_recover(h,3,True,3,old)
check('Zhou native three then at most two old-heat returns conserve seven', (b,e)==(3,2) and len(h.engine)==6)
h=Heat.make(e=4,h=1);check('Zhou full basic cooling leaves no fictitious extra heat', zhou_recover(h,3,True,3,set(h.hand))==(1,0))
h=Heat.make(e=1,h=4);check('Zhou lap quota blocks extra cooling', zhou_recover(h,3,True,2,set(h.hand),used=True)==(2,0))
h=Heat.make();speeds=['S1','S2','S3','S4'];cold,g,used=zhou_active(h,2,speeds,3)
check('Zhou active pays one speed and returns at most two old hand heat', used==1 and cold==2 and len(speeds)==3 and len(h.engine)==4)
check('Zhou active cannot discard the last required speed slot', zhou_active(Heat.make(),1,['S1'],1)[2]==0)
check('Zhou active cancellation or terminal state pays nothing', all(zhou_active(Heat.make(),1,['S1','S2'],1,**x)[2]==0 for x in [{'confirm':False},{'alive':False},{'uses':0}]))
check('Zhou cannot promise old-hand cooling from discard only', zhou_active(Heat.make(e=1,h=0),3,['S1','S2'],1)[2]==0)
check('Zhou guard restores only one legitimate slipstream step', g.protect('driver_smoke',5)==1 and not g.token)
check('Zhou guard cannot repeatedly puncture a blocked chain', g.protect('driver_smoke',5)==0)
check('Zhou guard never bypasses Busch voluntary fee or own costs', all(Guard(2).protect(k,2)==0 for k in ['busch_pressure','go_extra','ordinary_corner','weather','missing_card']))
check('Zhou guard cannot create a source, override refusal or act after terminal', all(Guard(2).protect('ice_jelly',2,**x)==0 for x in [{'legal':False},{'accept':False},{'alive':False}]) and Guard(2).protect('ice_jelly',0)==0)
g=Guard(2);g.next_personal_input();a=g.protect('ice_jelly',2);g.next_personal_input();check('Zhou guard duration expires by own input, with no new token', a==1 and g.remaining==0 and g.protect('ice_jelly',2)==0)
g=Guard(3);g.next_personal_input('pit');check('Zhou guard is cleared by pit, recovery skip or terminal input', g.remaining==0 and g.protect('driver_smoke',2)==0)
check('Ma passive requires completed Recover then Go, not startup gear or second apex', ma_corner(2,'Recover','Go',0,7,5)==(6,0,True) and ma_corner(2,'Go','Go',0,7,5)==(5,2,False) and ma_corner(2,'Recover','Go',1,7,5)==(5,2,False) and not ma_corner(2,'Recover','Go',0,7,5,previous_completed=False)[2])
check('Ma passive does not consume a second transition at tier one', not ma_corner(1,'Recover','Go',0,7,5,used_lap=1)[2] and ma_corner(3,'Recover','Go',0,7,5,used_lap=1)[2])
check('Ma replay cannot reapply first-apex relief', not ma_corner(3,'Recover','Go',0,7,5,replayed=True)[2])
h=Heat.make(e=2,h=4);ready,cold,used=ma_prepare(h,2,3)
check('Ma prep really forgoes one native cooling opportunity', (cold,used)==(2,1) and len(h.engine)==4 and len(h.locked)==1)
other=h.cool(10);check('Other CN cooling sources cannot reclaim the reserved heat in same Recover', other==1 and len(h.hand)==1 and h.locked==set(h.hand))
h.locked.clear();check('Ma reservation ends after Recover and never changes heat ownership', h.cool(1)==1 and len(h.engine)==6)
check('Ma follow-up Go gives one finite bonus then closes', ready.next_input()==3 and ready.next_input()==0)
check('Ma no-cooling or Go-mode prep cannot generate a free window', all(ma_prepare(Heat.make(),2,**x)[0] is None for x in [{'native_cooling':0},{'native_cooling':3,'mode':'Go'},{'native_cooling':3,'confirm':False}]))
check('Ma no old heat source cannot pay opportunity cost', ma_prepare(Heat.make(e=3,h=0),3,3)[0] is None)
ma_preview=MaReady(2)
check('Ma preview does not consume next Go window', ma_preview.next_input(confirm=False)==0 and ma_preview.ready and ma_preview.next_input()==3)
check('Ma interrupted, short-card or unpaid Go expires instead of extending', all(MaReady(3).next_input(**x)==0 for x in [{'kind':'Recover'},{'kind':'pit'},{'cards_ok':False},{'fees_ok':False},{'alive':False}]))
ma_ledger=Heat.make(e=2,h=4);before_discard=list(ma_ledger.discard);ma_prepare(ma_ledger,2,3)
check('Ma omitted cooling creates no payment receipt or Go overclock reduction', ma_ledger.discard==before_discard and ma_ledger.locked<=set(ma_ledger.hand) and china('Go',4)[1]==3)
check('Tsuchiya tier two discounts first positive apex, tier three first two', tsuchiya_discounts(2,[0,2,3])==[0,1,3] and tsuchiya_discounts(3,[0,2,3])==[0,1,2])
check('Tsuchiya repeated apex cannot take another discount', tsuchiya_discounts(3,[2,2],['C1','C1'])==[1,2])
check('Tsuchiya safe completed apex prepares one first-card bonus, not per-apex stacks', tsuchiya_pending(True)==1 and tsuchiya_pending(False)==0 and tsuchiya_pending(True,spun=True)==0)
check('Tsuchiya smoke rear range remains two then three, excludes ties', smoke(1,2) and not smoke(1,3) and smoke(2,3) and not smoke(3,0))
check('Tsuchiya smoke blocks only own eligible source and expires', not smoke(3,2,nearest=False) and not smoke(3,2,legal=False) and not smoke(3,2,active=False))
check('Zhou protection gives a narrow counter to Tsuchiya without restoring all bonuses', smoke(3,2) and Guard(2).protect('driver_smoke',4)==1)
check('Takumi familiarity is locked before first draw and accepts only map IDs', familiar_choice('C1') and not familiar_choice('C1',started=True) and not familiar_choice('C9'))
def deck():return Deck([SpeedCard('H1',4)],[SpeedCard('P1',1),SpeedCard('P2',2)],[SpeedCard(f'D{i}',i%4+1) for i in range(10)])
d=deck();ret,drawn=d.retain_and_refill(1,'P1',first_lap=True)
check('Takumi first lap can retain one played low card before refilling', ret and 'P1' in [c.id for c in d.hand] and len(d.hand)==7 and len(drawn)==5)
check('Takumi retained card is not also in discard or draw', 'P1' not in [c.id for c in d.discard+d.draw])
d=deck();ret,_=d.retain_and_refill(1,'P2');check('Takumi uses original printed threshold, not effective bonus', not ret)
d=deck();ret,_=d.retain_and_refill(2,'P2');check('Takumi tier two expands choice without extra hand size', ret and len(d.hand)==7)
check('Takumi ignored familiar apex, wrong corner, spin or used lap cannot retain', all(not deck().retain_and_refill(3,'P1',**x)[0] for x in [{'familiar':False},{'ignored':True},{'spun':True},{'used_lap':True},{'alive':False}]))
d=deck();d.played[0].attack=True
other_kinds=[]
for kind in ['heat','trick']:
    other=deck();other.played[0].kind=kind;other_kinds.append(not other.retain_and_refill(3,'P1')[0])
check('Takumi cannot retain ATTACK, heat or trick cards as ordinary speed', not d.retain_and_refill(3,'P1')[0] and all(other_kinds))
d=Deck([SpeedCard(f'H{i}',1) for i in range(7)],[SpeedCard('P1',1)],[]);check('Takumi full hand causes no extra slot or forced discard', not d.retain_and_refill(3,'P1')[0] and len(d.hand)==7)
check('Takumi tiers distinguish first apex, chosen of first two, and two apexes', gutter_targets(1,['C1','C2'],1)==() and gutter_targets(2,['C1','C2'],1)==('C2',) and gutter_targets(3,['C1','C2','C3'])==('C1','C2'))
check('Gutter exemption does not turn overspeed into Japanese exact-speed equality', not (8==7) and gutter_targets(1,['C1'])==('C1',))
h=Heat.make(e=2,h=0);old=set(h.discard);check('Takahashi tier one cannot pull from old discard but tier two may', takahashi_passive(h,1,True,old_discard=old)==0 and takahashi_passive(h,2,True,old_discard=old)==1)
check('Takahashi defense must actually commit, no preview or duplicate reward', takahashi_passive(Heat.make(),3,False,old_hand={'H2'})==0 and takahashi_passive(Heat.make(),3,True,replayed=True,old_hand={'H2'})==0)
h=Heat.make(e=2,h=2);old=set(h.discard);distance,cold,used=takahashi_active(h,2,4,old)
check('Takahashi relinquishes entire positive slipstream to recover at most two old discard', (distance,cold,used)==(0,2,1) and len(h.engine)==4)
h=Heat.make(e=2,h=2);check('Takahashi zero or absent legal slip cannot become free cooling or movement', takahashi_active(h,3,0,set(h.discard))==(0,0,0) and takahashi_active(h,3,4,set(h.discard),legal=False)==(0,0,0))
h=Heat.make(e=2,h=5);check('Takahashi needs real old discard and cannot cool nonexistent source', takahashi_active(h,3,4,set())[2]==0)
h=Heat.make();check('Takahashi preview and terminal preserve resources and uses', takahashi_active(h,3,4,set(h.discard),confirm=False)[2]==0 and takahashi_active(h,3,4,set(h.discard),alive=False)[2]==0)

ok=True
for e in range(8):
 for hand in range(8-e):
  for tier,base_cool in itertools.product([1,2,3],range(4)):
   parameter_cases+=1;h=Heat.make(e,hand);old=set(h.hand);b,x=zhou_recover(h,tier,True,base_cool,old);h.audit();ok &= b+x<=hand and x<=2
   h=Heat.make(e,hand);ready,c,u=ma_prepare(h,tier,base_cool)
   if ready:
    h.cool(20);ok &= len(h.locked)==1 and bool(h.hand)
    h.locked.clear()
   h.audit()
check('All legal CN heat partitions conserve seven and Ma cannot recover reserved cost',ok)
ok=True
for hand_count,tier,value,used,ignored,spun in itertools.product(range(9),[1,2,3],range(1,5),[False,True],[False,True],[False,True]):
 parameter_cases+=1
 d=Deck([SpeedCard(f'H{i}',1) for i in range(hand_count)],[SpeedCard('P',value)],[SpeedCard(f'D{i}',4) for i in range(8)])
 before=hand_count;r,drawn=d.retain_and_refill(tier,'P',used_lap=used,ignored=ignored,spun=spun);d.audit()
 ok &= len(d.hand)==max(before,7) and (not r or value<=tier and not used and not ignored and not spun)
check('Retained ordinary-card identities and original hand limits survive all combinations',ok)
ok=True
for tier,gap,legal,active in itertools.product([1,2,3],range(-2,7),[False,True],[False,True]):
 parameter_cases+=1
 blocked=smoke(tier,gap,legal=legal,active=active)
 ok &= not blocked or legal and active and 0<gap<=CFG['tsuchiya']['smoke_rear_range'][tier-1]
check('Smoke range and lifecycle remain bounded without global opponent debuff',ok)
ok=True
for e in range(8):
 for hand in range(8-e):
  for tier,slip in itertools.product([1,2,3],range(6)):
   parameter_cases+=1;h=Heat.make(e,hand);old=set(h.discard);move,cold,u=takahashi_active(h,tier,slip,old);h.audit();ok &= (u==0 and cold==0 or move==0 and cold<=2 and slip>0)
check('Replacement option never cools without forfeiting a positive slip request',ok)

results={'package':'P3c','kind':'independent_design_model_not_unity','total':len(checks),'passed':sum(x['passed'] for x in checks),'failed':sum(not x['passed'] for x in checks),'skipped':0,'parameter_cases':parameter_cases,'elapsed_seconds':round(time.perf_counter()-start,4),'input_sha256':hashlib.sha256(INPUT.read_bytes()).hexdigest(),'script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'checks':checks,'limits':CFG['limits']}
(HERE/'p3c-model-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:results[k] for k in ['total','passed','failed','skipped','parameter_cases','elapsed_seconds']}))
if results['failed']:
 print(json.dumps([x for x in checks if not x['passed']],ensure_ascii=False));raise SystemExit(1)
