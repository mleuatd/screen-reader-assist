import test from 'node:test';
import assert from 'node:assert/strict';
import './vendor/jszip.min.js';
import {defaults} from './settings.js';
import {openPortablePackage,createConfiguredPackage} from './zip-package.js';
import {parseIni,writeIni} from './ini.js';

const JSZip=globalThis.JSZip;
test('bundled ZIP library loaded without CDN',()=>{
  assert.equal(typeof JSZip,'function');
});
async function sampleZip(nested=''){
  const zip=new JSZip();
  zip.file(nested+'ScreenReaderAssist.exe',new Uint8Array([77,90,42,0,3]),{binary:true});
  zip.file(nested+'notes.txt','kept');
  zip.file(nested+'settings.ini',writeIni(defaults));
  return zip.generateAsync({type:'uint8array',compression:'DEFLATE'});
}
test('import ZIP, edit INI, and re-export intact executable and extra files',async()=>{
  const zip=await sampleZip();
  const bundle=await openPortablePackage(zip,JSZip);
  assert.equal(bundle.settings.reading.mode,'cursor');
  const settings=structuredClone(bundle.settings);
  settings.reading.mode='manga';
  settings.speech.voiceName='日本語の音声';
  const output=await createConfiguredPackage(bundle,settings);
  const saved=await JSZip.loadAsync(await output.arrayBuffer());
  assert.deepEqual(Array.from(await saved.file('ScreenReaderAssist.exe').async('uint8array')),[77,90,42,0,3]);
  assert.equal(await saved.file('notes.txt').async('string'),'kept');
  const parsed=parseIni(await saved.file('settings.ini').async('string'));
  assert.equal(parsed.reading.mode,'manga');
  assert.equal(parsed.speech.voiceName,'日本語の音声');
});
test('nested executable remains beside settings.ini in generated ZIP',async()=>{
  const bundle=await openPortablePackage(await sampleZip('portable/'),JSZip);
  const settings=structuredClone(bundle.settings);settings.speech.volume=17;
  const output=await createConfiguredPackage(bundle,settings);
  const saved=await JSZip.loadAsync(await output.arrayBuffer());
  assert.ok(saved.file('portable/ScreenReaderAssist.exe'));
  assert.equal(parseIni(await saved.file('portable/settings.ini').async('string')).speech.volume,17);
});
test('reject ZIP without Windows app',async()=>{
  const zip=new JSZip();zip.file('hello.txt','x');
  await assert.rejects(openPortablePackage(await zip.generateAsync({type:'uint8array'}),JSZip),/ScreenReaderAssist\.exe/);
});
test('import legacy app ZIP without INI uses editable defaults',async()=>{
  const zip=new JSZip();zip.file('ScreenReaderAssist.exe','MZ');
  const bundle=await openPortablePackage(await zip.generateAsync({type:'uint8array'}),JSZip);
  assert.equal(bundle.settings.reading.mode,'cursor');
  const output=await createConfiguredPackage(bundle,bundle.settings);
  const saved=await JSZip.loadAsync(await output.arrayBuffer());
  assert.ok(saved.file('settings.ini'));
});
