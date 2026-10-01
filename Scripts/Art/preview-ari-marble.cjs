// Render review GIFs using the SAME PPU/pivot imported by Unity; source art is unchanged.
const sharp=require(process.env.SHARP_MODULE||'sharp'),fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../../Assets/Junhan/Art/AriSkills/Marble');
const out=path.resolve(__dirname,'../../Docs/AriConcepts/FinalMotion');
(async()=>{
 fs.mkdirSync(out,{recursive:true});
 for(const name of ['Idle','Walk','Dash','Transform','Revert']){
  const buffers=[];const order=name==='Revert'?[7,6,5,4,3,2,1,0]:[0,1,2,3,4,5,6,7];
  for(const i of order){
   const file=path.join(root,(name==='Revert'?'Transform':name)+i+'.png');
   const meta=fs.readFileSync(file+'.meta','utf8'),size=await sharp(file).metadata();
   const ppu=Number(meta.match(/spritePixelsToUnits: ([\d.]+)/)[1]);
   const pivot=meta.match(/spritePivot: \{x: ([\d.-]+), y: ([\d.-]+)\}/);
   const w=Math.round(size.width*280/ppu),h=Math.round(size.height*280/ppu);
   let art=await sharp(file).resize(w,h,{kernel:'nearest'}).toBuffer();
   const left=Math.round(300-Number(pivot[1])*w),top=Math.round(350-(1-Number(pivot[2]))*h);
   const clipX=Math.max(0,-left),clipY=Math.max(0,-top);
   art=await sharp(art).extract({left:clipX,top:clipY,width:Math.min(w-clipX,640-Math.max(0,left)),height:Math.min(h-clipY,440-Math.max(0,top))}).toBuffer();
   const frame=await sharp({create:{width:640,height:440,channels:4,background:'#242428'}}).composite([{input:art,left:Math.max(0,left),top:Math.max(0,top)}]).raw().toBuffer();
   buffers.push(frame);
  }
  const delay=name==='Dash'?[30,30,30,30,30,30,30,700]:name==='Walk'?125:name==='Idle'?167:[70,70,70,70,70,70,70,700];
  await sharp(Buffer.concat(buffers),{raw:{width:640,height:440*8,channels:4,pageHeight:440}}).gif({delay,loop:0}).toFile(path.join(out,name+'.gif'));
 }
})();
