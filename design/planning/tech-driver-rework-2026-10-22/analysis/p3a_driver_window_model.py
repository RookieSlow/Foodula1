"""Independent P3a design model, not imported production C# or a Unity test."""
from dataclasses import dataclass, field
from copy import deepcopy
from pathlib import Path
from itertools import product
import hashlib
import json
import time

HERE = Path(__file__).parent
INPUT_PATH = HERE / 'p3a-driver-model-inputs.json'
CFG = json.loads(INPUT_PATH.read_text(encoding='utf-8'))


@dataclass
class State:
    kind: str
    tier: int
    uses: int
    engine: set = field(default_factory=lambda: set(range(8)))
    hand_heat: set = field(default_factory=set)
    discarded_heat: set = field(default_factory=set)
    speeds: int = 5
    discarded_speed: int = 0
    receipts: set = field(default_factory=set)
    receipt_turn: int = -100
    start_turn: int = -100
    last_tick: int = -100
    target: str = ''
    gap: int = 0
    active: bool = False
    source: str = ''
    used_event_turns: set = field(default_factory=set)


def make(kind, tier=1):
    c = CFG['drivers'][kind]
    return State(kind, tier, CFG['uses']['ordinary_by_active_tier'][tier-1] +
                 (CFG['uses']['uk_extra'] if c['team'] == 'UK' else 0))


def valid_pool(s):
    a, b, c = s.engine, s.hand_heat, s.discarded_heat
    return not (a & b or a & c or b & c) and a | b | c == set(range(8))


def pay(s, dest):
    if not s.engine:
        return False
    if s.receipt_turn != s.last_tick:
        s.receipts.clear()
        s.receipt_turn = s.last_tick
    token = min(s.engine)
    s.engine.remove(token)
    getattr(s, dest).add(token)
    s.receipts.add(token)
    return True


def refund_current_receipts(s):
    t = deepcopy(s)
    for token in t.receipts:
        for dest in (t.hand_heat, t.discarded_heat):
            if token in dest:
                dest.remove(token)
                t.engine.add(token)
    return t


def start(s, turn=1, gear=2, target='front', gap=3, source='cruise',
          tech_level=0, mode='free', alive=True, input_open=True, preview=False):
    original = deepcopy(s)
    if preview:
        return original, 0, False
    duration = 1 if s.kind == 'mansell' else 2 if s.kind == 'schumacher' else CFG['drivers']['vettel']['duration'][s.tier-1]-1
    active_now = s.active and turn <= s.start_turn + duration
    if (mode == 'tutorial' or not alive or not input_open or s.uses <= 0 or
            active_now or turn <= s.start_turn):
        return original, 0, False
    c = CFG['drivers'][s.kind]
    if s.kind in ('hunt', 'mansell') and not (target and 0 < gap <= c['target_range']):
        return original, 0, False
    if s.kind == 'schumacher' and not (1 <= gear <= c['preparation_gear_max']):
        return original, 0, False
    if s.kind == 'schumacher' and s.speeds - 1 < gear:
        return original, 0, False
    if s.kind == 'vettel' and not (source == 'cruise' or source == 'beer' and tech_level >= 1 or
                                   source == 'corner_exit' and tech_level >= 2):
        return original, 0, False
    if s.kind != 'vettel' and s.speeds < 2:
        return original, 0, False
    if s.kind in ('mansell', 'vettel') and not s.engine:
        return original, 0, False
    t = deepcopy(s)
    t.uses -= 1
    t.start_turn = turn
    t.last_tick = turn
    t.target = target
    t.gap = gap
    t.source = source
    if s.kind != 'vettel':
        t.speeds -= 1
        t.discarded_speed += 1
    t.active = s.kind != 'hunt'
    if s.kind == 'hunt':
        return t, c['active_bonus'][s.tier-1], True
    if s.kind == 'mansell':
        pay(t, 'discarded_heat')
        return t, c['first_bonus'][s.tier-1], True
    if s.kind == 'vettel':
        pay(t, 'discarded_heat')
    return t, 0, True


def tick(s, turn, gear=3, target='front', gap=2, alive=True,
         mode='free', recovering=False, preview=False):
    t = deepcopy(s)
    if preview or not s.active or turn <= s.last_tick:
        return t, 0
    t.last_tick = turn
    if not alive or mode == 'tutorial' or recovering:
        t.active = False
        return t, 0
    c = CFG['drivers'][s.kind]
    if s.kind == 'mansell':
        t.active = False
        if (turn == s.start_turn + 1 and target == s.target and 0 < gap < s.gap
                and pay(t, 'discarded_heat')):
            return t, c['second_bonus'][s.tier-1]
    elif s.kind == 'schumacher':
        elapsed = turn - s.start_turn
        if (elapsed not in (1, 2) or turn != s.last_tick+1 or gear < 3 or not pay(t, 'discarded_heat')):
            t.active = False
            return t, 0
        t.active = elapsed < 2
        return t, c['step_bonus'][s.tier-1]
    return t, 0


