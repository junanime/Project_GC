"""Read Unity-authored values, not a separately maintained simulation stat table."""
import pathlib
import re
import yaml

ROOT = pathlib.Path(__file__).resolve().parents[2]

def documents(path):
    text = (ROOT / path).read_text(encoding='utf-8-sig')
    text = re.sub(r'^%.*\n', '', text, flags=re.M)
    text = re.sub(r'^--- !u!.*$', '---', text, flags=re.M)
    return list(yaml.safe_load_all(text))

def load():
    guid = {}
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)
        if match:
            guid[match[1]] = pathlib.Path(str(meta)[:-5])
    level = documents('Assets/Blueprints/Levels/Level 1.asset')[0]['MonoBehaviour']
    table = level['monsterSpawnTable']
    normal = [0, 1, 4, 5, 6, 7]
    flat = [documents(guid[b['guid']])[0]['MonoBehaviour'] for pool in level['monsters'] for b in pool['monsterBlueprints']]
    weapon = next(d['MonoBehaviour'] for d in documents('Assets/Junhan/Prefabs/Abilities/Syringe Dart Ability.prefab') if 'MonoBehaviour' in d and 'damage' in d['MonoBehaviour'])
    config = dict(normal=normal, hp={str(i): b['hp'] for i, b in enumerate(flat)},
        gem={str(i): [(x['item'], x['dropChance']) for x in flat[i]['gemLootTable']['lootTable']] for i in normal},
        points=[(p['t'] * level['levelTime'] / 60, [p['spawnChances'][i] for i in normal]) for p in table['spawnChanceKeyframes']],
        rate_points=[(p['t'] * level['levelTime'], p['spawnRate']) for p in table['spawnRateKeyframes']],
        population_limit=level['normalSpawnPopulationLimit'], weapon=weapon)
    return config
