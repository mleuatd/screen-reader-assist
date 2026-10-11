import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {formatSpeechRate,normalizeSpeechRate,speechRateAccessibilityText} from './speech-rate.js';
import {writeIni,parseIni} from './ini.js';
import {defaults} from './settings.js';

test('speech speed slider has all 21 Windows SAPI steps',()=>{
  const html=readFileSync(fileURLToPath(new URL('./index.html',import.meta.url)),'utf8');
  assert.match(html,/<input id="rate"[^>]*type="range" min="-10" max="10" step="1" value="0"/);
  assert.match(html,/id="rate-reset"/);
  assert.match(html,/id="rate-help"/);
  assert.match(html,/id="rateVal"/);
});
test('screen reader description and visible speed labels work throughout range',()=>{
  assert.equal(formatSpeechRate(-10),'遅い（-10）');
  assert.equal(formatSpeechRate(0),'標準（0）');
  assert.equal(formatSpeechRate(7),'速い（＋7）');
  assert.equal(speechRateAccessibilityText(-2),'標準より遅い、マイナス2');
  assert.equal(speechRateAccessibilityText(0),'標準速度、0');
  assert.equal(speechRateAccessibilityText(10),'標準より速い、プラス10');
  assert.equal(normalizeSpeechRate(99),10);
  assert.equal(normalizeSpeechRate(-99),-10);
});
test('each speech slider step writes and reloads the same value from portable INI',()=>{
  for(let rate=-10;rate<=10;rate++){
    const settings=structuredClone(defaults);
    settings.speech.rate=rate;
    const ini=writeIni(settings);
    assert.match(ini,new RegExp('\\nrate='+rate+'\\n'));
    assert.equal(parseIni(ini).speech.rate,rate);
  }
});
