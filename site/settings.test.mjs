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

test('default reading mode and OCR fallback are configured',()=>{
  assert.equal(defaults.reading.mode,'cursor');
  assert.equal(defaults.reading.ocrFallback,true);
});
test('mode settings can be customized',()=>{
  const s=normalizeSettings({version:1,reading:{mode:'manga',dwellMilliseconds:1200,ocrFallback:false}});
  assert.equal(s.reading.mode,'manga');
  assert.equal(s.reading.dwellMilliseconds,1200);
  assert.equal(s.reading.ocrFallback,false);
});
test('legacy settings still load with cursor defaults',()=>{
  const s=normalizeSettings({version:1});
  assert.deepEqual(s.reading,defaults.reading);
});
test('invalid modes and dwell delays are normalized',()=>{
  const s=normalizeSettings({version:1,reading:{mode:'invalid',dwellMilliseconds:99999}});
  assert.equal(s.reading.mode,'cursor');
  assert.equal(s.reading.dwellMilliseconds,2000);
});
