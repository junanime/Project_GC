// Unity Services Wire 1.3.0 emits getDynCaller calls in Unity 2022.3, but
// this toolchain omits that helper. Use the framework's own dynCall ABI.
const fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib'),crypto=require('node:crypto'),vm=require('node:vm'),assert=require('node:assert/strict');
const helper='function getDynCaller(sig,ptr){return function(){return dynCall(sig,ptr,Array.prototype.slice.call(arguments));};}';
const marker=Buffer.from('UnityWeb Compressed Content (gzip)\0');
function pack(source){
 const gzip=zlib.gzipSync(source,{level:9}),header=Buffer.from(gzip.subarray(0,10));
 header[3]=16; // FCOMMENT: Unity's fallback loader recognizes this marker.
 return Buffer.concat([header,marker,gzip.subarray(10)]);
}
function repair(source){
 const calls=(source.match(/\bgetDynCaller\(/g)||[]).length;
 if(!calls||/\bfunction getDynCaller\(/.test(source))return {source,changed:false};
 assert(source.includes('function dynCall(sig,ptr,args){'),'Unknown Unity callback ABI: refusing compatibility patch');
 for(const sig of [...source.matchAll(/getDynCaller\("([^"]+)"/g)].map(m=>m[1]))
  assert(source.includes('Module["dynCall_'+sig+'"]'),'Missing callback export '+sig);
 const fixed=source.replace('function dynCall(sig,ptr,args){',helper+'function dynCall(sig,ptr,args){');
 new vm.Script(fixed);
 return {source:fixed,changed:true};
}
function prepare(root){
 const htmlFile=path.join(root,'index.html');let html=fs.readFileSync(htmlFile,'utf8');
 const oldName=html.match(/frameworkUrl:\s*buildUrl \+ '\/([^']+)'/)?.[1];
 assert(oldName&&path.basename(oldName)===oldName,'Missing framework');
 const original=fs.readFileSync(path.join(root,'Build',oldName));
 assert(original[0]===31&&original[1]===139,'Expected Gzip Unity framework');
 const result=repair(zlib.gunzipSync(original).toString());
 if(!result.changed&&original.subarray(0,100).includes(marker))return false;
 const packed=pack(result.source);
 const name=crypto.createHash('md5').update(packed).digest('hex')+'.framework.js.unityweb';
 fs.writeFileSync(path.join(root,'Build',name),packed);
 html=html.replace(oldName,name);fs.writeFileSync(htmlFile,html);
 fs.writeFileSync(path.join(root,'web-compatibility.json'),JSON.stringify({revision:1,reason:'Unity 2022.3 / Services Wire callback compatibility',framework:name},null,2));
 return true;
}
if(require.main===module){assert(process.argv[2]);console.log('Framework compatibility updated: '+prepare(path.resolve(process.argv[2])));}
module.exports={prepare,repair,pack};
