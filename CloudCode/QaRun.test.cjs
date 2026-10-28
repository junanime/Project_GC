const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
let item, writes=0, failScore=false;
const scores=new Map();
class DataApi {
  async getProtectedItems(){return {data:{results:item?[structuredClone(item)]:[]}};}
  async setProtectedItem(project,player,body){
    if(item && body.writeLock!==item.writeLock)throw Error('CONFLICT');
    item={...structuredClone(body),writeLock:String(++writes)};
  }
}
class LeaderboardsApi {
  async addLeaderboardPlayerScore(project,board,player,body){
    if(failScore){failScore=false;throw Error('TRANSIENT');}
    const key=board+player;scores.set(key,Math.max(scores.get(key)||0,body.score));
  }
}
const sandbox={module:{exports:{}},require:n=>n.includes('cloud-save')?{DataApi}:{LeaderboardsApi},Date};
vm.runInNewContext(fs.readFileSync(__dirname+'/QaRun.js','utf8'),sandbox);
const fn=sandbox.module.exports;
const context={projectId:'project',playerId:'player'};
const id='a'.repeat(32),id2='b'.repeat(32);
const base={runId:id,rules:'qa_s1_v1',normal:10,special:2,elite:1,miniBoss:0,boss:0,entered:1,cleared:1,overcharge:1,seconds:1,success:false};
const call=(action,data)=>fn({params:{action,payload:JSON.stringify(data)},context});
(async()=>{
 assert.equal(fn.validate(base,1),1100);
 const twoFields={...base,boss:2,entered:28,cleared:28,overcharge:28,seconds:1800,success:true};
 assert.equal(fn.validate(twoFields,1800),21300);
 for(const key of ['entered','cleared','overcharge'])assert.throws(()=>fn.validate({...twoFields,[key]:29},1800));
 for(const bad of [{normal:-1},{normal:1.5},{special:Infinity},{cleared:2},{rules:'old'},{seconds:9999},{success:'yes'}])assert.throws(()=>fn.validate({...base,...bad},1));
 assert.equal((await call('submit',base)).rejected,'RUN_NOT_FOUND');
 await call('begin-test',{runId:id});const started=item.value.started;
 await call('begin-test',{runId:id});assert.equal(item.value.started,started);
 failScore=true;await assert.rejects(call('submit',base),/TRANSIENT/);
 assert.equal(item.value.final,true);
 const retry=await call('submit',{...base,normal:100});assert.equal(retry.score,1100);
 assert.equal(scores.get('Validation_Onlyplayer'),1100);assert.equal(scores.has('qa_s1_v1player'),false);
 await call('begin-test',{runId:id2});await call('submit',{...base,runId:id2,normal:0,special:0,elite:0,entered:0,cleared:0,overcharge:0});
 assert.equal(scores.get('Validation_Onlyplayer'),1100);
 await call('begin',{runId:id});assert.equal((await call('submit',{...base,normal:-1})).rejected,'INVALID_COUNTER_normal');
 assert.equal(scores.has('qa_s1_v1player'),false);
 console.log('PASS: weights, seven invalid inputs, tickets, retry, failure recovery, highest-only, rejected submission, test isolation');
})().catch(e=>{console.error(e);process.exitCode=1;});
