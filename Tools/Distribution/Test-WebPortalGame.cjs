// Actual compiled-game smoke test in desktop Chromium; mobile is touch emulation, not hardware QA.
const {chromium}=require('playwright');const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const url=process.argv[2],out=process.argv[3];assert(['localhost','127.0.0.1'].includes(new URL(url).hostname));fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  for(const mode of ['pc','android','iphone']){
   const options=mode==='pc'?{viewport:{width:1366,height:768}}:{viewport:{width:844,height:390},isMobile:true,hasTouch:true,userAgent:mode==='android'?'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36':'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'};
   const context=await browser.newContext(options),page=await context.newPage(),logs=[];
   let entered=0;
   page.on('console',m=>{if(m.text().includes('[LobbyLoadoutApplier]'))entered++;});
   page.on('console',m=>{if(['warning','error'].includes(m.type()))logs.push({type:m.type(),text:m.text()});});page.on('pageerror',e=>logs.push({type:'pageerror',text:String(e)}));
   await page.goto(url);await page.screenshot({path:path.join(out,mode+'-before.png'),fullPage:true});
   await page.locator(`[data-device="${mode}"]`).click();
   let frame;if(mode==='pc')frame=await(await page.locator('#game-frame').elementHandle()).contentFrame();else{await page.waitForURL('**/mobile/?device='+mode);frame=page;}
   await frame.waitForLoadState();
   console.log('Loading real '+mode+' build');if(mode!=='pc')await frame.locator('#start').click();
   await frame.locator('#loading').waitFor({state:'hidden',timeout:240000});
   if(mode!=='pc')await frame.locator('#play').click();
   await page.waitForTimeout(5000);await page.screenshot({path:path.join(out,mode+'-lobby.png')});
   const state=await frame.evaluate(()=>({memory:unityInstance.GetMemoryInfo(),canvas:(()=>{const r=document.querySelector('canvas').getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};})()}));
   assert(state.memory&&state.canvas.width>100);assert(!logs.some(x=>x.type==='pageerror'));
   if(mode==='pc'){await page.locator('#fullscreen').click();await page.waitForTimeout(400);await page.screenshot({path:path.join(out,'pc-fullscreen.png')});await page.evaluate(()=>document.exitFullscreen());}
   async function tapGame(x,y){const r=await frame.locator('canvas').boundingBox();await page.mouse.click(r.x+r.width*x,r.y+r.height*y);}
   await tapGame(.86,.183);await page.waitForTimeout(350);
   await tapGame(.812,.338);await page.waitForTimeout(250);
   await page.screenshot({path:path.join(out,mode+'-prepare.png')});
   await tapGame(.79,.941);
   const until=Date.now()+60000;while(!entered&&Date.now()<until)await page.waitForTimeout(250);
   assert(entered>0,'Battle initializes for '+mode);await page.waitForTimeout(1800);
   await page.screenshot({path:path.join(out,mode+'-battle.png')});
   if(mode==='pc'){await page.keyboard.down('d');await page.waitForTimeout(500);await page.keyboard.up('d');await page.keyboard.press('Shift');}
   else await tapGame(.94,.88);
   await tapGame(.948,.235);await page.waitForTimeout(400);
   await page.screenshot({path:path.join(out,mode+'-settings.png')});
   assert(!logs.some(x=>x.type==='pageerror'),'No browser exceptions in '+mode+' battle');
   fs.writeFileSync(path.join(out,mode+'-evidence.json'),JSON.stringify({state,logs,actualPhoneTest:false},null,2));console.log('PASS real '+mode+' compiled build ready');await context.close();
  }
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
