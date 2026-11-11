"""Add a release to manifest.json (Jellyfin plugin repository format)."""
import datetime
import hashlib
import json
import sys

manifest_path, zip_path, version, target_abi, source_url, changelog = sys.argv[1:7]

with open(zip_path, "rb") as f:
    checksum = hashlib.md5(f.read()).hexdigest()

with open(manifest_path, encoding="utf-8") as f:
    manifest = json.load(f)

plugin = manifest[0]
plugin["versions"] = [v for v in plugin["versions"] if v["version"] != version]
plugin["versions"].insert(0, {
    "version": version,
    "changelog": changelog,
    "targetAbi": target_abi,
    "sourceUrl": source_url,
    "checksum": checksum,
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
})

with open(manifest_path, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2, ensure_ascii=False)
    f.write("\n")
