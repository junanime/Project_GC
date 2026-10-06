'use strict';
// Updates only a staged mobile shell/loader; the compiled game and sprites stay byte-identical.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),vm=require('node:vm'),assert=require('node:assert/strict');
function prepare(root){
 const htmlFile=path.join(root,'index.html');
 let html=fs.readFileSync(htmlFile,'utf8');
 const sizes={};let downloadBytes=0;
 for(const field of ['dataUrl','frameworkUrl','codeUrl']){
  const name=html.match(new RegExp(field+":buildUrl \\+ '/([^']+)'"))?.[1];
  assert(name&&path.basename(name)===name,'Invalid '+field);
  const file=path.join(root,'Build',name),fd=fs.openSync(file,'r'),head=Buffer.alloc(2),tail=Buffer.alloc(4);
  try{fs.readSync(fd,head,0,2,0);fs.readSync(fd,tail,0,4,fs.fstatSync(fd).size-4);}finally{fs.closeSync(fd);}
  assert.equal(head.toString('hex'),'1f8b','Expected gzip payload: '+name);
  sizes[name]=tail.readUInt32LE();
  downloadBytes+=fs.statSync(file).size;
  assert(sizes[name]>0&&sizes[name]<=1073741824,'Decoded payload too large');
 }
 const oldName=html.match(/loader.src=buildUrl \+ '\/([^']+)'/)?.[1];
 assert(oldName&&path.basename(oldName)===oldName,'Missing loader');
 let loader=fs.readFileSync(path.join(root,'Build',oldName),'utf8');
 const original='u.readBodyWithProgress=function(a,i,s){';
 const patched='u.readBodyWithProgress=(function(mobileModule){return function(a,i,s){if(mobileModule.mobileReadBodyWithProgress&&mobileModule.mobileDecodedFiles&&Object.prototype.hasOwnProperty.call(mobileModule.mobileDecodedFiles,new URL(a.url,document.URL).pathname.split("/").pop()))return mobileModule.mobileReadBodyWithProgress(a,i,s);';
 if(!loader.includes(patched)){
  assert.equal(loader.split(original).length,2,'Unrecognized Unity loader: refusing unsafe patch');
  const end='},u.fetchWithProgress=function(';
  assert.equal(loader.split(end).length,2,'Unrecognized Unity reader boundary');
  loader=loader.replace(original,patched);
  loader=loader.replace(end,'}})(u),u.fetchWithProgress=function(');
 }
 const name=crypto.createHash('md5').update(loader).digest('hex')+'.loader.js';
 function folderBytes(folder){return fs.existsSync(folder)?fs.readdirSync(folder,{withFileTypes:true}).reduce((total,item)=>total+(item.isDirectory()?folderBytes(path.join(folder,item.name)):fs.statSync(path.join(folder,item.name)).size),0):0;}
 const downloadMegabytes=Math.ceil((downloadBytes+Buffer.byteLength(loader)+folderBytes(path.join(root,'StreamingAssets')))/1000000);
 html=html.replace("loader.src=buildUrl + '/"+oldName+"'","loader.src=buildUrl + '/"+name+"'");
 assert(/const mobileBuildDecodedSizes=(?:null|\{[^;]+\});/.test(html),'Mobile template lacks memory reader');
 html=html.replace(/const mobileBuildDecodedSizes=(?:null|\{[^;]+\});/,'const mobileBuildDecodedSizes='+JSON.stringify(sizes)+';');
 html=html.replace(/const mobileDownloadMegabytes=\d+;/,'const mobileDownloadMegabytes='+downloadMegabytes+';');
 html=html.replace(/첫 다운로드 약 \d+MB/g,'첫 다운로드 약 '+downloadMegabytes+'MB');
 new vm.Script(loader);
 fs.writeFileSync(path.join(root,'Build',name),loader);
 fs.writeFileSync(htmlFile,html);
 const info={revision:'mobile-memory-2',loader:name,decodedBytes:sizes,downloadMegabytes,unityDataCache:false};
 fs.writeFileSync(path.join(root,'mobile-memory.json'),JSON.stringify(info,null,2));
 return info;
}
if(require.main===module){assert(process.argv[2],'Pass staged mobile build root');console.log(JSON.stringify(prepare(path.resolve(process.argv[2])),null,2));}
module.exports={prepare};
