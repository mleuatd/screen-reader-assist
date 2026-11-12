# -*- coding: utf-8 -*-
"""
日本語OCRの比較実験（本体アプリには未接続）。
- Windows 11向けのローカル代替候補 PaddleOCR / PP-OCRv6 を評価。
- ゲームのスクリーンショットをGitHubへ公開せず、類似の日本語UIを生成する。
- 原文は試験用の短文。モデルの予測を勝手に補正しない。
- 速度は初期化・初回認識・繰り返し認識を分けて計測する。
"""
import argparse
import json
import os
import pathlib
import sys
import statistics
import time
import unicodedata
from PIL import Image, ImageDraw, ImageFont

LINES = ["あら、プレゼントが届いています。", "有用なものだといいですね。"]
EXPECTED = "".join(LINES)


def normalize(value):
    # Only normalize typography for a fair metric, never rewrite OCR content.
    text = unicodedata.normalize("NFKC", value)
    return "".join(c for c in text if not c.isspace())


def distance(a, b):
    previous = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        current = [i]
        for j, cb in enumerate(b, 1):
            current.append(min(current[-1] + 1, previous[j] + 1, previous[j - 1] + (ca != cb)))
        previous = current
    return previous[-1]


def find_japanese_font():
    root = pathlib.Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    for name in ("YuGothM.ttc", "YuGothR.ttc", "meiryo.ttc", "msgothic.ttc"):
        candidate = root / name
        if candidate.is_file():
            return candidate
    raise RuntimeError("日本語フォントがありません。英数字フォントで代替して合格と扱いません。")


def make_test_image(target, font_path, mode):
    width, height = 860, 310
    picture = Image.new("RGB", (width, height), (45, 65, 88))
    draw = ImageDraw.Draw(picture)
    # Synthetic colored background and interface decoration. This does not
    # reproduce the user's privately provided screenshot.
    for y in range(height):
        shade = (y * 31) // height
        draw.line((0, y, width, y), fill=(29 + shade, 44 + shade, 75 + shade))
    for k in range(20):
        x = (k * 71) % width
        draw.rectangle((x, (k * 43) % height, x + 90, (k * 43) % height + 35),
                       fill=(41 + (k % 3) * 12, 95, 99))
    dialog = (46, 45, 813, 247)
    draw.rounded_rectangle(dialog, radius=10, fill=(241, 239, 231),
                           outline=(112, 202, 87), width=6)
    font = ImageFont.truetype(str(font_path), 43 if mode == "large" else 32)
    ink = (91, 45, 18) if mode == "brown" else (47, 35, 42)
    if mode == "noisy":
        for i in range(14):
            x = 65 + 44 * i
            draw.rectangle((x, 185, x + 20, 215), fill=(234, 230, 225))
    draw.text((73, 77), LINES[0], font=font, fill=ink)
    draw.text((73, 147), LINES[1], font=font, fill=ink)
    picture.save(target)
    return target


def extract_text(predictions):
    lines = []
    for item in predictions:
        try:
            if "rec_texts" in item:
                lines.extend(item["rec_texts"])
                continue
        except (TypeError, KeyError):
            pass
        payload = getattr(item, "json", None)
        if callable(payload):
            payload = payload()
        if isinstance(payload, dict):
            if "res" in payload:
                payload = payload["res"]
            lines.extend(payload.get("rec_texts", []))
    return "".join(str(line) for line in lines)


def run(model):
    from paddleocr import PaddleOCR
    from tempfile import TemporaryDirectory
    font_path = find_japanese_font()
    init_start = time.perf_counter()
    name = f"PP-OCRv6_{model}"
    ocr = PaddleOCR(
        engine="onnxruntime",
        text_detection_model_name=f"{name}_det",
        text_recognition_model_name=f"{name}_rec",
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        device="cpu",
    )
    init_sec = time.perf_counter() - init_start
    output = {"engine": "PaddleOCR", "model": name, "runtime": "onnxruntime",
              "init_seconds": round(init_sec, 3), "results": []}
    with TemporaryDirectory(prefix="japanese_ocr_experiment_") as folder:
        for variant in ("large", "brown", "noisy"):
            image_path = pathlib.Path(folder) / (variant + ".png")
            make_test_image(image_path, font_path, variant)
            duration = []
            first_result = ""
            for repeat in range(4):
                start = time.perf_counter()
                result = ocr.predict(str(image_path))
                duration.append((time.perf_counter() - start) * 1000)
                if repeat == 0:
                    first_result = extract_text(result)
            expected = normalize(EXPECTED)
            actual = normalize(first_result)
            errors = distance(expected, actual)
            summary = {
                "variant": variant,
                "expected_chars": len(expected),
                "recognized_chars": len(actual),
                "character_edits": errors,
                "character_error_rate": round(errors / max(1, len(expected)), 4),
                "exact": actual == expected,
                "cold_ms": round(duration[0], 1),
                "warm_median_ms": round(statistics.median(duration[1:]), 1),
            }
            output["results"].append(summary)
            print("日本語OCR:", json.dumps(summary, ensure_ascii=False), flush=True)
    print("日本語OCR試験結果:", json.dumps(output, ensure_ascii=False), flush=True)
    return output


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", choices=("small", "medium"), default="small")
    parser.add_argument("--summary", default=None)
    args = parser.parse_args()
    outcome = run(args.model)
    if args.summary:
        pathlib.Path(args.summary).write_text(json.dumps(outcome, ensure_ascii=False, indent=2),
                                              encoding="utf-8")
    if any(not row["exact"] for row in outcome["results"]):
        print("注意: 日本語の原文と一致しない例があります。これは精度比較の結果であり、インストール失敗ではありません。")
