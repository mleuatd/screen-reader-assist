// Pure helpers shared by the browser UI and Node smoke tests.
export const defaults = Object.freeze({
  version:1,
  speech:{rate:0,volume:100,voiceName:""},
  ocr:{copyToClipboard:false,reuseLastRegion:false,preferJapanese:true},
  magnifier:{enableTripleClick:false,zoom:2.0}
});
export function normalizeSettings(raw){
  if(!raw || typeof raw!=="object" || Array.isArray(raw) || raw.version!==1) throw new Error("設定バージョン1のJSONを指定してください");
  const sp=raw.speech||{}, oc=raw.ocr||{}, mg=raw.magnifier||{};
  const clamp=(x,min,max,fallback)=>Number.isFinite(Number(x))?Math.min(max,Math.max(min,Number(x))):fallback;
  return {
    version:1,
    speech:{rate:Math.round(clamp(sp.rate,-10,10,0)),volume:Math.round(clamp(sp.volume,0,100,100)),voiceName:typeof sp.voiceName==='string'?sp.voiceName.slice(0,100):""},
    ocr:{copyToClipboard:oc.copyToClipboard===true,reuseLastRegion:oc.reuseLastRegion===true,preferJapanese:oc.preferJapanese!==false},
    magnifier:{enableTripleClick:mg.enableTripleClick===true,zoom:clamp(mg.zoom,1.5,6,2)}
  };
}
