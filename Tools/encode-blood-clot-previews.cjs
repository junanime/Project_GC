// Encoding only: all artwork and motion frames are rendered by Unity.
const fs=require('fs');
const path=require('path');
const sharp=require(process.argv[2] || 'sharp');
(async()=>{
  const output=path.resolve('Documentation/Previews/BloodClot');
  fs.mkdirSync(output,{recursive:true});
  for(const key of ['Hyuki','Shini','Ari','Ashi']){
    const folder=path.resolve('Library/BloodClotPreview',key);
    const files=fs.readdirSync(folder).filter(f=>/^\d{3}\.png$/.test(f)).sort();
    const frames=[];
    for(const file of files)frames.push(await sharp(path.join(folder,file)).removeAlpha().raw().toBuffer());
    await sharp(Buffer.concat(frames),{raw:{width:640,height:400*frames.length,channels:3,pageHeight:400}})
      .gif({delay:Array(frames.length).fill(40),loop:0,effort:7}).toFile(path.join(output,key+'.gif'));
    console.log(key,await sharp(path.join(output,key+'.gif'),{animated:true}).metadata());
  }
})();