def amplify(s, turn, source, native_value, real=True, owner=True,
            origin='native', alive=True, preview=False):
    t = deepcopy(s)
    c = CFG['drivers']['vettel']
    if (preview or not s.active or s.kind != 'vettel' or not alive or not real or
            not owner or origin != 'native' or source != s.source or native_value <= 0 or
            not (s.start_turn <= turn < s.start_turn + c['duration'][s.tier-1]) or
            turn in s.used_event_turns):
        return t, 0
    t.used_event_turns.add(turn)
    return t, c['boost_per_native_event']


def passive(s, tier, condition, heat_threshold=0, source='',
            lap_used=False, preview=False):
    """Only passive payout identity/cap, not production trigger timing."""
    t = deepcopy(s)
    if preview or lap_used or not condition:
        return t, 0
    if s.kind == 'schumacher':
        return t, int(heat_threshold >= CFG['drivers']['schumacher']['passive_paid_threshold'][tier-1])
    if s.kind == 'vettel' and source not in CFG['drivers']['vettel']['passive_sources'][tier-1]:
        return t, 0
    if s.kind == 'hunt':
        return t, 1
    if t.hand_heat:
        token = min(t.hand_heat)
        t.hand_heat.remove(token)
        t.engine.add(token)
        return t, 1
    return t, 0


