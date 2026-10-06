const fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process'),http=require('node:http'),assert=require('node:assert/strict');
const root=process.argv[2],output=process.argv[3];
if(!root||!output)throw Error('Pass existing web build and temporary test output directory.');
fs.mkdirSync(output,{recursive:true});
const config=path.join(output,'server-test-config.json');
fs.writeFileSync(config,JSON.stringify({gameRoot:root,mobileRoot:root}));
const child=cp.spawn(process.execPath,[path.join(__dirname,'Share-WebDemoServer.cjs')],{env:{...process.env,PROJECT_GC_SHARE_CONFIG:config,PROJECT_GC_SHARE_PORT:'18727'},stdio:['ignore','pipe','pipe'],windowsHide:true});
function request(url,method='GET',headers={}){return new Promise((resolve,reject)=>{
  const req=http.request({host:'127.0.0.1',port:18727,path:url,method,headers},res=>{
    const chunks=[];res.on('data',c=>chunks.push(c));res.on('end',()=>resolve({status:res.statusCode,headers:res.headers,body:Buffer.concat(chunks)}));
  });req.on('error',reject);req.end();
});}
(async()=>{
  try{
    await new Promise((resolve,reject)=>{child.stdout.on('data',d=>{if(d.toString().includes('ready:'))resolve();});child.once('exit',c=>reject(Error('server exited '+c)));child.stderr.on('data',d=>console.error(d.toString()));});
    let count=0;function check(ok,label){assert.ok(ok,label);console.log('PASS '+label);count++;}
    check((await request('/')).status===200,'desktop root unchanged');
    const slash=await request('/mobile');check(slash.status===308&&slash.headers.location==='/mobile/','mobile trailing slash redirect');
    check((await request('/mobile/')).body.equals(fs.readFileSync(path.join(root,'index.html'))),'mobile relative-root HTML served');
    check((await request('/mobile/','HEAD')).body.length===0,'HEAD does not send body');
    for(const name of ['/../ProjectSettings/ProjectSettings.asset','/%2e%2e/ProjectSettings/ProjectSettings.asset','/mobile/../../LICENSE','/mobile/Launch-WebDemo.ps1','/Build/','/.git/config'])
      check((await request(name)).status===404,'private/unlisted path blocked '+name);
    check((await request('/%zz')).status===400,'malformed encoding rejected');
    check((await request('/mobile/','POST')).status===405,'writes rejected');
    const wasm=fs.readdirSync(path.join(root,'Build')).find(n=>n.includes('.wasm.'));
    const head=await request('/mobile/Build/'+wasm,'HEAD');
    check(head.status===200&&head.headers['content-encoding']==='gzip'&&head.headers['content-type']==='application/wasm','mobile native gzip and wasm MIME');
    const range=await request('/mobile/Build/'+wasm,'GET',{Range:'bytes=0-1'});
    check(range.status===206&&range.body.toString('hex')==='1f8b','ranged compressed bytes correct');
    check((await request('/mobile/Build/'+wasm,'GET',{Range:'bytes=99999999999-'})).status===416,'invalid range rejected');
    check(!(await request('/Build/'+wasm,'HEAD')).headers['content-encoding'],'desktop fallback encoding unchanged');
    console.log('FINISHED server checks='+count);
  }finally{child.kill();}
})().catch(e=>{console.error(e);process.exitCode=1;child.kill();});
