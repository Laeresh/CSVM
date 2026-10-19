import json, os, re
d = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "survey.json"), encoding="utf-8"))
names = re.compile(r"field|park|grass|grs|crop|forest|land|meadow|lawn|veg|hill|terpat|river|cliff|falls|rock")
for ch, r in d.items():
    if not r:
        continue
    print("==", ch)
    for x in r["rows"]:
        rgb = x["rgb"]
        greenish = rgb and rgb[1] >= rgb[0] and rgb[1] > rgb[2] + 8
        if x["area_m2"] >= 5000 and (greenish or names.search(x["tex"])) and not re.search(r"zep|skin|logo", x["tex"]):
            print("   %-24s %10d %5d nc=%-8d %-16s %s" % (x["tex"], x["area_m2"], x["flat_m2"], x["noclutter_m2"], x["template"] or "-", rgb))

