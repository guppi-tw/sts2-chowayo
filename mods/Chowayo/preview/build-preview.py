#!/usr/bin/env python3
"""Build a self-contained, offline voice editor from the local source recordings."""
import base64
import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
sources = root / ".artifacts/chowayo/audio-source"
clips = root / "mods/Chowayo/local-audio"

def audio(path):
    return base64.b64encode(path.read_bytes()).decode("ascii")

items = []
for key, label, filename, metadata, names, labels in [
    ("melody", "旋律", "chowayo-original.wav", "source.json", ["cho", "wa", "yo"], ["チョ", "ワ", "ヨ"]),
    ("hurt", "被ダメージ", "hurt-original.wav", "hurt-original.json", ["hurt"], ["アーウ"]),
    ("death", "死亡", "death-original.wav", "death-original.json", ["death"], ["ウアア"]),
]:
    info = json.loads((sources / metadata).read_text())
    ranges = list(info["cuts_seconds"].values()) if key == "melody" else [info["cut_seconds"]]
    items.append(dict(id=key, label=label, title=info["title"], source=info["source_page"],
                      sha256=info["sha256"], candidateId=info.get("candidate_id"), phraseIndex=info.get("phrase_index", 0), audio=audio(sources / filename),
                      boundaries=[ranges[0][0]] + [r[1] for r in ranges],
                      clips=[dict(id=n, label=l, audio=audio(clips / (n + ".wav")))
                             for n, l in zip(names, labels)]))

candidates = json.loads((sources / "candidates.json").read_text())
labels = {
    "ah": ("アッ", "短い被ダメージ・驚き"), "hurt": ("アーウ", "被ダメージ"),
    "death": ("ウアア", "死亡・大ダメージ"), "dont-press": ("ウアア スピキデルジバゼヨ", "敗北・ピンチ"),
    "dont-pull": ("髪を引っ張らないで", "敗北・ピンチ"),
    "dont-pull-full": ("髪を引っ張らないで フル", "敗北の長いリアクション"),
    "speaki": ("スピキ", "勝利・戦闘開始・報酬"), "tried-hard": ("スピキヲイジメヌンデ", "敗北・しょんぼり"),
    "chowayo-short": ("チョワヨ 短い版", "元の短い版との比較"),
    "chowayo-mop": ("チョワヨ モップ掃除版", "旋律・勝利"),
    "chowayo-hide": ("チョワヨ かくれんぼ版", "旋律・勝利"),
    "chowayo-pumpkin": ("チョワヨ かぼちゃ版", "旋律・勝利"),
    "stop-it": ("デルジバゼヨ", "被ダメージ・ピンチ"),
}
phrases = {
    "chowayo-mop": [[.27, .55, .79, 1.12], [1.18, 1.40, 1.63, 1.91], [2.99, 3.25, 3.50, 3.85]],
    "chowayo-hide": [[.25, .55, .84, 1.24], [1.30, 1.58, 1.804, 2.27], [3.29, 3.65, 4.02, 4.58]],
    "chowayo-pumpkin": [[.32, .58, .86, 1.21], [1.26, 1.59, 1.84, 2.19], [3.02, 3.28, 3.54, 3.92]],
    "chowayo-short": [[.3, .58, .82, 1.16]],
}
for c in candidates:
    c["label"], c["use"] = labels[c["id"]]
    c["audio"] = audio(sources / c["path"])
    c["phrases"] = phrases.get(c["id"], [])
for outcome in json.loads((sources / "outcome-cuts.json").read_text()):
    key, candidate_id, bounds = outcome["id"], outcome["source"], outcome["cut_seconds"]
    label = {"victory": "勝利", "defeat": "敗北"}[key]
    c = next(c for c in candidates if c["id"] == candidate_id)
    current = clips / (key + ".wav")
    items.append(dict(id=key, label=label, title=c["title"], source=c["source"], sha256=c["sha256"],
                      audio=c["audio"], boundaries=bounds, candidateId=candidate_id,
                      clips=[dict(id=key, label=label, audio=audio(current) if current.exists() else None)]))

payload = json.dumps(dict(slots=items, candidates=candidates), ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
template = Path(__file__).with_name("preview.html").read_text()
assert template.count("__AUDIO_DATA__") == 1
output = root / ".artifacts/chowayo/voice-preview.html"
output.write_text(template.replace("__AUDIO_DATA__", payload))
print(f"Created {output} ({output.stat().st_size:,} bytes)")
