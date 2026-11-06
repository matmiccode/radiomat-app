"""Convert a downloaded Radio Browser PL/MP3 catalogue into bundled data."""
import json
import re
import unicodedata
from pathlib import Path
from urllib.parse import urlparse

root = Path(__file__).resolve().parent.parent
rows = json.loads((root / "Data/poland.raw.json").read_text(encoding="utf-8"))
stations = []
names, urls = set(), set()
for row in rows:
    name = re.sub(r"\s+", " ", row.get("name", "")).strip()
    url = (row.get("url_resolved") or row.get("url") or "").strip()
    key = unicodedata.normalize("NFKC", name).casefold()
    if (not name or urlparse(url).scheme not in ("https", "http")
            or row.get("codec", "").upper() != "MP3" or row.get("hls")
            or key in names or url in urls):
        continue
    names.add(key)
    urls.add(url)
    description = " · ".join(x for x in [row.get("state", "").strip(),
                                         f"{row['bitrate']} kb/s" if row.get("bitrate") else ""] if x)
    stations.append(dict(Name=name, Url=url, Description=description or "Polskie radio internetowe",
                         Id="rb:" + row["stationuuid"]))
stations.sort(key=lambda s: s["Name"].casefold())
(root / "Data/poland.json").write_text(json.dumps(stations, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Katalog: {len(stations)} stacji, {len(rows)} wpisow zrodlowych")
