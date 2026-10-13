"""Curated, reviewed public-domain packs. Read CRS as data; never execute it.
Run after downloading the five original ZIPs into artifacts/cute-v1.2/acquisition.
The application itself remains offline. Every source byte is preserved and hashed.
"""
import configparser
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
ROLES = dict(Arrow="Normal", Help="Help", AppStarting="Working", Wait="Busy",
             Crosshair="Precision", IBeam="Text", NWPen="Handwriting", No="Unavailable",
             SizeNS="Vertical", SizeWE="Horizontal", SizeNWSE="DiagonalNWSE",
             SizeNESW="DiagonalNESW", SizeAll="Move", UpArrow="Alternate", Hand="Link")
PACKS = [
    ("kawaii-cat-curs0rs", "Kawaii Cat", "MUFFET!! (archive: Sun_l1ght)", "2022-08-14", "Animated little cats, with the author's original sequence and hotspots."),
    ("kawaii-pink-heart", "Kawaii Pink Heart", "bobalover (archive: yurilover)", "2026-07-25", "Pink hearts with playful animated busy, link and resizing roles."),
    ("bunny-cute-kawaii", "Bunny Cute Kawaii", "goat", "2019-12-03", "Soft bunny sprites. Three missing roles explicitly inherit FROST."),
    ("pastel", "Pastel", "saraantie · shapes: quackyduck", "2018-10-19", "Quiet pastel pointers. Three missing roles explicitly inherit FROST."),
    ("cool-cat", "Cool Cat", "Bobcat", "2020-11-30", "Sunglasses, cats and original animation. Includes four bonus resources."),
]
for slug, name, author, date, description in PACKS:
    acquisition = ROOT / "artifacts/cute-v1.2/acquisition"
    archive = acquisition / (slug + ".zip")
    evidence = json.loads((acquisition / (slug + ".json")).read_text(encoding="utf-8"))
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == evidence["sha256"]
    target = ROOT / "assets/themes" / ("cute-" + slug)
    assert not target.exists(), "Never overwrite an existing theme"
    with zipfile.ZipFile(archive) as z:
        assert "Released to Public Domain" in z.read("readme.txt").decode("utf-8-sig")
        files = [n for n in z.namelist() if n.lower().endswith((".cur", ".ani"))]
        mapping = {}
        crs = next((n for n in z.namelist() if n.endswith(".crs")), None)
        if crs:
            c = configparser.ConfigParser(interpolation=None)
            c.read_string(z.read(crs).decode("utf-8-sig"))
            for section, role in ROLES.items():
                if not c.has_section(section):
                    continue
                original = c[section]["Path"].replace("\\", "/")
                matches = [n for n in files if n.lower() == original.lower()]
                assert len(matches) == 1, (slug, role, original)
                mapping[role] = matches[0]
        else:
            mapping = dict(Normal="bunny cursor.ani", Help="help select.ani", Working="working on background.ani",
                           Busy="busy.ani", Precision="precision select.cur", Text="text select.ani",
                           Handwriting="handwriting.cur", Unavailable="unavailble.ani", Vertical="vertical resize.cur",
                           Horizontal="horizontal resize.cur", Move="move.cur", Alternate="alt select.ani")
        target.mkdir(parents=True)
        manifest = dict(SchemaVersion=1, Id="cute-" + slug, Name=name, Author=author,
                        Description=description, Preview="", Roles={}, SourceUrl=evidence["source"],
                        License="Release to Public Domain (RealWorld); inherited FROST resources: project MIT license",
                        Collection="cute", IsAnimated=any(n.endswith(".ani") for n in mapping.values()),
                        PublishedAt=date, PackageSha256=evidence["sha256"], InheritedRoles={}, SourceFiles=[])
        original_paths = {}
        for role in ROLES.values():
            original = mapping.get(role)
            if original:
                filename = role + original[-4:].lower()
                data = z.read(original)
                original_paths[original] = filename
            else:
                frost = json.loads((ROOT / "assets/cursors/theme.json").read_text(encoding="utf-8-sig"))
                source = ROOT / "assets/cursors" / frost["Roles"][role]
                filename, data = role + source.suffix, source.read_bytes()
                manifest["InheritedRoles"][role] = "frost"
            (target / filename).write_bytes(data)
            manifest["Roles"][role] = filename
        for index, original in enumerate(files):
            filename = original_paths.get(original)
            if filename is None:
                filename = "Extras/" + str(index) + original[-4:].lower()
                (target / "Extras").mkdir(exist_ok=True)
                (target / filename).write_bytes(z.read(original))
            manifest["SourceFiles"].append(dict(OriginalName=original, Path=filename, Sha256=hashlib.sha256(z.read(original)).hexdigest()))
        (target / "theme.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (target / "NOTICE.md").write_text(f"# {name}\n\nAuthor: {author}\nPublished: {date}\nSource: {evidence['source']}\nOriginal package: {evidence['download']}\nPackage SHA256: `{evidence['sha256']}`\n\nThe source page and included readme both state **Release to Public Domain**.\nTerms: https://www.rw-designer.com/licenses\nAcquired and checked: 2026-10-13. Third-party cursors retain their own terms.\n\nOriginal resources are byte-for-byte copies, renamed to stable role paths.\nCRS files were only read as role-mapping data, never executed. Extras preserve\nunused source cursors. Pastel uses Busy 2, as selected in the author's CRS;\nBusy is retained as an extra. Missing roles inherit explicitly identified FROST\nresources under the project MIT license. See theme.json for provenance/hashes.\n", encoding="utf-8")
        print(slug, len(files), "resources", len(mapping), "native roles", len(manifest["InheritedRoles"]), "inherited")
