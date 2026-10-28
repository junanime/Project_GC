const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync(path.join(__dirname,'../../Assets/WebGLTemplates/24tuMobileWeb/index.html'),'utf8');
const code=html.match(/function installGameTouchBridge[\s\S]*?(?=\n  const canvas)/)[0];
const handlers=new Map(),samples=[];let focused=0;
const install=vm.runInNewContext('('+code+')',{Set,JSON,window:{addEventListener(type,fn){handlers.set(type,fn);},removeEventListener(type){handlers.delete(type);}}});
const canvas={getBoundingClientRect(){return{left:40,top:20,width:800,height:450};},focus(){focused++;}};
const dispose=install(canvas,s=>samples.push(JSON.parse(s)));
function event(type,id,x=240,y=380,target=canvas){handlers.get(type)({type,target,changedTouches:[{identifier:id,clientX:x,clientY:y}]});}
event('touchstart',0);assert.equal(focused,1);assert.equal(samples[0].id,0);assert.equal(samples[0].phase,0);assert.equal(samples[0].x,.25);assert.ok(Math.abs(samples[0].y-.2)<1e-8);
event('touchmove',0,440,245);assert.equal(samples.at(-1).x,.5);assert.equal(samples.at(-1).y,.5);
event('touchstart',9,500,380);event('touchend',9);event('touchmove',0);
assert.equal(samples.at(-1).id,0);assert.equal(samples.at(-1).phase,1);
event('touchcancel',0);const count=samples.length;event('touchmove',0);assert.equal(samples.length,count);
event('touchstart',10,240,380,{});assert.equal(samples.length,count);
dispose();assert.equal(handlers.size,0);
console.log('PASS mobile touch bridge: zero-based id, canvas/DPR coordinates, movement, multi-touch ownership, cancel, non-canvas exclusion, cleanup');
