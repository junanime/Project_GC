// Lossless grid extraction only: generated RGBA pixels are not repainted or keyed.
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const path = require('path');
const root = path.resolve(__dirname, '../../Assets/Junhan/Art/AriSkills/Marble');
(async () => {
  for (const name of ['Idle', 'Walk', 'Dash', 'Transform']) {
    const file = path.join(root, name + 'Sheet.png');
    const meta = await sharp(file).metadata();
    if (!meta.hasAlpha) throw new Error(name + ' must have generated alpha');
    for (let i = 0; i < 8; ++i) {
      const left = Math.round((i % 4) * meta.width / 4);
      const top = Math.round(Math.floor(i / 4) * meta.height / 2);
      const width = Math.round((i % 4 + 1) * meta.width / 4) - left;
      const height = Math.round((Math.floor(i / 4) + 1) * meta.height / 2) - top;
      await sharp(file).extract({ left, top, width, height }).png().toFile(path.join(root, name + i + '.png'));
    }
    console.log(name, meta.width, meta.height, '8 lossless alpha frames');
  }
})();
