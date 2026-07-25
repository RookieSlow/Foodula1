#!/usr/bin/env python3
"""
Foodula 1 Balance Simulator
============================
Simulates AI-controlled races for all track x team combinations.
Reports lap times, heat accumulation, and corner penalties.

Usage: python balance_simulator.py [--races N] [--track TRACK_ID] [--verbose]
"""

import json
import os
import sys
import random
import math
from collections import defaultdict

# === PATH CONFIG ===
TRACKS_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Configs", "Tracks")

# === CARD DISTRIBUTION ===
SPEED_DECK = [1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4]
INITIAL_HEAT = 3
HAND_SIZE = 7
HEAT_POOL_SIZE = 6

# === TEAM DEFINITIONS ===
# Each team has: handling, durability, and special rules

TEAMS = {
    "uk_tea": {
        "name": "UK 英国",
        "emoji": "[UK]",
        "handling": 0,          # No innate handling bonus (gets from tech tree)
        "durability": 7,
        "heat_pool": 6,
        "straight_boost": 0,    # No innate boost
        "corner_penalty": 0,    # No extra penalty
        "corner_limit_bonus": 0, # Tech effect: +25% to tech bonuses (simplified as +1 at L2+)
        "slipstream_bonus": 0,
        "trick_uses": 3,        # +1 from Royal Engineer
        "recovery_skip": True,  # Standard recovery
    },
    "ger_bier": {
        "name": "DE 德国",
        "emoji": "[DE]",
        "handling": 1,
        "durability": 8,
        "heat_pool": 8,
        "straight_boost": 0,
        "corner_penalty": 0,
        "corner_limit_bonus": 0,
        "slipstream_bonus": 0,
        "straight_cruise": True,  # 1-value cards → 2 on straights
        "recovery_light": True,   # Recovery: 1-gear continue instead of skip
    },
    "ita_pasta_pizza": {
        "name": "IT 意大利",
        "emoji": "[IT]",
        "handling": 2,
        "durability": 6,
        "heat_pool": 6,
        "straight_boost": 0,
        "corner_penalty": 0,
        "corner_limit_bonus": 2,  # Passione in Curva: +2 to corner limits
        "exit_boost": True,       # +1 to first speed card after corner
        "slipstream_bonus": 0,
    },
    "usa_burger_cola": {
        "name": "US 美国",
        "emoji": "[US]",
        "handling": -1,
        "durability": 8,
        "heat_pool": 8,
        "straight_boost": 1,      # Straight Roar: +1 per card on straights
        "corner_penalty": 1,      # +1 extra heat when over limit
        "corner_limit_bonus": 0,
        "slipstream_bonus": 1,    # +1 slipstream effectiveness
    },
    "chn_dim_sum": {
        "name": "CN 中国",
        "emoji": "[CN]",
        "handling": -1,
        "durability": 7,
        "heat_pool": 6,
        "straight_boost": 0,
        "corner_penalty": 0,
        "corner_limit_bonus": -1,  # 弯速差: -1 to corner limits
        "ev_dual_gear": True,      # 2-gear system
        "pit_required": True,      # Must pit every 3 laps
    },
    "jpn_sushi_ramen": {
        "name": "JP 日本",
        "emoji": "[JP]",
        "handling": 1,
        "durability": 7,
        "heat_pool": 7,
        "straight_boost": 0,
        "corner_penalty": 0,
        "corner_limit_bonus": 1,   # Good corner performance
        "slipstream_bonus": 0,
        "trick_cards": True,       # Special trick card draw per race
    },
}


