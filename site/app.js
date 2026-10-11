import {defaults,normalizeSettings} from './settings.js';
import {parseIni,writeIni} from './ini.js';
import {formatSpeechRate,speechRateAccessibilityText} from './speech-rate.js';
import {openPortablePackage,createConfiguredPackage,downloadOfficialPackage,MAX_PACKAGE_BYTES} from './zip-package.js';

const byId=id=>document.getElementById(id);
let model=structuredClone(defaults),bundle=null;
const status=message=>{byId('message').textContent=message;};
const zipEngine=()=>{if(typeof window.JSZip!=='function')throw new Error('ZIP編集プログラムを読み込めませんでした。画面を更新してください。');return window.JSZip;};

function syncModel(){
  model.speech.rate=Number(byId('rate').value);
  model.speech.volume=Number(byId('volume').value);
  model.speech.voiceName=byId('voiceName').value;
  model.magnifier.zoom=Number(byId('zoom').value);
  model.reading.mode=byId('reading-mode').value;
  model.reading.dwellMilliseconds=Number(byId('reading-dwell').value);
  model.reading.ocrFallback=byId('reading-ocr').checked;
  model.magnifier.enableTripleClick=false;
  model.ocr.copyToClipboard=byId('copy').checked;
  model.ocr.preferJapanese=byId('japanese').checked;
  byId('rateVal').textContent=formatSpeechRate(model.speech.rate);
  byId('rate').setAttribute('aria-valuetext',speechRateAccessibilityText(model.speech.rate));
  byId('volumeVal').textContent=String(model.speech.volume);
  byId('preview').textContent=writeIni(model);
}
function syncForm(){
  byId('rate').value=model.speech.rate;
  byId('volume').value=model.speech.volume;
  byId('voiceName').value=model.speech.voiceName;
  byId('zoom').value=String(model.magnifier.zoom);
  byId('reading-mode').value=model.reading.mode;
  byId('reading-dwell').value=model.reading.dwellMilliseconds;
  byId('reading-ocr').checked=model.reading.ocrFallback;
  byId('triple').checked=false;
  byId('copy').checked=model.ocr.copyToClipboard;
  byId('japanese').checked=model.ocr.preferJapanese;
  syncModel();
}
function downloadBlob(file,name){
  const url=URL.createObjectURL(file);
  const link=document.createElement('a');link.href=url;link.download=name;document.body.appendChild(link);link.click();link.remove();
  setTimeout(()=>URL.revokeObjectURL(url),30000);
}
for(const el of document.querySelectorAll('input:not([type=file]),select'))el.addEventListener('input',syncModel);
byId('rate-reset').addEventListener('click',()=>{
  byId('rate').value='0';
  syncModel();
  byId('rate').focus();
});
byId('save').addEventListener('click',()=>{
  syncModel();
  downloadBlob(new Blob([writeIni(model)],{type:'text/plain;charset=utf-8'}),'settings.ini');
  status('settings.iniを保存しました。Windows版ZIPと同じフォルダーに置いてください。');
});
byId('copyJson').addEventListener('click',async()=>{
  syncModel();
  try {await navigator.clipboard.writeText(writeIni(model));status('INI設定をコピーしました。');}
  catch {status('コピーに失敗しました。INIのダウンロードを利用してください。');}
});
async function importFile(file){
  if(!file)return;
  if(/\.zip$/i.test(file.name)){
    if(file.size>MAX_PACKAGE_BYTES)throw new Error('ZIPファイルが大きすぎます。');
    status('ZIPをブラウザ内で解析しています…');
    const loaded=await openPortablePackage(file,zipEngine());
    bundle=loaded;
    model=loaded.settings;
    syncForm();
    status('ZIPからWindowsアプリを取り込みました。'+
      (loaded.zip.file(loaded.iniName)?'INI設定も読み込みました。':'INIはなかったため標準設定を使用します。')+
      ' 設定を編集してZIPを書き出せます。');
    return;
  }
  if(file.size>100000)throw new Error('設定ファイルが大きすぎます。');
  const content=await file.text();
  if(/\.ini$/i.test(file.name))model=parseIni(content);
  else if(/\.json$/i.test(file.name))model=normalizeSettings(JSON.parse(content));
  else throw new Error('ZIP、INI、JSONファイルを選択してください。');
  syncForm();
  status('設定を取り込みました。編集後、INIまたは設定済みZIPを書き出せます。');
}
async function withImport(file){
  try {await importFile(file);}catch(err){status('読み込み失敗: '+err.message);}
}
byId('import').addEventListener('change',async event=>{
  await withImport(event.target.files?.[0]);event.target.value='';
});
const drop=byId('drop-zone');
drop.addEventListener('dragover',event=>{event.preventDefault();drop.classList.add('dragover');});
drop.addEventListener('dragleave',()=>drop.classList.remove('dragover'));
drop.addEventListener('drop',async event=>{
  event.preventDefault();drop.classList.remove('dragover');
  await withImport(event.dataTransfer.files?.[0]);
});
drop.addEventListener('keydown',event=>{
  if((event.key==='Enter'||event.key===' ')&&event.target===drop){
    event.preventDefault();byId('import').click();
  }
});
drop.addEventListener('click',event=>{
  if(event.target===drop||event.target.tagName==='STRONG'||event.target.tagName==='P')
    byId('import').click();
});
byId('package').addEventListener('click',async()=>{
  const button=byId('package');button.disabled=true;
  try {
    syncModel();
    if(!bundle){
      status('公式Windows版ZIPを読み込み中です。初回は少し時間がかかります…');
      try {
        bundle=await downloadOfficialPackage(zipEngine());
      }catch(err) {
        status('ブラウザの制限により公式ZIPを直接取得できませんでした。上の「Windows用試用版をダウンロード」でZIPを保存し、この画面へドラッグ＆ドロップしてください。設定は保持されています。（'+err.message+'）');
        return;
      }
    }
    status('設定ファイルを組み込んだZIPを作成しています。画面を閉じずにお待ちください…');
    const output=await createConfiguredPackage(bundle,model);
    downloadBlob(output,'ScreenReaderAssist-custom-win-x64.zip');
    status('INI設定を組み込んだWindows用ZIPを保存しました。解凍するとEXEとsettings.iniが同じフォルダーに入っています。');
  }catch(err){status('ZIP作成失敗: '+err.message);}
  finally{button.disabled=false;}
});
syncForm();
