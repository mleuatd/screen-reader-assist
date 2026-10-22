#!/usr/bin/env python3
"""Read-only audit for original PurpleVTuber ZIP/PNG material.
Creates PNG contact sheets, JSON inventory and a CSV report. Never modifies source.
"""
from __future__ import annotations
import argparse, csv, hashlib, io, json, math, re, sys, zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

Image.MAX_IMAGE_PIXELS = 250_000_000

def sha256(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()

def scan_zip(path: Path):
    with zipfile.ZipFile(path) as z:
        for n in sorted(z.namelist(), key=str.casefold):
            if n.lower().endswith(".png") and not n.endswith("/"):
                yield n, z.read(n)

def scan_dir(path: Path):
    for p in sorted(path.rglob("*.png"), key=lambda p:str(p).casefold()):
        yield str(p.relative_to(path)), p.read_bytes()

def checker(size=(240,260), spacing=12):
    im=Image.new("RGB",size,(230,230,230)); d=ImageDraw.Draw(im)
    for y in range(0,size[1],spacing):
        for x in range(0,size[0],spacing):
            if (x//spacing+y//spacing)%2==0: d.rectangle((x,y,x+spacing-1,y+spacing-1),fill=(202,202,202))
    return im

def main():
    p=argparse.ArgumentParser()
    p.add_argument("source",type=Path,help="ZIP archive or directory of PNGs")
    p.add_argument("--out",type=Path,default=Path("outputs/source-audit"))
    p.add_argument("--select",default="",help="Case-insensitive substring, e.g. Stage322")
    p.add_argument("--limit",type=int,default=500)
    args=p.parse_args()
    args.out.mkdir(parents=True,exist_ok=True)
    if not args.source.exists(): raise SystemExit(f"NOT_FOUND: {args.source}")
    if args.source.is_file() and not zipfile.is_zipfile(args.source): raise SystemExit("Source is not a ZIP archive")
    source=(scan_zip(args.source) if args.source.is_file() else scan_dir(args.source))
    items=[];previews=[];total_matched=0; problems=[]
    tilew,tileh=310,330
    for name, raw in source:
        if args.select.casefold() not in name.casefold(): continue
        total_matched+=1
        if len(items)>=args.limit: continue
        try:
            with Image.open(io.BytesIO(raw)) as im:
                im.seek(0); rgba=im.convert("RGBA")
                bbox=rgba.getchannel("A").getbbox()
                extrema=rgba.getchannel("A").getextrema()
                rec=dict(file=name,width=rgba.width,height=rgba.height,sha256=sha256(raw),alpha_bbox=bbox,
                         alpha_min=extrema[0],alpha_max=extrema[1],is_empty=bbox is None,size_bytes=len(raw))
                if bbox is None: problems.append(dict(file=name,type="fully_transparent"))
                if rgba.mode!="RGBA":problems.append(dict(file=name,type="converted_to_rgba"))
                thumb=rgba.copy()
                thumb.thumbnail((tilew-18,tileh-73),Image.Resampling.LANCZOS)
                plate=checker((tilew,tileh-50))
                plate.paste(thumb,((tilew-thumb.width)//2,(tileh-50-thumb.height)//2),thumb)
                previews.append((plate,name,bbox))
                items.append(rec)
        except Exception as e:
            problems.append(dict(file=name,type="decode_failed",error=str(e)))
    columns=5;rows_per_sheet=6;per=columns*rows_per_sheet
    for offset in range(0,len(previews),per):
        page=Image.new("RGB",(columns*tilew,rows_per_sheet*tileh),(245,245,245))
        draw=ImageDraw.Draw(page)
        for j,(pic,name,bbox) in enumerate(previews[offset:offset+per]):
            x=(j%columns)*tilew;y=(j//columns)*tileh
            page.paste(pic,(x,y))
            stem=name if len(name)<=54 else "…"+name[-53:]
            draw.text((x+7,y+tileh-46),stem,fill=(16,16,16))
            draw.text((x+7,y+tileh-24),("EMPTY" if bbox is None else f"alpha {bbox}"),fill=(65,65,65))
        page.save(args.out/f"contact_sheet_{offset//per+1:03d}.png")
    report={
        "source":str(args.source),"selector":args.select,"matched":total_matched,
        "inspected":len(items),"truncated":total_matched>len(items),
        "status":"FILES_READ_ONLY_INSPECTED; NOT_A_LIVE2D_VALIDATION",
        "source_sha256":None,"images":items,"issues":problems
    }
    if args.source.is_file():
        h=hashlib.sha256()
        with args.source.open("rb") as f:
            for chunk in iter(lambda:f.read(4*1024*1024),b""):h.update(chunk)
        report["source_sha256"]=h.hexdigest()
    (args.out/"inventory.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    with (args.out/"inventory.csv").open("w",newline="",encoding="utf-8-sig") as f:
        writer=csv.DictWriter(f,fieldnames=["file","width","height","sha256","alpha_bbox","alpha_min","alpha_max","is_empty","size_bytes"])
        writer.writeheader();writer.writerows(items)
    print(json.dumps({k:report[k] for k in ("matched","inspected","truncated","issues")},ensure_ascii=False)[:2000])
    return 0 if items and not problems else (2 if not items else 0)
if __name__=="__main__":sys.exit(main())
