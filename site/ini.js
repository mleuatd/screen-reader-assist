import {defaults,normalizeSettings} from './settings.js';

// Browser equivalent of the Windows PortableIni.cs format.
// Export Japanese human-readable keys, retain support for old English INI.
const modeToCode={
  'カーソルで自動読み上げ':'cursor','カーソル自動読み上げ':'cursor','カーソル':'cursor',
  '漫画の吹き出し':'manga','漫画':'manga',
  '範囲選択のみ':'selection','範囲選択':'selection',
  '自動読み上げ停止':'off','停止':'off'
};
const codeToMode={
  cursor:'カーソルで自動読み上げ',
  manga:'漫画の吹き出し',
  selection:'範囲選択のみ',
  off:'自動読み上げ停止'
};
function readToggle(value) {
  const input=value.trim().toLowerCase();
  if(['はい','オン','有効','true','1','yes'].includes(input))return true;
  if(['いいえ','オフ','無効','false','0','no'].includes(input))return false;
  return null;
}
const numeric=value=>Number(value.normalize('NFKC'));
export function parseIni(source){
  if(typeof source!=='string'||source.length>100000)throw new Error('INIファイルが大きすぎます');
  let section='';
  const settings=structuredClone(defaults);
  for(const raw of source.replace(/^\uFEFF/,'').replace(/\r\n?/g,'\n').split('\n')){
    const line=raw.trim();
    if(!line||line.startsWith(';')||line.startsWith('#'))continue;
    if(/^\[[^\]]+\]$/.test(line)){section=line.slice(1,-1).trim().toLowerCase();continue;}
    const at=line.indexOf('=');
    if(at<1)continue;
    const key=line.slice(0,at).trim().toLowerCase();
    let value=line.slice(at+1).trim();
    if(value.length>=2&&value.startsWith('"')&&value.endsWith('"'))
      value=value.slice(1,-1).replace(/""/g,'"');
    switch(section+'.'+key){
      case '基本設定.設定形式のバージョン':
      case 'app.version':settings.version=numeric(value);break;
      case '読み取り動作.読み取りモード':
      case 'reading.mode':settings.reading.mode=modeToCode[value]??value;break;
      case '読み取り動作.カーソル停止時間（ミリ秒）':
      case 'reading.dwellmilliseconds':settings.reading.dwellMilliseconds=numeric(value);break;
      case '読み取り動作.画像の文字も読み取る':
      case 'reading.ocrfallback':{
        const b=readToggle(value);
        if(b!==null)settings.reading.ocrFallback=b;
        break;
      }
      case '音声読み上げ.読み上げ速度':
      case 'speech.rate':settings.speech.rate=numeric(value);break;
      case '音声読み上げ.音量':
      case 'speech.volume':settings.speech.volume=numeric(value);break;
      case '音声読み上げ.使用する音声名':
      case 'speech.voicename':settings.speech.voiceName=value;break;
      case '文字認識.認識した文字をコピー':
      case 'ocr.copytoclipboard':{
        const b=readToggle(value);
        if(b!==null)settings.ocr.copyToClipboard=b;
        break;
      }
      case '文字認識.前回の読み取り範囲を再利用':
      case 'ocr.reuselastregion':{
        const b=readToggle(value);
        if(b!==null)settings.ocr.reuseLastRegion=b;
        break;
      }
      case '文字認識.日本語の文字認識を優先':
      case 'ocr.preferjapanese':{
        const b=readToggle(value);
        if(b!==null)settings.ocr.preferJapanese=b;
        break;
      }
      case '画面拡大.拡大倍率':
      case 'magnifier.zoom':settings.magnifier.zoom=numeric(value);break;
      // Retired global triple-click feature stays disabled even in legacy INI.
    }
  }
  return normalizeSettings(settings);
}

export function writeIni(untrusted){
  const s=normalizeSettings(untrusted);
  const bool=x=>x?'はい':'いいえ';
  const voice=String(s.speech.voiceName??'').replace(/[\r\n]/g,' ').replace(/"/g,'""');
  return [
    '; Screen Reader Assist の設定ファイル（UTF-8）',
    '; ScreenReaderAssist.exe と同じフォルダーに置いてください。',
    '; メモ帳で変更した場合、アプリを再起動すると反映されます。',
    '[基本設定]',
    '設定形式のバージョン=1',
    '',
    '[読み取り動作]',
    '; 選択肢：カーソルで自動読み上げ／漫画の吹き出し／範囲選択のみ／自動読み上げ停止',
    '読み取りモード='+codeToMode[s.reading.mode],
    '; カーソルを止めてから読み始めるまでの時間（400～2000ミリ秒）',
    'カーソル停止時間（ミリ秒）='+s.reading.dwellMilliseconds,
    '画像の文字も読み取る='+bool(s.reading.ocrFallback),
    '',
    '[音声読み上げ]',
    '; －10が最も遅く、0が標準、＋10が最も速い（21段階）',
    '読み上げ速度='+s.speech.rate,
    '; 0～100',
    '音量='+s.speech.volume,
    '; 空欄なら利用できる日本語音声を優先して選びます。',
    '使用する音声名="'+voice+'"',
    '',
    '[文字認識]',
    '認識した文字をコピー='+bool(s.ocr.copyToClipboard),
    '前回の読み取り範囲を再利用='+bool(s.ocr.reuseLastRegion),
    '日本語の文字認識を優先='+bool(s.ocr.preferJapanese),
    '',
    '[画面拡大]',
    '拡大倍率='+s.magnifier.zoom,
    ''
  ].join('\n');
}
