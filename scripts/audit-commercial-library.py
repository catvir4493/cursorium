"""Prepare conservative review data from real signed-package identities; never grant third-party rights."""
import argparse, hashlib, html, json, pathlib, re, subprocess, zipfile
from datetime import datetime, timezone

p=argparse.ArgumentParser()
p.add_argument('--catalog',type=pathlib.Path,required=True)
p.add_argument('--packages',type=pathlib.Path,action='append',required=True)
p.add_argument('--fetch-record',type=pathlib.Path,required=True)
p.add_argument('--library-root',type=pathlib.Path,required=True)
p.add_argument('--output',type=pathlib.Path,required=True)
a=p.parse_args()
if a.output.exists():raise ValueError('Use a fresh rights staging directory')
catalog_bytes=a.catalog.read_bytes();catalog=json.loads(catalog_bytes)
fetches={r['url']:r for r in json.loads(a.fetch_record.read_text(encoding='utf-8-sig'))}
sha=lambda b:hashlib.sha256(b).hexdigest()
base='7ed2719a7a842e201b3e2fb38a992bf711e8fd9d'
license_bytes=subprocess.check_output(['git','show',base+':LICENSE'])
license_url='https://github.com/catvir4493/cursorium/blob/'+base+'/LICENSE'
records=[];ledger=[]
for r in catalog['Resources']:
    matches=[d/(r['Id']+'.zip') for d in a.packages if (d/(r['Id']+'.zip')).is_file()]
    matches=[path for path in matches if path.stat().st_size==r['PackageSize'] and sha(path.read_bytes())==r['SHA256'].lower()]
    if not matches:raise ValueError('No exact signed-catalog package: '+r['Id'])
    original=r['Type']=='trail' and r['Author']=='CURSORIUM' and r['License']=='MIT'
    with zipfile.ZipFile(matches[0]) as z:
        notice_entries=[i for i in z.infolist() if i.filename=='NOTICE.txt']
        if len(notice_entries)!=1 or notice_entries[0].file_size>256*1024:raise ValueError('Missing/bounded NOTICE: '+r['Id'])
        notice=z.read(notice_entries[0]); text=notice.decode('utf-8-sig')
        if r['Author'] not in text or (not original and r['SourceUrl'] not in text):raise ValueError('Attribution mismatch: '+r['Id'])
        if original and 'Copyright (c) 2026 FROST Project' not in text:raise ValueError('Original MIT copyright notice missing')
        cur=sum(i.filename.lower().endswith('.cur') for i in z.infolist());ani=sum(i.filename.lower().endswith('.ani') for i in z.infolist())
    ev=[dict(Url=r['PackageUrl'],Sha256=sha(notice),Kind='PackageNotice')]
    page=fetches.get(r['SourceUrl'])
    observed=None
    if page and page.get('fetched') and not original:
        snapshot=a.fetch_record.parent/(r['SourceUrl'].rsplit('/',1)[-1]+'.html')
        raw=snapshot.read_bytes()
        if sha(raw)!=page['sha256'].lower():raise ValueError('Source-page evidence changed: '+r['Id'])
        match=re.search(r'under the\s*<strong>(.*?)</strong>',raw.decode('utf-8'),re.S)
        observed=html.unescape(re.sub('<[^>]*>','',match[1])).strip() if match else None
    if original:ev.append(dict(Url=license_url,Sha256=sha(license_bytes),Kind='RepositoryLicense'))
    elif page and page.get('fetched'):ev.append(dict(Url=r['SourceUrl'],Sha256=page['sha256'].lower(),Kind='SourcePage'))
    else:ev.append(dict(Url='https://github.com/catvir4493/cursorium-library/blob/2ca809e/SOURCES.md',Sha256=sha((a.library_root/'SOURCES.md').read_bytes()),Kind='RepositorySource'))
    note=('Original data-only preset; retain project MIT and supplier notices. This is not Steam integration approval.' if original else 'Needs commercial provenance review. Site license declaration and package NOTICE retained; no independent chain-of-title certification. Existing Community availability unchanged.')
    if 'CC by' in r['License']:note+=' Source does not identify a CC BY version; obtain precise terms and attribution before Steam inclusion.'
    if r['Id']=='cute-pastel':note+=' Source credits quackyduck shapes; derivative permission chain remains open.'
    if r['Id'] in ('cute-kawaii-cat-curs0rs','cute-kawaii-pink-heart'):note+=' Author aliases/archive attribution require confirmation.'
    if original and r['SourceUrl'] not in text:note+=' Legacy package NOTICE lacks a source URL; this signed evidence supplies its immutable repository license link. Historical package is not rewritten.'
    entry=dict(StableId=r['Id'],Version=r['Version'],Scope='Online',PackageSha256=r['SHA256'],Author=r['Author'],SourceUrl=r['SourceUrl'],License=r['License'],LicenseEvidence=ev,CommercialClearance='Approved' if original else 'Needs Review',RedistributionPermission='AllowedWithNotice' if original else 'Needs Review',AttributionRequired=True,ReviewStatus='Reviewed' if original else 'Needs Review',MinimumClientVersion='1.6.0',DistributionChannel=['Development','Community','Steam'] if original else ['Development','Community'],WithdrawalStatus='Active',ReviewNote=note)
    records.append(entry);ledger.append(dict(entry,CurFiles=cur,AniFiles=ani,NativeRoles=len(r['NativeRoles']),InheritedRoles=len(r['SupportedRoles'])-len(r['NativeRoles']),NoticeSha256=sha(notice),SourcePageFetched=bool(page and page.get('fetched')),ObservedSourceLicense=observed))
