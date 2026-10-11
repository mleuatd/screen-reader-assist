import {defaults,normalizeSettings} from './settings.js';

// Browser equivalent of src/ScreenReaderAssist/PortableIni.cs.
// Only standard INI sections and key=value pairs; UTF-8, no XML or JSON required.
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
    const bool=()=>value.toLowerCase()==='true'?true:value.toLowerCase()==='false'?false:null;
    switch(section+'.'+key){
      case 'app.version':settings.version=Number(value);break;
      case 'reading.mode':settings.reading.mode=value;break;
      case 'reading.dwellmilliseconds':settings.reading.dwellMilliseconds=Number(value);break;
      case 'reading.ocrfallback':if(bool()!==null)settings.reading.ocrFallback=bool();break;
      case 'speech.rate':settings.speech.rate=Number(value);break;
      case 'speech.volume':settings.speech.volume=Number(value);break;
      case 'speech.voicename':settings.speech.voiceName=value;break;
      case 'ocr.copytoclipboard':if(bool()!==null)settings.ocr.copyToClipboard=bool();break;
      case 'ocr.reuselastregion':if(bool()!==null)settings.ocr.reuseLastRegion=bool();break;
      case 'ocr.preferjapanese':if(bool()!==null)settings.ocr.preferJapanese=bool();break;
      case 'magnifier.zoom':settings.magnifier.zoom=Number(value);break;
      // Global triple-click hook was removed; never revive it from INI.
    }
  }
  return normalizeSettings(settings);
}

export function writeIni(untrusted){
  const s=normalizeSettings(untrusted);
  const b=x=>x?'true':'false';
  const voice=String(s.speech.voiceName??'').replace(/[\r\n]/g,' ').replace(/"/g,'""');
  return [
    '; Screen Reader Assist - portable settings (UTF-8)',
    '; Place this file beside ScreenReaderAssist.exe. Edit with Notepad.',
    '[app]',
    'version=1',
    '',
    '[reading]',
    '; cursor / manga / selection / off',
    'mode='+s.reading.mode,
    'dwellMilliseconds='+s.reading.dwellMilliseconds,
    'ocrFallback='+b(s.reading.ocrFallback),
    '',
    '[speech]',
    'rate='+s.speech.rate,
    'volume='+s.speech.volume,
    'voiceName="'+voice+'"',
    '',
    '[ocr]',
    'copyToClipboard='+b(s.ocr.copyToClipboard),
    'reuseLastRegion='+b(s.ocr.reuseLastRegion),
    'preferJapanese='+b(s.ocr.preferJapanese),
    '',
    '[magnifier]',
    'zoom='+s.magnifier.zoom,
    ''
  ].join('\n');
}
