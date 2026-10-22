#!/usr/bin/env node
/* Minimal reproducible, licensed-by-this-project PSD test fixture:
   synthetic shapes only; NOT PurpleVTuber artwork, not a Cubism compatibility claim. */
const fs = require("node:fs");
const path = require("node:path");
const { writePsdBuffer, readPsd } = require("ag-psd");

const W=256,H=320;
function layer(name,box,color) {
  const data=new Uint8ClampedArray(W*H*4);
  for(let y=box[1];y<box[3];y++)for(let x=box[0];x<box[2];x++){
    const k=(y*W+x)*4;
    data[k]=color[0];data[k+1]=color[1];data[k+2]=color[2];data[k+3]=color[3]??255;
  }
  return {name,top:0,left:0,imageData:{width:W,height:H,data}};
}
const back=layer("hair_back", [66,35,190,200],[116,54,177,255]);
const torso=layer("hoodie_body", [63,160,193,292],[34,32,44,255]);
const face=layer("face_base", [86,65,170,182],[241,202,186,255]);
const bang=layer("hair_front", [76,41,178,93],[131,65,183,255]);
// Explicitly construct the composite: no ambiguous alpha compositing.
const composite=new Uint8ClampedArray(W*H*4);
for(const l of [back,torso,face,bang]){
  const a=l.imageData.data;
  for(let i=0;i<a.length;i+=4){
    if(a[i+3]){composite[i]=a[i];composite[i+1]=a[i+1];composite[i+2]=a[i+2];composite[i+3]=255;}
  }
}
const psd={width:W,height:H,imageData:{width:W,height:H,data:composite},
  children:[bang,face,torso,back]}; // Photoshop ordering: top to bottom.
const output=process.argv[2]||"outputs/synthetic/fixture.psd";
fs.mkdirSync(path.dirname(output),{recursive:true});
fs.writeFileSync(output,writePsdBuffer(psd,{noBackground:true,generateThumbnail:false}));
const again=readPsd(fs.readFileSync(output),{useImageData:true,skipThumbnail:true});
if(again.width!==W||again.height!==H||again.children.length!==4)throw Error("PSD roundtrip failed");
const expected=psd.children.map(x=>x.name).join("/");
const actual=again.children.map(x=>x.name).join("/");
if(actual!==expected)throw Error("PSD layer order mismatch "+actual+" != "+expected);
console.log(JSON.stringify({status:"PSD_ROUNDTRIP_OK",file:output,width:W,height:H,layers:actual,bytes:fs.statSync(output).size},null,2));
