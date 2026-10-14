"""Acquire public source evidence, never extract or execute downloaded contents.

Usage: python scripts/review-sources.py OUTPUT slug [slug ...]
Downloads remain private until a separate human license/quality review approves them.
"""
import hashlib
import json
import pathlib
import sys
from urllib.parse import urljoin, urlparse
import requests
from bs4 import BeautifulSoup


def acquire(root, slug):
    if not slug.replace('-', '').isalnum():
        raise ValueError('Invalid source slug')
    url = 'https://www.rw-designer.com/cursor-set/' + slug
    response = requests.get(url, timeout=30)
    response.raise_for_status()  # No retries around access controls.
    page = response.content
    soup = BeautifulSoup(page, 'html.parser')
    links = [(a.get_text(' ', strip=True), urljoin(url, a.get('href', ''))) for a in soup.find_all('a')]
    download = next((href for text, href in links if text.startswith('Download (')), None)
    text = soup.get_text('\n', strip=True)
    folder = root / slug
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'source.html').write_bytes(page)
    (folder / 'source.txt').write_text(text, encoding='utf-8')
    record = dict(source=url, pageSha256=hashlib.sha256(page).hexdigest(), download=download,
                  title=soup.title.get_text() if soup.title else '', review='Pending: no redistribution approval')
    if download:
        parsed = urlparse(download)
        if parsed.scheme != 'https' or parsed.hostname != 'www.rw-designer.com':
            raise ValueError('Unexpected download origin')
        with requests.get(download, timeout=45, stream=True, allow_redirects=False) as package:
            package.raise_for_status()
            if package.is_redirect:
                raise ValueError('Unreviewed download redirect')
            data = bytearray()
            for part in package.iter_content(65536):
                data.extend(part)
                if len(data) > 32 * 1024 * 1024:
                    raise ValueError('Source archive size limit')
        if data[:4] != b'PK\x03\x04':
            raise ValueError('Download is not a ZIP')
        (folder / 'original.zip').write_bytes(data)
        record.update(sha256=hashlib.sha256(data).hexdigest(), size=len(data))
    (folder / 'evidence.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
    start = text.find('Published on')
    end = text.find('Top reviews', start)
    print(json.dumps(dict(slug=slug, **record, excerpt=text[start:end][:5500]), ensure_ascii=True))


if __name__ == '__main__':
    destination = pathlib.Path(sys.argv[1]).resolve()
    for name in sys.argv[2:]:
        try:
            acquire(destination, name)
        except Exception as exc:
            print(json.dumps(dict(slug=name, error=str(exc))))
