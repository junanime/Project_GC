// Local-only browser QA. Requires Playwright and an installed Chrome; not shipped with the game.
const {chromium} = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const readline = require('node:readline');
const url = process.argv[2] || 'http://localhost:18724/';
if (!['localhost','127.0.0.1'].includes(new URL(url).hostname)) throw Error('QA is restricted to the local demo.');
const output = process.argv[3] || path.resolve('Library/WebDemoBrowserQA');
fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser = await chromium.launch({channel:'chrome',headless:true});
  const page = await browser.newPage({viewport:{width:1280,height:900}});
  const logs = [];
  page.on('console',m=>{ const row={type:m.type(),text:m.text()}; logs.push(row); if(row.type==='error'||row.text.includes('[WebDemo]')) console.log(JSON.stringify(row)); });
  page.on('pageerror',e=>{logs.push({type:'pageerror',text:String(e)});console.log('PAGE_ERROR '+e);});
  page.on('crash',()=>console.log('PAGE_CRASH'));
  await page.goto(url,{waitUntil:'domcontentloaded',timeout:30000});
  let number=0;
  async function state() {
    const file=path.join(output,`screen-${++number}.png`);
    await page.screenshot({path:file});
    console.log(JSON.stringify({screenshot:file,loading:await page.locator('#loading').isVisible(),status:await page.locator('#status').textContent(),logs:logs.splice(0)}));
  }
  await state();
  console.log('READY: JSON commands state, click(x,y), press(key), down(key), up(key), reload, close.');
  let queue=Promise.resolve();
  const rl=readline.createInterface({input:process.stdin});
  rl.on('line',line=>queue=queue.then(async()=>{
    const cmd=JSON.parse(line);
    if(cmd.action==='click')await page.mouse.click(cmd.x,cmd.y);
    else if(cmd.action==='press')await page.keyboard.press(cmd.key);
    else if(cmd.action==='down')await page.keyboard.down(cmd.key);
    else if(cmd.action==='up')await page.keyboard.up(cmd.key);
    else if(cmd.action==='reload')await page.reload({waitUntil:'domcontentloaded'});
    else if(cmd.action==='close'){await browser.close();rl.close();process.exit(0);}
    await state();
  }).catch(e=>console.log('QA_ERROR '+e.stack)));
})().catch(e=>{console.error(e);process.exit(1);});
