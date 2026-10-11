import {combinedSampleText,overlappingIds,closestId,TripleTapTracker,boundZoom} from './demo-logic.mjs';
const byId = id => document.getElementById(id);
const speech = 'speechSynthesis' in window && 'SpeechSynthesisUtterance' in window ? window.speechSynthesis : null;
const state = {lastText:'',lastBubble:null,mode:'range',zoomed:false,panX:0,panY:0,originX:50,originY:50,dragging:false};
const status = message=>byId('demo-status').textContent=message;

function speak(text){
  const value=String(text??'').trim();
  if(!value){status('読み上げる文章がありません。');return;}
  state.lastText=value;
  if(!speech){status('このブラウザでは音声読み上げが利用できません。文章は画面で確認できます。');return;}
  speech.cancel();
  const utterance=new SpeechSynthesisUtterance(value);
  utterance.lang='ja-JP';
  utterance.rate=Number(byId('demo-rate').value);
  utterance.pitch=Number(byId('demo-pitch').value);
  utterance.volume=1;
  const voice=speech.getVoices().find(v=>v.voiceURI===byId('demo-voice').value);
  if(voice)utterance.voice=voice;
  utterance.onerror=()=>status('音声の再生に失敗しました。ブラウザの音声設定をご確認ください。');
  // Retain a reference so browsers do not prematurely collect the utterance.
  window.__currentDemoUtterance=utterance;
  speech.speak(utterance);
  status('読み上げ中：'+value);
}

function populateVoices(){
  const selector=byId('demo-voice');const keep=selector.value;
  const voices=speech?.getVoices()||[];
  selector.replaceChildren(new Option('端末の既定音声',''));
  voices.sort((a,b)=>Number(b.lang.startsWith('ja'))-Number(a.lang.startsWith('ja')) || a.name.localeCompare(b.name,'ja'));
  for(const v of voices){selector.add(new Option(v.name+'（'+v.lang+'）',v.voiceURI));}
  selector.value=voices.some(v=>v.voiceURI===keep)?keep:'';
}
if(speech){populateVoices();speech.addEventListener?.('voiceschanged',populateVoices);}
byId('demo-rate').addEventListener('input',e=>byId('demo-rate-out').textContent=Number(e.target.value).toFixed(1)+'倍');
byId('demo-pitch').addEventListener('input',e=>byId('demo-pitch-out').textContent=Number(e.target.value).toFixed(1));
byId('demo-voice-test').addEventListener('click',()=>speak('こんにちは。画面に書かれた文字を読み上げます。'));
byId('demo-stop').addEventListener('click',()=>{speech?.cancel();status('読み上げを停止しました。')});
byId('demo-repeat').addEventListener('click',()=>speak(state.lastText));
byId('demo-custom-read').addEventListener('click',()=>speak(byId('demo-custom-text').value));

for(const button of document.querySelectorAll('[data-mode]'))button.addEventListener('click',()=>{
  for(const b of document.querySelectorAll('[data-mode]'))b.setAttribute('aria-pressed',String(b===button));
  for(const panel of document.querySelectorAll('.demo-panel'))panel.hidden=panel.id!=='mode-'+button.dataset.mode;
  state.mode=button.dataset.mode;
  status({range:'範囲読み上げモードです。',manga:'漫画読み上げモードです。',zoom:'画面拡大モードです。'}[state.mode]);
});

const targets=[...document.querySelectorAll('.reading-target')];
const region=byId('range-screen'),selectionRect=byId('selection-rectangle');
function targetBoxes(){return targets.map(t=>({id:t.dataset.sampleId,...rectPlain(t.getBoundingClientRect())}));}
// Make DOMRect extraction work in Chromium and browsers without DOMRect#toJSON.
function rectPlain(r){return {left:r.left,top:r.top,right:r.right,bottom:r.bottom};}
function readSelected(ids){
  const list=targets.filter(t=>ids.includes(t.dataset.sampleId)).map(t=>({text:t.dataset.sampleText}));
  const value=combinedSampleText(list);
  byId('range-result').textContent=value||'文章が見つかりませんでした。';
  speak(value);
}
for(const button of document.querySelectorAll('[data-pick]'))button.addEventListener('click',()=>readSelected([button.dataset.pick]));
byId('read-all').addEventListener('click',()=>readSelected(targets.map(x=>x.dataset.sampleId)));
let rangePointer=null;
region.addEventListener('pointerdown',e=>{
  if(e.button!==0 && e.pointerType==='mouse')return;
  e.preventDefault();
  rangePointer={pointerId:e.pointerId,startX:e.clientX,startY:e.clientY,x:e.clientX,y:e.clientY};
  try{region.setPointerCapture(e.pointerId);}catch{}
  selectionRect.hidden=false;renderSelection();
});
function renderSelection(){if(!rangePointer)return;const r=region.getBoundingClientRect();const x1=rangePointer.startX-r.left,y1=rangePointer.startY-r.top,x2=rangePointer.x-r.left,y2=rangePointer.y-r.top;
  selectionRect.style.left=Math.min(x1,x2)+'px';selectionRect.style.top=Math.min(y1,y2)+'px';selectionRect.style.width=Math.abs(x2-x1)+'px';selectionRect.style.height=Math.abs(y2-y1)+'px';
}
region.addEventListener('pointermove',e=>{if(!rangePointer||rangePointer.pointerId!==e.pointerId)return;rangePointer.x=e.clientX;rangePointer.y=e.clientY;renderSelection();});
region.addEventListener('pointerup',e=>{if(!rangePointer||rangePointer.pointerId!==e.pointerId)return;const p=rangePointer;rangePointer=null;selectionRect.hidden=true;
  const boxes=targetBoxes();const box={x1:p.startX,y1:p.startY,x2:e.clientX,y2:e.clientY};
  const isTap=Math.hypot(box.x2-box.x1,box.y2-box.y1)<8;
  let ids=isTap?[]:overlappingIds(box,boxes);
  if(!ids.length){const id=closestId(e.clientX,e.clientY,boxes);ids=id?[id]:[];}
  readSelected(ids);
});
region.addEventListener('pointercancel',()=>{rangePointer=null;selectionRect.hidden=true});
region.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();readSelected(targets.map(x=>x.dataset.sampleId));}});

