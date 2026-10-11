import {parseIni,writeIni} from './ini.js';
import {defaults} from './settings.js';

// Uses the locally hosted, MIT-licensed JSZip. The uploaded ZIP never leaves the browser.
export const OFFICIAL_PACKAGE_URL='https://github.com/mleuatd/screen-reader-assist/releases/download/v0.6.2-preview.1/ScreenReaderAssist-win-x64.zip';
export const MAX_PACKAGE_BYTES=180*1024*1024;

function executablePath(zip){
  const matches=Object.values(zip.files).filter(e=>
    !e.dir && /(^|\/)ScreenReaderAssist\.exe$/i.test(e.name.replace(/\\/g,'/')));
  if(matches.length!==1)throw new Error('ScreenReaderAssist.exeが1個入ったWindows用ZIPを選択してください。');
  return matches[0].name;
}

export async function openPortablePackage(data,JSZip){
  const zip=await JSZip.loadAsync(data,{checkCRC32:false,createFolders:false});
  const exe=executablePath(zip);
  const folder=exe.slice(0,exe.length-'ScreenReaderAssist.exe'.length);
  const iniName=folder+'settings.ini';
  let settings=structuredClone(defaults);
  const file=zip.file(iniName);
  if(file) settings=parseIni(await file.async('string'));
  return {zip,exe,iniName,settings};
}

export async function createConfiguredPackage(bundle,settings){
  if(!bundle?.zip||!bundle.exe||!bundle.zip.file(bundle.exe))throw new Error('Windows版ZIPを先に読み込んでください。');
  bundle.zip.file(bundle.iniName,writeIni(settings),{binary:false});
  // JSZip preserves existing archive entries and only replaces the settings.
  return bundle.zip.generateAsync({type:'blob',compression:'DEFLATE',compressionOptions:{level:6},streamFiles:true});
}

export async function downloadOfficialPackage(JSZip,fetcher=fetch){
  // A cross-origin release may be blocked by browser CORS: callers show a manual import link.
  const result=await fetcher(OFFICIAL_PACKAGE_URL,{cache:'no-store'});
  if(!result.ok)throw new Error('公式Windows版ZIPの取得に失敗しました（HTTP '+result.status+'）。');
  const declared=Number(result.headers.get('content-length'));
  if(declared>MAX_PACKAGE_BYTES)throw new Error('公式ZIPが大きすぎます。');
  const data=await result.arrayBuffer();
  if(data.byteLength>MAX_PACKAGE_BYTES)throw new Error('公式ZIPが大きすぎます。');
  return openPortablePackage(data,JSZip);
}
