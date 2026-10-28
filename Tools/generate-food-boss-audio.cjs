// Verify selected audio; pass --source to import an approved review package.
// Python 3 standard library only. Does not regenerate or alter the sound designs.
const {spawnSync}=require('child_process');
const path=require('path');
const r=spawnSync(process.env.PYTHON||'python',['-X','utf8',path.join(__dirname,'Audio','build_food_boss_audio.py'),...process.argv.slice(2)],{stdio:'inherit',windowsHide:true});
if(r.error){console.error(r.error.message);process.exit(1);}
process.exit(r.status===null?1:r.status);