class PlayerState:
    """Runtime state for one car in the race."""
    def __init__(self, team_id, team_def):
        self.team_id = team_id
        self.team = team_def
        self.position = 0          # Cell index
        self.lap = 0
        self.gear = 2              # Start in gear 2
        self.cn_gear = "go"        # CN only: "go" or "recover"
        self.cn_consecutive = 0    # CN only: consecutive same-gear count
        self.cn_battery = 3        # CN only: battery charges (decrements per lap)
        self.cn_pitted_this_lap = False

        # Deck management
        self.deck = list(SPEED_DECK)
        self.discard = []
        self.hand = []
        self.heat_pool = team_def["heat_pool"]
        self.heat_in_discard = 0

        # Stats
        self.total_turns = 0
        self.total_heat_generated = 0
        self.total_corner_penalties = 0
        self.total_over_speed = 0
        self.corner_checks_passed = 0
        self.corner_checks_failed = 0
        self.engine_stalls = 0
        self.engine_blowups = 0
        self.blowup_counter = 0
        self.finished = False
        self.turn_log = []

    def effective_corner_limit(self, base_limit):
        """Apply team handling bonus to a corner's speed limit."""
        bonus = self.team.get("corner_limit_bonus", 0)
        return max(1, min(4, base_limit + bonus))

    def draw_hand(self):
        """Draw up to HAND_SIZE cards."""
        while len(self.hand) < HAND_SIZE:
            if not self.deck:
                if not self.discard:
                    break
                self.deck = self.discard[:]
                random.shuffle(self.deck)
                self.discard = []
            self.hand.append(self.deck.pop())
        random.shuffle(self.hand)

    def play_cards(self, count, on_straights=False):
        """
        Play `count` speed cards from hand.
        Returns list of card values (with team modifiers applied).
        on_straights: whether the entire movement is on straight cells.
        """
        # Sort hand: prefer higher values, but keep heat (0) unpicked
        speed_cards = sorted([c for c in self.hand if c > 0], reverse=True)
        played = speed_cards[:count]
        shortage = count - len(played)

        # Remove played cards from hand
        for c in played:
            self.hand.remove(c)

        # Engine stall: not enough speed cards
        if shortage > 0:
            for _ in range(shortage):
                if self.heat_pool > 0:
                    self.heat_pool -= 1
                    self.discard.append(0)  # Heat card to discard
                    self.heat_in_discard += 1
                    self.total_heat_generated += 1
            self.engine_stalls += shortage

        # Apply modifiers
        result = []
        for v in played:
            mv = v
            # US straight boost
            if on_straights and self.team.get("straight_boost", 0) > 0:
                mv += self.team["straight_boost"]
            # DE straight cruise: 1 → 2 on straights
            if on_straights and self.team.get("straight_cruise") and v == 1:
                mv = 2
            result.append(mv)

        return result

    def discard_played(self, cards):
        """Move played cards to discard pile."""
        self.discard.extend(cards)

    def cooldown(self):
        """Step 5: Gear-based cooldown."""
        if self.team.get("ev_dual_gear"):
            return  # CN uses Recover instead

        if self.gear == 1:
            cooled = 3
        elif self.gear == 2:
            cooled = 1
        else:
            cooled = 0

        for _ in range(cooled):
            if 0 in self.hand:
                self.hand.remove(0)
                self.heat_pool += 1

    def cn_recover_cooldown(self):
        """CN Recover gear: 1 card + cooldown based on consecutive count."""
        cooled = max(0, 3 - self.cn_consecutive + 1)
        for _ in range(cooled):
            if 0 in self.hand:
                self.hand.remove(0)
                self.heat_pool += 1

    def checkpoint_blowup(self):
        """Check if engine blows up."""
        if self.heat_pool <= 0:
            self.blowup_counter += 1
            if self.blowup_counter >= 3:
                self.finished = True
                return True
        return False


def load_track(track_id):
    """Load a track JSON and return TrackConfig-like dict."""
    path = os.path.join(TRACKS_DIR, f"{track_id}.json")
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def get_apexes(cells):
    """Return list of (index, cornerId, cornerLimit, cornerLevel) for all apex cells."""
    apexes = []
    for c in cells:
        if c.get("type") == "corner" and c.get("isApex"):
            apexes.append({
                "index": c["index"],
                "cornerId": c["cornerId"],
                "cornerLimit": c["cornerLimit"],
                "cornerLevel": c["cornerLevel"],
            })
    return apexes


