// Browser-independent demo helpers: no OCR or screen capture is performed here.
export function combinedSampleText(items) {
  return items.map(item => item.text?.trim() || '').filter(Boolean).join('。').replace(/。。+/g,'。');
}
export function overlappingIds(box, targets){
  const left=Math.min(box.x1,box.x2),right=Math.max(box.x1,box.x2);
  const top=Math.min(box.y1,box.y2),bottom=Math.max(box.y1,box.y2);
  return targets.filter(t=>Math.min(right,t.right)>Math.max(left,t.left) && Math.min(bottom,t.bottom)>Math.max(top,t.top)).map(t=>t.id);
}
export function closestId(x,y,targets){
  let best=null, dist=Infinity;
  for(const t of targets){
    const dx=Math.max(t.left-x,0,x-t.right),dy=Math.max(t.top-y,0,y-t.bottom);
    const d=dx*dx+dy*dy;
    if(d<dist){dist=d;best=t.id;}
  }
  return best;
}
export class TripleTapTracker {
  constructor({maxGap=420,maxDistance=38}={}) {this.maxGap=maxGap;this.maxDistance=maxDistance;this.count=0;this.previous=null;}
  add({time,x,y}){
    if(this.previous && time-this.previous.time>=0 && time-this.previous.time<=this.maxGap && Math.hypot(x-this.previous.x,y-this.previous.y)<=this.maxDistance){this.count+=1;}
    else this.count=1;
    this.previous={time,x,y};
    if(this.count===3){this.reset();return true;}
    return false;
  }
  reset(){this.count=0;this.previous=null;}
}
export function boundZoom(z){return Math.max(1.5,Math.min(4,Number.isFinite(Number(z))?Number(z):2));}