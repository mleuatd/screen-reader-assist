import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeSettings,defaults} from './settings.js';
test('defaults JSON roundtrip',()=>assert.deepEqual(normalizeSettings(JSON.parse(JSON.stringify(defaults))),defaults));
test('clamping untrusted settings',()=>{
  const s=normalizeSettings({version:1,speech:{rate:999,volume:-50},magnifier:{zoom:33},ocr:{copyToClipboard:true}});
  assert.equal(s.speech.rate,10);assert.equal(s.speech.volume,0);assert.equal(s.magnifier.zoom,6);
  assert.equal(s.ocr.copyToClipboard,true);
});
test('reject unknown schema',()=>assert.throws(()=>normalizeSettings({version:5})));
test('no user input injection in schema',()=>{
  const s=normalizeSettings({version:1,speech:{voiceName:'a'.repeat(10000)}});
  assert.equal(s.speech.voiceName.length,100);
});

test('legacy triple click setting is disabled to prevent interference',()=>{
  assert.equal(normalizeSettings({version:1,magnifier:{enableTripleClick:true}}).magnifier.enableTripleClick,false);
});