def find_next_apex(cells, from_pos, lookahead=20):
    """Find the next apex cell within lookahead distance."""
    n = len(cells)
    for i in range(1, lookahead + 1):
        idx = (from_pos + i) % n
        cell = cells[idx]
        if cell.get("type") == "corner" and cell.get("isApex"):
            return {
                "distance": i,
                "index": idx,
                "cornerId": cell["cornerId"],
                "cornerLimit": cell["cornerLimit"],
                "cornerLevel": cell["cornerLevel"],
            }
    return None


def is_on_straight(cells, pos):
    """Check if a cell is a straight (or start_finish or pit)."""
    return cells[pos]["type"] in ("straight", "start_finish", "pit_entry", "pit_exit")


def all_straight_path(cells, from_pos, distance):
    """Check if the entire movement path is on straight cells."""
    n = len(cells)
    for i in range(1, distance + 1):
        idx = (from_pos + i) % n
        if not is_on_straight(cells, idx):
            return False
    return True


def simulate_race(track_id, team_id, team_def, verbose=False):
    """Simulate one AI-controlled race. Returns stats dict."""
    track = load_track(track_id)
    cells = track["cells"]
    total_cells = len(cells)
    total_laps = track["laps"]
    apexes = get_apexes(cells)

    player = PlayerState(team_id, team_def)
    target_cells = total_cells * total_laps  # finish line = pass start/finish N times

    if verbose:
        print(f"\n{'='*60}")
        print(f"Track: {track['trackName']} ({total_cells} cells x {total_laps} laps)")
        print(f"Team: {team_def['emoji']} {team_def['name']}")
        print(f"{'='*60}")

    turns = 0
    max_turns = 200  # Safety limit

    while turns < max_turns and not player.finished:
        turns += 1
        player.total_turns = turns

        # CN battery check
        if team_def.get("ev_dual_gear") and player.lap > 0 and not player.cn_pitted_this_lap:
            player.cn_battery = max(0, 3 - player.lap)

        # === STEP 1: Choose Gear ===
        # Look ahead for corners within expected movement range
        next_apex = find_next_apex(cells, player.position, lookahead=20)
        # Also look for the NEXT apex after that
        next_next_apex = None
        if next_apex:
            peek_start = (next_apex["index"] + 1) % total_cells
            next_next_apex = find_next_apex(cells, peek_start, lookahead=15)

        if team_def.get("ev_dual_gear"):
            # === CN 2-GEAR AI ===
            # Strategy: Go aggressively. 1st Go=3 cards, 2nd+ Go=4 cards.
            # CN's design: fast on straights, pay corner penalties as "tax".
            # Recover only when: consecutive Go >= 4, or heat pool critically low.
            # MAX 2 consecutive Recovers — force Go cycle to avoid Recover-lock.
            should_recover = False

            go_card_count = 4 if player.cn_consecutive >= 2 else 3

            # How many consecutive Recovers have we done?
            cn_recovers = getattr(player, "_cn_recover_streak", 0)

            # Force Go if we've been Recovering too long
            if cn_recovers >= 2:
                should_recover = False
            elif player.cn_consecutive >= 4:
                should_recover = True
            elif player.heat_pool <= 1 and cn_recovers < 2:
                should_recover = True
            elif player.blowup_counter >= 1 and cn_recovers < 2:
                should_recover = True

            if should_recover:
                player.cn_gear = "recover"
                player._cn_recover_streak = cn_recovers + 1
            else:
                player.cn_gear = "go"
                player._cn_recover_streak = 0

            # Track consecutive
            if turns > 1:
                prev_gear = getattr(player, "_prev_cn_gear", None)
                if prev_gear == player.cn_gear:
                    player.cn_consecutive += 1
                else:
                    player.cn_consecutive = 1
            else:
                player.cn_consecutive = 1
            player._prev_cn_gear = player.cn_gear

            # CN gear effects
            if player.cn_gear == "go":
                # 1st consecutive Go = 3 cards, 2nd+ = 4 cards (boosted output)
                effective_gear = 4 if player.cn_consecutive >= 2 else 3
                # Consecutive go penalty (heat cost)
                go_heat = max(0, player.cn_consecutive - 1)
                if go_heat > 0:
                    for _ in range(go_heat):
                        if player.heat_pool > 0:
                            player.heat_pool -= 1
                            player.discard.append(0)
                            player.heat_in_discard += 1
                            player.total_heat_generated += 1
            else:  # recover
                effective_gear = 1
                player.cn_recover_cooldown()

        else:
            # === Standard 4-gear AI ===
            # Strategy: estimate average card value, choose gear such that
            # expected total speed <= upcoming corner limit.
            avg_card_value = 2.3  # rough average of speed deck

            if next_apex is None:
                target_gear = 4
            else:
                eff_limit = player.effective_corner_limit(next_apex["cornerLimit"])
                dist = next_apex["distance"]

                # How many cards can we safely play?
                max_safe_cards = max(1, int(eff_limit / avg_card_value))

                if dist <= 3:
                    # Very close to corner: be conservative
                    target_gear = min(max_safe_cards, 2)
                elif dist <= 6:
                    # Approaching corner
                    target_gear = min(max_safe_cards + 1, 3)
                elif dist <= 12:
                    target_gear = min(max_safe_cards + 1, 4)
                else:
                    target_gear = 4

                # If next_next_apex is very close after, be more careful
                if next_next_apex and next_next_apex["distance"] + dist <= 10:
                    target_gear = min(target_gear, 3)

            # Smooth gear changes: max +-2 per turn
            gear_change = target_gear - player.gear
            if gear_change > 2:
                target_gear = player.gear + 2
            elif gear_change < -2:
                target_gear = player.gear - 2

            # Avoid gear 4 if heat pool is low (risk management)
            if player.heat_pool <= 2 and target_gear >= 3:
                target_gear = max(1, target_gear - 1)

            player.gear = max(1, min(4, target_gear))
            effective_gear = player.gear

        # === STEP 2: Draw ===
        player.draw_hand()

        # === STEP 3: Play Cards ===
        on_straights = all_straight_path(cells, player.position, effective_gear * 4)
        played = player.play_cards(effective_gear, on_straights=on_straights)
        total_speed = sum(played)

        if verbose:
            gear_str = f"G{player.gear}" if not team_def.get("ev_dual_gear") else f"{player.cn_gear.upper()}"
            print(f"  Turn {turns}: {gear_str} speed={total_speed} cards={played} pos={player.position}→", end="")

        # === STEP 4: Move ===
        old_pos = player.position
        for step in range(1, total_speed + 1):
            new_idx = (old_pos + step) % total_cells
            cell = cells[new_idx]

            # Check start/finish crossing
            if cell["type"] == "start_finish" and new_idx != old_pos:
                player.lap += 1
                if team_def.get("ev_dual_gear"):
                    player.cn_battery = max(0, player.cn_battery - 1)
                    player.cn_pitted_this_lap = False

        player.position = (old_pos + total_speed) % total_cells

        if verbose:
            print(f"{player.position} (lap {player.lap})")

        # === STEP 5: Cooldown ===
        if not team_def.get("ev_dual_gear"):
            player.cooldown()

        # === STEP 6: Corner Check (apex-based) ===
        path_len = total_speed
        for step in range(1, path_len + 1):
            idx = (old_pos + step) % total_cells
            cell = cells[idx]
            if cell.get("type") == "corner" and cell.get("isApex"):
                eff_limit = player.effective_corner_limit(cell["cornerLimit"])
                if total_speed > eff_limit:
                    over = total_speed - eff_limit
                    # US corner penalty
                    extra_heat = team_def.get("corner_penalty", 0)
                    total_penalty = over + extra_heat
                    player.total_heat_generated += total_penalty
                    player.total_over_speed += over
                    player.total_corner_penalties += total_penalty
                    player.corner_checks_failed += 1
                    # Pay heat from pool
                    for _ in range(total_penalty):
                        if player.heat_pool > 0:
                            player.heat_pool -= 1
                            player.discard.append(0)
                            player.heat_in_discard += 1
                else:
                    player.corner_checks_passed += 1

                    # IT exit boost: next turn first card +1
                    # (simplified: just note it passed cleanly)

        # === STEP 7: Slipstream (simplified - no opponent in solo sim) ===

        # === STEP 8: Discard (optional) - skip ===

        # === STEP 9: End turn ===
        player.discard_played([c for c in played if c > 0])  # Only real speed cards to discard

        # Draw to fill hand for next turn
        # (done at step 2 of next turn)

        # Check finish
        if player.lap >= total_laps:
            player.finished = True

    # Collect final stats
    stats = {
        "track_id": track_id,
        "team_id": team_id,
        "turns": turns,
        "finished": player.finished,
        "heat_generated": player.total_heat_generated,
        "corner_penalties": player.total_corner_penalties,
        "over_speed": player.total_over_speed,
        "checks_passed": player.corner_checks_passed,
        "checks_failed": player.corner_checks_failed,
        "engine_stalls": player.engine_stalls,
        "blowup_counter": player.blowup_counter,
        "avg_speed": (len(cells) * total_laps) / max(1, turns),
    }
    return stats

