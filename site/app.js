import {defaults,normalizeSettings} from './settings.js';
const byId=id=>document.getElementById(id);
let model=structuredClone(defaults);
function syncModel(){
  model.speech.rate=Number(byId('rate').value);
  model.speech.volume=Number(byId('volume').value);
  model.speech.voiceName=byId('voiceName').value;
  model.magnifier.zoom=Number(byId('zoom').value);
  model.magnifier.enableTripleClick=byId('triple').checked;
  model.ocr.copyToClipboard=byId('copy').checked;
  model.ocr.preferJapanese=byId('japanese').checked;
  byId('rateVal').textContent=String(model.speech.rate);
  byId('volumeVal').textContent=String(model.speech.volume);
  byId('preview').textContent=JSON.stringify(model,null,2);
}
function syncForm(){
  byId('rate').value=model.speech.rate;
  byId('volume').value=model.speech.volume;
  byId('voiceName').value=model.speech.voiceName;
  byId('zoom').value=String(model.magnifier.zoom);
  byId('triple').checked=model.magnifier.enableTripleClick;
  byId('copy').checked=model.ocr.copyToClipboard;
  byId('japanese').checked=model.ocr.preferJapanese;
  syncModel();
}
const json=()=>JSON.stringify(model,null,2)+'\n';
for(const el of document.querySelectorAll('input:not([type=file]),select')) el.addEventListener('input',syncModel);
byId('save').addEventListener('click',()=>{
  syncModel();
  const url=URL.createObjectURL(new Blob([json()],{type:'application/json'}));
  const a=document.createElement('a');a.href=url;a.download='screen-reader-assist-settings.json';a.click();
  // Wait a turn before revoking, so the download can begin.
  setTimeout(()=>URL.revokeObjectURL(url),1000);
  byId('message').textContent='JSONファイルを保存しました。';
});
byId('copyJson').addEventListener('click',async()=>{
  syncModel();
  try {await navigator.clipboard.writeText(json());byId('message').textContent='JSONをコピーしました。';}
  catch {byId('message').textContent='コピーに失敗しました。ダウンロードを利用してください。';}
});
byId('import').addEventListener('change',async event=>{
  const file=event.target.files?.[0];if(!file)return;
  try {
    if(file.size>100000)throw new Error('設定ファイルが大きすぎます');
    model=normalizeSettings(JSON.parse(await file.text()));syncForm();
    byId('message').textContent='設定を読み込みました。JSONを保存するとWindows側でも利用できます。';
  } catch(err){byId('message').textContent='読み込み失敗: '+err.message;}
  event.target.value='';
});
syncForm();
