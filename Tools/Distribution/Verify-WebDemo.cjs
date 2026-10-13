const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const vm = require('node:vm');
const root = process.argv[2];
if (!root) throw Error('Pass the Web demo build directory.');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
if (html.includes('{{{')) throw Error('Unexpanded WebGL template.');
new vm.Script(html.match(/<script>([\s\S]*?)<\/script>/)[1]);
const files = [...html.matchAll(/buildUrl \+ '([^']+)'/g)].map(m => path.join(root, 'Build', m[1]));
if (files.length !== 4) throw Error('Expected four build files, got ' + files.length);
for (const file of files) if (!fs.existsSync(file) || !fs.statSync(file).size) throw Error('Missing file: ' + file);
const wasm = files.find(file => file.includes('.wasm.'));
if (zlib.gunzipSync(fs.readFileSync(wasm)).subarray(0, 4).toString('hex') !== '0061736d') throw Error('Invalid WebAssembly payload.');
for (const name of ['Launch-WebDemo.cmd', 'Launch-WebDemo.ps1', 'game-icon.png', 'web-build.json', '시작방법.txt'])
    if (!fs.existsSync(path.join(root, name))) throw Error('Missing support file: ' + name);
console.log('PASS: HTML JavaScript, four build references, WebAssembly payload, local launcher and guide.');
