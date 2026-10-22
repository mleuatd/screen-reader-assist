#!/usr/bin/env python3
"""Compare rebuilt RGBA composites against untouched reference rest pose.
Does NOT claim that alpha holes or drawings have been fixed.
"""
import argparse,json
from pathlib import Path
from PIL import Image,ImageChops,ImageEnhance
def main():
 p=argparse.ArgumentParser()
 p.add_argument("--reference",required=True,type=Path)
 p.add_argument("--composite",required=True,type=Path)
 p.add_argument("--out",default="outputs/comparison",type=Path)
 p.add_argument("--tolerance",type=int,default=18)
 a=p.parse_args();a.out.mkdir(parents=True,exist_ok=True)
 with Image.open(a.reference) as r,Image.open(a.composite) as c:
  r=r.convert("RGBA");c=c.convert("RGBA")
  if r.size!=c.size:raise SystemExit(f"SIZE_MISMATCH: {r.size} != {c.size}")
  refA=r.getchannel("A");compA=c.getchannel("A")
  import numpy as np
  rs=np.asarray(r,dtype=np.int16);cs=np.asarray(c,dtype=np.int16)
  visible=rs[:,:,3]>=16
  absent=visible & (cs[:,:,3]+a.tolerance<rs[:,:,3])
  wrong=visible & (np.max(abs(rs[:,:,:3]-cs[:,:,:3]),axis=2)>a.tolerance) & ~absent
  # mask encodes missing regions in red, colors mismatches in amber
  output=np.zeros((r.height,r.width,4),dtype=np.uint8)
  output[absent]=[255,0,0,230]
  output[wrong]=[255,185,0,180]
  Image.fromarray(output,"RGBA").save(a.out/"difference_overlay.png")
  overlay=r.copy();overlay.alpha_composite(Image.fromarray(output,"RGBA"))
  overlay.save(a.out/"reference_with_differences.png")
  report={"status":"TECHNICAL_COMPARISON_ONLY","dimensions":r.size,"visible_pixels":int(visible.sum()),
          "missing_alpha_pixels":int(absent.sum()),"wrong_color_pixels":int(wrong.sum()),
          "missing_fraction":round(float(absent.sum()/max(1,visible.sum())),7),
          "wrong_color_fraction":round(float(wrong.sum()/max(1,visible.sum())),7),
          "tolerance":a.tolerance,"passed":bool(not absent.any() and not wrong.any()),
          "IMPORTANT":"Rest-pose match alone does not prove good occluded underpainting or Cubism import."}
  (a.out/"comparison.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
  print(json.dumps(report,ensure_ascii=False))
  return 0 if report["passed"] else 3
if __name__=="__main__":raise SystemExit(main())
