'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),zlib=require('node:zlib'),assert=require('node:assert/strict');
const source=fs.readFileSync(path.resolve(__dirname,'../../Assets/WebGLTemplates/24tuMobileWeb/index.html'),'utf8');
const factory=source.match(/function createMobileDownloadReader\([\s\S]+?(?=\n  function syncViewport)/)[0];
let allocations=[];
class TrackedBytes extends Uint8Array{constructor(value){super(value);if(typeof value==='number')allocations.push(value);}}
const create=vm.runInNewContext('('+factory+')',{URL,location:{href:'https://game.test/mobile/'},Uint8Array:TrackedBytes,ReadableStream,DecompressionStream});
let checks=0;
function check(value,label){assert(value,label);console.log('PASS '+label);checks++;}
function response(chunks,extra={}){let index=0;return{ok:true,status:200,url:'https://game.test/mobile/Build/test.data.unityweb',body:new ReadableStream({pull(c){if(index<chunks.length)c.enqueue(chunks[index++]);else c.close();}}),...extra};}
(async()=>{
 const data=Uint8Array.from({length:1024*1024+17},(_,i)=>i%251),sizes={'test.data.unityweb':data.length};
 const chunks=[];for(let i=0;i<data.length;i+=8137)chunks.push(data.subarray(i,i+8137));
 const events=[],failures=[];
 allocations=[];
 const read=create(sizes,e=>failures.push(e));
 const result=await read(response(chunks),e=>events.push({type:e.type,total:e.total,loaded:e.loaded}));
 check(Buffer.from(result.parsedBody).equals(Buffer.from(data)),'many uneven chunks retain every original byte');
 check(allocations.length===1&&allocations[0]===data.length,'exactly one payload allocation with no EOF copy');
 check(events.at(-1).type==='load'&&events.at(-1).loaded===data.length,'complete progress uses decoded length');
 check(failures.length===0,'valid stream produces no failure');
 const packed=zlib.gzipSync(data);
 const decoded=await read(response([packed.subarray(0,1),packed.subarray(1,500),packed.subarray(500)]),()=>{});
 check(Buffer.from(decoded.parsedBody).equals(Buffer.from(data)),'native streaming gzip works without a server encoding header, including split magic');
 await assert.rejects(read(response([data.subarray(0,20)]),()=>{}));check(failures.length===1,'truncated response is rejected with visible failure');
 await assert.rejects(read(response([data,new Uint8Array(1)]),()=>{}));check(failures.length===2,'oversized response is rejected');
 await assert.rejects(read(response([],{ok:false,status:503}),()=>{}));check(failures.at(-1).includes('503'),'HTTP failures are reported before allocation');
 await assert.rejects(read(response([],{url:'https://game.test/unknown'}),()=>{}));check(failures.length===4,'unknown payload is not guessed');
 check(create(null,()=>{})===undefined,'unprepared template safely uses the standard loader');
 if(process.argv[2]){
  const root=path.resolve(process.argv[2]),info=JSON.parse(fs.readFileSync(path.join(root,'mobile-memory.json'),'utf8'));
  const loader=fs.readFileSync(path.join(root,'Build',info.loader),'utf8');new vm.Script(loader);
  const assignment=loader.slice(loader.indexOf('u.readBodyWithProgress='),loader.indexOf(',u.fetchWithProgress='));
  const module={mobileDecodedFiles:sizes,mobileReadBodyWithProgress:read};
  vm.runInNewContext(assignment,{u:module,URL,document:{URL:'https://game.test/mobile/'},Uint8Array,console});
  check(Buffer.from((await module.readBodyWithProgress(response(chunks),()=>{})).parsedBody).equals(Buffer.from(data)),'patched loader calls exact-size reader for game payload');
  const sidecar=response([new Uint8Array([65,66])],{url:'https://game.test/mobile/StreamingAssets/aa/settings.json',headers:new Headers({'Content-Length':'2'})});
  const streamed=[];
  const sidecarResult=await module.readBodyWithProgress(sidecar,event=>{if(event.chunk)streamed.push(...event.chunk);},true);
  check(Buffer.from(sidecarResult.parsedBody).toString()==='AB'&&streamed.join(',')==='65,66','Addressables and other Unity requests keep the original streaming behavior');
 }
 console.log('FINISHED memory checks='+checks);
})().catch(e=>{console.error(e);process.exitCode=1;});
