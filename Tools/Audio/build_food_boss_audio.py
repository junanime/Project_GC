"""Import/verify the selected boss SFX without resynthesis or normalization."""
from pathlib import Path
import argparse, array, hashlib, json, math, shutil, wave
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/FoodBosses/Audio'
MANIFEST = Path(__file__).with_name('selected-manifest.json')
EVENTS = 'Shot Radial Dash Land Skill1 Skill2 Recover Break Phase Death Skid HurtCry ChaseCry'.split()
EXPECTED = {(b,e) for b in range(5) for e in EVENTS} | {(0,'Consume'),(3,'Beam')}
def inspect(path):
    with wave.open(str(path), 'rb') as w:
        assert w.getsampwidth() == 2 and w.getnchannels() == 1, str(path)
        sr, frames = w.getframerate(), w.getnframes()
        samples = array.array('h', w.readframes(frames))
    peak = max(abs(v) for v in samples)
    assert 0 < peak < 32767, f'Empty/clipped PCM: {path}'
    return dict(seconds=round(frames/sr,6), sampleRate=sr, peak=peak/32768,
                rms=math.sqrt(sum(v*v for v in samples)/len(samples))/32768)
def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', type=Path, help='Import a selected package with manifest.json and clips/')
    a=p.parse_args()
    source=a.source
    doc=json.loads(((source/'manifest.json') if source else MANIFEST).read_text(encoding='utf-8-sig'))
    clips=doc['clips']
    assert len(clips)==67 and {(c['boss'],c['event']) for c in clips}==EXPECTED
    prepared=[]
    for c in clips:
        name=f"{c['boss']}-{c['event']}.wav"
        path=(source/'clips'/name) if source else OUT/name
        assert hashlib.sha256(path.read_bytes()).hexdigest()==c['sha256'], str(path)
        prepared.append((c,path,name,inspect(path)))
    for name,master in [('3-Skill1.wav','TangdengiCharge-20261021.wav')]:
        path=(source/'clips'/name) if source else OUT/name
        assert path.read_bytes()==(MANIFEST.parent/'Approved'/master).read_bytes(), name
    if source:
        for c,path,name,stats in prepared:
            shutil.copyfile(path,OUT/name)
        cleaned=[{k:c[k] for k in ('boss','event','label','sha256')} | {'file':name,**stats} for c,path,name,stats in prepared]
        MANIFEST.write_text(json.dumps({'selectionDate':'2026-10-21','version':'clean-v2-with-cries','clips':cleaned},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    measurements=[dict(file=name,sha256=c['sha256'],**stats) for c,path,name,stats in prepared]
    (ROOT/'Documentation/FoodBossAudioMeasurements.json').write_text(json.dumps(measurements,indent=2)+'\n',encoding='utf-8')
    print('PASS: 67 selected WAVs verified; approved Tangdengi charge unchanged.')
if __name__=='__main__': main()
