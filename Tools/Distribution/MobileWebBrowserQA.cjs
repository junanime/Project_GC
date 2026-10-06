// Local development QA with emulated touch + software ASTC, NOT actual Safari performance.
const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),readline=require('node:readline');
const url=process.argv[2]||'http://127.0.0.1:18726/mobile/';
if(!['localhost','127.0.0.1'].includes(new URL(url).hostname))throw Error('QA is restricted to the local demo.');
const output=process.argv[3]||path.resolve('Library/MobileWebBrowserQA');
fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser=await chromium.launch({channel:'chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  const page=await browser.newPage({viewport:{width:844,height:390},hasTouch:true,isMobile:true,deviceScaleFactor:1,
    userAgent:'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'});
  const cdp=await page.context().newCDPSession(page),logs=[];
  let entered=0;
  page.on('console',m=>{const row={type:m.type(),text:m.text()};logs.push(row);if(row.text.includes('[LobbyLoadoutApplier]'))entered++;if(row.type==='error'||row.text.includes('[WebDemo]'))console.log(JSON.stringify(row));});
  page.on('pageerror',e=>{logs.push({type:'pageerror',text:String(e)});console.log('PAGE_ERROR '+e);});
  page.on('crash',()=>console.log('PAGE_CRASH'));
  await page.goto(url,{waitUntil:'domcontentloaded',timeout:30000});
  let number=0;
  async function state(){
    const screenshot=path.join(output,`screen-${++number}.png`);await page.screenshot({path:screenshot});
    const memory=await page.evaluate(()=>typeof unityInstance!=='undefined'&&unityInstance?unityInstance.GetMemoryInfo():null);
    const result={screenshot,memory,entered,loading:await page.locator('#loading').isVisible(),resume:await page.locator('#resume').isVisible(),rotate:await page.locator('#rotate').isVisible(),status:await page.locator('#status').textContent(),logs:logs.splice(0)};
    fs.appendFileSync(path.join(output,'browser-log.jsonl'),JSON.stringify(result)+'\n');console.log(JSON.stringify(result));
  }
  async function awaitRun(previous){
    const until=Date.now()+45000;
    while(entered<=previous&&Date.now()<until)await page.waitForTimeout(250);
    if(entered<=previous)throw Error('Run did not initialize');
    await page.waitForTimeout(800);
  }
  async function smoke(){
    // Coordinates are from the inspected 844x390 lobby/HUD; uses a separate QA browser save.
    await page.touchscreen.tap(671,72);await page.waitForTimeout(200);
    await page.touchscreen.tap(638,132);await page.waitForTimeout(200);await state();
    let before=entered;await page.touchscreen.tap(622,367);await awaitRun(before);await state();
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:140,y:325,id:1},{x:704,y:325,id:2}]});
    await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:169,y:325,id:1},{x:704,y:298,id:2}]});
    await page.waitForTimeout(800);await state();
    await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    await page.touchscreen.tap(620,336);
    await page.waitForTimeout(300);await page.touchscreen.tap(531,94);await page.waitForTimeout(200);await state();
    await page.setViewportSize({width:390,height:844});await page.waitForTimeout(300);await state();
    await page.setViewportSize({width:844,height:390});await page.waitForTimeout(300);await state();
    await page.touchscreen.tap(422,173);await page.waitForTimeout(200);await state();
    await page.touchscreen.tap(505,255);await page.waitForTimeout(2500);await state();
    before=entered;await page.touchscreen.tap(622,367);await awaitRun(before);await state();
    await page.touchscreen.tap(531,94);await page.waitForTimeout(200);await state();
    console.log('SMOKE_FLOW_COMPLETE: inspect screenshots for UI and the second run');
  }
  await state();console.log('READY: JSON actions state, start, play, smoke, tap(x,y), touch(type,points), resize(width,height), reload, close');
  let queue=Promise.resolve();const rl=readline.createInterface({input:process.stdin});
  rl.on('line',line=>queue=queue.then(async()=>{
    const cmd=JSON.parse(line);
    if(cmd.action==='start')await page.locator('#start').tap();
    else if(cmd.action==='play')await page.locator('#play').tap();
    else if(cmd.action==='smoke')await smoke();
    else if(cmd.action==='tap')await page.touchscreen.tap(cmd.x,cmd.y);
    else if(cmd.action==='touch')await cdp.send('Input.dispatchTouchEvent',{type:cmd.type,touchPoints:cmd.points||[]});
    else if(cmd.action==='resize')await page.setViewportSize({width:cmd.width,height:cmd.height});
    else if(cmd.action==='reload')await page.reload({waitUntil:'domcontentloaded'});
    else if(cmd.action==='close'){await browser.close();rl.close();process.exit(0);}
    await state();
  }).catch(e=>console.log('QA_ERROR '+e.stack)));
})().catch(e=>{console.error(e);process.exit(1);});
