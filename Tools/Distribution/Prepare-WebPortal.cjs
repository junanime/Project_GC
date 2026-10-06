// Reuses existing compiled games. Only the HTML shell is updated; hashed game data stays untouched.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const [desktop,mobile,portal,backup]=process.argv.slice(2);
assert(desktop&&mobile&&portal&&backup,'Pass desktop root, mobile root, portal output and a fresh backup directory');
fs.mkdirSync(backup,{recursive:true});fs.mkdirSync(portal,{recursive:true});
function preserve(file,label){const target=path.join(backup,label);assert(!fs.existsSync(target),'Use a fresh backup directory');fs.copyFileSync(file,target);}
const pcFile=path.join(desktop,'index.html'),mobileFile=path.join(mobile,'index.html');
preserve(pcFile,'pc-index.html');preserve(mobileFile,'mobile-index.html');
let pc=fs.readFileSync(pcFile,'utf8');
if(!pc.includes('data-portal-embed'))pc=pc.replace('</head>',`<style data-portal-embed>
html.portal-embed,html.portal-embed body{width:100%;height:100%;min-height:0;padding:0;overflow:hidden;background:#171521}
html.portal-embed main{width:min(100vw,calc(100vh * 16 / 9));height:auto}
html.portal-embed header,html.portal-embed footer{display:none}
html.portal-embed #game{border:0;border-radius:0;box-shadow:none}
</style>
<script>if(new URLSearchParams(location.search).get('embed')==='1')document.documentElement.classList.add('portal-embed');</script>
</head>`);
const existing=fs.readFileSync(mobileFile,'utf8');
let template=fs.readFileSync(path.resolve(__dirname,'../../Assets/WebGLTemplates/24tuMobileWeb/index.html'),'utf8');
const names={DATA_FILENAME:/dataUrl:buildUrl \+ '\/([^']+)'/,FRAMEWORK_FILENAME:/frameworkUrl:buildUrl \+ '\/([^']+)'/,CODE_FILENAME:/codeUrl:buildUrl \+ '\/([^']+)'/,LOADER_FILENAME:/loader.src=buildUrl \+ '\/([^']+)'/};
for(const [key,re] of Object.entries(names)){
 const value=existing.match(re)?.[1];assert(value,'Missing compiled '+key);
 template=template.replaceAll('{{{ '+key+' }}}',value);
}
for(const [key,field] of Object.entries({COMPANY_NAME:'companyName',PRODUCT_NAME:'productName',PRODUCT_VERSION:'productVersion'})){
 const value=existing.match(new RegExp(field+':("[^"\\r\\n]*")'))?.[1];assert(value,'Missing compiled '+field);
 template=template.replace('{{{ JSON.stringify('+key+') }}}',value);
}
assert(!template.includes('{{{'),'Unresolved Unity template field');
fs.writeFileSync(pcFile,pc);fs.writeFileSync(mobileFile,template);
require('./Prepare-MobileWebMemory.cjs').prepare(mobile);
fs.copyFileSync(path.join(__dirname,'WebPortal/index.html'),path.join(portal,'index.html'));
console.log('Prepared chooser and refreshed existing player HTML; game data unchanged.');
