#!/usr/bin/env python3
'''Census the compiled destruction-slot calls against the live effects closure.'''
import argparse
import collections
import json
import re
from pathlib import Path

CHAPTERS = ('C1', 'C1B', 'C1C', 'C2', 'C2B', 'C3', 'C4', 'C5')
LOCAL_CHOREOGRAPHY = {
    'brokezep_fall0', 'brokezep_fall1', 'brokezep_fall2', 'destroy_in_water',
    'destroy_the_escapeboat', 'dtleng_destroyed.flt', 'dtzep_rocksright',
    'dx_destroy_nacelle_left.flt', 'dx_destroy_nacelle_right.flt',
    'firetruck1_start', 'firetruck2_start', 'firetruck3_start', 'firetruck4_start',
    'genx12', 'goose_cooked', 'goose_down_lwing', 'goose_down_rwing',
    'hkzep_rocksleft', 'hkzep_rocksright', 'maingun_flying_parts',
    'spruce_enginedest.flt', 'zep_engine_boom',
}


def load_archive(path):
    out = []
    for json_path in sorted(path.glob('*.json')):
        doc = json.loads(json_path.read_text(encoding='utf-8-sig'))
        if isinstance(doc, dict) and 'sequences' in doc:
            doc['_source'] = json_path.as_posix()
            out.append(doc)
    return out


def effect_names(path):
    source = path.read_text(encoding='utf-8-sig')
    match = re.search(
        r'EffectAnimNames\s*=\s*\{(?P<body>.*?)\n\s*\};', source, re.DOTALL
    )
    if not match:
        raise ValueError(f'{path}: EffectAnimNames initializer not found')
    body = re.sub(r'//.*', '', match.group('body'))
    names = re.findall(r'\x22([^\x22]+)\x22', body)
    if not names or len(names) != len(set(names)):
        raise ValueError(f'{path}: empty or duplicate EffectAnimNames')
    return names


def call_names(sequence):
    for event in sequence.get('events') or []:
        data = event.get('data') if isinstance(event, dict) else None
        call = data.get('CallAnimation') if isinstance(data, dict) else None
        if isinstance(call, dict) and isinstance(call.get('name'), str):
            yield call['name']


def census_program(definitions, roots):
    by_anim = collections.defaultdict(list)
    for definition in definitions:
        name = definition.get('anim_name')
        if isinstance(name, str) and name:
            by_anim[name.lower()].append(definition)
    handled = set()
    queue = collections.deque(roots)
    while queue:
        called = queue.popleft()
        key = called.lower()
        if key in handled:
            continue
        handled.add(key)
        for definition in by_anim.get(key, []):
            for sequence in definition.get('sequences') or []:
                queue.extend(call_names(sequence))
            reset = definition.get('reset_state')
            if isinstance(reset, dict):
                queue.extend(call_names(reset))

    calls = set()
    for definition in definitions:
        health = definition.get('health')
        if isinstance(health, (int, float)) and health > 0:
            sequence = definition.get('unknown_seq')
            if not isinstance(sequence, dict):
                continue
            if str(sequence.get('seq_state', 'Initial')).lower() == 'oncall':
                continue
            for event_index, called in enumerate(call_names(sequence)):
                calls.add((definition['_source'], event_index, called))
    routed = {row for row in calls if row[2].lower() in handled}
    outside = calls - routed
    traits = set()
    for target in {row[2] for row in outside}:
        for definition in by_anim.get(target.lower(), []):
            kinds = set()
            for sequence in (definition.get('sequences') or []) + [
                definition.get('unknown_seq'),
                definition.get('reset_state'),
            ]:
                if not isinstance(sequence, dict):
                    continue
                for event in sequence.get('events') or []:
                    data = event.get('data') if isinstance(event, dict) else None
                    if isinstance(data, dict):
                        kinds.update(data)
            traits.add(
                (
                    target,
                    definition.get('name') or '',
                    definition.get('anim_root_name') or '',
                    definition.get('health') or 0,
                    ','.join(sorted(kinds)),
                )
            )
    return routed, outside, handled, traits


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path('extracted'))
    parser.add_argument(
        '--factory', type=Path, default=Path('CSVM/src/Session/WorldEffectsFactory.cs')
    )
    parser.add_argument('--dump', type=Path)
    args = parser.parse_args()

    names = effect_names(args.factory)
    routed, outside, handled, traits = set(), set(), set(), set()
    programs, files = 0, set()
    for chapter in CHAPTERS:
        chapter_defs = load_archive(args.root / chapter / 'cam_anim')
        mission_dirs = sorted((args.root / chapter).glob('*/mis_anim'))
        if not chapter_defs or not mission_dirs:
            raise SystemExit(f'Compiled animation extraction missing for {chapter}')
        for mission_dir in mission_dirs:
            definitions = chapter_defs + load_archive(mission_dir)
            programs += 1
            files.update(row['_source'] for row in definitions)
            found_routed, found_outside, found_handled, found_traits = census_program(
                definitions, names
            )
            routed.update(found_routed)
            outside.update(found_outside)
            handled.update(found_handled)
            traits.update(found_traits)

    routed_counts = collections.Counter(row[2] for row in routed)
    outside_counts = collections.Counter(row[2] for row in outside)
    outside_names = set(outside_counts)
    unclassified = sorted(outside_names - LOCAL_CHOREOGRAPHY)
    stale_local = sorted(LOCAL_CHOREOGRAPHY - outside_names)
    report = {
        'chapters': list(CHAPTERS),
        'chapter_mission_programs': programs,
        'compiled_json_files_read': len(files),
        'effect_anim_names': len(names),
        'death_slot_call_animation_events': len(routed) + len(outside),
        'death_slot_call_animation_targets': len(routed_counts) + len(outside_counts),
        'routed_death_effect_call_events': len(routed),
        'routed_death_effect_targets': dict(sorted(routed_counts.items())),
        'effect_runtime_handled_names': len(handled),
        'outside_effect_runtime_events': len(outside),
        'outside_effect_runtime_targets': dict(sorted(outside_counts.items())),
        'unclassified_targets': unclassified,
        'stale_local_choreography_targets': stale_local,
        'outside_target_definitions': [
            {
                'target': row[0],
                'name': row[1],
                'anim_root_name': row[2],
                'health': row[3],
                'event_kinds': row[4].split(',') if row[4] else [],
            }
            for row in sorted(traits)
        ],
    }
    report['outside_rows'] = [
        {
            'source': row[0],
            'call_index': row[1],
            'target': row[2],
        }
        for row in sorted(outside)
    ]
    summary = {k: v for k, v in report.items() if k != 'outside_rows'}
    print(json.dumps(summary, indent=2))
    if args.dump:
        args.dump.parent.mkdir(parents=True, exist_ok=True)
        args.dump.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(f'wrote {args.dump.as_posix()}')
    if unclassified or stale_local:
        raise SystemExit('Death-effect classification is not closed')


if __name__ == '__main__':
    main()