const bubbles=[...document.querySelectorAll('[data-bubble-text]')];
for(const b of bubbles)b.addEventListener('click',()=>{
  bubbles.forEach(x=>x.classList.remove('is-reading'));
  b.classList.add('is-reading');state.lastBubble=b;
  byId('manga-result').textContent=b.dataset.bubbleText;
  speak(b.dataset.bubbleText);
});
byId('manga-read-all').addEventListener('click',()=>{
  bubbles.forEach(x=>x.classList.remove('is-reading'));
  byId('manga-result').textContent=combinedSampleText(bubbles.map(x=>({text:x.dataset.bubbleText})));
  speak(byId('manga-result').textContent);
});

const zoomViewport=byId('zoom-viewport'),zoomContent=byId('zoom-content'),tracker=new TripleTapTracker();
const zoomButton=byId('zoom-toggle');
function renderZoom(){
  const zoom=state.zoomed?boundZoom(byId('demo-zoom-level').value):1;
  zoomContent.style.transformOrigin=state.originX+'% '+state.originY+'%';
  zoomContent.style.transform=`translate(${state.panX}px, ${state.panY}px) scale(${zoom})`;
  zoomViewport.classList.toggle('is-zoomed',state.zoomed);
  zoomButton.textContent=state.zoomed?'拡大を解除':'拡大する';
  zoomButton.setAttribute('aria-pressed',String(state.zoomed));
  byId('zoom-state').textContent=state.zoomed?`拡大中：${zoom.toFixed(1)}倍。サンプル画面内だけ拡大しています。`:'拡大していません。';
}
function changeZoom(active,x=50,y=50){state.zoomed=active;state.originX=x;state.originY=y;state.panX=0;state.panY=0;renderZoom();}
byId('demo-zoom-level').addEventListener('input',e=>{byId('demo-zoom-out').textContent=boundZoom(e.target.value).toFixed(1)+'倍';renderZoom();});
zoomButton.addEventListener('click',()=>changeZoom(!state.zoomed));
byId('zoom-reset').addEventListener('click',()=>changeZoom(false));
for(const [id,dx,dy] of [['zoom-left',-55,0],['zoom-right',55,0],['zoom-up',0,-55],['zoom-down',0,55]])byId(id).addEventListener('click',()=>{
  if(!state.zoomed)changeZoom(true);
  state.panX=Math.max(-240,Math.min(240,state.panX+dx));state.panY=Math.max(-220,Math.min(220,state.panY+dy));renderZoom();
});
let gesture=null;
zoomViewport.addEventListener('pointerdown',e=>{
  if(e.button!==0&&e.pointerType==='mouse')return;
  const r=zoomViewport.getBoundingClientRect();
  const triple=tracker.add({time:e.timeStamp,x:e.clientX,y:e.clientY});
  if(triple){
    const previous=state.zoomed;
    changeZoom(!previous,Math.min(100,Math.max(0,100*(e.clientX-r.left)/r.width)),Math.min(100,Math.max(0,100*(e.clientY-r.top)/r.height)));
    gesture={id:e.pointerId,startX:e.clientX,startY:e.clientY,wasZoomed:previous,moved:false};
    try{zoomViewport.setPointerCapture(e.pointerId)}catch{}
  }
});
zoomViewport.addEventListener('pointermove',e=>{
  if(!gesture||gesture.id!==e.pointerId||!state.zoomed)return;
  const dx=e.clientX-gesture.startX,dy=e.clientY-gesture.startY;
  if(Math.hypot(dx,dy)>12)gesture.moved=true;
  if(gesture.moved){state.panX=Math.max(-240,Math.min(240,dx));state.panY=Math.max(-220,Math.min(220,dy));renderZoom();}
});
zoomViewport.addEventListener('pointerup',e=>{
  if(!gesture||gesture.id!==e.pointerId)return;
  if(gesture.moved)changeZoom(false);
  gesture=null;
});
zoomViewport.addEventListener('pointercancel',()=>{gesture=null;tracker.reset();changeZoom(false);});
zoomViewport.addEventListener('keydown',e=>{
  if(e.key==='Enter'||e.key===' '){e.preventDefault();changeZoom(!state.zoomed);}
  if(state.zoomed&&['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key)){
    e.preventDefault();const k={ArrowLeft:[-30,0],ArrowRight:[30,0],ArrowUp:[0,-30],ArrowDown:[0,30]}[e.key];state.panX=Math.max(-240,Math.min(240,state.panX+k[0]));state.panY=Math.max(-220,Math.min(220,state.panY+k[1]));renderZoom();
  }
});
renderZoom();