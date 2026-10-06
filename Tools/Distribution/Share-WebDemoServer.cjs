'use strict';
// Static game-only host. Both profiles stay on one origin; no source or private files are exposed.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const configFile=process.env.PROJECT_GC_SHARE_CONFIG||path.join(__dirname,'active-build.json');
const buildsPrefix=fs.realpathSync(process.env.PROJECT_GC_BUILDS_DIR||'D:/Project_GC/Project_GC/Builds')+path.sep;
let allowed=new Map();
function checkedRoot(folder){
  const root=fs.realpathSync(folder);
  if(!root.toLowerCase().startsWith(buildsPrefix.toLowerCase()))throw Error('Build root must be inside Builds');
  return root;
}
function add(map,root,relative,prefix,version){
  const file=path.join(root,relative),st=fs.lstatSync(file);
  if(st.isSymbolicLink())throw Error('Symbolic links are not served');
  if(st.isDirectory())for(const name of fs.readdirSync(file))add(map,root,relative+'/'+name,prefix,version);
  else if(st.isFile()){
    let gzip=false;
    if(prefix==='/mobile'&&relative.endsWith('.unityweb')){
      const fd=fs.openSync(file,'r'),header=Buffer.alloc(2);
      try{fs.readSync(fd,header,0,2,0);gzip=header[0]===31&&header[1]===139;}finally{fs.closeSync(fd);}
    }
    map.set(prefix+'/'+relative,{file,size:st.size,version,gzip});
  }
}
function profile(map,folder,previous,prefix){
  const root=checkedRoot(folder),info=JSON.parse(fs.readFileSync(path.join(root,'web-build.json'),'utf8'));
  for(const previousRoot of previous||[]){
    const oldRoot=checkedRoot(previousRoot);
    for(const item of ['Build','StreamingAssets'])if(fs.existsSync(path.join(oldRoot,item)))add(map,oldRoot,item,prefix,'previous');
  }
  for(const item of ['index.html','game-icon.png','Build','StreamingAssets','LICENSE.txt','ThirdPartyNotices.md','web-build.json']){
    if(fs.existsSync(path.join(root,item)))add(map,root,item,prefix,info.sourceCommit);
  }
  if(!map.has(prefix+'/index.html'))throw Error('Missing index.html');
  map.set(prefix+'/',map.get(prefix+'/index.html'));
}
function reload(){
  const config=JSON.parse(fs.readFileSync(configFile,'utf8').replace(/^\uFEFF/,'')),next=new Map();
  profile(next,config.gameRoot,config.previousRoots,'');
  if(config.mobileRoot)profile(next,config.mobileRoot,config.previousMobileRoots,'/mobile');
  // Keep the desktop player in the same URL directory: Unity's browser save key depends on it.
  next.set('/play-pc.html',next.get('/index.html'));
  if(config.portalRoot){
    const root=checkedRoot(config.portalRoot);
    add(next,root,'index.html','/portal','device-selector-v1');
    next.set('/',next.get('/portal/index.html'));
    next.set('/index.html',next.get('/portal/index.html'));
  }
  allowed=next;console.log('Activated game profiles:',config.gameRoot,config.mobileRoot||'(no mobile)',allowed.size,'files');
}
reload();
fs.watchFile(configFile,{interval:1000},()=>{try{reload();}catch(e){console.error('Keeping previous build:',e.message);}});
const types={'.html':'text/html; charset=utf-8','.js':'application/javascript','.json':'application/json','.png':'image/png','.wasm':'application/wasm','.txt':'text/plain; charset=utf-8','.md':'text/plain; charset=utf-8'};
const server=http.createServer((req,res)=>{
  res.setHeader('X-Content-Type-Options','nosniff');res.setHeader('Referrer-Policy','no-referrer');
  res.setHeader('X-Robots-Tag','noindex, nofollow, noarchive');res.setHeader('Cross-Origin-Resource-Policy','same-origin');
  if(!['GET','HEAD'].includes(req.method)){res.writeHead(405,{Allow:'GET, HEAD'});return res.end();}
  let pathname;
  try{pathname=decodeURIComponent(req.url.split('?')[0]);}catch{res.writeHead(400);return res.end();}
  if(pathname==='/mobile'&&allowed.has('/mobile/')){res.writeHead(308,{Location:'/mobile/'});return res.end();}
  const entry=allowed.get(pathname);
  if(!entry){res.writeHead(404);return res.end();}
  res.setHeader('X-Game-Version',entry.version||'unknown');
  const ext=path.extname(entry.gzip?entry.file.replace(/\.unityweb$/,''):entry.file);
  res.setHeader('Content-Type',types[ext]||'application/octet-stream');
  res.setHeader('Cache-Control',/^\/(mobile\/)?Build\//.test(pathname)?'public, max-age=86400, immutable':'no-cache');
  if(entry.gzip)res.setHeader('Content-Encoding','gzip');
  res.setHeader('Accept-Ranges','bytes');
  let start=0,end=entry.size-1,status=200;
  if(req.headers.range){
    const match=/^bytes=(\d*)-(\d*)$/.exec(req.headers.range);
    if(!match||(!match[1]&&!match[2])){res.writeHead(416,{'Content-Range':`bytes */${entry.size}`});return res.end();}
    if(!match[1])start=Math.max(0,entry.size-Number(match[2]));
    else{start=Number(match[1]);if(match[2])end=Math.min(end,Number(match[2]));}
    if(!Number.isSafeInteger(start)||!Number.isSafeInteger(end)||start>end||start>=entry.size){res.writeHead(416,{'Content-Range':`bytes */${entry.size}`});return res.end();}
    status=206;res.setHeader('Content-Range',`bytes ${start}-${end}/${entry.size}`);
  }
  res.writeHead(status,{'Content-Length':Math.max(0,end-start+1)});
  if(req.method==='HEAD'||!entry.size)return res.end();
  const stream=fs.createReadStream(entry.file,{start,end});
  stream.on('error',()=>res.destroy());res.on('close',()=>stream.destroy());stream.pipe(res);
});
server.requestTimeout=30000;server.headersTimeout=15000;
const port=Number(process.env.PROJECT_GC_SHARE_PORT||18725);
server.listen(port,'127.0.0.1',()=>console.log(`Game-only server ready: 127.0.0.1:${port}`));
server.on('error',e=>{console.error(e.message);process.exit(1);});
