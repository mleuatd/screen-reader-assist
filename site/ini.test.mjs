import test from 'node:test';
import assert from 'node:assert/strict';
import {defaults} from './settings.js';
import {parseIni,writeIni} from './ini.js';

test('INI text round-trips all editable settings including Japanese voice',()=>{
  const expected=structuredClone(defaults);
  expected.reading={mode:'manga',dwellMilliseconds:1250,ocrFallback:false};
  expected.speech={rate:-4,volume:55,voiceName:'日本語 "はるか=試験"'};
  expected.magnifier.zoom=4;
  expected.ocr.copyToClipboard=true;
  expected.ocr.preferJapanese=false;
  assert.deepEqual(parseIni(writeIni(expected)),expected);
});
test('INI parser ignores unknown settings and supports comments',()=>{
  const s=parseIni('; comment\n[reading]\nmode=selection\nunknown=whatever\n[unused]\nfoo=bar');
  assert.equal(s.reading.mode,'selection');
  assert.equal(s.speech.volume,100);
});
test('INI parser clamps values and rejects unknown version',()=>{
  const s=parseIni('[reading]\ndwellMilliseconds=90000\nmode=weird\n[ocr]\ncopyToClipboard=true');
  assert.equal(s.reading.dwellMilliseconds,2000);
  assert.equal(s.reading.mode,'cursor');
  assert.equal(s.ocr.copyToClipboard,true);
  assert.throws(()=>parseIni('[app]\nversion=99'),/バージョン|version|JSON/);
});
test('INI never enables deprecated triple-click',()=>{
  assert.equal(parseIni('[magnifier]\nenableTripleClick=true').magnifier.enableTripleClick,false);
});
test('INI is UTF-8 friendly plain text, not JSON/XML',()=>{
  const ini=writeIni(defaults);
  assert.ok(ini.includes('[reading]')&&ini.includes('[speech]'));
  assert.ok(!ini.trimStart().startsWith('{'));
  assert.ok(!ini.includes('<settings>'));
});