def run():
    started = time.perf_counter()
    results = []
    cases = 0

    def check(name, condition):
        results.append({'name': name, 'passed': bool(condition)})

    check('英国次数为2/2/3 德国为1/1/2',
          [[make(k, n).uses for n in (1, 2, 3)] for k in CFG['drivers']] ==
          [[2, 2, 3], [2, 2, 3], [1, 1, 2], [1, 1, 2]])
    all_preview, all_closed, all_terminal, all_pool, caps, same_turn = [], [], [], [], [], []
    for kind, tier, count, gap in product(CFG['drivers'], (1, 2, 3), range(9), range(-1, 9)):
        cases += 1
        s = make(kind, tier)
        s.engine, s.discarded_heat = set(range(count)), set(range(count, 8))
        t, b, ok = start(s, gap=gap)
        all_pool.append(valid_pool(t))
        p, pb, pok = start(s, gap=gap, preview=True)
        all_preview.append((p, pb, pok) == (s, 0, False))
        all_closed.append(start(s, input_open=False) == (s, 0, False))
        all_terminal.append(start(s, alive=False) == (s, 0, False))
        if ok:
            caps.append(t.uses == s.uses-1 and t.discarded_speed + t.speeds == 5)
            same_turn.append(not start(t)[2])
    check('枚举启动与付款守恒', all(all_pool))
    check('预览不提交或消费', all(all_preview))
    check('输入关闭拒绝', all(all_closed))
    check('终态拒绝启动', all(all_terminal))
    check('启动只扣一次次数及原速度牌', all(caps))
    check('重复启动不会再次支付', all(same_turn))
    check('教程不启动 生涯候选可运行但不写存档',
          all(not start(make(k), mode='tutorial')[2] and start(make(k), mode='career')[2]
              for k in CFG['drivers']))
    check('不足两张速度牌时不半笔扣费', all(
          start(State(k, 1, 2, speeds=1)) == (State(k, 1, 2, speeds=1), 0, False)
          for k in ('hunt', 'mansell', 'schumacher')))
    check('亨特上限是整次2/3/3而非每张牌加成',
          [start(make('hunt', n))[1] for n in (1, 2, 3)] == [2, 3, 3])
    check('环线跨终点按绝对进度而非取模伪造套圈目标',
          start(make('hunt'), gap=101-99)[2] and not start(make('hunt'), gap=1-99)[2] and
          not start(make('hunt'), gap=201-99)[2])

    a, bonus, _ = start(make('mansell', 2), gap=6)
    good, b = tick(a, 2, gap=4)
    check('曼森实际追近后才兑现', bonus == 2 and b == 3 and len(good.engine) == 6)
    check('曼森换目标 不追近 已超越 均停止',
          all(tick(a, 2, gap=g, target=target)[1] == 0 for g, target in
              [(6, 'front'), (7, 'front'), (0, 'front'), (-1, 'front'), (4, 'other')]))
    check('曼森过期或恢复不延长窗口', tick(a, 3)[1] == 0 and tick(a, 2, recovering=True)[1] == 0)
    check('曼森二次兑现不能同回合刷取', tick(good, 2)[1] == 0)
    poor = deepcopy(a)
    poor.discarded_heat |= poor.engine
    poor.engine.clear()
    check('曼森第二笔不足不赠送移动', tick(poor, 2)[1] == 0 and valid_pool(tick(poor, 2)[0]))

    a, _, _ = start(make('schumacher', 2), gear=2)
    b, m1 = tick(a, 2)
    c, m2 = tick(b, 3)
    check('舒马赫需要准备然后两笔真实付款', m1+m2 == 6 and len(c.engine) == 6 and not c.active)
    check('舒马赫不能高挡准备', not start(make('schumacher'), gear=3)[2])
    check('舒马赫准备挡位也需要足够原速度牌',
          not start(State('schumacher', 1, 1, speeds=2), gear=2)[2] and
          start(State('schumacher', 1, 1, speeds=2), gear=1)[2])
    check('舒马赫低挡执行会失效', tick(a, 2, gear=2)[1] == 0 and not tick(a, 2, gear=2)[0].active)
    check('舒马赫失败或跳过后不能第三回合补领', tick(tick(a, 2, recovering=True)[0], 3)[1] == 0 and tick(a, 3)[1] == 0)
    check('舒马赫没有永久免弯道热或无限重放', tick(c, 4)[1] == 0 and tick(b, 2)[1] == 0)
    check('跨个人回合的旧真实付款不进入本回合返还',
          len(refund_current_receipts(c).engine) == 7 and len(refund_current_receipts(c).discarded_heat) == 1)

    a, _, _ = start(make('vettel', 2), source='beer', tech_level=1)
    b, m1 = amplify(a, 1, 'beer', 2)
    c, m2 = amplify(b, 2, 'beer', 2)
    check('维特尔两回合封顶4格并锁来源', m1+m2 == 4 and amplify(a, 1, 'cruise', 1)[1] == 0)
    check('同回合三次黑啤仅增幅一次', amplify(b, 1, 'beer', 2)[1] == 0)
    check('未结算 空值 他人 非原生都不增幅', all(
          amplify(a, 1, 'beer', val, **kw)[1] == 0 for val, kw in
          [(0, {}), (2, {'real': False}), (2, {'owner': False}),
           (2, {'origin': 'driver'}), (2, {'preview': True}), (2, {'alive': False})]))
    check('维特尔窗口不因未触发而顺延', amplify(a, 3, 'beer', 2)[1] == 0)
    check('无科技仍可选基础巡航 未解锁来源拒绝',
          start(make('vettel'), source='cruise')[2] and
          not start(make('vettel'), source='beer')[2] and
          not start(make('vettel'), source='corner_exit', tech_level=1)[2])
    old = make('vettel')
    old.engine.remove(7)
    old.discarded_heat.add(7)
    old, _, _ = start(old)
    check('旧热不能冒充当前付款返还', refund_current_receipts(old).discarded_heat == {7} and len(refund_current_receipts(old).engine) == 7)
    one = refund_current_receipts(a)
    check('德国返还当前付款收据后不能再次增加热牌',
          len(one.engine) == 8 and refund_current_receipts(one) == one and valid_pool(one))

    combined = deepcopy(a)
    for _ in range(3):
        pay(combined, 'discarded_heat')
    combined, amp = amplify(combined, 1, 'beer', 2)
    before_uses = combined.uses
    returned = refund_current_receipts(combined)
    check('德国三次黑啤加维特尔增幅为8格而非12格', 3*2+amp == 8)
    check('回热不重置车手次数或事件计数',
          returned.uses == before_uses and amplify(returned, 1, 'beer', 2)[1] == 0)
    check('过期后第二次发动另扣次数 不刷新旧窗口',
          start(c, turn=3, source='beer', tech_level=1)[2] is False and
          start(start(make('vettel', 3))[0], turn=3)[0].uses == 0)
    check('四类被动的每圈已用门控', all(
          passive(make(k), 1, True, heat_threshold=9, source='cruise', lap_used=True)[1] == 0
          for k in CFG['drivers']))
    s = make('vettel')
    pay(s, 'hand_heat')
    check('被动冷却只搬真实手牌热且预览不搬',
          len(passive(s, 1, True, source='cruise')[0].engine) == 8 and
          passive(s, 1, True, source='cruise', preview=True)[0] == s)
    check('维特尔被动三级只扩大合法来源集合',
          [passive(s, n, True, source='beer')[1] for n in (1, 2, 3)] == [0, 1, 1] and
          [passive(s, n, True, source='corner_exit')[1] for n in (1, 2, 3)] == [0, 0, 1])
    check('调校折扣需达到真实弯道付款门槛',
          [passive(make('schumacher'), n, True, heat_threshold=1)[1] for n in (1, 2, 3)] == [0, 1, 1])

    result = {'kind': 'independent_design_model_not_unity', 'total': len(results),
              'passed': sum(x['passed'] for x in results),
              'failed': sum(not x['passed'] for x in results), 'skipped': 0,
              'parameter_cases': cases, 'elapsed_seconds': round(time.perf_counter()-started, 4),
              'input_sha256': hashlib.sha256(INPUT_PATH.read_bytes()).hexdigest(),
              'script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'checks': results,
              'limits': CFG['scope'], 'unity_tests_run': 0}
    (HERE/'p3a-model-results.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('checks', 'limits')}, ensure_ascii=False))
    assert result['failed'] == 0, [x for x in results if not x['passed']]


if __name__ == '__main__':
    run()
