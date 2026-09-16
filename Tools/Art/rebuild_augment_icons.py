"""Rebuild or byte-verify icons from immutable master + approved effect layers.
Usage: python Tools/Art/rebuild_augment_icons.py [--check]
Requires numpy, Pillow. No image generation or per-component transforms.
"""
import hashlib,json,sys
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[2]
source=root/'ArtSource/AugmentIcons'
records=json.loads((root/'Documentation/AugmentIcons/manifest.json').read_text(encoding='utf-8'))
master_path=source/'NeedleMaster.png'
master_hash=hashlib.sha256(master_path.read_bytes()).hexdigest()
count=0
for row in records:
    path=root/row['path']
    if row['category']=='Legendary':
        assert hashlib.sha256(path.read_bytes()).hexdigest()==row['source_sha256']
        continue
    assert master_hash==row['source_sha256']
    master=Image.open(master_path).convert('RGBA').crop(row['master_crop'])
    result=Image.open(source/'Effects'/f"{row['id']}.png").convert('RGBA')
    for transform in row['transforms']:
        matrix=np.array(transform['matrix']); scale=transform['scale']
        assert np.allclose(matrix.T@matrix,np.eye(2)*scale**2), 'Nonuniform scaling forbidden'
        assert np.linalg.det(matrix)>0, 'Mirroring forbidden'
        inverse=np.linalg.inv(matrix); offset=-inverse@np.array(transform['translation'])
        coefficients=(inverse[0,0],inverse[0,1],offset[0],inverse[1,0],inverse[1,1],offset[1])
        layer=master.transform(result.size,Image.Transform.AFFINE,coefficients,resample=Image.Resampling.BICUBIC)
        result=Image.alpha_composite(result,layer)
    final=Image.new('RGBA',(1254,1254))
    final.alpha_composite(result.resize((1128,1128),Image.Resampling.LANCZOS),(63,63))
    if '--check' in sys.argv:
        assert np.array_equal(np.array(final),np.array(Image.open(path))), row['id']
        assert final.getchannel('A').getextrema()==(0,255)
    else: final.save(path)
    count+=1
assert count==21
print('PASS: 21 pixel-exact reconstructions from one master, similarity transforms only; 6 legendary originals unchanged.')
