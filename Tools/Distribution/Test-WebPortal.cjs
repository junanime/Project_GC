// Browser-shell regression coverage. Unity is stubbed; this is not a phone hardware test.
const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const url=process.argv[2]||'http://127.0.0.1:18726/',out=process.argv[3];
assert(out);assert(['localhost','127.0.0.1'].includes(new URL(url).hostname));fs.mkdirSync(out,{recursive:true});
const ios='Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1';
const android='Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36';
let count=0;function check(value,label){assert(value,label);console.log('PASS '+label);count++;}
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  for(const kind of ['pc','android','iphone','iphone-chrome','inapp']){
   const mobile=kind!=='pc',ua=kind==='android'?android:kind==='iphone-chrome'?ios.replace('Version/18.0','CriOS/129.0.0.0'):kind==='inapp'?ios+' KAKAOTALK/11.0':ios;
   const context=await browser.newContext({viewport:mobile?{width:390,height:844}:{width:1600,height:1000},...(mobile?{hasTouch:true,isMobile:true,userAgent:ua}:{})});
   const page=await context.newPage(),errors=[],buildRequests=[];page.on('pageerror',e=>errors.push(String(e)));
   page.on('request',r=>{if(r.url().includes('/Build/'))buildRequests.push(r.url());});
   await page.route('**/*.loader.js',route=>route.fulfill({contentType:'application/javascript',body:`window.createUnityInstance=async(canvas,config,progress)=>{window.qaConfig=config;window.qaMessages=[];progress(1);return {SendMessage:(...args)=>qaMessages.push(args),SetFullscreen:()=>{},GetMemoryInfo:()=>({})};};`}));
   await page.goto(url);await page.screenshot({path:path.join(out,kind+'-chooser.png'),fullPage:true});
   check(await page.locator('[data-device]').count()===3,kind+' three buttons');
   check(buildRequests.length===0&&await page.locator('iframe').count()===0,kind+' no game download before choice');
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),kind+' no horizontal overflow');
   const selected=kind==='pc'?'pc':kind==='android'?'android':'iphone';
   await page.locator('[data-device="'+(selected==='pc'?'android':'pc')+'"]').click();
   check(await page.locator('iframe').count()===0,kind+' wrong device blocked');
   await page.locator('[data-device="'+selected+'"]').click();
   if(kind==='inapp'){
    check(await page.locator('iframe').count()===0&&(await page.locator('#message').textContent()).includes('Safari'),'in-app Safari guidance');
   }else{
    let frame;
    if(mobile){await page.waitForURL('**/mobile/?device='+selected);frame=page;check(await page.locator('iframe').count()===0,kind+' mobile is a top-level page');}
    else frame=await (await page.locator('#game-frame').elementHandle()).contentFrame();
    await frame.waitForLoadState();
    if(mobile){
     check(await frame.locator('#rotate').isVisible(),kind+' portrait instruction');
     await page.setViewportSize({width:844,height:390});await frame.locator('#start').click();await frame.locator('#resume').waitFor();
     check((await frame.locator('#instructions').textContent()).includes(selected==='android'?'Chrome':'Safari'),kind+' correct browser guidance');
     await frame.locator('#play').click();check(await frame.locator('#resume').isHidden(),kind+' touch start');
     await page.setViewportSize({width:390,height:844});
     await page.waitForTimeout(120);
     check(await frame.evaluate(()=>qaMessages.some(args=>args[2]==='1')),kind+' portrait pause bridge');
     await page.setViewportSize({width:844,height:390});
    }else{
     await frame.waitForFunction(()=>!!window.qaConfig);
     check(new URL(frame.url()).pathname==='/play-pc.html','desktop same URL directory for save storage');
     check(await frame.locator('header').isHidden()&&await page.locator('.paper').isVisible(),'embedded player and right-hand paper guide');
     await page.locator('#fullscreen').click();check(await page.evaluate(()=>!!document.fullscreenElement),'fullscreen screen wrapper');
     await page.evaluate(()=>document.exitFullscreen());
    }
    check(buildRequests.length===1,kind+' only chosen loader requested');
    await page.screenshot({path:path.join(out,kind+'-player-shell.png')});
   }
   check(errors.length===0,kind+' no JavaScript errors: '+errors.join(','));await context.close();
  }
  console.log('FINISHED portal checks='+count);
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
