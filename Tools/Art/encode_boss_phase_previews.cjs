// Encoding only. Every input frame is captured from Unity's actual Level1 renderer.
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const ffmpeg = process.argv[2] || 'ffmpeg';
const output = path.resolve(process.argv[3] || 'Library/FoodBossProof/Preview');
const names = ['롤팽이', '데니카니', '마리비', '탕뎅이', '후루미'];
const ids = ['rolpangi', 'denikani', 'maribi', 'tangdengi', 'hurumi'];
(async () => {
  fs.mkdirSync(output, { recursive: true });
  const source = path.resolve('Library/FoodBossProof');
  for (let boss = 0; boss < 5; boss++) {
    const files = fs.readdirSync(source).filter(n => n.startsWith(`phase-${boss}-`) && n.endsWith('.png')).sort();
    if (files.length !== 185) throw Error(`Expected 185 Unity frames for ${names[boss]}, found ${files.length}`);
    const target = path.join(output, `${ids[boss]}.gif`);
    const encoded = spawnSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-framerate', '25',
      '-i', path.join(source, `phase-${boss}-%03d.png`), '-filter_complex',
      '[0:v]split[a][b];[a]palettegen=stats_mode=diff:max_colors=256[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle',
      '-loop', '0', target], { encoding: 'utf8', windowsHide: true });
    if(encoded.status !== 0) throw Error(encoded.stderr || 'GIF encoder failed');
    fs.copyFileSync(path.join(source, files[15]), path.join(output, `${ids[boss]}-phase1.png`));
    fs.copyFileSync(path.join(source, files[175]), path.join(output, `${ids[boss]}-phase2.png`));

    console.log(JSON.stringify({ boss: names[boss], target, frames: files.length, duration: 7400, bytes: fs.statSync(target).size }));
  }
  fs.writeFileSync(path.join(output,'index.html'), `<!doctype html><html lang="ko"><meta charset="utf-8"><title>보스 2페이즈 전환 · 인게임 캡처</title><style>body{margin:32px auto;max-width:1280px;background:#211b27;color:#fff3d9;font-family:system-ui}h1{font-size:26px}p{color:#d1bfbe}img{width:100%;border-radius:8px}section{margin:36px 0}a{color:#f9d472}</style><h1>보스 1 → 2페이즈 전환</h1><p>실제 Level1 렌더링 · 1280×720 · 원래 재생 속도 · C형 체력바 · GIF는 무음</p>${names.map((n,i)=>`<section><h2>${n}</h2><img src="${ids[i]}.gif" alt="${n} 전환"><p><a href="${ids[i]}-phase1.png">1페이즈</a> · <a href="${ids[i]}-phase2.png">2페이즈</a></p></section>`).join('')}</html>`);
})();
