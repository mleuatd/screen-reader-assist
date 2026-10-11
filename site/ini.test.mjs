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
  assert.ok(ini.includes('[読み取り動作]')&&ini.includes('[音声読み上げ]'));
  assert.ok(!ini.includes('[reading]')&&!ini.includes('[speech]'));
  assert.ok(!ini.trimStart().startsWith('{'));
  assert.ok(!ini.includes('<settings>'));
});

test('all user-editable sections and keys are written in Japanese',()=>{
  const ini=writeIni(defaults);
  for(const name of ['基本設定','読み取り動作','音声読み上げ','文字認識','画面拡大'])
    assert.ok(ini.includes('['+name+']'),name);
  for(const key of ['設定形式のバージョン','読み取りモード','カーソル停止時間（ミリ秒）',
    '画像の文字も読み取る','読み上げ速度','音量','使用する音声名',
    '認識した文字をコピー','前回の読み取り範囲を再利用','日本語の文字認識を優先','拡大倍率'])
    assert.ok(ini.includes(key+'='),key);
  assert.ok(ini.includes('画像の文字も読み取る=はい'));
  assert.ok(ini.includes('認識した文字をコピー=いいえ'));
});
test('Japanese mode labels are readable and round trip',()=>{
  for(const [label,mode] of [['カーソルで自動読み上げ','cursor'],['漫画の吹き出し','manga'],['範囲選択のみ','selection'],['自動読み上げ停止','off']]){
    assert.equal(parseIni('[読み取り動作]\\n読み取りモード='+label).reading.mode,mode);
    const input=structuredClone(defaults); input.reading.mode=mode;
    assert.equal(parseIni(writeIni(input)).reading.mode,mode);
  }
});
test('Japanese toggles and fullwidth numbers are accepted',()=>{
  const parsed=parseIni('[音声読み上げ]\\n読み上げ速度=－３\\n音量=８０\\n' +
    '[読み取り動作]\\nカーソル停止時間（ミリ秒）=１０００\\n画像の文字も読み取る=オフ');
  assert.equal(parsed.speech.rate,-3);
  assert.equal(parsed.speech.volume,80);
  assert.equal(parsed.reading.dwellMilliseconds,1000);
  assert.equal(parsed.reading.ocrFallback,false);
  assert.equal(parseIni('[文字認識]\\n認識した文字をコピー=有効').ocr.copyToClipboard,true);
  assert.equal(parseIni('[文字認識]\\n日本語の文字認識を優先=無効').ocr.preferJapanese,false);
});
test('legacy English INI remains readable and converts into Japanese',()=>{
  const old=parseIni('[app]\\nversion=1\\n[reading]\\nmode=manga\\ndwellMilliseconds=900\\nocrFallback=false\\n' +
    '[speech]\\nrate=5\\nvolume=65\\nvoiceName="Test"\\n' +
    '[ocr]\\ncopyToClipboard=true\\nreuseLastRegion=true\\npreferJapanese=false\\n' +
    '[magnifier]\\nzoom=3.5');
  assert.equal(old.reading.mode,'manga');
  assert.equal(old.speech.rate,5);
  assert.equal(old.ocr.reuseLastRegion,true);
  const exported=writeIni(old);
  assert.ok(exported.includes('[音声読み上げ]')&&exported.includes('読み上げ速度=5'));
  assert.deepEqual(parseIni(exported),old);
});