for theme in ('frost','obsidian'):
    folder=pathlib.Path('assets/cursors' if theme=='frost' else 'assets/themes/obsidian');manifest=json.loads((folder/'theme.json').read_bytes());h=hashlib.sha256()
    for role,file in manifest['Roles'].items():h.update((role+pathlib.Path(file).suffix).encode());h.update((folder/file).read_bytes())
    records.append(dict(StableId=theme,Version='unversioned',Scope='Bundled',PackageSha256=h.hexdigest(),Author='CURSORIUM',SourceUrl='https://github.com/catvir4493/cursorium',License='MIT',LicenseEvidence=[dict(Url=license_url,Sha256=sha(license_bytes),Kind='RepositoryLicense')],CommercialClearance='Approved',RedistributionPermission='AllowedWithNotice',AttributionRequired=True,ReviewStatus='Reviewed',MinimumClientVersion='1.6.0',DistributionChannel=['Development','Community','Steam'],WithdrawalStatus='Active',ReviewNote='Generated from the MIT project CursorArtwork source; native image/timing bytes preserved. No resource-level version was previously declared.'))
effect_hash=sha(pathlib.Path('assets/catalog/effects.json').read_bytes())
for effect in ('comet','minimal','neon','stardust'):
    e=dict(records[-1],StableId=effect,PackageSha256=effect_hash,ReviewNote='Built-in trusted renderer preset; digest covers effects.json. Renderer algorithm remains desktop code, not a downloaded executable.')
    records.append(e)
a.output.mkdir(parents=True)
rights=dict(SchemaVersion=2,Version='1.0.0',CatalogSha256=sha(catalog_bytes),Resources=records)
(a.output/'rights.json').write_text(json.dumps(rights,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(a.output/'index.json').write_bytes(catalog_bytes);(a.output/'index.json.sig').write_bytes(pathlib.Path(str(a.catalog)+'.sig').read_bytes())
(a.output/'review-ledger.json').write_text(json.dumps(dict(reviewedAt=datetime.now(timezone.utc).isoformat(),online=len(catalog['Resources']),bundledAdditional=6,records=ledger,sourceFetches=list(fetches.values()),note='Package hashes/NOTICE inspected. Commercial approval is limited to original MIT data; third-party resources remain Needs Review.'),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Prepared',len(records),'rights records; third-party commercial clearance stays Needs Review')
