#!/usr/bin/env python3
"""Schema-level validation for an official runtime reference; not a renderer test."""
from __future__ import annotations
import argparse,json
from pathlib import Path
def validate(root:Path):
 model=json.loads((root/"Hiyori.model3.json").read_text(encoding="utf-8-sig"))
 physics=json.loads((root/"Hiyori.physics3.json").read_text(encoding="utf-8-sig"))
 pose=json.loads((root/"Hiyori.pose3.json").read_text(encoding="utf-8-sig"))
 display=json.loads((root/"Hiyori.cdi3.json").read_text(encoding="utf-8-sig"))
 errors=[]
 refs=model.get("FileReferences",{})
 for k in ("Moc","Textures","Physics"):
  if k not in refs: errors.append("model3.json missing FileReferences."+k)
 if refs.get("Physics")!="Hiyori.physics3.json": errors.append("physics reference mismatch")
 if physics.get("Version")!=3: errors.append("physics Version != 3")
 meta=physics.get("Meta",{})
 if meta.get("PhysicsSettingCount")!=len(physics.get("PhysicsSettings",[])):
  errors.append("physics setting count mismatch")
 ps=physics.get("PhysicsSettings",[])
 for idx,s in enumerate(ps):
  for k in ("Id","Input","Output","Vertices","Normalization"):
   if k not in s:errors.append(f"physics settings[{idx}] missing {k}")
 for i,s in enumerate(ps):
  for v in s.get("Output",[]):
   if "Destination" not in v: errors.append(f"Output Destination missing in setting {i}")
 return {
  "reference":"Official Live2D/CubismWebSamples develop branch / Hiyori",
  "scope":"structural JSON reference audit ONLY, not PurpleVTuber model physics",
  "model_version":model.get("Version"),
  "physics_version":physics.get("Version"),
  "physics_settings":len(ps),
  "physics_groups":[x.get("Name") for x in meta.get("PhysicsDictionary",[])],
  "pose_version":pose.get("Version"),
  "display_info_version":display.get("Version"),
  "errors":errors,
  "passed":not errors
 }
def main():
 p=argparse.ArgumentParser();p.add_argument("--dir",type=Path,required=True);p.add_argument("--out",type=Path,required=True)
 a=p.parse_args();a.out.parent.mkdir(parents=True,exist_ok=True)
 report=validate(a.dir)
 a.out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 print(json.dumps(report,ensure_ascii=False))
 return 0 if report["passed"] else 1
if __name__=="__main__":raise SystemExit(main())