def run_balance_suite(num_races=10, track_filter=None, verbose=False):
    """Run full balance simulation suite."""
    print(f"\n{'='*70}")
    print(f"==Foodula 1 Balance Simulator")
    print(f"=={num_races} races per track x team combination")
    print(f"{'='*70}\n")

    # Get track list
    if track_filter:
        track_ids = [track_filter]
    else:
        # Only primary 6 tracks
        track_ids = [
            "silverstone_afternoon_tea",
            "nurburgring_bier",
            "monza_pasta",
            "indianapolis_burger",
            "shanghai_dim_sum",
            "suzuka_sushi",
        ]

    all_results = []

    for track_id in track_ids:
        track = load_track(track_id)
        print(f"\n{'-'*70}")
        print(f"[TRACK] {track['trackName']} ({track['gameCellCount']} cells x {track['laps']} laps = {track['gameCellCount'] * track['laps']} total)")
        print(f"{'-'*70}")

        track_results = []

        for team_id, team_def in TEAMS.items():
            race_stats = []
            for r in range(num_races):
                if verbose:
                    print(f"\n--- Race {r+1}: {team_def['emoji']} {team_def['name']} ---")
                stats = simulate_race(track_id, team_id, team_def, verbose=verbose)
                race_stats.append(stats)

            # Aggregate
            avg = {k: sum(s[k] for s in race_stats) / len(race_stats)
                   for k in ["turns", "heat_generated", "corner_penalties",
                              "checks_failed", "engine_stalls", "avg_speed"]}
            avg["finish_rate"] = sum(1 for s in race_stats if s["finished"]) / len(race_stats)

            # Compute performance score (higher = better)
            # Primary: fewer turns = faster race. Secondary: heat efficiency.
            # Turns dominate - you win by finishing first, not by having the coolest engine.
            perf_score = (
                (1.0 / max(1, avg["turns"])) * 1000  # speed primary
                - avg["heat_generated"] * 0.5          # heat is a cost, but minor
                - avg["engine_stalls"] * 5              # stalls are bad
                + avg["finish_rate"] * 100              # finishing is important
            )

            avg["perf_score"] = perf_score
            avg["team_id"] = team_id
            avg["team_name"] = team_def["name"]
            avg["team_emoji"] = team_def["emoji"]
            avg["track_id"] = track_id
            avg["track_name"] = track["trackName"]
            track_results.append(avg)

            status = "OK" if avg["finish_rate"] >= 0.9 else "WARN" if avg["finish_rate"] >= 0.5 else "FAIL"
            print(f"  {status} {team_def['emoji']} {team_def['name']:6s} | "
                  f"turns={avg['turns']:5.1f} | "
                  f"heat={avg['heat_generated']:5.1f} | "
                  f"stalls={avg['engine_stalls']:4.1f} | "
                  f"speed={avg['avg_speed']:5.1f} | "
                  f"score={perf_score:6.1f}")

        # Sort by performance score
        track_results.sort(key=lambda x: x["perf_score"], reverse=True)
        all_results.extend(track_results)

        print(f"\n  Rank on this track:")
        for i, r in enumerate(track_results):
            print(f"    {i+1}. {r['team_emoji']} {r['team_name']:6s} (score: {r['perf_score']:.1f})")

    # === CROSS-TRACK SUMMARY ===
    print(f"\n\n{'#'*70}")
    print(f"==CROSS-TRACK SUMMARY")
    print(f"{'#'*70}\n")

    # Per-team average across all tracks
    print("Team Performance (averaged across all 6 tracks):")
    print(f"{'Team':<12} {'Turns':>7} {'Heat':>7} {'Stalls':>7} {'Speed':>7} {'Score':>8}")
    print("-" * 55)

    team_summaries = []
    for team_id, team_def in TEAMS.items():
        team_results = [r for r in all_results if r["team_id"] == team_id]
        if not team_results:
            continue
        avg = {k: sum(r[k] for r in team_results) / len(team_results)
               for k in ["turns", "heat_generated", "engine_stalls", "perf_score", "avg_speed"]}
        avg["team_id"] = team_id
        avg["team_name"] = team_def["name"]
        avg["team_emoji"] = team_def["emoji"]
        team_summaries.append(avg)

    team_summaries.sort(key=lambda x: x["perf_score"], reverse=True)
    for i, s in enumerate(team_summaries):
        print(f"{i+1}. {s['team_emoji']} {s['team_name']:<8s} "
              f"{s['turns']:6.1f} {s['heat_generated']:6.1f} "
              f"{s['engine_stalls']:6.1f} {s['avg_speed']:6.1f} {s['perf_score']:7.1f}")

    # Track difficulty ranking
    print(f"\nTrack Difficulty (higher score = easier for all teams):")
    track_summaries = []
    for track_id in track_ids:
        tr = [r for r in all_results if r["track_id"] == track_id]
        if not tr:
            continue
        avg_score = sum(r["perf_score"] for r in tr) / len(tr)
        avg_heat = sum(r["heat_generated"] for r in tr) / len(tr)
        track_summaries.append({
            "track_name": tr[0]["track_name"],
            "avg_score": avg_score,
            "avg_heat": avg_heat,
        })
    track_summaries.sort(key=lambda x: x["avg_score"], reverse=True)
    for i, t in enumerate(track_summaries):
        print(f"  {i+1}. {t['track_name']:<25s} score={t['avg_score']:.1f} avg_heat={t['avg_heat']:.1f}")

    return all_results


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Foodula 1 Balance Simulator")
    parser.add_argument("--races", type=int, default=10, help="Number of races per combination")
    parser.add_argument("--track", type=str, default=None, help="Only simulate this track ID")
    parser.add_argument("--verbose", action="store_true", help="Print per-turn logs")
    parser.add_argument("--seed", type=int, default=42, help="Random seed")
    args = parser.parse_args()

    random.seed(args.seed)
    run_balance_suite(num_races=args.races, track_filter=args.track, verbose=args.verbose)
