// Tests the HTML shell only, with a stub Unity loader; not a real iPhone/game test.
const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const file=path.resolve(__dirname,'../../Assets/WebGLTemplates/24tuMobileWeb/index.html');
const source=fs.readFileSync(file,'utf8').replace(/\{\{\{\s*(\w+_FILENAME)\s*\}\}\}/g,(_,name)=>name+'.js')
  .replace(/\{\{\{\s*JSON.stringify\([^)]+\)\s*\}\}\}/g,'"QA"');
(async()=>{
  const browser=await chromium.launch({channel:'chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  let count=0;
  const check=(ok,label)=>{assert.ok(ok,label);console.log('PASS '+label);count++;};
  try{
    const context=await browser.newContext({viewport:{width:844,height:390},hasTouch:true,isMobile:true});
    const page=await context.newPage();
    let loaders=0;
    await page.route('**/*',route=>{
      if(route.request().url().includes('LOADER_FILENAME')){
        loaders++;return route.fulfill({contentType:'application/javascript',body:'window.messages=[];window.createUnityInstance=(c,o,p)=>{p(1);return Promise.resolve({SendMessage:(...m)=>messages.push(m)});};'});
      }
      if(route.request().url().endsWith('.png'))return route.fulfill({status:204,body:''});
      return route.fulfill({contentType:'text/html',body:source});
    });
    await page.goto('http://127.0.0.1:19999/');
    check(loaders===0,'large files require explicit start tap');
    check(!await page.locator('#rotate').isVisible(),'landscape accepted');
    await page.locator('#start').tap();await page.locator('#resume').waitFor();
    check(loaders===1,'single Unity loader started');
    check(await page.evaluate(()=>messages.at(-1)[2]==='1'),'tap-to-play overlay pauses Unity');
    await page.locator('#play').tap();
    check(await page.evaluate(()=>messages.at(-1)[2]==='0'),'play tap unblocks Unity');
    const bounds=await page.locator('#unity-canvas').boundingBox();
    check(bounds.x>=0&&bounds.y>=0&&bounds.x+bounds.width<=845&&bounds.y+bounds.height<=391,'landscape canvas fits viewport');
    check(Math.abs(bounds.width/bounds.height-16/9)<.01,'game aspect ratio preserved');
    await page.setViewportSize({width:390,height:844});await page.locator('#rotate').waitFor();
    await page.waitForFunction(()=>messages.at(-1)[2]==='1');
    check(true,'portrait shows rotate overlay and pauses');
    await page.setViewportSize({width:844,height:390});await page.locator('#rotate').waitFor({state:'hidden'});
    await page.waitForFunction(()=>messages.at(-1)[2]==='0');check(true,'return to landscape resumes');
    const unavailable=await context.newPage();
    await unavailable.addInitScript(()=>HTMLCanvasElement.prototype.getContext=()=>null);
    await unavailable.route('**/*',route=>route.fulfill({contentType:'text/html',body:source}));
    await unavailable.goto('http://127.0.0.1:19999/');await unavailable.locator('#start').tap();
    check((await unavailable.locator('#instructions').textContent()).includes('WebGL 2'),'unsupported browser receives actionable warning');
    check(await unavailable.locator('#start').textContent()==='다시 시도','failed load is retryable');
    console.log('FINISHED shell checks='+count);
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
