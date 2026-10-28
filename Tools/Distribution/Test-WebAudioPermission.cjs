const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
let lib,resumes=0;const listeners=new Map(),moduleState={deinitializers:[]};
const context={state:'suspended',resume(){resumes++;this.state='running';return Promise.resolve();}};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../../Assets/WebAudioPermission.jslib'),'utf8'),{
 LibraryManager:{library:{}},mergeInto(_,value){lib=value;},Module:moduleState,WEBAudio:{audioContext:context},console,
 window:{addEventListener(type,fn){listeners.set(type,fn);},removeEventListener(type,fn){assert.equal(listeners.get(type),fn);listeners.delete(type);}}
});
lib.WebAudio_InstallGestureResume();lib.WebAudio_InstallGestureResume();
assert.equal(listeners.size,4);assert.equal(moduleState.deinitializers.length,1);
assert.equal(lib.WebAudio_IsRunning(),0);listeners.get('keydown')();assert.equal(resumes,1);assert.equal(lib.WebAudio_IsRunning(),1);
listeners.get('pointerup')();assert.equal(resumes,1);
context.state='interrupted';listeners.get('touchend')();assert.equal(resumes,2);
context.state='closed';listeners.get('pointerdown')();assert.equal(resumes,2);
moduleState.deinitializers[0]();assert.equal(listeners.size,0);assert.equal(moduleState.gameAudioGestureResume,undefined);
console.log('PASS audio permission: single install, keyboard unlock, running no-op, interrupted touch resume, closed no-op, complete cleanup');
